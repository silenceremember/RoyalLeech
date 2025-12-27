using UnityEngine;

/// <summary>
/// Color preset for resource icons.
/// Create one for each card suit to define colors for fill, background, and bubbles.
/// </summary>
[CreateAssetMenu(fileName = "NewIconColorPreset", menuName = "Game/Icon Color Preset")]
public class IconColorPreset : ScriptableObject
{
    [Header("Main Colors")]
    [Tooltip("Color of the liquid fill")]
    public Color fillColor = new Color(0.3f, 0.7f, 0.95f, 1f);
    
    [Tooltip("Color of the empty background")]
    public Color backgroundColor = new Color(0.1f, 0.15f, 0.25f, 1f);
    
    [Range(0, 1)]
    [Tooltip("Blend of background color (0 = black, 1 = full color)")]
    public float backgroundAlpha = 0.7f;
}
