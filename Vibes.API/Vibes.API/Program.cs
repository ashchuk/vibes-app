using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Quartz;
using Telegram.Bot;
using Vibes.API;
using Vibes.API.Configuration;
using Vibes.API.Context;
using Vibes.API.Extensions;
using Vibes.API.Services.Jobs;
var builder = WebApplication.CreateBuilder(args);

// Load configurations from appsettings.json
IConfigurationSection botConfigSection = builder.Configuration.GetSection("BotConfiguration");
builder.Services.Configure<BotConfiguration>(botConfigSection);
IConfigurationSection geminiConfiguration = builder.Configuration.GetSection("GeminiConfiguration");
builder.Services.Configure<GeminiConfiguration>(geminiConfiguration);
IConfigurationSection calendarConfiguration = builder.Configuration.GetSection("GoogleCalendarConfiguration");
builder.Services.Configure<GoogleCalendarConfiguration>(calendarConfiguration);
IConfigurationSection memoConfiguration = builder.Configuration.GetSection("MemoConfiguration");
builder.Services.Configure<MemoConfiguration>(memoConfiguration);
IConfigurationSection quartzConfigSection = builder.Configuration.GetSection("QuartzConfiguration");
builder.Services.Configure<QuartzConfiguration>(quartzConfigSection);

// Add services to the container.
//
// Swagger
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddCustomSwagger();

// Controllers
builder.Services.AddControllers();

// Telegram bot client
builder.Services.AddHttpClient("tgwebhook")
    .AddTypedClient<ITelegramBotClient>(httpClient =>
        new TelegramBotClient(botConfigSection.Get<BotConfiguration>()!.BotToken, httpClient));

// Application services: database, calendar, MCP, Mem0 memory, etc
builder.Services.AddServices();

// SQLite database
builder.Services.AddDbContext<IVibesContext, VibesContext>(options =>
{
    _ = options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    _ = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "AppData", "Storage", "Database"));
    var datasource = Path.Combine(Directory.GetCurrentDirectory(), "AppData", "Storage", "Database", "Vibes.db");
    _ = options.UseSqlite($"Data Source={datasource}");
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Говорим приложению, что оно должно доверять заголовкам X-Forwarded-For и X-Forwarded-Proto
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// Регистрация планировщика задач
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();

    // Получаем CRON-выражение из конфигурации
    var cronSchedule = quartzConfigSection.Get<QuartzConfiguration>()?.MorningCheckupCronSchedule;

    // Проверка на случай, если значение в конфиге отсутствует
    if (string.IsNullOrWhiteSpace(cronSchedule))
    {
        // Устанавливаем безопасное значение по умолчанию и выводим ошибку в лог
        cronSchedule = "0 0 6 * * ?"; // Каждый день в 6:00 UTC
        Console.WriteLine("[WARNING] CRON-выражение для утреннего чекапа не найдено в конфигурации. Используется значение по умолчанию: " + cronSchedule);
    }

    var jobKey = new JobKey("MorningCheckupJob");
    q.AddJob<MorningCheckupJob>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("MorningCheckupJob-trigger")
        .WithCronSchedule(cronSchedule)
        .StartNow()
    );
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

app.UseSwagger(c => { c.RouteTemplate = "api/swagger/{documentName}/swagger.json"; });
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "Vibes API V1");
    c.RoutePrefix = "api/swagger";
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/vibes", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new Vibe
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .Take(1).ToArray();
        return forecast;
    })
    .WithName("GetVibe");

app.MapControllers();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "AppData", "Static")),
    RequestPath = "",
    // Uncomment to enable static files authorization
    // OnPrepareResponse = ctx =>
    // {
    //     if (ctx.Context.User.Identity is { IsAuthenticated: true }) return;
    //
    //     // respond HTTP 401 Unauthorized.
    //     ctx.Context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
    //     // flush response
    //     ctx.Context.Response.ContentLength = 0;
    //     ctx.Context.Response.Body = Stream.Null;
    //     // disable cache
    //     ctx.Context.Response.Headers.Append("Cache-Control", "no-store");
    // }
});

// Application state init
using (IServiceScope scope = app.Services.CreateScope())
{
    // Set up the Telegram Bot webhook  
    ITelegramBotClient bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
    IOptions<BotConfiguration> botConfig = scope.ServiceProvider.GetRequiredService<IOptions<BotConfiguration>>();
    
    await bot.SetWebhook(
        botConfig.Value.BotWebhookUrl.ToString(),
        allowedUpdates: [],
        secretToken: botConfig.Value.SecretToken,
        cancellationToken: CancellationToken.None);

    // Migrate the database if needed 
    if (scope.ServiceProvider.GetRequiredService<IVibesContext>() is VibesContext context)
    {
        IEnumerable<string> migrations = await context.Database.GetPendingMigrationsAsync();
        if (migrations.Any())
        {
            await context.Database.MigrateAsync();
        }
    }
}

await app.RunAsync();

namespace Vibes.API
{
    record Vibe(DateOnly Date, string? Summary);
}