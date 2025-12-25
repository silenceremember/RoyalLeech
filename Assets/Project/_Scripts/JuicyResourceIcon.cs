using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Controller for JuicyResourceIcon shader effects.
/// Provides C# API for triggering and animating all juicy effects.
/// Supports mesh-based shadow like ShaderShadow for Image.
/// 
/// MAXIMUM JUICE: Includes DOTween-based animations for all effects.
/// </summary>
[RequireComponent(typeof(Graphic))]
[ExecuteAlways]
public class JuicyResourceIcon : MonoBehaviour, IMeshModifier
{
    [Header("Color Preset")]
    [Tooltip("Optional: assign a color preset to override colors")]
    public JuicyIconColorPreset colorPreset;
    
    [Header("Fill Effect")]
    [Range(0, 1)] public float fillAmount = 1f;
    public Color fillColor = new Color(0.3f, 0.7f, 0.95f, 1f);  // Nice blue
    public Color backgroundColor = new Color(0.1f, 0.15f, 0.25f, 1f);  // Dark blue
    [Range(0, 1)] public float backgroundAlpha = 0.7f;
    public float fillWaveStrength = 0.02f;
    public float fillWaveSpeed = 3f;
    
    [Header("Liquid Effects")]
    [Range(0, 0.15f)] public float meniscusStrength = 0.04f;  // Edge curve (always on)
    [Range(0, 1)] public float liquidTurbulence = 0f;         // Sharp waves (shake)
    [Range(0, 1)] public float bubbleIntensity = 0f;          // Bubbles (shake)
    [Range(0.05f, 0.2f)] public float bubbleSize = 0.1f;
    public Color bubbleColor = new Color(0.7f, 0.9f, 1f, 0.8f);  // Light blue bubbles
    [Range(0, 1)] public float splashIntensity = 0f;          // Splash (choice)
    
    [Header("Pixelation")]
    [Tooltip("0 = off, 32-128 = pixelated")]
    public float pixelDensity = 0f;
    
    [Header("Glow and Pulse")]
    public Color glowColor = new Color(1f, 0.8f, 0.2f, 1f);
    [Range(0, 2)] public float glowIntensity = 0f;
    [Range(0, 0.1f)] public float glowSize = 0.02f;
    public float pulseSpeed = 2f;
    [Range(0, 1)] public float pulseIntensity = 0f;
    
    [Header("Shake Effect")]
    [Range(0, 20)] public float shakeIntensity = 0f;
    public float shakeSpeed = 30f;
    
    [Header("Highlight Flash")]
    public Color highlightColor = Color.white;
    [Range(0, 1)] public float highlightIntensity = 0f;
    
    [Header("Color Tint")]
    public Color tintOverlay = new Color(1, 1, 1, 0);
    
    [Header("Shadow")]
    public Color shadowColor = new Color(0, 0, 0, 0.5f);
    public float shadowIntensity = 5f;
    
    [Header("Animation Presets")]
    [Tooltip("Duration for fill animations")]
    public float fillDuration = 0.5f;
    public Ease fillEase = Ease.OutBack;
    
    [Tooltip("Duration for highlight/flash")]
    public float flashDuration = 0.15f;
    
    [Tooltip("Duration for shake")]
    public float shakeDuration = 0.3f;
    
    [Tooltip("Scale punch intensity")]
    public float punchScale = 0.2f;
    
    [Header("Idle Animation")]
    public bool enableIdleAnimation = true;
    public float idleRotationAmount = 2f;     // Degrees of Z rotation
    public float idleScaleAmount = 0.02f;     // Scale breathing
    public float idleSpeed = 1.5f;            // Animation speed
    
    [Header("Card Following (3D Look At)")]
    public bool enableCardFollowing = true;
    public float followTiltAmount = 8f;        // Max tilt degrees towards card
    public float followSpeed = 8f;             // How fast to follow
    
    [Header("Debug")]
    [SerializeField] private Vector2 currentShadowOffset;
    
    private Graphic _graphic;
    private Canvas _canvas;
    private Material _materialInstance;
    private RectTransform _rectTransform;
    private Sequence _currentSequence;
    private Tween _fillTween;
    private Tween _shakeTween;
    private Tween _pulseTween;
    private Tween _glowTween;
    private Tween _previewGlowTween;
    private Tween _previewPulseTween;
    private bool _isPreviewActive;
    
    // Idle and follow tracking
    private float _idleTime;
    private Vector2 _cardScreenPosition; // Card position in screen space for 3D look-at
    private float _currentMagnitude; // How much this resource will change (0-1 normalized)
    private Vector3 _baseScale = Vector3.one;
    private Quaternion _baseRotation = Quaternion.identity;
    
    // Shader property IDs
    private static readonly int FillAmountID = Shader.PropertyToID("_FillAmount");
    private static readonly int FillColorID = Shader.PropertyToID("_FillColor");
    private static readonly int BackgroundColorID = Shader.PropertyToID("_BackgroundColor");
    private static readonly int BackgroundAlphaID = Shader.PropertyToID("_BackgroundAlpha");
    private static readonly int FillWaveStrengthID = Shader.PropertyToID("_FillWaveStrength");
    private static readonly int FillWaveSpeedID = Shader.PropertyToID("_FillWaveSpeed");
    
    // Liquid IDs
    private static readonly int MeniscusStrengthID = Shader.PropertyToID("_MeniscusStrength");
    private static readonly int LiquidTurbulenceID = Shader.PropertyToID("_LiquidTurbulence");
    private static readonly int BubbleIntensityID = Shader.PropertyToID("_BubbleIntensity");
    private static readonly int BubbleSizeID = Shader.PropertyToID("_BubbleSize");
    private static readonly int BubbleColorID = Shader.PropertyToID("_BubbleColor");
    private static readonly int SplashIntensityID = Shader.PropertyToID("_SplashIntensity");
    private static readonly int PixelDensityID = Shader.PropertyToID("_PixelDensity");
    
    private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");
    private static readonly int GlowSizeID = Shader.PropertyToID("_GlowSize");
    private static readonly int PulseSpeedID = Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseIntensityID = Shader.PropertyToID("_PulseIntensity");
    private static readonly int ShakeIntensityID = Shader.PropertyToID("_ShakeIntensity");
    private static readonly int ShakeSpeedID = Shader.PropertyToID("_ShakeSpeed");
    private static readonly int HighlightColorID = Shader.PropertyToID("_HighlightColor");
    private static readonly int HighlightIntensityID = Shader.PropertyToID("_HighlightIntensity");
    private static readonly int TintOverlayID = Shader.PropertyToID("_TintOverlay");
    private static readonly int ShadowColorID = Shader.PropertyToID("_ShadowColor");
    
    void Awake()
    {
        _graphic = GetComponent<Graphic>();
        _rectTransform = GetComponent<RectTransform>();
    }
    
    void Start()
    {
        CreateMaterialInstance();
        _canvas = GetComponentInParent<Canvas>();
        
        // Random offset for idle to avoid sync between icons
        _idleTime = Random.Range(0f, 100f);
        _baseScale = _rectTransform.localScale;
        _baseRotation = _rectTransform.localRotation;
        
        // Apply color preset if assigned
        ApplyColorPreset();
        
        // Reset liquid to calm state on start
        ResetLiquidToCalm();
    }
    
    /// <summary>
    /// Apply colors from the color preset (if assigned)
    /// </summary>
    public void ApplyColorPreset()
    {
        if (colorPreset == null) return;
        
        fillColor = colorPreset.fillColor;
        backgroundColor = colorPreset.backgroundColor;
        backgroundAlpha = colorPreset.backgroundAlpha;
        bubbleColor = colorPreset.bubbleColor;
        glowColor = colorPreset.glowColor;
    }
    
    /// <summary>
    /// Instantly reset all liquid effects to calm (no animation)
    /// </summary>
    void ResetLiquidToCalm()
    {
        liquidTurbulence = 0f;
        bubbleIntensity = 0f;
        splashIntensity = 0f;
    }
    
    void Update()
    {
        if (!Application.isPlaying) return;
        
        // Idle animation
        _idleTime += Time.deltaTime;
        
        ApplyIdleAndFollowEffect();
    }
    
    void ApplyIdleAndFollowEffect()
    {
        Vector3 targetScale = _baseScale;
        Quaternion targetRotation = _baseRotation;
        Vector3 targetPosition = Vector3.zero; // Local position offset
        
        // Get camera and my screen position once
        Camera cam = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = _canvas.worldCamera;
        if (cam == null) cam = Camera.main;
        
        Vector2 myScreenPos = RectTransformUtility.WorldToScreenPoint(cam, transform.position);
        
        // === ENHANCED IDLE ANIMATION ===
        if (enableIdleAnimation)
        {
            // Multi-frequency rotation wobble (more organic with perlin-like feel)
            float t = _idleTime * idleSpeed;
            
            // Primary wobble
            float rotZ1 = Mathf.Sin(t) * idleRotationAmount;
            // Secondary wave (different frequency creates organic movement)
            float rotZ2 = Mathf.Sin(t * 1.7f + 0.5f) * idleRotationAmount * 0.3f;
            // Third harmonic
            float rotZ3 = Mathf.Sin(t * 2.3f + 1.2f) * idleRotationAmount * 0.15f;
            
            float idleRotZ = rotZ1 + rotZ2 + rotZ3;
            
            // 3D tilt with multiple frequencies
            float idleRotX = Mathf.Sin(t * 0.7f) * (idleRotationAmount * 0.4f)
                           + Mathf.Sin(t * 1.3f + 0.8f) * (idleRotationAmount * 0.2f);
            float idleRotY = Mathf.Cos(t * 0.5f) * (idleRotationAmount * 0.3f)
                           + Mathf.Cos(t * 1.1f + 0.4f) * (idleRotationAmount * 0.15f);
            
            targetRotation = Quaternion.Euler(idleRotX, idleRotY, idleRotZ);
            
            // Scale breathing (multiple harmonics)
            float breath = 1f + Mathf.Sin(t * 0.8f) * idleScaleAmount
                             + Mathf.Sin(t * 1.6f + 0.3f) * (idleScaleAmount * 0.3f);
            targetScale = _baseScale * breath;
            
            // Subtle position offset (floating effect)
            float posX = Mathf.Sin(t * 0.9f) * 1.5f + Mathf.Sin(t * 1.5f) * 0.5f;
            float posY = Mathf.Cos(t * 0.7f) * 1.0f + Mathf.Cos(t * 1.3f) * 0.3f;
            targetPosition = new Vector3(posX, posY, 0);
        }
        
        // === CURSOR TRACKING (subtle) ===
        if (enableCardFollowing)
        {
            // Get cursor position
            Vector2 cursorPos = UnityEngine.InputSystem.Mouse.current != null 
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : Vector2.zero;
            
            // Direction from icon to cursor
            Vector2 dirToCursor = cursorPos - myScreenPos;
            float cursorNormX = Mathf.Clamp(dirToCursor.x / Screen.width, -1f, 1f);
            float cursorNormY = Mathf.Clamp(dirToCursor.y / Screen.height, -1f, 1f);
            
            // Subtle cursor tracking (30% weight) - equal sensitivity in all directions
            float cursorWeight = 0.3f;
            float cursorTiltY = -cursorNormX * followTiltAmount * cursorWeight; // Left/right
            float cursorTiltX = cursorNormY * followTiltAmount * cursorWeight;   // Up/down (same multiplier)
            
            Quaternion cursorRotation = Quaternion.Euler(cursorTiltX, cursorTiltY, 0);
            targetRotation = targetRotation * cursorRotation;
        }
        
        // === CARD FOLLOWING (3D Look At) - intensity scales with magnitude ===
        if (enableCardFollowing && _cardScreenPosition.sqrMagnitude > 0.01f)
        {
            // Direction from this icon to the card
            Vector2 dirToCard = _cardScreenPosition - myScreenPos;
            
            // Normalize relative to screen size (account for aspect ratio)
            float aspectRatio = (float)Screen.width / Screen.height;
            float normX = dirToCard.x / Screen.width;
            float normY = dirToCard.y / Screen.height * aspectRatio; // Compensate for aspect ratio
            
            // Magnitude-based intensity: bigger changes = more intense look-at
            // 0 magnitude = 30% base interest, 1 magnitude = 150% (very interested!)
            float magnitudeMultiplier = Mathf.Lerp(0.3f, 1.5f, _currentMagnitude);
            
            // 3D tilt (main tracking) - EQUAL sensitivity in all directions
            float cardWeight = 0.7f * magnitudeMultiplier;
            float tiltY = -normX * followTiltAmount * 2f * cardWeight;  // Left/right
            float tiltX = normY * followTiltAmount * 2f * cardWeight;   // Up/down (SAME as Y now!)
            float tiltZ = -normX * followTiltAmount * 0.3f * cardWeight; // Reduced Z roll
            
            Quaternion cardRotation = Quaternion.Euler(tiltX, tiltY, tiltZ);
            targetRotation = targetRotation * cardRotation;
            
            // Lean towards card - EQUAL in both directions
            float leanX = normX * 3f * magnitudeMultiplier;
            float leanY = normY * 3f * magnitudeMultiplier; // Same as X now
            targetPosition += new Vector3(leanX, leanY, 0);
        }
        
        // Apply with smooth interpolation
        _rectTransform.localRotation = Quaternion.Slerp(
            _rectTransform.localRotation, 
            targetRotation, 
            Time.deltaTime * followSpeed
        );
        _rectTransform.localScale = Vector3.Lerp(
            _rectTransform.localScale,
            targetScale,
            Time.deltaTime * followSpeed
        );
        
        // Apply position offset (anchoredPosition)
        Vector2 currentPos = _rectTransform.anchoredPosition;
        Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.y);
        _rectTransform.anchoredPosition = Vector2.Lerp(currentPos, targetPos2D, Time.deltaTime * followSpeed * 0.5f);
    }
    
    /// <summary>
    /// Set card screen position for 3D look-at. Called by GameManager.
    /// Pass the card's world position.
    /// </summary>
    public void SetCardPosition(Vector3 cardWorldPosition)
    {
        Camera cam = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = _canvas.worldCamera;
        if (cam == null) cam = Camera.main;
        
        _cardScreenPosition = RectTransformUtility.WorldToScreenPoint(cam, cardWorldPosition);
    }
    
    /// <summary>
    /// Set magnitude for this resource (how much it will change).
    /// Scales look-at intensity: 0 = mild interest, 1 = very intense stare.
    /// </summary>
    public void SetMagnitude(float normalizedMagnitude)
    {
        _currentMagnitude = Mathf.Clamp01(normalizedMagnitude);
    }
    
    /// <summary>
    /// Reset card tracking and magnitude
    /// </summary>
    public void ResetCardTracking()
    {
        _cardScreenPosition = Vector2.zero;
        _currentMagnitude = 0f;
    }
    
    void OnEnable()
    {
        if (_graphic != null)
            _graphic.SetVerticesDirty();
    }
    
    void OnDisable()
    {
        KillAllTweens();
        if (_graphic != null)
            _graphic.SetVerticesDirty();
    }
    
    void OnDestroy()
    {
        KillAllTweens();
        
        if (_materialInstance != null)
        {
            if (Application.isPlaying)
                Destroy(_materialInstance);
            else
                DestroyImmediate(_materialInstance);
        }
    }
    
    void KillAllTweens()
    {
        _currentSequence?.Kill();
        _fillTween?.Kill();
        _shakeTween?.Kill();
        _pulseTween?.Kill();
        _glowTween?.Kill();
    }
    
    void CreateMaterialInstance()
    {
        if (_graphic == null) return;
        
        if (_graphic.material != null && _graphic.material.shader.name == "Custom/JuicyResourceIcon")
        {
            _materialInstance = new Material(_graphic.material);
            _graphic.material = _materialInstance;
        }
    }
    
    void LateUpdate()
    {
        if (_materialInstance == null)
        {
            if (_graphic != null && _graphic.material != null && 
                _graphic.material.shader.name == "Custom/JuicyResourceIcon")
            {
                CreateMaterialInstance();
            }
            else
            {
                return;
            }
        }
        
        // Update all shader properties
        UpdateShaderProperties();
        
        // Calculate shadow offset
        Vector2 newOffset = CalculateShadowDirection() * shadowIntensity;
        if (Vector2.Distance(newOffset, currentShadowOffset) > 0.1f)
        {
            currentShadowOffset = newOffset;
            if (_graphic != null)
                _graphic.SetVerticesDirty();
        }
    }
    
    void UpdateShaderProperties()
    {
        // Fill
        _materialInstance.SetFloat(FillAmountID, fillAmount);
        _materialInstance.SetColor(FillColorID, fillColor);
        _materialInstance.SetColor(BackgroundColorID, backgroundColor);
        _materialInstance.SetFloat(BackgroundAlphaID, backgroundAlpha);
        _materialInstance.SetFloat(FillWaveStrengthID, fillWaveStrength);
        _materialInstance.SetFloat(FillWaveSpeedID, fillWaveSpeed);
        
        // Liquid Effects
        _materialInstance.SetFloat(MeniscusStrengthID, meniscusStrength);
        _materialInstance.SetFloat(LiquidTurbulenceID, liquidTurbulence);
        _materialInstance.SetFloat(BubbleIntensityID, bubbleIntensity);
        _materialInstance.SetFloat(BubbleSizeID, bubbleSize);
        _materialInstance.SetColor(BubbleColorID, bubbleColor);
        _materialInstance.SetFloat(SplashIntensityID, splashIntensity);
        _materialInstance.SetFloat(PixelDensityID, pixelDensity);
        
        // Glow/Pulse
        _materialInstance.SetColor(GlowColorID, glowColor);
        _materialInstance.SetFloat(GlowIntensityID, glowIntensity);
        _materialInstance.SetFloat(GlowSizeID, glowSize);
        _materialInstance.SetFloat(PulseSpeedID, pulseSpeed);
        _materialInstance.SetFloat(PulseIntensityID, pulseIntensity);
        
        // Shake
        _materialInstance.SetFloat(ShakeIntensityID, shakeIntensity);
        _materialInstance.SetFloat(ShakeSpeedID, shakeSpeed);
        
        // Highlight
        _materialInstance.SetColor(HighlightColorID, highlightColor);
        _materialInstance.SetFloat(HighlightIntensityID, highlightIntensity);
        
        // Tint
        _materialInstance.SetColor(TintOverlayID, tintOverlay);
        
        // Shadow
        _materialInstance.SetColor(ShadowColorID, shadowColor);
    }
    
    Vector2 CalculateShadowDirection()
    {
        if (ShadowLightSource.Instance == null)
            return new Vector2(1, -1).normalized;
        
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        
        Camera cam = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = _canvas.worldCamera;
        if (cam == null) cam = Camera.main;
        
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, transform.position);
        screenPos -= new Vector2(Screen.width / 2f, Screen.height / 2f);
        
        return ShadowLightSource.Instance.GetShadowDirection(screenPos);
    }
    
    // === IMeshModifier for Shadow ===
    
    public void ModifyMesh(Mesh mesh)
    {
        using (var vh = new VertexHelper(mesh))
        {
            ModifyMesh(vh);
            vh.FillMesh(mesh);
        }
    }
    
    public void ModifyMesh(VertexHelper vh)
    {
        if (!enabled || !gameObject.activeInHierarchy)
            return;
        
        int originalVertCount = vh.currentVertCount;
        if (originalVertCount == 0)
            return;
        
        // Get original vertices
        System.Collections.Generic.List<UIVertex> originalVerts = 
            new System.Collections.Generic.List<UIVertex>();
        for (int i = 0; i < originalVertCount; i++)
        {
            UIVertex v = new UIVertex();
            vh.PopulateUIVertex(ref v, i);
            originalVerts.Add(v);
        }
        
        // Calculate offset
        Vector3 offset = new Vector3(currentShadowOffset.x, currentShadowOffset.y, 0);
        if (_canvas != null)
        {
            offset /= _canvas.scaleFactor;
        }
        offset = transform.InverseTransformVector(offset);
        
        // Build triangles (quads: 4 verts = 6 indices)
        int quadCount = originalVertCount / 4;
        System.Collections.Generic.List<int> triangles = 
            new System.Collections.Generic.List<int>();
        for (int q = 0; q < quadCount; q++)
        {
            int b = q * 4;
            triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
            triangles.Add(b + 2); triangles.Add(b + 3); triangles.Add(b);
        }
        
        // Clear and rebuild
        vh.Clear();
        
        // Shadow vertices first
        for (int i = 0; i < originalVerts.Count; i++)
        {
            UIVertex v = originalVerts[i];
            v.position += offset;
            v.uv1 = new Vector4(1, 0, 0, 0); // Shadow flag
            vh.AddVert(v);
        }
        
        // Shadow triangles
        for (int i = 0; i < triangles.Count; i += 3)
        {
            vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
        
        // Main vertices
        int mainBase = vh.currentVertCount;
        for (int i = 0; i < originalVerts.Count; i++)
        {
            UIVertex v = originalVerts[i];
            v.uv1 = new Vector4(0, 0, 0, 0); // Main flag
            vh.AddVert(v);
        }
        
        // Main triangles
        for (int i = 0; i < triangles.Count; i += 3)
        {
            vh.AddTriangle(
                mainBase + triangles[i],
                mainBase + triangles[i + 1],
                mainBase + triangles[i + 2]
            );
        }
    }
    
    void OnValidate()
    {
        if (_graphic != null)
            _graphic.SetVerticesDirty();
        
        if (_materialInstance != null)
            UpdateShaderProperties();
    }
    
    // =====================================================
    // === MAXIMUM JUICE API ===
    // =====================================================
    
    /// <summary>
    /// Animate fill to target value with all the juice!
    /// Includes scale punch and glow.
    /// </summary>
    public Tween AnimateFillTo(float targetFill, float duration = -1)
    {
        if (duration < 0) duration = fillDuration;
        
        _fillTween?.Kill();
        _fillTween = DOTween.To(() => fillAmount, x => fillAmount = x, targetFill, duration)
            .SetEase(fillEase);
        
        return _fillTween;
    }
    
    /// <summary>
    /// JUICY resource gain effect!
    /// Flash + scale punch + glow burst + fill increase + LIQUID SPLASH!
    /// </summary>
    public Sequence PlayGainEffect(float newFillAmount, Color? flashColor = null)
    {
        _currentSequence?.Kill();
        _currentSequence = DOTween.Sequence();
        
        Color flash = flashColor ?? new Color(0.5f, 1f, 0.5f, 1f); // Green flash
        
        // LIQUID SPLASH!
        PlaySplash(0.8f, 0.5f);
        
        // Scale punch
        _currentSequence.Append(
            _rectTransform.DOPunchScale(Vector3.one * punchScale, flashDuration * 2, 1, 0.5f)
        );
        
        // Flash
        _currentSequence.Join(
            DOTween.To(() => highlightIntensity, x => highlightIntensity = x, 0.8f, flashDuration * 0.5f)
                .SetEase(Ease.OutQuad)
        );
        highlightColor = flash;
        
        // Glow burst
        _currentSequence.Join(
            DOTween.To(() => glowIntensity, x => glowIntensity = x, 1.5f, flashDuration)
                .SetEase(Ease.OutQuad)
        );
        glowColor = flash;
        
        // Fill animation
        _currentSequence.Join(AnimateFillTo(newFillAmount));
        
        // Fade out effects
        _currentSequence.Append(
            DOTween.To(() => highlightIntensity, x => highlightIntensity = x, 0f, flashDuration)
        );
        _currentSequence.Join(
            DOTween.To(() => glowIntensity, x => glowIntensity = x, 0f, flashDuration * 2)
        );
        
        return _currentSequence;
    }
    
    /// <summary>
    /// JUICY resource loss effect!
    /// Red flash + shake + fill decrease + LIQUID SPLASH!
    /// </summary>
    public Sequence PlayLossEffect(float newFillAmount, Color? flashColor = null)
    {
        _currentSequence?.Kill();
        _currentSequence = DOTween.Sequence();
        
        Color flash = flashColor ?? new Color(1f, 0.3f, 0.3f, 1f); // Red flash
        
        // LIQUID SPLASH!
        PlaySplash(1.0f, 0.6f);
        
        // Shake (reduced to balance with punch)
        _currentSequence.Append(
            DOTween.To(() => shakeIntensity, x => shakeIntensity = x, 2.5f, shakeDuration * 0.3f)
                .SetEase(Ease.OutQuad)
        );
        
        // Flash
        _currentSequence.Join(
            DOTween.To(() => highlightIntensity, x => highlightIntensity = x, 0.6f, flashDuration * 0.5f)
                .SetEase(Ease.OutQuad)
        );
        highlightColor = flash;
        
        // Tint overlay
        tintOverlay = new Color(flash.r, flash.g, flash.b, 0.3f);
        
        // Fill animation
        _currentSequence.Join(AnimateFillTo(newFillAmount, fillDuration * 0.7f));
        
        // Fade out effects
        _currentSequence.Append(
            DOTween.To(() => shakeIntensity, x => shakeIntensity = x, 0f, shakeDuration)
        );
        _currentSequence.Join(
            DOTween.To(() => highlightIntensity, x => highlightIntensity = x, 0f, flashDuration)
        );
        _currentSequence.Join(
            DOTween.To(() => tintOverlay, x => tintOverlay = x, new Color(1, 1, 1, 0), flashDuration * 2)
        );
        
        return _currentSequence;
    }
    
    /// <summary>
    /// Critical/low resource warning effect!
    /// Continuous pulse + red glow.
    /// </summary>
    public void StartCriticalPulse(Color? pulseColor = null)
    {
        Color glow = pulseColor ?? new Color(1f, 0.2f, 0.2f, 1f);
        glowColor = glow;
        
        _pulseTween?.Kill();
        pulseIntensity = 0.5f;
        glowIntensity = 0.8f;
    }
    
    public void StopCriticalPulse()
    {
        _pulseTween?.Kill();
        
        DOTween.To(() => pulseIntensity, x => pulseIntensity = x, 0f, 0.3f);
        DOTween.To(() => glowIntensity, x => glowIntensity = x, 0f, 0.3f);
    }
    
    /// <summary>
    /// Quick highlight flash.
    /// </summary>
    public Tween Flash(Color? color = null, float intensity = 0.7f)
    {
        highlightColor = color ?? Color.white;
        highlightIntensity = intensity;
        
        return DOTween.To(() => highlightIntensity, x => highlightIntensity = x, 0f, flashDuration)
            .SetEase(Ease.OutQuad);
    }
    
    /// <summary>
    /// Punch scale effect.
    /// </summary>
    public Tween PunchScale(float scale = -1)
    {
        if (scale < 0) scale = punchScale;
        return _rectTransform.DOPunchScale(Vector3.one * scale, flashDuration * 2, 1, 0.5f);
    }
    
    /// <summary>
    /// Shake effect.
    /// </summary>
    public Tween Shake(float intensity = 10f, float duration = -1)
    {
        if (duration < 0) duration = shakeDuration;
        
        _shakeTween?.Kill();
        shakeIntensity = intensity;
        _shakeTween = DOTween.To(() => shakeIntensity, x => shakeIntensity = x, 0f, duration)
            .SetEase(Ease.OutQuad);
        
        return _shakeTween;
    }
    
    /// <summary>
    /// Glow effect with auto-fade.
    /// </summary>
    public Tween Glow(Color? color = null, float intensity = 1f, float duration = 0.5f)
    {
        glowColor = color ?? glowColor;
        glowIntensity = intensity;
        
        _glowTween?.Kill();
        _glowTween = DOTween.To(() => glowIntensity, x => glowIntensity = x, 0f, duration)
            .SetEase(Ease.OutQuad);
        
        return _glowTween;
    }
    
    // === SIMPLE API (no animation) ===
    
    public void SetFill(float amount) => fillAmount = amount;
    public void SetHighlight(float intensity) => highlightIntensity = intensity;
    public void SetPulse(float intensity) => pulseIntensity = intensity;
    public void SetGlowIntensity(float intensity) => glowIntensity = intensity;
    public void SetShake(float intensity) => shakeIntensity = intensity;
    public void SetTint(Color color) => tintOverlay = color;
    public void ClearTint() => tintOverlay = new Color(1, 1, 1, 0);
    
    // === LIQUID API ===
    
    public void SetLiquidTurbulence(float value) => liquidTurbulence = Mathf.Clamp01(value);
    public void SetBubbleIntensity(float value) => bubbleIntensity = Mathf.Clamp01(value);
    public void SetSplashIntensity(float value) => splashIntensity = Mathf.Clamp01(value);
    
    private Tween _turbulenceTween;
    private Tween _splashTween;
    
    /// <summary>
    /// SPLASH effect on choice! Central bump with spreading waves.
    /// Turns OFF turbulence, turns ON splash.
    /// </summary>
    public Sequence PlaySplash(float intensity = 1f, float duration = 0.6f)
    {
        Sequence splash = DOTween.Sequence();
        
        float targetSplash = 0.5f + intensity * 0.5f; // Strong splash!
        
        // Kill turbulence immediately, start splash
        splash.Append(DOTween.To(() => liquidTurbulence, x => liquidTurbulence = x, 0f, duration * 0.1f));
        splash.Join(DOTween.To(() => bubbleIntensity, x => bubbleIntensity = x, 0f, duration * 0.15f));
        splash.Join(DOTween.To(() => splashIntensity, x => splashIntensity = x, targetSplash, duration * 0.08f).SetEase(Ease.OutQuad));
        
        // Splash fades out slowly
        splash.Append(DOTween.To(() => splashIntensity, x => splashIntensity = x, 0f, duration * 0.9f).SetEase(Ease.InOutSine));
        
        return splash;
    }
    
    /// <summary>
    /// Agitation during shake/preview - turbulence + bubbles.
    /// </summary>
    public void SetLiquidAgitation(float agitation)
    {
        agitation = Mathf.Clamp01(agitation);
        liquidTurbulence = Mathf.Lerp(0f, 0.8f, agitation);
        bubbleIntensity = Mathf.Lerp(0f, 0.5f, agitation);
    }
    
    /// <summary>
    /// Reset liquid to calm (no turbulence, no bubbles, no splash).
    /// </summary>
    public void CalmLiquid(float duration = 0.3f)
    {
        _turbulenceTween?.Kill();
        _splashTween?.Kill();
        
        _turbulenceTween = DOTween.To(() => liquidTurbulence, x => liquidTurbulence = x, 0f, duration);
        DOTween.To(() => bubbleIntensity, x => bubbleIntensity = x, 0f, duration);
        DOTween.To(() => splashIntensity, x => splashIntensity = x, 0f, duration);
    }
    
    // =====================================================
    // === MAGNITUDE-BASED JUICE (no gain/loss reveal) ===
    // =====================================================
    
    /// <summary>
    /// Play magnitude-based effect - intensity scales with |delta|.
    /// Doesn't reveal whether it's gain or loss, just how BIG the change is.
    /// Uses neutral golden color.
    /// </summary>
    /// <param name="newFillAmount">Target fill 0-1</param>
    /// <param name="magnitude">Absolute magnitude 0-100 (or whatever your max delta is)</param>
    /// <param name="maxMagnitude">Max expected magnitude for scaling (default 30)</param>
    public Sequence PlayMagnitudeEffect(float newFillAmount, float magnitude, float maxMagnitude = 30f)
    {
        _currentSequence?.Kill();
        _currentSequence = DOTween.Sequence();
        
        // Normalize magnitude to 0-1 scale
        float t = Mathf.Clamp01(magnitude / maxMagnitude);
        
        // Neutral golden color - doesn't reveal gain/loss
        Color effectColor = new Color(1f, 0.85f, 0.4f, 1f);
        
        // === SCALE PUNCH (stronger with magnitude) ===
        float punchAmount = Mathf.Lerp(0.05f, punchScale * 1.5f, t);
        _currentSequence.Append(
            _rectTransform.DOPunchScale(Vector3.one * punchAmount, flashDuration * 2f, 1, 0.5f)
        );
        
        // === SHAKE (only for significant changes) ===
        if (t > 0.2f)
        {
            float shakeAmount = Mathf.Lerp(0.5f, 3f, t);
            _currentSequence.Join(
                DOTween.To(() => shakeIntensity, x => shakeIntensity = x, shakeAmount, shakeDuration * 0.3f)
                    .SetEase(Ease.OutQuad)
            );
        }
        
        // === GLOW (scales with magnitude) ===
        float glowAmount = Mathf.Lerp(0.3f, 1.5f, t);
        glowColor = effectColor;
        _currentSequence.Join(
            DOTween.To(() => glowIntensity, x => glowIntensity = x, glowAmount, flashDuration)
                .SetEase(Ease.OutQuad)
        );
        
        // === HIGHLIGHT FLASH (subtle for small, strong for big) ===
        float flashAmount = Mathf.Lerp(0.2f, 0.7f, t);
        highlightColor = effectColor;
        _currentSequence.Join(
            DOTween.To(() => highlightIntensity, x => highlightIntensity = x, flashAmount, flashDuration * 0.5f)
                .SetEase(Ease.OutQuad)
        );
        
        // === FILL ANIMATION ===
        // Duration also scales - big changes animate slightly faster (more dramatic)
        float fillDur = Mathf.Lerp(fillDuration, fillDuration * 0.7f, t);
        _currentSequence.Join(AnimateFillTo(newFillAmount, fillDur));
        
        // === FADE OUT ALL EFFECTS ===
        float fadeTime = Mathf.Lerp(0.2f, 0.4f, t);
        
        _currentSequence.Append(
            DOTween.To(() => highlightIntensity, x => highlightIntensity = x, 0f, fadeTime)
        );
        _currentSequence.Join(
            DOTween.To(() => glowIntensity, x => glowIntensity = x, 0f, fadeTime * 1.5f)
        );
        _currentSequence.Join(
            DOTween.To(() => shakeIntensity, x => shakeIntensity = x, 0f, fadeTime * 2f)
        );
        
        return _currentSequence;
    }
    
    /// <summary>
    /// Preview/highlight effect for when player is hovering/dragging.
    /// Shows that this resource WILL change via SHAKE + LIQUID AGITATION!
    /// Intensity scales with both magnitude AND proximity to decision.
    /// </summary>
    /// <param name="magnitude">How big the change will be (|delta|)</param>
    /// <param name="swipeProgress">How close to making the decision (0-1)</param>
    /// <param name="maxMagnitude">Max expected magnitude for scaling</param>
    public void PlayHighlightPreview(float magnitude, float swipeProgress = 1f, float maxMagnitude = 30f)
    {
        _previewGlowTween?.Kill();
        _previewPulseTween?.Kill();
        
        _isPreviewActive = true;
        
        float magnitudeT = Mathf.Clamp01(magnitude / maxMagnitude);
        float t = magnitudeT * swipeProgress;
        
        // Shake
        shakeIntensity = Mathf.Lerp(0.1f, 3f, t);
        
        // Liquid agitation - turbulence + bubbles
        liquidTurbulence = Mathf.Lerp(0f, 0.8f, t);
        bubbleIntensity = Mathf.Lerp(0f, 0.4f, t);
    }
    
    /// <summary>
    /// Stop preview highlight.
    /// </summary>
    public void StopHighlightPreview()
    {
        if (!_isPreviewActive) return; // Not showing preview
        _isPreviewActive = false;
        
        // Kill existing tweens before creating new ones
        _previewGlowTween?.Kill();
        _previewPulseTween?.Kill();
        
        _previewGlowTween = DOTween.To(() => glowIntensity, x => glowIntensity = x, 0f, 0.2f);
        _previewPulseTween = DOTween.To(() => pulseIntensity, x => pulseIntensity = x, 0f, 0.2f);
        
        // Fade out shake
        DOTween.To(() => shakeIntensity, x => shakeIntensity = x, 0f, 0.15f);
        
        // Calm liquid back to idle state
        CalmLiquid(0.3f);
    }
    
    public void ClearAllEffects()
    {
        highlightIntensity = 0;
        pulseIntensity = 0;
        glowIntensity = 0;
        shakeIntensity = 0;
        tintOverlay = new Color(1, 1, 1, 0);
    }
}
