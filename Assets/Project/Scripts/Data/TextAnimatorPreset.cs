// TextAnimatorPreset.cs
// Royal Leech Ultimate Text Animation Constructor
// ScriptableObject с модульными эффектами и ВСЕМИ настройками

using UnityEngine;

/// <summary>
/// Режим анимации.
/// </summary>
public enum AnimationMode
{
    TimeBasedTypewriter,  // Посимвольное появление по времени
    DistanceBased         // Управление извне через progress
}

/// <summary>
/// Пресет для анимации текста.
/// Содержит ВСЕ настройки: режим, тайминги, эффекты.
/// </summary>
[CreateAssetMenu(fileName = "NewTextPreset", menuName = "Game/Text Animator Preset")]
public class TextAnimatorPreset : ScriptableObject
{
    [Header("=== MODE ===")]
    [Tooltip("Режим анимации")]
    public AnimationMode mode = AnimationMode.TimeBasedTypewriter;
    
    [Tooltip("Включить per-letter эффекты")]
    public bool enableEffects = true;
    
    [Header("=== TIME-BASED SETTINGS ===")]
    [Tooltip("Символов в секунду (для TimeBasedTypewriter)")]
    public float charactersPerSecond = 30f;
    
    [Tooltip("Задержка перед началом")]
    public float delay = 0f;
    
    [Tooltip("Автостарт при активации")]
    public bool startOnEnable = false;
    
    [Header("=== DISTANCE-BASED SETTINGS ===")]
    [Tooltip("Скорость интерполяции символов")]
    public float interpolationSpeed = 15f;
    
    [Header("=== TIMING ===")]
    [Tooltip("Длительность появления одной буквы")]
    [Range(0.05f, 1f)]
    public float appearDuration = 0.25f;
    
    [Tooltip("Задержка между появлением букв (stagger)")]
    [Range(0f, 0.2f)]
    public float appearStagger = 0.03f;
    
    [Tooltip("Скорость интерполяции эффектов")]
    [Range(5f, 30f)]
    public float effectSmoothSpeed = 15f;
    
    [Tooltip("Время плавного перехода между состояниями (кроссфейд)")]
    [Range(0.05f, 0.5f)]
    public float stateTransitionDuration = 0.15f;
    
    [Header("=== APPEAR EFFECTS ===")]
    [Tooltip("Эффекты при появлении буквы")]
    public StateEffects appearEffects = new StateEffects();
    
    [Header("=== IDLE EFFECTS ===")]
    [Tooltip("Эффекты в состоянии покоя (progress = 0)")]
    public StateEffects idleEffects = new StateEffects();
    
    [Header("=== ACTIVE EFFECTS ===")]
    [Tooltip("Эффекты при свайпе (0 < progress < 1)")]
    public StateEffects activeEffects = new StateEffects();
    
    [Header("=== SELECTED EFFECTS ===")]
    [Tooltip("Эффекты при выборе (progress >= 1)")]
    public StateEffects selectedEffects = new StateEffects();
    
    [Header("=== DISAPPEAR NORMAL ===")]
    [Tooltip("Обычное пропадание при свайпе назад")]
    public StateEffects disappearNormalEffects = new StateEffects();
    [Tooltip("Длительность обычного пропадания")]
    [Range(0.05f, 0.5f)]
    public float disappearNormalDuration = 0.15f;
    
    [Header("=== DISAPPEAR RETURN ===")]
    [Tooltip("Быстрое пропадание при возврате к центру или смене направления")]
    public StateEffects disappearReturnEffects = new StateEffects();
    [Tooltip("Длительность пропадания при возврате")]
    [Range(0.05f, 0.5f)]
    public float disappearReturnDuration = 0.15f;
    
    [Header("=== DISAPPEAR SELECTED ===")]
    [Tooltip("Пропадание после выбора")]
    public StateEffects disappearSelectedEffects = new StateEffects();
    [Tooltip("Длительность пропадания после выбора")]
    [Range(0.05f, 0.5f)]
    public float disappearSelectedDuration = 0.25f;
    
    /// <summary>
    /// Вычислить эффекты для появления.
    /// </summary>
    public EffectResult CalculateAppear(float time, int charIndex, float stateProgress)
    {
        EffectResult result = appearEffects.Calculate(time, charIndex, stateProgress, 1f);
        
        // Если нет эффектов появления - просто показываем сразу без интерполяции scale
        // (scale уже = 1 от Identity, так что буква появляется мгновенно)
        // Если нужна базовая интерполяция - раскомментируйте:
        // if (appearEffects.effects == null || appearEffects.effects.Length == 0)
        // {
        //     result.scale = stateProgress; // 0 -> 1
        // }
        
        return result;
    }
    
    /// <summary>
    /// Вычислить эффекты для Idle/Active/Selected в зависимости от progress.
    /// </summary>
    public EffectResult CalculateIdle(float time, int charIndex, float progress)
    {
        bool isSelected = progress >= 1f;
        bool isActive = progress > 0.01f;
        
        if (isSelected)
        {
            return selectedEffects.Calculate(time, charIndex, 1f, 1f);
        }
        else if (isActive)
        {
            EffectResult idle = idleEffects.Calculate(time, charIndex, 1f, 1f);
            EffectResult active = activeEffects.Calculate(time, charIndex, 1f, progress);
            
            return new EffectResult
            {
                offset = idle.offset + active.offset * progress,
                scale = Mathf.Lerp(idle.scale, active.scale, progress),
                alpha = idle.alpha * active.alpha,
                rotation = idle.rotation + active.rotation * progress
            };
        }
        else
        {
            return idleEffects.Calculate(time, charIndex, 1f, 1f);
        }
    }
    
    /// <summary>
    /// Вычислить эффекты для исчезновения по типу.
    /// Для Return/Selected все буквы анимируются одновременно (charIndex = 0).
    /// </summary>
    public EffectResult CalculateDisappear(float time, int charIndex, float stateProgress, DisappearMode mode)
    {
        // Для Return и Selected используем charIndex=0 чтобы все буквы
        // анимировались одинаково (без побуквенного stagger)
        int effectiveCharIndex = (mode == DisappearMode.Return || mode == DisappearMode.Selected) 
            ? 0 
            : charIndex;
        
        switch (mode)
        {
            case DisappearMode.Return:
                return disappearReturnEffects.Calculate(time, effectiveCharIndex, stateProgress, 1f);
            case DisappearMode.Selected:
                return disappearSelectedEffects.Calculate(time, effectiveCharIndex, stateProgress, 1f);
            default:
                return disappearNormalEffects.Calculate(time, effectiveCharIndex, stateProgress, 1f);
        }
    }
    
    /// <summary>
    /// Получить длительность пропадания по типу.
    /// </summary>
    public float GetDisappearDuration(DisappearMode mode)
    {
        switch (mode)
        {
            case DisappearMode.Return:
                return disappearReturnDuration;
            case DisappearMode.Selected:
                return disappearSelectedDuration;
            default:
                return disappearNormalDuration;
        }
    }
}
