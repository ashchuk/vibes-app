using System.Text.Json;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Vibes.API.Configuration;
using Vibes.API.Models;

namespace Vibes.API.Services;

public class TelegramMessageHandler(
    ILogger<TelegramMessageHandler> logger,
    ITelegramBotClient botClient,
    ICalendarService calendarService,
    ILlmService llmService,
    IDatabaseService databaseService,
    IOptions<GoogleCalendarConfiguration> googleCalendarConfiguration)
    : IUpdateHandler
{
    public async Task HandleUpdateAsync(
        ITelegramBotClient _,
        Update update,
        CancellationToken cancellationToken)
    {
        var handler = update switch
        {
            { CallbackQuery: { } callbackQuery } => OnCallbackQuery(callbackQuery, cancellationToken),
            { Message: { } message } => OnMessage(message, cancellationToken),
            _ => UnknownUpdateHandlerAsync(update)
        };
        await handler;
    }

    private async Task OnCallbackQuery(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.From is null || callbackQuery.Data is null || callbackQuery.Message is null) return;

        var user = await databaseService.GetOrCreateUserAsync(callbackQuery.From);

        // Убираем кнопки из предыдущего сообщения, чтобы избежать повторных нажатий
        await botClient.EditMessageReplyMarkup(
            chatId: callbackQuery.Message.Chat.Id,
            messageId: callbackQuery.Message.MessageId,
            replyMarkup: null,
            cancellationToken: cancellationToken);

        var task = callbackQuery.Data switch
        {
            // --- ОБРАБОТЧИКИ ДЛЯ КНОПОК ОНБОРДИНГА ---
            "onboarding_start" when user.State == ConversationState.OnboardingAwaitingStart => HandleOnboardingStart(user, callbackQuery.From.Id, cancellationToken),
            "connect_calendar_onboarding" => HandleConnectCalendarOnboarding(user, callbackQuery, cancellationToken),
            "skip_calendar_onboarding" => HandleSkipCalendarOnboarding(user, callbackQuery, cancellationToken),

            "command_check_calendar" => HandleCheckCalendarCommand(user, callbackQuery.From.Id, cancellationToken),
            "command_plan" => HandlePlanCommand(user, callbackQuery.From.Id, cancellationToken),
            
            // --- ОБРАБОТЧИК ДЛЯ КНОПОК ОЦЕНКИ ЭНЕРГИИ ---
            var data when data.StartsWith("energy_rating_")
                => HandleEnergyRatingCallback(user, callbackQuery, cancellationToken),

            // --- ОБРАБОТЧИК ДЛЯ КНОПКИ ПРИНЯТЬ ПЛАН ---
            "plan_accept" => HandlePlanAccept(user, callbackQuery.Message.Chat.Id, callbackQuery.Message, cancellationToken),
            "dialog_cancel" => HandleDialogCancel(user, callbackQuery, cancellationToken),
            
            // --- ОБРАБОТЧИК ДЛЯ КНОПКИ ИЗМЕНИТЬ ПЛАН ---
            "plan_edit" => HandlePlanEdit(user, callbackQuery.Message.Chat.Id, cancellationToken),

            var data when data.StartsWith("rate_event_")
                => HandleEventRatingCallback(user, callbackQuery, cancellationToken),

            _ => botClient.AnswerCallbackQuery(callbackQuery.Id, "Эта кнопка уже неактивна", cancellationToken: cancellationToken)
        };
        await task;
    }

    private async Task HandleDialogCancel(VibesUser user, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        // 1. Сбрасываем состояние пользователя
        user.State = ConversationState.None;
        user.ConversationContext = null;
        await databaseService.UpdateUserAsync(user);

        // 2. Редактируем предыдущее сообщение, чтобы убрать кнопки и подтвердить отмену
        await botClient.EditMessageText(
            chatId: callbackQuery.Message.Chat.Id,
            messageId: callbackQuery.Message.MessageId,
            text: "Хорошо, отменили.",
            replyMarkup: null,
            cancellationToken: cancellationToken
        );
    }
    
    private async Task HandleConnectCalendarOnboarding(VibesUser user, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        // Отправляем ту же самую ссылку для авторизации
        await HandleConnectCalendarCommand(user, callbackQuery.From.Id, cancellationToken);

        // Завершаем онбординг и отправляем приветственное сообщение
        await FinalizeOnboarding(user, callbackQuery.From.Id, cancellationToken);
    }

    private async Task HandleSkipCalendarOnboarding(VibesUser user, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        // Просто завершаем онбординг
        await FinalizeOnboarding(user, callbackQuery.From.Id, cancellationToken);
    }

    // Общий метод для завершения онбординга
    private async Task FinalizeOnboarding(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        user.State = ConversationState.None;
        user.IsOnboardingCompleted = true;
        await databaseService.UpdateUserAsync(user);

        var finalText = "Отлично, мы готовы начинать!\n\n" +
                        "Просто напишите мне, что у вас на уме. Например: \"Составь план на завтра\" или \"Чувствую себя уставшим\". Я пойму вас.\n\n" +
                        "Если захотите подключить календарь позже, просто напишите \"подключить календарь\".";

        await botClient.SendMessage(chatId, finalText, cancellationToken: cancellationToken);
    }

    private async Task HandleEventRatingCallback(VibesUser user, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        // 1. Парсим callbackData
        var parts = callbackQuery.Data!.Split('_');
        var eventId = parts[2];
        var vibeTypeStr = parts[3];

        // 2. Получаем список событий из контекста
        var eventsToRate = JsonSerializer.Deserialize<Dictionary<string, string>>(user.ConversationContext ?? "{}")
                           ?? new Dictionary<string, string>();

        // 3. Сохраняем оценку в базу данных
        var newRating = new EventRating
        {
            UserId = user.Id,
            GoogleEventId = eventId,
            EventSummary = eventsToRate.GetValueOrDefault(eventId, "Неизвестное событие"),
            Vibe = Enum.Parse<VibeType>(vibeTypeStr, true),
            RatedAtUtc = DateTime.UtcNow
        };
        await databaseService.AddRecordAsync(newRating);

        // 4. Удаляем только что оцененное событие из нашего списка
        eventsToRate.Remove(eventId);

        // 5. Проверяем, остались ли еще события
        if (eventsToRate.Any())
        {
            // Есть еще события. Показываем следующее.
            var nextEvent = eventsToRate.First();
            var nextEventId = nextEvent.Key;
            var nextEventSummary = nextEvent.Value;

            var keyboard = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("⚡️ Заряжает", $"rate_event_{nextEventId}_Energize"),
                InlineKeyboardButton.WithCallbackData("😐 Нейтрально", $"rate_event_{nextEventId}_Neutral"),
                InlineKeyboardButton.WithCallbackData("🪫 Утомляет", $"rate_event_{nextEventId}_Drain")
            );

            // Редактируем предыдущее сообщение, чтобы не спамить в чат
            await botClient.EditMessageText(
                chatId: user.TelegramId,
                messageId: callbackQuery.Message.MessageId,
                text: $"Отлично. А как насчет \"{nextEventSummary}\"?",
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );

            // Обновляем контекст с оставшимися событиями
            user.ConversationContext = JsonSerializer.Serialize(eventsToRate);
            await databaseService.UpdateUserAsync(user);
        }
        else
        {
            // События закончились. Завершаем диалог.
            user.State = ConversationState.None;
            user.ConversationContext = null;
            await databaseService.UpdateUserAsync(user);

            await botClient.EditMessageText(
                chatId: user.TelegramId,
                messageId: callbackQuery.Message.MessageId,
                text: "Спасибо! Все итоги дня подведены. Отличного вечера!",
                replyMarkup: null, // Убираем кнопки
                cancellationToken: cancellationToken
            );
        }
    }

    // Вспомогательный метод для чистоты кода
    private async Task HandleOnboardingStart(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        user.State = ConversationState.OnboardingAwaitingTimezone;
        await databaseService.UpdateUserAsync(user);

        await botClient.SendMessage(
            chatId: chatId,
            text: "Отлично! Укажи, пожалуйста, твой город или часовой пояс в формате UTC+X (например, UTC+3).",
            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("❌ Отмена", "dialog_cancel")),
            cancellationToken: cancellationToken);
    }

    // Вспомогательный метод для обработки оценки энергии
    private async Task HandleEnergyRatingCallback(VibesUser user, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var rating = callbackQuery.Data!.Split('_').LastOrDefault() ?? "unknown";
        logger.LogInformation("Пользователь {UserId} оценил энергию как: {Rating}", user.Id, rating);

        // --- ИЗМЕНЕНИЕ: СОХРАНЯЕМ ОЦЕНКУ В КОНТЕКСТ ---
        var context = new MorningCheckupContext
        {
            EnergyRating = rating
        };
        user.ConversationContext = JsonSerializer.Serialize(context);

        await botClient.AnswerCallbackQuery(callbackQuery.Id, $"Принято: {rating}", cancellationToken: cancellationToken);

        user.State = ConversationState.AwaitingMorningSleepHours;
        await databaseService.UpdateUserAsync(user);

        await botClient.SendMessage(
            chatId: callbackQuery.From.Id,
            text: "Понял. А сколько примерно часов ты спал(а) сегодня?",
            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("❌ Отмена", "dialog_cancel")),
            cancellationToken: cancellationToken);
    }

    private async Task OnMessage(Message message, CancellationToken cancellationToken)
    {
        if (message.From is null) return;

        var user = await databaseService.GetOrCreateUserAsync(message.From);

        string? messageText = message.Text;
        
        // 1. Если это аудио или видео, транскрибируем его в текст
        if (message.Voice is not null || message.VideoNote is not null)
        {
            await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);
            var fileId = message.Voice?.FileId ?? message.VideoNote!.FileId;
            var mimeType = message.Voice is not null ? "audio/ogg" : "video/mp4";
        
            await using var memoryStream = new MemoryStream();
            var file = await botClient.GetFile(fileId, cancellationToken);
            if (file.FilePath is null)
            {
                await botClient.SendMessage(message.Chat.Id, "Не удалось обработать голосовое сообщение.", cancellationToken: cancellationToken);
                return;
            }
        
            await botClient.DownloadFile(file.FilePath, memoryStream, cancellationToken);
            memoryStream.Position = 0;

            messageText = await llmService.TranscribeAudioAsync(memoryStream, mimeType);
        }
        
        if (!string.IsNullOrEmpty(messageText))
        {
            var command = messageText.Split(' ')[0];
            var task = command switch
            {
                "/start" => HandleStartCommand(user, message.Chat.Id, cancellationToken),
                "/plan" => HandlePlanCommand(user, message.Chat.Id, cancellationToken),
                "/energy" => HandleEnergyCommand(user, message.Chat.Id, cancellationToken),
                "/connect_calendar" => HandleConnectCalendarCommand(user, message.Chat.Id, cancellationToken),
                "/check_calendar" => HandleCheckCalendarCommand(user, message.Chat.Id, cancellationToken),
                "/about" => HandleAboutCommand(user, message.Chat.Id, cancellationToken),
                _ => HandleConversation(user, message, messageText, cancellationToken)
            };
            await task;
        }
        else if (message.Photo is not null)
        {
            await HandlePhotoMessage(user, message, cancellationToken);
        }
        else
        {
            await botClient.SendMessage(chatId: message.Chat.Id, text: "Извини, я пока не знаю, как на это ответить.", cancellationToken: cancellationToken);
        }
    }

    private async Task HandleAboutCommand(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        var aboutText = "Vibes — ваш личный AI-ассистент для управления энергией и задачами.\n\n" +
                        "Я помогаю находить баланс и избегать выгорания, анализируя ваше расписание и привычки.";

        var inlineKeyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(
            "Перейти на наш сайт",
            "https://vibes.nakodeelee.ru"
        ));

        await botClient.SendMessage(
            chatId: chatId,
            text: aboutText,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken
        );
    }
    
    private async Task HandleStartCommand(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        if (user.IsOnboardingCompleted)
        {
            // Пользователь уже проходил онбординг, просто приветствуем
            await botClient.SendMessage(
                chatId: chatId,
                text: $"С возвращением, {user.FirstName}! Чем я могу помочь сегодня? Напишите, что у вас на уме, или используйте команду /plan, чтобы составить расписание.",
                cancellationToken: cancellationToken);
            return;
        }

        // --- НОВЫЙ ОНБОРДИНГ ---
        user.State = ConversationState.OnboardingAwaitingStart;
        await databaseService.UpdateUserAsync(user);

        // Вместо гифки и длинного текста даем короткое и емкое приветствие
        var welcomeText = $"Привет, {user.FirstName}! Я Vibes — ваш личный AI-ассистент для управления энергией.\n\n" +
                          "Мои две суперсилы:\n\n" +
                          "🧠 **Память:** Я запоминаю ваши предпочтения и то, что вас заряжает или утомляет, чтобы давать действительно персональные советы.\n\n" +
                          "🗓️ **Интеграция с Google Calendar:** Я могу видеть ваше расписание, чтобы помогать планировать день и находить окна для отдыха и фокуса.\n\n" +
                          "Чтобы я мог вам помогать, лучше всего сразу подключить ваш календарь. Это безопасно и займет всего минуту.";

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            // Кнопка, которая сразу запускает подключение календаря
            InlineKeyboardButton.WithCallbackData("✅ Подключить Google Calendar", "connect_calendar_onboarding"),
            // Кнопка для тех, кто хочет сделать это позже
            InlineKeyboardButton.WithCallbackData("Позже", "skip_calendar_onboarding")
        });

        await botClient.SendMessage(
            chatId: chatId,
            text: welcomeText,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleConversation(VibesUser user, Message message, string recognizedText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(recognizedText)) return; // Мы обрабатываем только текстовые сообщения в этом методе

        switch (user.State)
        {
            // --- СЦЕНАРИЙ ОНБОРДИНГА ---
            case ConversationState.OnboardingAwaitingTimezone:
                var userInput = message.Text;
    
                // Вызываем LLM для определения таймзоны
                await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);

                var timeZoneId = await llmService.GetTimeZoneIdFromUserInputAsync(userInput);

                if (timeZoneId == null)
                {
                    // Не удалось определить, просим еще раз
                    await botClient.SendMessage(message.Chat.Id, 
                        "Не смог распознать часовой пояс. Попробуйте, пожалуйста, еще раз. Например: 'Москва' или 'UTC+3'.", 
                        cancellationToken: cancellationToken);
                    break;
                }

                user.TimeZoneId = timeZoneId;
                logger.LogInformation("Пользователю {UserId} установлена таймзона: {TimeZoneId}", user.Id, timeZoneId);
                
                user.State = ConversationState.None;
                user.IsOnboardingCompleted = true; // Важно!
                await databaseService.UpdateUserAsync(user);

                // 2. Формируем новое, вовлекающее сообщение
                var transitionText = $"Отлично, таймзона ({timeZoneId}) установлена!\n\n" +
                                     "Теперь я готов помочь вам спланировать ваш день. Чтобы я мог составить для вас лучший план, мне нужно понимать ваш контекст.\n\n" +
                                     "**Самый лучший способ — подключить ваш Google Calendar.**";

                // 3. Предлагаем ключевое действие — подключение календаря
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ Подключить Google Calendar", "connect_calendar_onboarding"),
                    InlineKeyboardButton.WithCallbackData("Пока пропустить", "skip_calendar_onboarding")
                });

                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: transitionText,
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken
                );
                break;

            // --- СЦЕНАРИЙ СБОРА РЕТРО-ДАННЫХ (User Story #8) ---
            case ConversationState.AwaitingRetroSleepAndActivity:
                logger.LogInformation("Пользователь {UserId} прислал ретро-данные: {RetroData}", user.Id, recognizedText);

                // Отправляем текст в LlmService и получаем готовый инсайт
                await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);

                var insight = await llmService.GenerateRetroInsightAsync(recognizedText);

                if (insight.StartsWith("[ERROR]"))
                {
                    logger.LogWarning("LLM вернула ошибку при генерации ретро-инсайта: {Error}", insight);
                    // Заменяем техническую ошибку на вежливый ответ
                    insight = "Спасибо! Я сохранил эти данные. Чтобы я мог делать более точные выводы, попробуй описать свой сон и активность за последние пару дней.";

                    await botClient.SendMessage(message.Chat.Id, insight, cancellationToken: cancellationToken);
                    // Остаемся в том же состоянии, чтобы пользователь мог попробовать еще раз
                    break;
                }

                await botClient.SendMessage(message.Chat.Id, insight, cancellationToken: cancellationToken);

                user.State = ConversationState.None;
                user.IsOnboardingCompleted = true; // Фиксируем факт что онбординг прошел
                await databaseService.UpdateUserAsync(user);
                await HandlePlanCommand(user, message.Chat.Id, cancellationToken);
                break;

            // --- СЦЕНАРИЙ УТРЕННЕГО ЧЕКАПА ---
            case ConversationState.AwaitingMorningSleepHours:
                logger.LogInformation("Пользователь {UserId} спал: {SleepHours}", user.Id, recognizedText);

                // --- ИЗМЕНЕНИЕ: ОБНОВЛЯЕМ КОНТЕКСТ ДАННЫМИ О СНЕ ---
                var contextN3 = JsonSerializer.Deserialize<MorningCheckupContext>(user.ConversationContext ?? "{}")
                                ?? new MorningCheckupContext();
                contextN3.SleepHours = recognizedText;
                user.ConversationContext = JsonSerializer.Serialize(contextN3);

                user.State = ConversationState.AwaitingMorningPlans;
                await databaseService.UpdateUserAsync(user);

                await botClient.SendMessage(message.Chat.Id,
                    "Отлично. Чем сегодня займёмся? Назови 1–3 обязательных дела.", 
                    replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("❌ Отмена", "dialog_cancel")),
                    cancellationToken: cancellationToken);
                break;

            case ConversationState.AwaitingMorningPlans:
                logger.LogInformation("Планы пользователя {UserId}: {Plans}", user.Id, recognizedText);

                // --- НАЧИНАЕТСЯ ГЛАВНАЯ ЛОГИКА ---

                // 1. Восстанавливаем контекст
                var contextN4 = JsonSerializer.Deserialize<MorningCheckupContext>(user.ConversationContext ?? "{}")
                                ?? new MorningCheckupContext();

                // 2. Получаем события из календаря
                var morningPlancalendarEvents = await calendarService.GetUpcomingEvents(user, 5);

                // 3. Вызываем LLM для генерации плана
                await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);

                var generatedPlan = await llmService.GenerateMorningPlanAsync(
                    contextN4.EnergyRating ?? "не указана",
                    contextN4.SleepHours ?? "не указано",
                    recognizedText,
                    morningPlancalendarEvents
                );

                if (generatedPlan.StartsWith("[ERROR]"))
                {
                    logger.LogWarning("LLM вернула ошибку при генерации утреннего плана: {Error}", generatedPlan);
                    // Заменяем техническую ошибку на вежливый ответ
                    generatedPlan = "Похоже, в вашем сообщении нет конкретных задач. Попробуйте еще раз перечислить 1-3 дела на сегодня.";

                    await botClient.SendMessage(message.Chat.Id, generatedPlan, cancellationToken: cancellationToken);
                    // Остаемся в том же состоянии, чтобы пользователь мог попробовать еще раз
                    break;
                }

                // 4. Завершаем диалог и отправляем результат
                user.State = ConversationState.None;
                user.ConversationContext = null; // Очищаем контекст
                await databaseService.UpdateUserAsync(user);

                await SendFormattedMessageAsync(
                    message.Chat.Id,
                    generatedPlan,
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Принять план 👍", "plan_accept"), InlineKeyboardButton.WithCallbackData("Править ✏️", "plan_edit"),
                    }),
                    cancellationToken: cancellationToken);
                break;

            // --- СЦЕНАРИЙ ПЛАНИРОВАНИЯ ПО ТЕКСТУ ---
            case ConversationState.AwaitingSchedulePhoto:
                logger.LogInformation("Пользователь {UserId} прислал расписание текстом: {TextSchedule}", user.Id, recognizedText);


                // 1. Сообщаем пользователю, что мы начали работу
                await botClient.SendMessage(message.Chat.Id, "Принял! Сверяюсь с вашим календарем и составляю лучший план на день. Секунду...", cancellationToken: cancellationToken);
                await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);

                // 2.1 Получаем события из календаря
                var scheduleCalendarEvents = await calendarService.GetUpcomingEvents(user);
                // 2.2 ИЗВЛЕКАЕМ "ПАМЯТЬ" ИЗ БАЗЫ ДАННЫХ
                var recentPlans = await databaseService.GetRecentDailyPlansAsync(user.Id);
                var recentRatings = await databaseService.GetRecentEventRatingsAsync(user.Id);

                // 3. Вызываем LLM для генерации плана на основе текста и событий календаря
                await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);

                var structuredPlan = await llmService.GeneratePlanFromTextAsync(
                    recognizedText,
                    scheduleCalendarEvents,
                    recentPlans,
                    recentRatings);

                if (structuredPlan.StartsWith("[ERROR]"))
                {
                    logger.LogWarning("LLM вернула ошибку при генерации плана из текста: {Error}", structuredPlan);
                    // Заменяем техническую ошибку на вежливый ответ
                    structuredPlan = "Похоже, в вашем сообщении нет конкретных задач. Попробуйте перечислить 1-3 дела, которые вы хотите сегодня выполнить.";

                    // В этом случае кнопки "Принять/Править" не нужны
                    await botClient.SendMessage(message.Chat.Id, structuredPlan, cancellationToken: cancellationToken);
                    // Важно: не меняем состояние, пользователь может попробовать еще раз
                    break;
                }
                // --- КОНЕЦ НОВОЙ ЛОГИКИ ОБРАБОТКИ ОТВЕТА ---

                user.State = ConversationState.None;
                await databaseService.UpdateUserAsync(user);

                await SendFormattedMessageAsync(
                    message.Chat.Id,
                    structuredPlan,
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Подтвердить 👍", "plan_accept"), InlineKeyboardButton.WithCallbackData("Изменить ✏️", "plan_edit"),
                    }),
                    cancellationToken);
                break;

            default:
                // Если мы не ожидаем ответа, но пользователь что-то написал,
                // вежливо предлагаем начать с известной команды.
                // Если мы не находимся в середине какого-то диалога,
                // пытаемся понять, что хочет пользователь.
                await HandleDefaultMessageAsync(user, message, recognizedText, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Обрабатывает нажатие на кнопку "Подтвердить план".
    /// </summary>
    private async Task HandlePlanAccept(VibesUser user, long chatId, Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Пользователь {UserId} подтвердил план.", user.Id);
        var planText = message.Text ?? "План не был сохранен.";

        // 1. Сохраняем план в нашу базу данных
        var newPlan = new DailyPlan
        {
            UserId = user.Id,
            PlanDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PlanText = planText,
            CreatedUtc = DateTime.UtcNow
        };
        await databaseService.AddRecordAsync(newPlan);

        // 2. Если календарь подключен, пытаемся создать событие
        if (user.IsGoogleCalendarConnected)
        {
            // 2.1. Отправляем текст плана в LLM, чтобы извлечь детали события
            await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);

            var extractedEvent = await llmService.ExtractFirstEventFromPlanAsync(planText, user.TimeZoneId ?? "Etc/UTC");

            if (extractedEvent is { Found: true, StartTime: not null, EndTime: not null })
            {
                // 2.2. Если детали успешно извлечены, создаем событие в Google Calendar
                var createdEvent = await calendarService.CreateEventAsync(user, extractedEvent.Title, extractedEvent.StartTime.Value, extractedEvent.EndTime.Value);

                if (createdEvent?.HtmlLink != null)
                {
                    // 2.3. Отправляем подтверждение с прямой ссылкой на событие
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"Отлично! План принят, и я создал для вас фокус-блок \"{extractedEvent.Title}\" в Google Calendar. <a href=\"{createdEvent.HtmlLink}\">Посмотреть событие</a>.",
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                    return; // Завершаем, чтобы не отправлять второе сообщение
                }
            }
        }

        // 3. Этот текст отправится, если календарь не подключен или не удалось создать/извлечь событие
        await botClient.SendMessage(
            chatId: chatId,
            text: "Отлично! План принят и сохранен. Подключите Google Calendar, чтобы я мог автоматически добавлять ключевые задачи в ваше расписание!",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Обрабатывает нажатие на кнопку "Изменить план".
    /// </summary>
    private async Task HandlePlanEdit(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Пользователь {UserId} хочет изменить план.", user.Id);

        // TODO: Здесь можно реализовать более сложную логику редактирования (например, с помощью LLM),
        // но для хакатона самый простой и надежный способ — предложить пользователю пересоздать план.

        // Переводим пользователя в состояние ожидания нового текстового плана
        user.State = ConversationState.AwaitingSchedulePhoto;
        await databaseService.UpdateUserAsync(user);

        await botClient.SendMessage(
            chatId: chatId,
            text: "Хорошо, давай скорректируем. Напиши, что бы ты хотел изменить, или просто пришли новый список задач.",
            cancellationToken: cancellationToken);
    }

    public async Task StartMorningCheckupAsync(VibesUser user, CancellationToken cancellationToken)
    {
        // --- ЗАПИСЫВАЕМ ВРЕМЯ ОТПРАВКИ ---
        // Мы делаем это *перед* отправкой, чтобы гарантировать, что даже если отправка займет время,
        // мы не отправим сообщение дважды.
        user.LastCheckupSentUtc = DateTime.UtcNow;

        user.State = ConversationState.AwaitingMorningEnergyRating;
        user.ConversationContext = null;
        await databaseService.UpdateUserAsync(user);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("1-3 (Низкая)", "energy_rating_low"), InlineKeyboardButton.WithCallbackData("4-6 (Средняя)", "energy_rating_medium")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("7-8 (Высокая)", "energy_rating_high"), InlineKeyboardButton.WithCallbackData("9-10 (Супер!)", "energy_rating_very_high")
            }
        });

        var personalizedText = $"Доброе утро, {user.FirstName}! Скан энергии — от 1 до 10: как ты сейчас?";

        await botClient.SendMessage(
            chatId: user.TelegramId,
            text: personalizedText,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    public async Task StartEveningCheckupAsync(VibesUser user, CancellationToken cancellationToken)
    {
        // --- ДОБАВЛЯЕМ ЗАПИСЬ ВРЕМЕНИ ОТПРАВКИ ---
        user.LastEveningCheckupSentUtc = DateTime.UtcNow;
        await databaseService.UpdateUserAsync(user);

        // 1. Получаем события за сегодняшний день (по UTC)
        var eventsToday = await calendarService.GetEventsForDateAsync(user, DateTime.UtcNow);

        if (eventsToday == null || eventsToday.Count == 0)
        {
            // Если событий не было, проводим упрощенный чекап.
            // Переводим в состояние ожидания текстового ответа.
            user.State = ConversationState.AwaitingEveningEnergyRating;
            await databaseService.UpdateUserAsync(user);

            await botClient.SendMessage(
                chatId: user.TelegramId,
                text: "Похоже, сегодня в календаре не было событий. Как прошел твой день в целом? Оцени, пожалуйста, свою энергию от 1 до 10.",
                cancellationToken: cancellationToken);
            return;
        }

        // 2. Если события были, начинаем диалог с их оценки
        user.State = ConversationState.AwaitingEventRating;

        // 3. Сохраняем ID и названия событий в контекст для последующей обработки.
        // Это оптимизация, чтобы не дергать Google API за названием каждого события.
        var eventSummaries = eventsToday.ToDictionary(e => e.Id, e => e.Summary);
        user.ConversationContext = JsonSerializer.Serialize(eventSummaries);
        await databaseService.UpdateUserAsync(user);

        // 4. Берем первое событие и отправляем вопрос
        var firstEvent = eventsToday.First();
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("⚡️ Заряжает", $"rate_event_{firstEvent.Id}_Energize"),
            InlineKeyboardButton.WithCallbackData("😐 Нейтрально", $"rate_event_{firstEvent.Id}_Neutral"),
            InlineKeyboardButton.WithCallbackData("🪫 Утомляет", $"rate_event_{firstEvent.Id}_Drain")
        );

        await botClient.SendMessage(
            chatId: user.TelegramId,
            text: $"Давай подведем итоги дня. Как ты оценишь событие \"{firstEvent.Summary}\"?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleConnectCalendarCommand(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        var authUrl = calendarService.GenerateAuthUrl(user.TelegramId);

        var inlineKeyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl(
            text: "Подключить Google Calendar",
            url: authUrl
        ));

        await botClient.SendMessage(
            chatId: chatId,
            text: "Чтобы я мог анализировать твое расписание, пожалуйста, предоставь доступ к своему Google Календарю.",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }
    /// <summary>
    /// Обрабатывает тестовую команду для получения и отображения событий из Google Calendar.
    /// </summary>
    private async Task HandleCheckCalendarCommand(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        // Шаг 1: Проверяем, подключен ли вообще календарь.
        if (!user.IsGoogleCalendarConnected)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "Ваш Google Календарь еще не подключен. Сначала используйте команду /connect_calendar.",
                cancellationToken: cancellationToken);
            return;
        }

        // Шаг 2: Отправляем пользователю сообщение о том, что мы начали работу.
        await botClient.SendMessage(
            chatId: chatId,
            text: "🔍 Запрашиваю ближайшие события из вашего календаря...",
            cancellationToken: cancellationToken);

        try
        {
            // Шаг 3: Вызываем наш сервис для получения 10 ближайших событий.
            var events = await calendarService.GetUpcomingEvents(user, 10);

            // Шаг 4: Обрабатываем случай, если событий нет.
            if (events is null || events.Count == 0)
            {
                await botClient.SendMessage(chatId, "✅ В вашем календаре нет предстоящих событий.", cancellationToken: cancellationToken);
                return;
            }

            // Шаг 5: Формируем красивый ответ с помощью StringBuilder и Markdown.
            var responseBuilder = new System.Text.StringBuilder("Вот ваши ближайшие события:\n\n");
            foreach (var calendarEvent in events)
            {
                // Google API может вернуть либо конкретное время, либо только дату (для событий на весь день).
                var eventTime = calendarEvent.Start.DateTimeDateTimeOffset.HasValue
                    ? calendarEvent.Start.DateTimeDateTimeOffset.Value.ToLocalTime().ToString("g") // Формат "24.08.2025 14:30"
                    : calendarEvent.Start.Date; // Формат "2025-08-24"

                responseBuilder.AppendLine($"🗓️ *{calendarEvent.Summary}*");
                responseBuilder.AppendLine($"   - Начало: `{eventTime}`");
                responseBuilder.AppendLine(); // Пустая строка для отступа
            }

            // Шаг 6: Отправляем отформатированное сообщение.
            await SendFormattedMessageAsync(chatId,
                responseBuilder.ToString(),
                replyMarkup: new InlineKeyboardMarkup(),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // Шаг 7: Обрабатываем возможные ошибки (например, если токен был отозван).
            logger.LogError(ex, "Ошибка при получении событий из Google Calendar для пользователя {UserId}", user.Id);
            await botClient.SendMessage(chatId,
                """
                ❌ Произошла ошибка при попытке прочитать ваш календарь.
                 Возможно, потребуется переподключить его с помощью команды /connect_calendar.
                """,
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Обрабатывает получение фотографии. User Story #11.
    /// </summary>
    private async Task HandlePhotoMessage(VibesUser user, Message message, CancellationToken cancellationToken)
    {
        // Проверяем, ожидает ли бот фото. Если нет - игнорируем.
        if (user.State != ConversationState.AwaitingSchedulePhoto)
        {
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Спасибо за фото! Если хочешь, чтобы я составил по нему план, сначала используй команду /plan.",
                cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Получил фото! Отправил на распознавание... 🤖",
            cancellationToken: cancellationToken);

        try
        {
            // 1. Выбираем фото лучшего качества (последнее в массиве) и получаем его FileId
            var fileId = message.Photo!.Last().FileId;

            // 2. Скачиваем файл с серверов Telegram в виде потока
            await using var memoryStream = new MemoryStream();
            var file = await botClient.GetFile(fileId, cancellationToken);

            // Проверяем, что FilePath не null
            if (file.FilePath is null)
            {
                throw new Exception("Не удалось получить путь к файлу от Telegram.");
            }

            await botClient.DownloadFile(file.FilePath, memoryStream, cancellationToken);
            memoryStream.Position = 0; // Сбрасываем позицию потока в начало для чтения

            // 3. Отправляем поток в наш LlmService
            await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);
            var recognizedText = await llmService.RecognizeScheduleFromImageAsync(memoryStream);

            string responseToUser;

            if (recognizedText.StartsWith("[ERROR]"))
            {
                // LLM вернула ошибку, даем пользователю вежливый и безопасный ответ
                logger.LogWarning("LLM вернула ошибку при распознавании изображения: {Error}", recognizedText);

                responseToUser = recognizedText.Contains("Нерелевантное")
                    ? "Похоже, на этом фото нет расписания. Попробуйте сфотографировать ваш календарь или список дел."
                    : "Я вижу на фото текст, но он не похож на список задач. Пожалуйста, убедитесь, что на фото именно ваше расписание.";
            }
            else
            {
                // Все в порядке, формируем подтверждающее сообщение
                responseToUser = $"Вот что я смог разобрать:\n\n{recognizedText}\n\n" +
                                 "Используем эти данные для составления плана? Можешь скопировать, отредактировать и отправить мне исправленный вариант.";
            }

            await botClient.SendMessage(message.Chat.Id, responseToUser, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке фото от пользователя {UserId}", user.Id);
            await botClient.SendMessage(message.Chat.Id, "Произошла ошибка при обработке фото. Попробуй еще раз.", cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Обрабатывает команду /plan. User Story из раздела "5) изменение настроек".
    /// </summary>
    private async Task HandlePlanCommand(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        // Переводим пользователя в состояние ожидания ввода расписания
        user.State = ConversationState.AwaitingSchedulePhoto; // Это состояние подходит и для фото, и для текста
        await databaseService.UpdateUserAsync(user);

        await botClient.SendMessage(
            chatId: chatId,
            text: "Пришли фото расписания или напиши 1–3 главные задачи — соберу план.",
            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("❌ Отмена", "dialog_cancel")),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Обрабатывает команду /energy. User Story из раздела "5) изменение настроек".
    /// </summary>
    private async Task HandleEnergyCommand(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        // Переводим пользователя в состояние ожидания оценки энергии
        user.State = ConversationState.AwaitingMorningEnergyRating; // Переиспользуем состояние, оно подходит
        await databaseService.UpdateUserAsync(user);

        // Создаем инлайн-кнопки для быстрого ответа
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            // Одна строка кнопок
            new[]
            {
                InlineKeyboardButton.WithCallbackData("1-3 (Низкая)", "energy_rating_low"), InlineKeyboardButton.WithCallbackData("4-6 (Средняя)", "energy_rating_medium"),
            },
            // Вторая строка кнопок
            new[]
            {
                InlineKeyboardButton.WithCallbackData("7-8 (Высокая)", "energy_rating_high"), InlineKeyboardButton.WithCallbackData("9-10 (Супер!)", "energy_rating_very_high"),
            }
        });

        await botClient.SendMessage(
            chatId: chatId,
            text: "Быстрый чек: как ты сейчас по шкале от 1 до 10?",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Обрабатывает ошибки, возникшие во время работы бота.
    /// </summary>
    public Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        logger.LogError("Произошла ошибка при обработке обновления (источник: {Source}):\n{ErrorMessage}", source, errorMessage);

        // Для стабильности можно добавить небольшую задержку в случае проблем с сетью
        if (exception is RequestException)
        {
            return Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        return Task.CompletedTask;
    }

    /// Надежно отправляет сообщение с Markdown-разметкой.
    /// Если Telegram не может распарсить разметку, отправляет сообщение как обычный текст.
    /// </summary>
    private async Task SendFormattedMessageAsync(long chatId, string text, ReplyMarkup? replyMarkup, CancellationToken cancellationToken)
    {
        try
        {
            // Попытка №1: Отправить с Markdown
            await botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Markdown,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("can't parse entities"))
        {
            // Попытка №2: Если парсинг не удался, логируем ошибку и отправляем как обычный текст
            logger.LogWarning(ex, "Не удалось отправить сообщение с Markdown-разметкой. Отправляю как обычный текст. Проблемный текст: {Text}", text);

            await botClient.SendMessage(
                chatId: chatId,
                text: text, // Тот же самый текст
                parseMode: ParseMode.None, // <-- Отправляем БЕЗ форматирования
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        // Другие исключения (например, проблемы с сетью) будут обработаны выше по стеку вызовов.
    }

    private async Task HandleDefaultMessageAsync(VibesUser user, Message message, string recognizedText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(recognizedText)) return;
        
        // 0. Если пользователь новый и пишет что-то, кроме /start,
        // мы все равно должны сначала провести его через онбординг.
        if (!user.IsOnboardingCompleted)
        {
            await HandleStartCommand(user, message.Chat.Id, cancellationToken);
            return;
        }
        
        // 1. Отправляем текст в LLM для классификации
        await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);
        var intent = await llmService.ClassifyUserIntentAsync(recognizedText);

        // 2. Выполняем действие в зависимости от намерения
        switch (intent)
        {
            case UserIntent.Plan:
                logger.LogInformation("Определено намерение: Plan");
                // Запускаем сценарий планирования
                await HandlePlanCommand(user, message.Chat.Id, cancellationToken);
                break;

            case UserIntent.SetEnergy:
                logger.LogInformation("Определено намерение: SetEnergy");
                // Запускаем сценарий оценки энергии
                await HandleEnergyCommand(user, message.Chat.Id, cancellationToken);
                break;

            case UserIntent.CheckCalendar:
                logger.LogInformation("Определено намерение: CheckCalendar");
                await HandleCheckCalendarCommand(user, message.Chat.Id, cancellationToken);
                break;

            case UserIntent.ActivateCalendar:
                logger.LogInformation("Определено намерение: ActivateCalendar");
                await HandleConnectCalendarCommand(user, message.Chat.Id, cancellationToken);
                break;

            case UserIntent.About:
                logger.LogInformation("Определено намерение: About");
                await HandleAboutCommand(user, message.Chat.Id, cancellationToken);
                break;
            
            case UserIntent.GeneralChat:
                logger.LogInformation("Определено намерение: GeneralChat");
    
                if (message.Text is not null)
                {
                    await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);

                    var response = await llmService.GenerateGeneralChatResponseAsync(message.Text);
                    if (response.Contains("[ERROR]"))
                    {
                        await botClient.SendMessage(message.Chat.Id, "Я здесь, чтобы помочь вам с планированием и энергией. Давайте сосредоточимся на этом!", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.SendMessage(message.Chat.Id, response, cancellationToken: cancellationToken);
                    }
                }
                break;

            default: // Unknown
                logger.LogInformation("Намерение не определено.");
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Я не совсем понял, что вы имеете в виду. Вы можете попросить меня составить план, проверить календарь или оценить энергию.",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Обрабатывает неизвестные типы обновлений от Telegram.
    /// </summary>
    private Task UnknownUpdateHandlerAsync(Update update)
    {
        logger.LogInformation("Получен неизвестный тип обновления: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }
}