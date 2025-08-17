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
            // --- ОБРАБОТЧИК ДЛЯ КНОПОК ОНБОРДИНГА ---
            "onboarding_start" when user.State == ConversationState.OnboardingAwaitingStart
                => HandleOnboardingStart(user, callbackQuery.From.Id, cancellationToken),

            // --- ОБРАБОТЧИК ДЛЯ КНОПОК ОЦЕНКИ ЭНЕРГИИ ---
            var data when data.StartsWith("energy_rating_")
                => HandleEnergyRatingCallback(user, callbackQuery, cancellationToken),

            // --- ОБРАБОТЧИК ДЛЯ КНОПКИ ПРИНЯТЬ ПЛАН ---
            "plan_accept" => HandlePlanAccept(user, callbackQuery.Message.Chat.Id, callbackQuery.Message, cancellationToken),

            // --- ОБРАБОТЧИК ДЛЯ КНОПКИ ИЗМЕНИТЬ ПЛАН ---
            "plan_edit" => HandlePlanEdit(user, callbackQuery.Message.Chat.Id, cancellationToken),

            var data when data.StartsWith("rate_event_")
                => HandleEventRatingCallback(user, callbackQuery, cancellationToken),

            _ => botClient.AnswerCallbackQuery(callbackQuery.Id, "Эта кнопка уже неактивна", cancellationToken: cancellationToken)
        };
        await task;
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
            cancellationToken: cancellationToken);
    }

    private async Task OnMessage(Message message, CancellationToken cancellationToken)
    {
        if (message.From is null) return;

        var user = await databaseService.GetOrCreateUserAsync(message.From);

        if (message.Text is { } messageText)
        {
            var command = messageText.Split(' ')[0];
            var task = command switch
            {
                "/start" => HandleStartCommand(user, message.Chat.Id, cancellationToken),
                "/plan" => HandlePlanCommand(user, message.Chat.Id, cancellationToken),
                "/energy" => HandleEnergyCommand(user, message.Chat.Id, cancellationToken),
                "/connect_calendar" => HandleConnectCalendarCommand(user, message.Chat.Id, cancellationToken),
                "/check_calendar" => HandleCheckCalendarCommand(user, message.Chat.Id, cancellationToken),
                _ => HandleConversation(user, message, cancellationToken)
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

    private async Task HandleStartCommand(VibesUser user, long chatId, CancellationToken cancellationToken)
    {
        user.State = ConversationState.OnboardingAwaitingStart;
        user.ConversationContext = null;
        await databaseService.UpdateUserAsync(user);

        var personalizedText = $"{user.FirstName ?? "Привет"}! Я помогу спланировать день так, чтобы энергии хватало на важное и ты не выгорал. Давай за 30 секунд настроим часовой пояс и удобные времена напоминаний.";

        var inlineKeyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("OK, поехали", "onboarding_start"));

        await botClient.SendMessage(chatId: chatId, text: personalizedText, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }

    private async Task HandleConversation(VibesUser user, Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null) return; // Мы обрабатываем только текстовые сообщения в этом методе

        switch (user.State)
        {
            // --- СЦЕНАРИЙ ОНБОРДИНГА ---
            case ConversationState.OnboardingAwaitingTimezone:
                var timezone = message.Text;
                // TODO: Добавить валидацию таймзоны и сохранить ее в профиле пользователя
                logger.LogInformation("Пользователь {UserId} установил таймзону: {Timezone}", user.Id, timezone);

                user.State = ConversationState.AwaitingRetroSleepAndActivity; // Переходим к сбору ретро-данных
                await databaseService.UpdateUserAsync(user);

                const string retroText = "Готово! Теперь я буду учитывать твою таймзону.\n\nХочешь ввести данные о сне и шаговой активности за предыдущие 2 дня? Тогда я смогу прислать тебе инсайты уже сейчас.\n\nПросто напиши в свободной форме, например: 'Позавчера спал 6ч, прошел 5000 шагов. Вчера 8ч, 10000 шагов'.";
                await botClient.SendMessage(message.Chat.Id, retroText, cancellationToken: cancellationToken);
                break;

            // --- СЦЕНАРИЙ СБОРА РЕТРО-ДАННЫХ (User Story #8) ---
            case ConversationState.AwaitingRetroSleepAndActivity:
                var retroData = message.Text;
                logger.LogInformation("Пользователь {UserId} прислал ретро-данные: {RetroData}", user.Id, retroData);

                // Отправляем текст в LlmService и получаем готовый инсайт
                var insight = await llmService.GenerateRetroInsightAsync(retroData);

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
                var sleepHours = message.Text;
                logger.LogInformation("Пользователь {UserId} спал: {SleepHours}", user.Id, sleepHours);

                // --- ИЗМЕНЕНИЕ: ОБНОВЛЯЕМ КОНТЕКСТ ДАННЫМИ О СНЕ ---
                var contextN3 = JsonSerializer.Deserialize<MorningCheckupContext>(user.ConversationContext ?? "{}")
                                ?? new MorningCheckupContext();
                contextN3.SleepHours = sleepHours;
                user.ConversationContext = JsonSerializer.Serialize(contextN3);

                user.State = ConversationState.AwaitingMorningPlans;
                await databaseService.UpdateUserAsync(user);

                await botClient.SendMessage(message.Chat.Id, "Отлично. Чем сегодня займёмся? Назови 1–3 обязательных дела.", cancellationToken: cancellationToken);
                break;

            case ConversationState.AwaitingMorningPlans:
                var plans = message.Text;
                logger.LogInformation("Планы пользователя {UserId}: {Plans}", user.Id, plans);

                // --- НАЧИНАЕТСЯ ГЛАВНАЯ ЛОГИКА ---

                // 1. Восстанавливаем контекст
                var contextN4 = JsonSerializer.Deserialize<MorningCheckupContext>(user.ConversationContext ?? "{}")
                                ?? new MorningCheckupContext();

                // 2. Получаем события из календаря
                var morningPlancalendarEvents = await calendarService.GetUpcomingEvents(user, 5);

                // 3. Вызываем LLM для генерации плана
                var generatedPlan = await llmService.GenerateMorningPlanAsync(
                    contextN4.EnergyRating ?? "не указана",
                    contextN4.SleepHours ?? "не указано",
                    plans,
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
                var textSchedule = message.Text;
                logger.LogInformation("Пользователь {UserId} прислал расписание текстом: {TextSchedule}", user.Id, textSchedule);


                // 1. Сообщаем пользователю, что мы начали работу
                await botClient.SendMessage(message.Chat.Id, "Принял! Сверяюсь с вашим календарем и составляю лучший план на день. Секунду...", cancellationToken: cancellationToken);

                // 2.1 Получаем события из календаря
                var scheduleCalendarEvents = await calendarService.GetUpcomingEvents(user);
                // 2.2 ИЗВЛЕКАЕМ "ПАМЯТЬ" ИЗ БАЗЫ ДАННЫХ
                var recentPlans = await databaseService.GetRecentDailyPlansAsync(user.Id);
                var recentRatings = await databaseService.GetRecentEventRatingsAsync(user.Id);
                
                // 3. Вызываем LLM для генерации плана на основе текста и событий календаря
                var structuredPlan = await llmService.GeneratePlanFromTextAsync(
                    textSchedule,
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
                await HandleDefaultMessageAsync(user, message, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Обрабатывает нажатие на кнопку "Подтвердить план".
    /// </summary>
    private async Task HandlePlanAccept(VibesUser user, long chatId, Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Пользователь {UserId} подтвердил план.", user.Id);

        // Извлекаем текст плана из сообщения, к которому были привязаны кнопки.
        var planText = message.Text;
        if (string.IsNullOrWhiteSpace(planText))
        {
            logger.LogWarning("Текст плана для сохранения пуст. Пользователь {UserId}", user.Id);
            planText = "План не был сохранен из-за ошибки.";
        }

        // Создаем и сохраняем запись о плане в базе данных с помощью нашего нового универсального метода.
        var newPlan = new DailyPlan
        {
            UserId = user.Id,
            PlanDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PlanText = planText,
            CreatedUtc = DateTime.UtcNow
        };
        await databaseService.AddRecordAsync(newPlan);

        // Отправляем пользователю позитивное подтверждение.
        await botClient.SendMessage(
            chatId: chatId,
            text: "Отлично! План принят и сохранен. Я буду рядом, чтобы помочь тебе в течение дня и напомню о важных моментах. 😉",
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

    private async Task HandleDefaultMessageAsync(VibesUser user, Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null) return;

        // 1. Отправляем текст в LLM для классификации
        var intent = await llmService.ClassifyUserIntentAsync(message.Text);

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

            case UserIntent.GeneralChat:
                logger.LogInformation("Определено намерение: GeneralChat");
                // TODO: Реализовать ответ-заглушку для общей болтовни
                await botClient.SendMessage(message.Chat.Id, "Я здесь, чтобы помочь вам с планированием и энергией. Давайте сосредоточимся на этом!", cancellationToken: cancellationToken);
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