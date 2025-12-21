using UnityEngine;

/// <summary>
/// Global light source for Balatro-style shadows.
/// Simplified to only support the 'Screen Center' perspective mode.
/// </summary>
public class ShadowLightSource : MonoBehaviour
{
    public static ShadowLightSource Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Global offset multiplier for all shadows. Higher values = longer shadows away from center.")]
    public float globalIntensity = 15f; 

    [Tooltip("Shift the center of the light source from the exact screen center.")]
    public Vector2 centerOffset = Vector2.zero;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Calculates the shadow offset for a given object position in Screen Space.
    /// Uses a pseudo-3D perspective from the center of the screen.
    /// </summary>
    public Vector2 GetShadowOffset(Vector2 objectScreenPosition)
    {
        // Light is virtually at Screen Center (0,0) + Offset
        // Shadow Direction = ObjectPos - LightPos
        
        Vector2 lightPos = centerOffset;
        Vector2 direction = objectScreenPosition - lightPos;
        
        float distance = direction.magnitude;
        
        if (distance < 0.1f) return Vector2.zero;

        // Balatro logic: Shadows grow massive at edges
        float distanceFactor = Mathf.Clamp(distance / 500f, 0f, 2.5f);
        
        Vector2 offset = direction.normalized * (globalIntensity * distanceFactor);
        return offset;
    }
}
