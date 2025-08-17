namespace Vibes.API.Models;

public enum ConversationState
{
    /// <summary>
    /// Нет активного многошагового диалога.
    /// </summary>
    None,

    // --- Состояния Онбординга ---
    OnboardingAwaitingStart,
    OnboardingAwaitingTimezone,
    
    // --- Состояния для ретро-данных (User Story #8) ---
    AwaitingRetroSleepAndActivity,

    // --- Состояния для утреннего чекапа (User Story #2) ---
    AwaitingMorningEnergyRating,
    AwaitingMorningSleepHours,
    AwaitingMorningPlans,

    // --- Состояния для распознавания расписания (User Story #11) ---
    AwaitingSchedulePhoto,
    OnboardingCompleted
}