using Microsoft.OpenApi.Models;
using Vibes.API.Services;

namespace Vibes.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddServices(this IServiceCollection services)
    {
        // Misc services
        services.AddScoped<IDatabaseService, DatabaseService>();
        services.AddTransient<ICalendarService, CalendarService>();
        services.AddTransient<IMcpService, McpService>();
        services.AddTransient<ILlmService, LlmService>();
        
        // Telegram message handler
        services.AddTransient<TelegramMessageHandler>();
    }

    public static void AddCustomSwagger(this IServiceCollection services) =>
        services.AddSwaggerGen(option =>
        {
            option.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Vibes API",
                Version = "v1",
            });
        });
}