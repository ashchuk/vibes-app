using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using Vibes.API.Configuration;
using Vibes.API.Models;

namespace Vibes.API.Services;

public interface ICalendarService
{
    string GenerateAuthUrl(long telegramUserId);
    Task HandleAuthCallback(VibesUser user, string code);
    Task<IList<Event>> GetUpcomingEvents(VibesUser user, int maxResults = 10);
    
    /// Получает все события пользователя за указанную дату.
    /// </summary>
    /// <param name="user">Пользователь</param>
    /// <param name="date">Дата, за которую нужно получить события</param>
    /// <returns>Список событий.</returns>
    Task<IList<Event>> GetEventsForDateAsync(VibesUser user, DateTime date);
    
    /// <summary>
    /// Получает одно конкретное событие по его ID.
    /// </summary>
    /// <param name="user">Пользователь</param>
    /// <param name="eventId">ID события из Google Calendar</param>
    /// <returns>Объект события или null, если не найдено.</returns>
    Task<Event?> GetEventByIdAsync(VibesUser user, string eventId);
}

public class CalendarService : ICalendarService
{
    private readonly GoogleCalendarConfiguration _config;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(IOptions<GoogleCalendarConfiguration> config, ILogger<CalendarService> logger)
    {
        _logger = logger;
        _config = config.Value;
    }

    private IAuthorizationCodeFlow CreateFlow()
    {
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _config.ClientId,
                ClientSecret = _config.ClientSecret
            },
            Scopes =
            [
                Google.Apis.Calendar.v3.CalendarService.Scope.CalendarReadonly
            ],
            Prompt = "consent"
        });
    }

    public string GenerateAuthUrl(long telegramUserId)
    {
        var flow = CreateFlow();
        var redirectUri = _config.RedirectUri; // Этот URL должен совпадать с тем, что в Google Console

        var authUrl = flow.CreateAuthorizationCodeRequest(redirectUri);
        
        authUrl.State = telegramUserId.ToString(); // Отправляем ID пользователя, чтобы узнать его при callback'е

        return authUrl.Build().ToString();
    }

    public async Task HandleAuthCallback(VibesUser user, string code)
    {
        _logger.LogWarning("!!! Current REDIRECT URL: {RedirectUri} !!!", _config.RedirectUri);
        var flow = CreateFlow();
        var tokenResponse = await flow.ExchangeCodeForTokenAsync(
            userId: user.TelegramId.ToString(), // Связываем токен с ID нашего пользователя
            code: code,
            redirectUri: _config.RedirectUri,
            CancellationToken.None);

        user.GoogleCalendarRefreshToken = tokenResponse.RefreshToken;
        _logger.LogInformation("Успешно получен и сохранен RefreshToken для пользователя {UserId}", user.Id);
    }

    public async Task<IList<Event>> GetUpcomingEvents(VibesUser user, int maxResults = 10)
    {
        if (!user.IsGoogleCalendarConnected)
        {
            _logger.LogWarning("Попытка получить события для пользователя {UserId} без подключенного календаря.", user.Id);
            return new List<Event>();
        }

        var userCredential = new UserCredential(CreateFlow(), user.TelegramId.ToString(), new TokenResponse
        {
            RefreshToken = user.GoogleCalendarRefreshToken
        });
        
        var calendarService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = userCredential,
            ApplicationName = _config.ApplicationName
        });

        var request = calendarService.Events.List("primary");
        request.TimeMinDateTimeOffset = DateTime.Now;
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.MaxResults = maxResults;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var events = await request.ExecuteAsync();
        return events.Items;
    }
    
    public async Task<IList<Event>> GetEventsForDateAsync(VibesUser user, DateTime date)
    {
        if (!user.IsGoogleCalendarConnected)
        {
            _logger.LogWarning("Попытка получить события для пользователя {UserId} без подключенного календаря.", user.Id);
            return new List<Event>();
        }
        
        var userCredential = new UserCredential(CreateFlow(), user.TelegramId.ToString(), new TokenResponse
        {
            RefreshToken = user.GoogleCalendarRefreshToken
        });
        
        var googleCalendarService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = userCredential,
            ApplicationName = _config.ApplicationName,
        });

        var request = googleCalendarService.Events.List("primary");
        
        // Устанавливаем временные рамки: с начала до конца указанного дня
        request.TimeMinDateTimeOffset = date.Date; // 00:00:00 указанного дня
        request.TimeMaxDateTimeOffset = date.Date.AddDays(1).AddTicks(-1); // 23:59:59.999... указанного дня
        
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var events = await request.ExecuteAsync();
        return events.Items ?? new List<Event>(); // Возвращаем пустой список, если Items is null
    }
    
    public async Task<Event?> GetEventByIdAsync(VibesUser user, string eventId)
    {
        if (!user.IsGoogleCalendarConnected || string.IsNullOrEmpty(eventId))
        {
            return null;
        }
        
        var userCredential = new UserCredential(CreateFlow(), user.TelegramId.ToString(), new TokenResponse
        {
            RefreshToken = user.GoogleCalendarRefreshToken
        });
        
        var googleCalendarService = new Google.Apis.Calendar.v3.CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = userCredential,
            ApplicationName = _config.ApplicationName,
        });

        try
        {
            // Используем метод .Get() для получения события по ID
            var request = googleCalendarService.Events.Get("primary", eventId);
            return await request.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении события по ID {EventId} для пользователя {UserId}", eventId, user.Id);
            return null;
        }
    }
}