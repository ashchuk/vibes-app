using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Vibes.API.Services;

namespace Vibes.API.Controllers;

[ApiController]
[Route("/api/google-auth")]
public class GoogleAuthController(
    ICalendarService calendarService,
    IDatabaseService databaseService,
    ITelegramBotClient botClient,
    ILogger<GoogleAuthController> logger) : ControllerBase
{

    // Этот метод не будет вызываться напрямую, он нужен для получения URI
    private string GetCallbackUrl() => Url.Action(nameof(Callback), "GoogleAuth", null, Request.Scheme)!;


    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code) || !long.TryParse(state, out var telegramUserId))
        {
            logger.LogError("Неверный callback от Google. Code: {Code}, State: {State}", code, state);
            return BadRequest("Неверные параметры авторизации.");
        }

        // Упрощенное создание для примера
        var user = await databaseService.GetOrCreateUserAsync(new()
        {
            Id = telegramUserId
        });
        if (user == null)
        {
            return NotFound("Пользователь не найден.");
        }

        // Обмениваем код на токен и сохраняем его
        await calendarService.HandleAuthCallback(user, code);
        await databaseService.UpdateUserAsync(user);
        logger.LogInformation("Календарь для пользователя {UserId} успешно подключен.", user.Id);

        try
        {
            var successText = "Отлично, ваш Google Calendar успешно подключен! 🎉\n\n" +
                              "Теперь я могу видеть ваше расписание и помогать с планированием.\n\n" +
                              "Хотите, я прямо сейчас **проверю ваш календарь** на сегодня или **составлю план** на завтра?";

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                // Эти callbackData будут перехвачены нашим OnCallbackQuery и запустят соответствующие команды
                InlineKeyboardButton.WithCallbackData("🔍 Проверить календарь", "command_check_calendar"),
                InlineKeyboardButton.WithCallbackData("📝 Составить план", "command_plan")
            });

            await botClient.SendMessage(
                chatId: telegramUserId,
                text: successText,
                replyMarkup: inlineKeyboard,
                cancellationToken: CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось отправить подтверждающее сообщение в Telegram после подключения календаря для пользователя {UserId}", user.Id);
        }
        
        // Показываем пользователю страницу об успехе
        var htmlContent = """
                <!DOCTYPE html>
                <html lang="ru">
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>Успешно!</title>
                    <style>
                        body {
                            margin: 0;
                            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
                            background-color: #f0f2f5;
                            display: flex;
                            justify-content: center;
                            align-items: center;
                            height: 100vh;
                            color: #1c1e21;
                        }
                        .card {
                            background-color: #ffffff;
                            border-radius: 12px;
                            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
                            text-align: center;
                            padding: 40px 30px;
                            max-width: 400px;
                            width: 90%;
                            transform: scale(0.95);
                            opacity: 0;
                            animation: fadeInScale 0.5s forwards ease-out;
                        }
                        .icon {
                            width: 72px;
                            height: 72px;
                            margin-bottom: 20px;
                        }
                        h1 {
                            font-size: 24px;
                            font-weight: 600;
                            margin: 0 0 10px 0;
                        }
                        p {
                            font-size: 16px;
                            color: #606770;
                            line-height: 1.5;
                        }
                        @keyframes fadeInScale {
                            to {
                                transform: scale(1);
                                opacity: 1;
                            }
                        }
                    </style>
                </head>
                <body>
                    <div class="card">
                        <!-- SVG иконка зеленой галочки -->
                        <svg class="icon" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                            <path fill="#34c759" d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
                        </svg>
                        
                        <h1>Отлично!</h1>
                        <p>Ваш Google Календарь успешно подключен. Можете возвращаться в Telegram.</p>
                    </div>
                </body>
                </html>
        """;

        return new ContentResult
        {
            Content = htmlContent,
            ContentType = "text/html; charset=utf-8",
            StatusCode = 200
        };
    }
}