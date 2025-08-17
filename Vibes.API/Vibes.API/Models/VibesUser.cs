namespace Vibes.API.Models;

public class VibesUser
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public bool IsBot { get; set; }
    public required string Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public required string LanguageCode { get; set; }
    
    /// <summary>
    /// Текущее состояние диалога с пользователем. Например: "AWAITING_TIMEZONE_CONFIRMATION".
    /// </summary>
    public ConversationState? State { get; set; } = Models.ConversationState.None;
    
    /// <summary>
    /// Временные данные для многошагового диалога в формате JSON.
    /// Например, хранение ID сообщения для редактирования или временных настроек.
    /// </summary>
    public string? ConversationContext { get; set; }
    
    /// <summary>
    /// --- ПОЛЯ ДЛЯ GOOGLE CALENDAR ---
    /// Мы храним только RefreshToken.
    /// Это долгоживущий токен, который позволяет нам получать новый AccessToken в любое время.
    /// Это безопасно и эффективно.
    /// </summary>
    public string? GoogleCalendarRefreshToken { get; set; }
    public bool IsGoogleCalendarConnected => !string.IsNullOrEmpty(GoogleCalendarRefreshToken);
    
    /// <summary>
    /// Хранит дату и время (в UTC) последнего успешно отправленного утреннего чекапа.
    /// </summary>
    public DateTime? LastCheckupSentUtc { get; set; }
    
    /// <summary>
    /// Хранит дату и время (в UTC) последнего успешно отправленного вечернего чекапа.
    /// </summary>
    public DateTime? LastEveningCheckupSentUtc { get; set; }
    
    public bool IsOnboardingCompleted { get; set; } = false;
}