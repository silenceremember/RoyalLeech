using UnityEngine;
using DG.Tweening;

/// <summary>
/// Effect preset for resource icons.
/// Minimal settings for visual tuning.
/// </summary>
[CreateAssetMenu(fileName = "IconEffectPreset", menuName = "Game/Icon Effect Preset")]
public class IconEffectPreset : ScriptableObject
{
    [Header("Liquid Visuals")]
    public float fillWaveStrength = 0.02f;
    public float fillWaveSpeed = 3f;
    [Range(0, 0.15f)] public float meniscusStrength = 0.04f;
    [Range(0.02f, 0.25f)] public float bubbleSize = 0.08f;
    [Range(0, 1)] public float bubbleDensity = 0.4f;
    [Range(0.1f, 2f)] public float bubbleSpeed = 0.6f;
    [Tooltip("0 = smooth, 8-32 = pixelated bubbles")]
    public float bubblePixelation = 0f;
    
    [Header("Bubble Tier Modifiers")]
    [Tooltip("Speed multiplier for Minor tier (smaller changes = slower bubbles)")]
    [Range(0.3f, 1f)] public float bubbleSpeedMinor = 0.5f;
    [Tooltip("Speed multiplier for Normal tier")]
    [Range(0.8f, 1.2f)] public float bubbleSpeedNormal = 1.0f;
    [Tooltip("Speed multiplier for Major tier (bigger changes = faster bubbles)")]
    [Range(1f, 2f)] public float bubbleSpeedMajor = 1.5f;
    
    [Tooltip("Size multiplier for Minor tier")]
    [Range(0.5f, 1f)] public float bubbleSizeMinor = 0.7f;
    [Tooltip("Size multiplier for Normal tier")]
    [Range(0.8f, 1.2f)] public float bubbleSizeNormal = 1.0f;
    [Tooltip("Size multiplier for Major tier")]
    [Range(1f, 1.5f)] public float bubbleSizeMajor = 1.2f;
    
    [Tooltip("0 = off, 32-128 = pixelated")]
    public float pixelDensity = 0f;
    
    [Header("Effect Strength")]
    [Tooltip("Lighten strength on GAIN")]
    [Range(0, 1)] public float increaseStrength = 0.5f;
    
    [Tooltip("Darken strength on LOSS")]
    [Range(0, 1)] public float decreaseStrength = 0.5f;
    
    [Tooltip("Darken strength on critical glow")]
    [Range(0, 1)] public float glowStrength = 0.5f;
    
    [Tooltip("Splash intensity on GAIN")]
    [Range(0, 2)] public float gainSplashIntensity = 1.0f;
    
    [Tooltip("Splash intensity on LOSS")]
    [Range(0, 2)] public float lossSplashIntensity = 1.2f;
    
    [Tooltip("Shake intensity on LOSS")]
    [Range(0, 15)] public float lossShakeIntensity = 5f;
    
    [Tooltip("Scale punch intensity on GAIN (positive = grow)")]
    [Range(0, 0.5f)] public float increasePunchScale = 0.2f;
    
    [Tooltip("Scale punch intensity on LOSS (shrink effect)")]
    [Range(0, 0.5f)] public float decreasePunchScale = 0.15f;
    
    [Header("Effect Tiers (Minor / Normal / Major)")]
    [Tooltip("Delta <= this = Minor effect")]
    [Range(1, 10)] public int minorThreshold = 3;
    
    [Tooltip("Delta > minorThreshold and <= this = Normal effect. Delta > this = Major effect")]
    [Range(5, 30)] public int majorThreshold = 10;
    
    [Tooltip("Multiplier for Minor effects (e.g. 0.5 = 50% of Normal)")]
    [Range(0.2f, 0.9f)] public float minorMultiplier = 0.5f;
    
    [Tooltip("Multiplier for Major effects (e.g. 1.5 = 150% of Normal)")]
    [Range(1.1f, 2f)] public float majorMultiplier = 1.5f;
    
    [Header("Timing")]
    [Tooltip("Total effect duration (fill, flash, fade all fit within this)")]
    public float effectDuration = 0.8f;
    
    [Tooltip("Duration for per-letter visual effects (seconds)")]
    public float letterEffectDuration = 0.6f;
    
    [Tooltip("Speed of pulse and glow animations")]
    public float pulseSpeed = 4f;
    
    [Header("Trailing Fill (delayed damage indicator)")]
    [Tooltip("Delay before trailing starts to catch up (seconds)")]
    public float trailingDelay = 0.8f;
    
    [Tooltip("How fast trailing catches up to actual fill (duration in seconds)")]
    public float trailingDuration = 0.5f;
    
    [Header("Preview Effect (when hovering over choice)")]
    [Tooltip("Base shake intensity for preview (0 = min shake when far, this value = max shake at threshold)")]
    [Range(1, 20)] public float previewShakeBase = 5f;
    
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
    
    [Header("Critical Glow")]
    [Range(0, 0.3f)] public float criticalLowThreshold = 0.1f;
    [Range(0.7f, 1f)] public float criticalHighThreshold = 0.9f;
}
