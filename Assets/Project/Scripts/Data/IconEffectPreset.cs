using UnityEngine;
using DG.Tweening;

/// <summary>
/// Effect preset for resource icons.
/// Contains all animation, effect, and behavior settings.
/// Shared across all icons for consistent behavior.
/// </summary>
[CreateAssetMenu(fileName = "IconEffectPreset", menuName = "Game/Icon Effect Preset")]
public class IconEffectPreset : ScriptableObject
{
    [Header("Fill Effect")]
    public float fillWaveStrength = 0.02f;
    public float fillWaveSpeed = 3f;
    
    [Header("Liquid Effects")]
    [Range(0, 0.15f)] public float meniscusStrength = 0.04f;
    [Range(0.05f, 0.2f)] public float bubbleSize = 0.1f;
    
    [Header("Pixelation")]
    [Tooltip("0 = off, 32-128 = pixelated")]
    public float pixelDensity = 0f;
    
    [Header("Effect Strength")]
    [Tooltip("Multiplier for INCREASE effect (lighten towards white)")]
    [Range(0, 2)] public float increaseStrength = 0.5f;
    
    [Tooltip("Multiplier for DECREASE effect (darken towards black)")]
    [Range(0, 2)] public float decreaseStrength = 0.5f;
    
    [Tooltip("Multiplier for glow effect")]
    [Range(0, 2)] public float glowStrength = 0.5f;
    
    [Tooltip("Pulse speed for effects")]
    public float pulseSpeed = 4f;
    
    [Header("Gain Effect (Increase)")]
    [Tooltip("Effect intensity for resource GAIN (0-1)")]
    [Range(0, 1)] public float gainEffectIntensity = 1.0f;
    
    [Tooltip("Pulse intensity during gain")]
    [Range(0, 1)] public float gainPulseIntensity = 0.6f;
    
    [Tooltip("Splash intensity during gain")]
    [Range(0, 2)] public float gainSplashIntensity = 1.0f;
    public float gainSplashDuration = 0.6f;
    
    [Header("Loss Effect (Decrease)")]
    [Tooltip("Effect intensity for resource LOSS (0-1)")]
    [Range(0, 1)] public float lossEffectIntensity = 1.0f;
    
    [Tooltip("Pulse intensity during loss")]
    [Range(0, 1)] public float lossPulseIntensity = 0.5f;
    
    [Tooltip("Shake intensity during loss")]
    [Range(0, 10)] public float lossShakeIntensity = 5f;
    
    [Tooltip("Splash intensity during loss")]
    [Range(0, 2)] public float lossSplashIntensity = 1.2f;
    public float lossSplashDuration = 0.7f;
    
    [Header("Animation Timing")]
    [Tooltip("Duration for fill animations")]
    public float fillDuration = 0.5f;
    public Ease fillEase = Ease.OutBack;
    
    [Tooltip("Duration for highlight/flash")]
    public float flashDuration = 0.15f;
    
    [Tooltip("Duration for shake")]
    public float shakeDuration = 0.3f;
    
    [Tooltip("Scale punch intensity")]
    [Range(0, 0.5f)] public float punchScale = 0.2f;
    
    [Tooltip("Hold duration before fade")]
    public float effectHoldDuration = 0.2f;
    
    [Tooltip("Fade out duration")]
    public float effectFadeDuration = 0.5f;
    
    [Header("Idle Animation")]
    public bool enableIdleAnimation = true;
    public float idleRotationAmount = 2f;
    public float idleScaleAmount = 0.02f;
    public float idleSpeed = 1.5f;
    
    [Header("Card Following")]
    public bool enableCardFollowing = true;
    public float followTiltAmount = 8f;
    public float followSpeed = 8f;
    
    [Header("Shadow")]
    public Color shadowColor = new Color(0, 0, 0, 0.5f);
    public float shadowIntensity = 5f;
    
    [Header("Shake")]
    public float shakeSpeed = 30f;
    
    [Header("Critical Glow (activates when fillAmount is at threshold)")]
    [Tooltip("Fill threshold for critical state (low)")]
    [Range(0, 0.5f)] public float criticalLowThreshold = 0.1f;
    
    [Tooltip("Fill threshold for critical state (high)")]
    [Range(0.5f, 1f)] public float criticalHighThreshold = 0.9f;
}
