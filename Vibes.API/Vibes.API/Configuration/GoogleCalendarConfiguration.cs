namespace Vibes.API.Configuration;

public class GoogleCalendarConfiguration
{
    public string RedirectUri { get; init; } = default!;
    public string ApplicationName { get; init; } = default!;
    public string ClientId { get; init; } = default!;
    public string ClientSecret { get; init; } = default!;
}