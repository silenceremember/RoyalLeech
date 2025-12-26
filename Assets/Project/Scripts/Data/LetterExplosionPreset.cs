using UnityEngine;

/// <summary>
/// Preset for letter explosion effect.
/// Controls how letters fly from text to resource icons.
/// </summary>
[CreateAssetMenu(fileName = "LetterExplosionPreset", menuName = "Game/Letter Explosion Preset")]
public class LetterExplosionPreset : ScriptableObject
{
    [Header("=== FLIGHT ===")]
    [Tooltip("Общая длительность полёта буквы (секунды)")]
    public float flightDuration = 0.6f;
    
    [Tooltip("Случайный разброс длительности")]
    [Range(0, 0.3f)]
    public float flightDurationRandomness = 0.15f;
    
    [Tooltip("Начальная задержка перед стартом каждой буквы (stagger)")]
    [Range(0, 0.1f)]
    public float staggerDelay = 0.02f;
    
    [Header("=== INITIAL EXPLOSION ===")]
    [Tooltip("Сила начального разброса (пиксели)")]
    public float explosionForce = 150f;
    
    [Tooltip("Случайный угол начального разброса (градусы)")]
    public float explosionAngleRandomness = 45f;
    
    [Header("=== BEZIER CURVE ===")]
    [Tooltip("Высота дуги (control point offset)")]
    public float arcHeight = 100f;
    
    [Tooltip("Случайный разброс высоты дуги")]
    [Range(0, 1)]
    public float arcRandomness = 0.3f;
    
    [Tooltip("Боковое смещение контрольной точки для создания изгиба")]
    public float arcSideOffset = 50f;
    
    [Header("=== VISUAL ===")]
    [Tooltip("Начальный масштаб при взрыве (больше 1 = увеличение)")]
    public float initialScale = 1.3f;
    
    [Tooltip("Конечный масштаб при прибытии")]
    public float finalScale = 0.2f;
    
    [Tooltip("Скорость вращения (градусов/сек)")]
    public float rotationSpeed = 720f;
    
    [Tooltip("Случайность направления вращения")]
    public bool randomRotationDirection = true;
    
    [Header("=== EASING ===")]
    [Tooltip("Кривая позиции (0=start, 1=target)")]
    public AnimationCurve positionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Tooltip("Кривая масштаба (0=initial, 1=final)")]
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Tooltip("Кривая прозрачности (1=visible, 0=invisible)")]
    public AnimationCurve alphaCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.7f, 1f),
        new Keyframe(1f, 0f)
    );
    
    [Header("=== ICON FEEDBACK ===")]
    [Tooltip("Сила glow burst при получении буквы")]
    [Range(0, 1)]
    public float arrivalGlowBurst = 0.3f;
    
    [Tooltip("Сила scale punch при получении буквы")]
    [Range(0, 0.2f)]
    public float arrivalPunchScale = 0.05f;
    
    [Tooltip("Сила splash при получении буквы")]
    [Range(0, 0.5f)]
    public float arrivalSplash = 0.2f;
}
