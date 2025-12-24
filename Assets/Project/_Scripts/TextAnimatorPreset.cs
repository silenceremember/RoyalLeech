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
[CreateAssetMenu(fileName = "NewTextPreset", menuName = "Royal Leech/Text Animator Preset")]
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
    
    [Tooltip("Длительность исчезновения одной буквы")]
    [Range(0.05f, 0.5f)]
    public float disappearDuration = 0.15f;
    
    [Tooltip("Скорость интерполяции эффектов")]
    [Range(5f, 30f)]
    public float effectSmoothSpeed = 15f;
    
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
    
    [Header("=== DISAPPEAR EFFECTS ===")]
    [Tooltip("Эффекты при исчезновении буквы")]
    public StateEffects disappearEffects = new StateEffects();
    
    /// <summary>
    /// Вычислить эффекты для появления.
    /// </summary>
    public EffectResult CalculateAppear(float time, int charIndex, float stateProgress)
    {
        return appearEffects.Calculate(time, charIndex, stateProgress, 1f);
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
    /// Вычислить эффекты для исчезновения.
    /// </summary>
    public EffectResult CalculateDisappear(float time, int charIndex, float stateProgress)
    {
        return disappearEffects.Calculate(time, charIndex, stateProgress, 1f);
    }
}
