// LetterEffect.cs
// Royal Leech Modular Text Effects
// Модульные эффекты для per-letter анимации

using UnityEngine;
using System;

/// <summary>
/// Типы эффектов для букв.
/// </summary>
public enum EffectType
{
    None,
    Wave,       // Случайная волна по Y
    Pulse,      // Пульсация масштаба
    Bounce,     // Bounce scale (0 → overshoot → 1)
    Fade,       // Fade alpha
    Slide,      // Смещение (вверх/вниз/влево/вправо)
    Jitter      // Микро-тряска (опционально)
}

/// <summary>
/// Направление для Slide эффекта.
/// </summary>
public enum SlideDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Результат вычисления эффекта.
/// </summary>
public struct EffectResult
{
    public Vector2 offset;
    public float scale;
    public float alpha;
    public float rotation;
    
    public static EffectResult Identity => new EffectResult
    {
        offset = Vector2.zero,
        scale = 1f,
        alpha = 1f,
        rotation = 0f
    };
    
    /// <summary>
    /// Комбинирует два результата (additive для offset/rotation, multiplicative для scale/alpha).
    /// </summary>
    public static EffectResult Combine(EffectResult a, EffectResult b)
    {
        return new EffectResult
        {
            offset = a.offset + b.offset,
            scale = a.scale * b.scale,
            alpha = a.alpha * b.alpha,
            rotation = a.rotation + b.rotation
        };
    }
}

/// <summary>
/// Модульный эффект для буквы. Настраивается в инспекторе.
/// </summary>
[Serializable]
public class LetterEffect
{
    [Tooltip("Тип эффекта")]
    public EffectType type = EffectType.None;
    
    [Header("Wave Settings")]
    [Tooltip("Амплитуда волны (pixels)")]
    public float waveAmplitude = 4f;
    [Tooltip("Минимальная скорость волны")]
    public float waveSpeedMin = 1f;
    [Tooltip("Максимальная скорость волны")]
    public float waveSpeedMax = 2.5f;
    
    [Header("Pulse Settings")]
    [Tooltip("Амплитуда пульсации (0.1 = 10%)")]
    public float pulseAmplitude = 0.1f;
    [Tooltip("Скорость пульсации")]
    public float pulseSpeed = 6f;
    
    [Header("Bounce Settings")]
    [Tooltip("Overshoot (1.2 = 20% перелёт)")]
    public float bounceOvershoot = 1.2f;
    
    [Header("Fade Settings")]
    [Tooltip("Начальная прозрачность (для appear: 0, для disappear: 1)")]
    public float fadeFrom = 0f;
    [Tooltip("Конечная прозрачность")]
    public float fadeTo = 1f;
    
    [Header("Slide Settings")]
    [Tooltip("Направление смещения")]
    public SlideDirection slideDirection = SlideDirection.Down;
    [Tooltip("Дистанция смещения (pixels)")]
    public float slideDistance = 20f;
    
    [Header("Jitter Settings")]
    [Tooltip("Интенсивность тряски")]
    public float jitterIntensity = 2f;
    [Tooltip("Скорость тряски")]
    public float jitterSpeed = 30f;
    
    /// <summary>
    /// Вычислить эффект для конкретной буквы.
    /// </summary>
    /// <param name="time">Глобальное время анимации</param>
    /// <param name="charIndex">Индекс буквы</param>
    /// <param name="stateProgress">Прогресс в текущем состоянии (0-1 для appear/disappear)</param>
    /// <param name="intensity">Интенсивность эффекта (для active/selected)</param>
    public EffectResult Calculate(float time, int charIndex, float stateProgress, float intensity = 1f)
    {
        EffectResult result = EffectResult.Identity;
        
        switch (type)
        {
            case EffectType.Wave:
                result = CalculateWave(time, charIndex, intensity);
                break;
            case EffectType.Pulse:
                result = CalculatePulse(time, charIndex, intensity);
                break;
            case EffectType.Bounce:
                result = CalculateBounce(stateProgress);
                break;
            case EffectType.Fade:
                result = CalculateFade(stateProgress);
                break;
            case EffectType.Slide:
                result = CalculateSlide(stateProgress);
                break;
            case EffectType.Jitter:
                result = CalculateJitter(time, charIndex, intensity);
                break;
        }
        
        return result;
    }
    
    private EffectResult CalculateWave(float time, int charIndex, float intensity)
    {
        // Детерминированный "рандом" для скорости и фазы
        float hash1 = Frac(Mathf.Sin(charIndex * 12.9898f + 78.233f) * 43758.5453f);
        float hash2 = Frac(Mathf.Sin(charIndex * 43.758f + 12.9898f) * 78.233f);
        
        float speed = Mathf.Lerp(waveSpeedMin, waveSpeedMax, hash1);
        float phase = hash2 * Mathf.PI * 2f;
        
        float y = Mathf.Sin(time * speed + phase) * waveAmplitude * intensity;
        
        return new EffectResult
        {
            offset = new Vector2(0f, y),
            scale = 1f,
            alpha = 1f,
            rotation = 0f
        };
    }
    
    private EffectResult CalculatePulse(float time, int charIndex, float intensity)
    {
        float hash = Frac(Mathf.Sin(charIndex * 43.758f + 12.9898f) * 78.233f);
        float phase = hash * Mathf.PI * 2f;
        
        float pulse = Mathf.Sin(time * pulseSpeed + phase) * pulseAmplitude * intensity;
        
        return new EffectResult
        {
            offset = Vector2.zero,
            scale = 1f + pulse,
            alpha = 1f,
            rotation = 0f
        };
    }
    
    private EffectResult CalculateBounce(float t)
    {
        // EaseOutBack curve
        float c1 = 1.70158f * (bounceOvershoot - 1f);
        float c3 = c1 + 1f;
        float scale = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        
        return new EffectResult
        {
            offset = Vector2.zero,
            scale = Mathf.Max(0f, scale),
            alpha = 1f,
            rotation = 0f
        };
    }
    
    private EffectResult CalculateFade(float t)
    {
        float alpha = Mathf.Lerp(fadeFrom, fadeTo, t);
        
        return new EffectResult
        {
            offset = Vector2.zero,
            scale = 1f,
            alpha = alpha,
            rotation = 0f
        };
    }
    
    private EffectResult CalculateSlide(float t)
    {
        float distance = Mathf.Lerp(slideDistance, 0f, t);
        Vector2 offset = Vector2.zero;
        
        switch (slideDirection)
        {
            case SlideDirection.Up:
                offset = new Vector2(0f, distance);
                break;
            case SlideDirection.Down:
                offset = new Vector2(0f, -distance);
                break;
            case SlideDirection.Left:
                offset = new Vector2(-distance, 0f);
                break;
            case SlideDirection.Right:
                offset = new Vector2(distance, 0f);
                break;
        }
        
        return new EffectResult
        {
            offset = offset,
            scale = 1f,
            alpha = 1f,
            rotation = 0f
        };
    }
    
    private EffectResult CalculateJitter(float time, int charIndex, float intensity)
    {
        float phase = time * jitterSpeed + charIndex * 17.3f;
        float x = (Mathf.PerlinNoise(phase, 0f) - 0.5f) * 2f * jitterIntensity * intensity;
        float y = (Mathf.PerlinNoise(0f, phase) - 0.5f) * 2f * jitterIntensity * intensity;
        
        return new EffectResult
        {
            offset = new Vector2(x, y),
            scale = 1f,
            alpha = 1f,
            rotation = 0f
        };
    }
    
    private float Frac(float x)
    {
        return x - Mathf.Floor(x);
    }
}

/// <summary>
/// Конфигурация эффектов для одного состояния.
/// </summary>
[Serializable]
public class StateEffects
{
    [Tooltip("Эффекты для этого состояния")]
    public LetterEffect[] effects = new LetterEffect[0];
    
    /// <summary>
    /// Вычислить комбинированный результат всех эффектов.
    /// </summary>
    public EffectResult Calculate(float time, int charIndex, float stateProgress, float intensity = 1f)
    {
        EffectResult result = EffectResult.Identity;
        
        foreach (var effect in effects)
        {
            if (effect != null && effect.type != EffectType.None)
            {
                result = EffectResult.Combine(result, effect.Calculate(time, charIndex, stateProgress, intensity));
            }
        }
        
        return result;
    }
}
