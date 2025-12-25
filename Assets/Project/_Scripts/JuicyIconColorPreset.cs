using UnityEngine;

/// <summary>
/// Color preset for JuicyResourceIcon.
/// Create one for each card suit!
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
    
    [Header("Glow")]
    [Tooltip("Color of the glow effect")]
    public Color glowColor = new Color(1f, 0.9f, 0.5f, 1f);
}
