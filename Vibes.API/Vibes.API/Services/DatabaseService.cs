using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;
using Vibes.API.Context;
using Vibes.API.Models;

namespace Vibes.API.Services;

public interface IDatabaseService
{
    /// <summary>
    /// Находит пользователя по Telegram ID или создает нового, если не найден.
    /// </summary>
    Task<VibesUser> GetOrCreateUserAsync(User from);

    /// <summary>
    /// Обновляет данные пользователя в базе данных.
    /// </summary>
    Task UpdateUserAsync(VibesUser user);
    
    Task<List<VibesUser>> GetAllUsersAsync();
    
    /// <summary>
    /// Добавляет любую новую запись (сущность) в базу данных.
    /// </summary>
    /// <typeparam name="T">Тип сущности (например, DailyPlan, VibesMetric)</typeparam>
    /// <param name="record">Объект для сохранения</param>
    Task AddRecordAsync<T>(T record) where T : class;
}

public class DatabaseService : IDatabaseService
{
    private readonly IVibesContext _context;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IVibesContext context, ILogger<DatabaseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Находит пользователя по Telegram ID. Если пользователь не найден, создает новую запись в базе данных.
    /// </summary>
    public async Task<VibesUser> GetOrCreateUserAsync(User from)
    {
        // Ищем пользователя в базе данных по его уникальному Telegram ID
        var user = await _context.VibesUsers.FirstOrDefaultAsync(u => u.TelegramId == from.Id);

        // Если пользователь найден, возвращаем его
        if (user is not null)
        {
            return user;
        }

        // Если пользователь не найден, создаем нового
        _logger.LogInformation("Новый пользователь: {FirstName} (@{Username}), ID: {TelegramId}. Создаем запись в БД.",
            from.FirstName, from.Username, from.Id);

        var newUser = new VibesUser
        {
            TelegramId = from.Id,
            IsBot = from.IsBot,
            FirstName = from.FirstName,
            LastName = from.LastName,
            Username = from.Username ?? $"user_{from.Id}", // Username может быть null, предусматриваем это
            LanguageCode = from.LanguageCode ?? "en", // Язык по умолчанию, если не определен
            State = ConversationState.None // Начальное состояние по умолчанию
        };

        // Используем наш вспомогательный метод из IVibesContext для добавления и сохранения
        return await _context.AddNewRecord(newUser);
    }

    /// <summary>
    /// Обновляет данные существующего пользователя в базе данных.
    /// </summary>
    public async Task UpdateUserAsync(VibesUser user)
    {
        await _context.UpdateRecord(user);
        _logger.LogInformation("Обновлено состояние пользователя {UserId} на {State}", user.Id, user.State);
    }
    
    public async Task<List<VibesUser>> GetAllUsersAsync()
    {
        return await _context.VibesUsers.ToListAsync();
    }
    
    public async Task AddRecordAsync<T>(T record) where T : class
    {
        // Мы просто вызываем уже существующий в нашем IVibesContext метод AddNewRecord.
        // Это пример хорошего разделения ответственности: DatabaseService отвечает за бизнес-логику,
        // а VibesContext - за низкоуровневые операции с базой данных.
        await _context.AddNewRecord(record);
        _logger.LogInformation("В базу данных добавлена новая запись типа {RecordType}", typeof(T).Name);
    }
}