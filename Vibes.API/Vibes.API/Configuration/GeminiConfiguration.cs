namespace Vibes.API.Configuration;

public class GeminiConfiguration
{
    public string ApiKey { get; init; } = default!;
    public string ImageBaseUrl  { get; init; } = default!;
    public string TextBaseUrl { get; init; } = default!;
}
