using UnityEngine;

/// <summary>
/// Color preset for JuicyResourceIcon.
/// Create one for each card suit!
/// Contains main colors + 3 effect colors (glow, increase, decrease).
/// </summary>
[CreateAssetMenu(fileName = "NewIconColorPreset", menuName = "Juicy/Icon Color Preset")]
public class JuicyIconColorPreset : ScriptableObject
{
    [Header("Main Colors")]
    [Tooltip("Color of the liquid fill")]
    public Color fillColor = new Color(0.3f, 0.7f, 0.95f, 1f);
    
    [Tooltip("Color of the empty background")]
    public Color backgroundColor = new Color(0.1f, 0.15f, 0.25f, 1f);
    
    [Range(0, 1)]
    [Tooltip("Opacity of the background")]
    public float backgroundAlpha = 0.7f;
    
    [Header("Bubble Colors")]
    [Tooltip("Color of bubbles (alpha = opacity)")]
    public Color bubbleColor = new Color(0.7f, 0.9f, 1f, 0.7f);
    
    [Header("Effect Colors")]
    [Tooltip("Glow color for warning state (resource >= 90% or <= 10%)")]
    public Color glowColor = new Color(1f, 0.85f, 0.4f, 1f); // Warm golden
    
    [Tooltip("Effect color when resource INCREASES")]
    public Color increaseColor = new Color(0.3f, 0.9f, 1f, 1f); // Cyan/Light blue
    
    [Tooltip("Effect color when resource DECREASES")]
    public Color decreaseColor = new Color(1f, 0.6f, 0.2f, 1f); // Orange/Amber
}
