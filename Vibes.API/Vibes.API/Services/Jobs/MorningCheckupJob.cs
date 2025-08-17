using Quartz;

namespace Vibes.API.Services.Jobs;

// Атрибут, который запрещает одновременный запуск нескольких экземпляров этого задания.
// Если проверка пользователей займет больше минуты, новая не начнется, пока старая не закончится.
[DisallowConcurrentExecution]
public class MorningCheckupJob : IJob
{
    private readonly ILogger<MorningCheckupJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    // Зависимости будут внедрены автоматически благодаря интеграции с ASP.NET Core
    public MorningCheckupJob(ILogger<MorningCheckupJob> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Quartz Job: Наступило время для утреннего чекапа. Запускаю проверку.");
        
        try
        {
            // Используем тот же подход со 'scope' для получения сервисов
            using var scope = _scopeFactory.CreateScope();
            var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
            var messageHandler = scope.ServiceProvider.GetRequiredService<TelegramMessageHandler>();
            
            var users = await databaseService.GetAllUsersAsync();

            foreach (var user in users)
            {
                // Проверяем, есть ли у пользователя отметка о последнем чекапе,
                // и совпадает ли ДАТА этой отметки с СЕГОДНЯШНЕЙ ДАТОЙ (в UTC).
                if (user.LastCheckupSentUtc.HasValue && user.LastCheckupSentUtc.Value.Date == DateTime.UtcNow.Date)
                {
                    _logger.LogInformation("Пропускаю пользователя {UserId}. Утренний чекап уже был отправлен сегодня.", user.Id);
                    continue; // Переходим к следующему пользователю
                }
                
                _logger.LogInformation("Quartz Job: Инициирую чекап для пользователя {UserId}", user.Id);
                await messageHandler.StartMorningCheckupAsync(user, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quartz Job: Произошла ошибка при выполнении утреннего чекапа.");
        }
    }
}