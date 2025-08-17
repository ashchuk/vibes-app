namespace Vibes.API.Models;

public enum UserIntent
{
    // Пользователь хочет составить или изменить план
    Plan,
    // Пользователь хочет зафиксировать уровень энергии
    SetEnergy,
    // Пользователь хочет проверить события в календаре
    CheckCalendar,
    // Пользователь хочет авторизоваться в календаре
    ActivateCalendar,
    // Беседа
    GeneralChat,
    // Расскажи о себе
    About,
    // Намерение не удалось определить
    Unknown
}