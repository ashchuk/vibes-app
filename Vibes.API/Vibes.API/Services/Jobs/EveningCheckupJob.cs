using Quartz;

namespace Vibes.API.Services.Jobs;

[DisallowConcurrentExecution]
public class EveningCheckupJob : IJob
{
    private readonly ILogger<EveningCheckupJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public EveningCheckupJob(ILogger<EveningCheckupJob> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Quartz Job (CRON): Наступило время для вечернего чекапа. Запускаю проверку.");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
            var messageHandler = scope.ServiceProvider.GetRequiredService<TelegramMessageHandler>();

            // Выбираем только активных пользователей
            var users = await databaseService.GetActiveUsersForCheckupAsync();
            _logger.LogInformation("Найдено {UserCount} активных пользователей для вечернего чекапа.", users.Count);

            foreach (var user in users)
            {
                // Проверяем, что чекап сегодня еще не отправлялся
                if (user.LastEveningCheckupSentUtc.HasValue && user.LastEveningCheckupSentUtc.Value.Date == DateTime.UtcNow.Date)
                {
                    _logger.LogInformation("Пропускаю пользователя {UserId}. Вечерний чекап уже был отправлен сегодня.", user.Id);
                    continue;
                }

                _logger.LogInformation("Инициирую вечерний чекап для пользователя {UserId}", user.Id);
                await messageHandler.StartEveningCheckupAsync(user, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quartz Job: Произошла ошибка при выполнении вечернего чекапа.");
        }
    }
}