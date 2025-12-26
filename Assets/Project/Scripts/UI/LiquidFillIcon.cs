using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Controller for LiquidFill shader effects on resource icons.
/// Provides C# API for triggering and animating liquid fill, glow, shake effects.
/// Supports mesh-based shadow for UI elements.
/// </summary>
[RequireComponent(typeof(Graphic))]
[ExecuteAlways]
public class LiquidFillIcon : MonoBehaviour, IMeshModifier
{
    [Header("Presets")]
    [Tooltip("Assign a color preset for this icon")]
    public IconColorPreset colorPreset;
    
    [Tooltip("Shared effect preset for all icons")]
    public IconEffectPreset effectPreset;
    
    [Header("Runtime State (read-only)")]
    [Range(0, 1)] public float fillAmount = 1f;
    [Range(-1, 1)] public float effectIntensity = 0f;
    [Range(0, 2)] public float glowIntensity = 0f;
    [Range(0, 1)] public float pulseIntensity = 0f;
    [Range(0, 20)] public float shakeIntensity = 0f;
    [Range(0, 1)] public float liquidTurbulence = 0f;
    [Range(0, 1)] public float bubbleIntensity = 0f;
    [Range(0, 1)] public float splashIntensity = 0f;
    
    // Colors from preset (with fallbacks)
    Color FillColor => colorPreset != null ? colorPreset.fillColor : Color.white;
    Color BackgroundColor => colorPreset != null ? colorPreset.backgroundColor : new Color(0.1f, 0.1f, 0.1f);
    float BackgroundAlpha => colorPreset != null ? colorPreset.backgroundAlpha : 0.7f;
    Color BubbleColor => colorPreset != null ? colorPreset.bubbleColor : Color.white;
    
    // Values from effect preset (with fallbacks)
    float FillWaveStrength => effectPreset != null ? effectPreset.fillWaveStrength : 0.02f;
    float FillWaveSpeed => effectPreset != null ? effectPreset.fillWaveSpeed : 3f;
    float MeniscusStrength => effectPreset != null ? effectPreset.meniscusStrength : 0.04f;
    float BubbleSize => effectPreset != null ? effectPreset.bubbleSize : 0.1f;
    float PixelDensity => effectPreset != null ? effectPreset.pixelDensity : 0f;
    float IncreaseStrength => effectPreset != null ? effectPreset.increaseStrength : 0.5f;
    float DecreaseStrength => effectPreset != null ? effectPreset.decreaseStrength : 0.5f;
    float GlowStrength => effectPreset != null ? effectPreset.glowStrength : 0.5f;
    float GainSplashIntensity => effectPreset != null ? effectPreset.gainSplashIntensity : 1f;
    float LossSplashIntensity => effectPreset != null ? effectPreset.lossSplashIntensity : 1.2f;
    float LossShakeIntensity => effectPreset != null ? effectPreset.lossShakeIntensity : 5f;
    float IncreasePunchScale => effectPreset != null ? effectPreset.increasePunchScale : 0.2f;
    float DecreasePunchScale => effectPreset != null ? effectPreset.decreasePunchScale : 0.15f;
    int MinorThreshold => effectPreset != null ? effectPreset.minorThreshold : 3;
    int MajorThreshold => effectPreset != null ? effectPreset.majorThreshold : 10;
    float MinorMultiplier => effectPreset != null ? effectPreset.minorMultiplier : 0.5f;
    float MajorMultiplier => effectPreset != null ? effectPreset.majorMultiplier : 1.5f;
    float EffectDuration => effectPreset != null ? effectPreset.effectDuration : 0.8f;
    float PulseSpeed => effectPreset != null ? effectPreset.pulseSpeed : 4f;
    float TrailingDelay => effectPreset != null ? effectPreset.trailingDelay : 0.8f;
    float TrailingDuration => effectPreset != null ? effectPreset.trailingDuration : 0.5f;
    float PreviewShakeBase => effectPreset != null ? effectPreset.previewShakeBase : 5f;
    bool EnableIdleAnimation => effectPreset != null ? effectPreset.enableIdleAnimation : true;
    float IdleRotationAmount => effectPreset != null ? effectPreset.idleRotationAmount : 2f;
    float IdleScaleAmount => effectPreset != null ? effectPreset.idleScaleAmount : 0.02f;
    float IdleSpeed => effectPreset != null ? effectPreset.idleSpeed : 1.5f;
    bool EnableCardFollowing => effectPreset != null ? effectPreset.enableCardFollowing : true;
    float FollowTiltAmount => effectPreset != null ? effectPreset.followTiltAmount : 8f;
    float FollowSpeed => effectPreset != null ? effectPreset.followSpeed : 8f;
    Color ShadowColor => effectPreset != null ? effectPreset.shadowColor : new Color(0, 0, 0, 0.5f);
    float ShadowIntensity => effectPreset != null ? effectPreset.shadowIntensity : 5f;
    float CriticalLowThreshold => effectPreset != null ? effectPreset.criticalLowThreshold : 0.1f;
    float CriticalHighThreshold => effectPreset != null ? effectPreset.criticalHighThreshold : 0.9f;
    
    // Runtime shadow tracking
    private Vector2 _currentShadowOffset;
    

    private Graphic _graphic;
    private Canvas _canvas;
    private Material _materialInstance;
    private RectTransform _rectTransform;
    private Sequence _currentSequence;
    private Tween _fillTween;
    private Tween _trailingTween;
    private Tween _shakeTween;
    private Tween _pulseTween;
    private Tween _glowTween;
    private Tween _previewGlowTween;
    private Tween _previewPulseTween;
    private bool _isPreviewActive;
    
    // Trailing fill (delayed damage indicator)
    private float trailingFill = 1f;
    
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
    
    private static readonly int EffectIntensityID = Shader.PropertyToID("_EffectIntensity");
    private static readonly int IncreaseStrengthID = Shader.PropertyToID("_IncreaseStrength");
    private static readonly int DecreaseStrengthID = Shader.PropertyToID("_DecreaseStrength");
    private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");
    private static readonly int GlowStrengthID = Shader.PropertyToID("_GlowStrength");
    private static readonly int PulseSpeedID = Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseIntensityID = Shader.PropertyToID("_PulseIntensity");
    private static readonly int ShakeIntensityID = Shader.PropertyToID("_ShakeIntensity");
    private static readonly int ShakeSpeedID = Shader.PropertyToID("_ShakeSpeed");
    private static readonly int ShadowColorID = Shader.PropertyToID("_ShadowColor");
    private static readonly int TrailingFillID = Shader.PropertyToID("_TrailingFill");
    
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
        
        // Initialize trailing to match current fill
        trailingFill = fillAmount;
        
        // Reset liquid to calm state on start
        ResetLiquidToCalm();
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
        
        // Auto glow when resource is critical (<=10% or >=90%)
        UpdateCriticalGlow();
        
        ApplyIdleAndFollowEffect();
    }
    
    /// <summary>
    /// Automatically enable glow when fillAmount is critical
    /// </summary>
    private bool _isCriticalGlowActive = false;
    
    void UpdateCriticalGlow()
    {
        bool isCritical = fillAmount <= CriticalLowThreshold || fillAmount >= CriticalHighThreshold;
        
        if (isCritical)
        {
            _isCriticalGlowActive = true;
            // Set glow to 1.0 (strength from preset controls visual intensity)
            glowIntensity = Mathf.Lerp(glowIntensity, 1f, Time.deltaTime * 5f);
        }
        else if (_isCriticalGlowActive)
        {
            // Fade out glow
            glowIntensity = Mathf.Lerp(glowIntensity, 0f, Time.deltaTime * 5f);
            if (glowIntensity < 0.01f)
            {
                glowIntensity = 0f;
                _isCriticalGlowActive = false;
            }
        }
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
        if (EnableIdleAnimation)
        {
            // Multi-frequency rotation wobble (more organic with perlin-like feel)
            float t = _idleTime * IdleSpeed;
            
            // Primary wobble
            float rotZ1 = Mathf.Sin(t) * IdleRotationAmount;
            // Secondary wave (different frequency creates organic movement)
            float rotZ2 = Mathf.Sin(t * 1.7f + 0.5f) * IdleRotationAmount * 0.3f;
            // Third harmonic
            float rotZ3 = Mathf.Sin(t * 2.3f + 1.2f) * IdleRotationAmount * 0.15f;
            
            float idleRotZ = rotZ1 + rotZ2 + rotZ3;
            
            // 3D tilt with multiple frequencies
            float idleRotX = Mathf.Sin(t * 0.7f) * (IdleRotationAmount * 0.4f)
                           + Mathf.Sin(t * 1.3f + 0.8f) * (IdleRotationAmount * 0.2f);
            float idleRotY = Mathf.Cos(t * 0.5f) * (IdleRotationAmount * 0.3f)
                           + Mathf.Cos(t * 1.1f + 0.4f) * (IdleRotationAmount * 0.15f);
            
            targetRotation = Quaternion.Euler(idleRotX, idleRotY, idleRotZ);
            
            // Scale breathing (multiple harmonics)
            float breath = 1f + Mathf.Sin(t * 0.8f) * IdleScaleAmount
                             + Mathf.Sin(t * 1.6f + 0.3f) * (IdleScaleAmount * 0.3f);
            targetScale = _baseScale * breath;
            
            // Subtle position offset (floating effect)
            float posX = Mathf.Sin(t * 0.9f) * 1.5f + Mathf.Sin(t * 1.5f) * 0.5f;
            float posY = Mathf.Cos(t * 0.7f) * 1.0f + Mathf.Cos(t * 1.3f) * 0.3f;
            targetPosition = new Vector3(posX, posY, 0);
        }
        
        // === CURSOR TRACKING (subtle) ===
        if (EnableCardFollowing)
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
            float cursorTiltY = -cursorNormX * FollowTiltAmount * cursorWeight; // Left/right
            float cursorTiltX = cursorNormY * FollowTiltAmount * cursorWeight;   // Up/down (same multiplier)
            
            Quaternion cursorRotation = Quaternion.Euler(cursorTiltX, cursorTiltY, 0);
            targetRotation = targetRotation * cursorRotation;
        }
        
        // === CARD FOLLOWING (3D Look At) - intensity scales with magnitude ===
        if (EnableCardFollowing && _cardScreenPosition.sqrMagnitude > 0.01f)
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
            float tiltY = -normX * FollowTiltAmount * 2f * cardWeight;  // Left/right
            float tiltX = normY * FollowTiltAmount * 2f * cardWeight;   // Up/down (SAME as Y now!)
            float tiltZ = -normX * FollowTiltAmount * 0.3f * cardWeight; // Reduced Z roll
            
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
            Time.deltaTime * FollowSpeed
        );
        _rectTransform.localScale = Vector3.Lerp(
            _rectTransform.localScale,
            targetScale,
            Time.deltaTime * FollowSpeed
        );
        
        // Apply position offset (anchoredPosition)
        Vector2 currentPos = _rectTransform.anchoredPosition;
        Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.y);
        _rectTransform.anchoredPosition = Vector2.Lerp(currentPos, targetPos2D, Time.deltaTime * FollowSpeed * 0.5f);
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
        
        if (_graphic.material != null && _graphic.material.shader.name == "RoyalLeech/UI/LiquidFill")
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
                _graphic.material.shader.name == "RoyalLeech/UI/LiquidFill")
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
        Vector2 newOffset = CalculateShadowDirection() * ShadowIntensity;
        if (Vector2.Distance(newOffset, _currentShadowOffset) > 0.1f)
        {
            _currentShadowOffset = newOffset;
            if (_graphic != null)
                _graphic.SetVerticesDirty();
        }
    }
    
    void UpdateShaderProperties()
    {
        // Fill (colors from preset)
        _materialInstance.SetFloat(FillAmountID, fillAmount);
        _materialInstance.SetColor(FillColorID, FillColor);
        _materialInstance.SetColor(BackgroundColorID, BackgroundColor);
        _materialInstance.SetFloat(BackgroundAlphaID, BackgroundAlpha);
        _materialInstance.SetFloat(FillWaveStrengthID, FillWaveStrength);
        _materialInstance.SetFloat(FillWaveSpeedID, FillWaveSpeed);
        
        // Liquid Effects
        _materialInstance.SetFloat(MeniscusStrengthID, MeniscusStrength);
        _materialInstance.SetFloat(LiquidTurbulenceID, liquidTurbulence);
        _materialInstance.SetFloat(BubbleIntensityID, bubbleIntensity);
        _materialInstance.SetFloat(BubbleSizeID, BubbleSize);
        _materialInstance.SetColor(BubbleColorID, BubbleColor);
        _materialInstance.SetFloat(SplashIntensityID, splashIntensity);
        _materialInstance.SetFloat(PixelDensityID, PixelDensity);
        
        // Effects (white-based)
        _materialInstance.SetFloat(EffectIntensityID, effectIntensity);
        _materialInstance.SetFloat(IncreaseStrengthID, IncreaseStrength);
        _materialInstance.SetFloat(DecreaseStrengthID, DecreaseStrength);
        _materialInstance.SetFloat(GlowIntensityID, glowIntensity);
        _materialInstance.SetFloat(GlowStrengthID, GlowStrength);
        _materialInstance.SetFloat(PulseSpeedID, PulseSpeed);
        _materialInstance.SetFloat(PulseIntensityID, pulseIntensity);
        
        // Shake
        _materialInstance.SetFloat(ShakeIntensityID, shakeIntensity);
        _materialInstance.SetFloat(ShakeSpeedID, 30f); // hardcoded shake speed
        
        // Trailing fill (delayed damage indicator)
        _materialInstance.SetFloat(TrailingFillID, trailingFill);
        
        // Shadow
        _materialInstance.SetColor(ShadowColorID, ShadowColor);
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
        Vector3 offset = new Vector3(_currentShadowOffset.x, _currentShadowOffset.y, 0);
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
        if (duration < 0) duration = EffectDuration * 0.5f;
        
        _fillTween?.Kill();
        _fillTween = DOTween.To(() => fillAmount, x => fillAmount = x, targetFill, duration)
            .SetEase(Ease.OutBack);
        
        return _fillTween;
    }
    
    /// <summary>
    /// JUICY resource gain effect!
    /// Uses effectIntensity > 0 to ADD white. Scale punch + fill increase + LIQUID SPLASH!
    /// </summary>
    /// <param name="newFillAmount">Target fill amount</param>
    /// <param name="delta">Amount changed (used to determine minor/major effect)</param>
    public Sequence PlayGainEffect(float newFillAmount, int delta = 100)
    {
        _currentSequence?.Kill();
        _currentSequence = DOTween.Sequence();
        
        // Determine effect tier: Minor / Normal / Major
        int absDelta = Mathf.Abs(delta);
        float multiplier;
        if (absDelta <= MinorThreshold)
            multiplier = MinorMultiplier;      // Minor effect
        else if (absDelta > MajorThreshold)
            multiplier = MajorMultiplier;      // Major effect
        else
            multiplier = 1f;                   // Normal effect
        
        // Timing derived from effectDuration
        float punchDur = EffectDuration * 0.4f;
        float fillDur = EffectDuration * 0.5f;
        float fadeDur = EffectDuration * 0.5f;
        
        // LIQUID SPLASH (scaled by multiplier)
        PlaySplash(GainSplashIntensity * multiplier, EffectDuration);
        
        // Reset glow
        glowIntensity = 0f;
        _isCriticalGlowActive = false;
        
        // Set effect intensity (scaled by multiplier)
        effectIntensity = 1f * multiplier;
        
        // Scale punch - positive = grow (scaled by multiplier)
        _currentSequence.Append(
            _rectTransform.DOPunchScale(Vector3.one * IncreasePunchScale * multiplier, punchDur, 2, 0.5f)
        );
        
        // Fill animation
        _currentSequence.Join(AnimateFillTo(newFillAmount, fillDur));
        
        // TRAILING: trailing stays at OLD level, then catches up to new fill
        // (shows the "future" fill level as light zone above current trailing)
        _trailingTween?.Kill();
        _trailingTween = DOTween.To(() => trailingFill, x => trailingFill = x, newFillAmount, TrailingDuration)
            .SetDelay(TrailingDelay)
            .SetEase(Ease.InOutQuad);
        
        // Fade out effect
        _currentSequence.Append(
            DOTween.To(() => effectIntensity, x => effectIntensity = x, 0f, fadeDur).SetEase(Ease.OutQuad)
        );
        
        return _currentSequence;
    }
    
    /// <summary>
    /// JUICY resource loss effect!
    /// Uses effectIntensity < 0 to SUBTRACT (darken). Shake + fill decrease + LIQUID SPLASH!
    /// Punch is NEGATIVE (shrink) for loss!
    /// Includes TRAILING FILL effect (delayed damage indicator).
    /// </summary>
    /// <param name="newFillAmount">Target fill amount</param>
    /// <param name="delta">Amount changed (absolute value used to determine minor/major effect)</param>
    public Sequence PlayLossEffect(float newFillAmount, int delta = 100)
    {
        _currentSequence?.Kill();
        _trailingTween?.Kill();
        _currentSequence = DOTween.Sequence();
        
        // Determine effect tier: Minor / Normal / Major
        int absDelta = Mathf.Abs(delta);
        float multiplier;
        if (absDelta <= MinorThreshold)
            multiplier = MinorMultiplier;      // Minor effect
        else if (absDelta > MajorThreshold)
            multiplier = MajorMultiplier;      // Major effect
        else
            multiplier = 1f;                   // Normal effect
        
        // Timing derived from effectDuration
        float shakeDur = EffectDuration * 0.4f;
        float punchDur = EffectDuration * 0.4f;
        float fillDur = EffectDuration * 0.5f;
        float fadeDur = EffectDuration * 0.5f;
        
        // === TRAILING FILL (delayed damage indicator) ===
        // Keep trailing at current fill amount, then animate to new value after delay
        // trailingFill stays at current value, actual fill drops immediately
        
        // LIQUID SPLASH (scaled by multiplier)
        PlaySplash(LossSplashIntensity * multiplier, EffectDuration);
        
        // Reset glow
        glowIntensity = 0f;
        _isCriticalGlowActive = false;
        
        // Set effect intensity (negative = darken, scaled by multiplier)
        effectIntensity = -1f * multiplier;
        shakeIntensity = LossShakeIntensity * multiplier;
        
        // Scale punch - NEGATIVE = shrink (scaled by multiplier)
        _currentSequence.Append(
            _rectTransform.DOPunchScale(Vector3.one * -DecreasePunchScale * multiplier, punchDur, 2, 0.5f)
        );
        
        // Shake - always play, scaled by multiplier (base intensity 15f for visibility)
        _currentSequence.Join(
            _rectTransform.DOShakePosition(shakeDur, 15f * multiplier, 20, 90f, false, true)
        );
        
        // Fill animation (actual fill drops immediately)
        _currentSequence.Join(AnimateFillTo(newFillAmount, fillDur));
        
        // Trailing fill animation: delay, then catch up
        _trailingTween = DOTween.To(() => trailingFill, x => trailingFill = x, newFillAmount, TrailingDuration)
            .SetDelay(TrailingDelay)
            .SetEase(Ease.InOutQuad);
        
        // Fade out effects
        _currentSequence.Append(
            DOTween.To(() => shakeIntensity, x => shakeIntensity = x, 0f, fadeDur * 0.8f)
        );
        _currentSequence.Join(
            DOTween.To(() => effectIntensity, x => effectIntensity = x, 0f, fadeDur).SetEase(Ease.OutQuad)
        );
        
        return _currentSequence;
    }
    
    /// <summary>
    /// Critical/low resource warning effect!
    /// Continuous pulse + red glow.
    /// </summary>
    public void StartCriticalPulse()
    {
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
    /// Quick glow burst effect (uses GlowColor from preset).
    /// </summary>
    public Tween GlowBurst(float intensity = 0.7f)
    {
        glowIntensity = intensity;
        
        return DOTween.To(() => glowIntensity, x => glowIntensity = x, 0f, EffectDuration * 0.3f)
            .SetEase(Ease.OutQuad);
    }
    
    /// <summary>
    /// Punch scale effect (uses IncreasePunchScale by default).
    /// </summary>
    public Tween DoPunchScale(float scale = -1)
    {
        if (scale < 0) scale = IncreasePunchScale;
        return _rectTransform.DOPunchScale(Vector3.one * scale, EffectDuration * 0.4f, 1, 0.5f);
    }
    
    /// <summary>
    /// Shake effect.
    /// </summary>
    public Tween Shake(float intensity = 10f, float duration = -1)
    {
        if (duration < 0) duration = EffectDuration * 0.4f;
        
        _shakeTween?.Kill();
        shakeIntensity = intensity;
        _shakeTween = DOTween.To(() => shakeIntensity, x => shakeIntensity = x, 0f, duration)
            .SetEase(Ease.OutQuad);
        
        return _shakeTween;
    }
    
    /// <summary>
    /// Glow effect with auto-fade.
    /// </summary>
    public Tween Glow(float intensity = 1f, float duration = 0.5f)
    {
        glowIntensity = intensity;
        
        _glowTween?.Kill();
        _glowTween = DOTween.To(() => glowIntensity, x => glowIntensity = x, 0f, duration)
            .SetEase(Ease.OutQuad);
        
        return _glowTween;
    }
    
    // === SIMPLE API (no animation) ===
    
    public void SetFill(float amount) => fillAmount = amount;
    public void SetPulse(float intensity) => pulseIntensity = intensity;
    public void SetGlowIntensity(float intensity) => glowIntensity = intensity;
    public void SetShake(float intensity) => shakeIntensity = intensity;
    
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
    // === SIMPLE EFFECT (no magnitude scaling) ===
    // =====================================================
    
    /// <summary>
    /// Play effect for resource change - automatically picks IncreaseColor or DecreaseColor.
    /// Effect intensity does NOT scale with magnitude.
    /// </summary>
    /// <param name="newFillAmount">Target fill 0-1</param>
    /// <param name="isIncrease">True for gain, false for loss</param>
    public Sequence PlayChangeEffect(float newFillAmount, bool isIncrease)
    {
        if (isIncrease)
            return PlayGainEffect(newFillAmount);
        else
            return PlayLossEffect(newFillAmount);
    }
    
    /// <summary>
    /// Preview/highlight effect for when player is hovering/dragging.
    /// Shows that this resource WILL change via SHAKE + LIQUID AGITATION!
    /// Intensity depends on swipeProgress AND delta (Minor/Normal/Major system).
    /// </summary>
    /// <param name="swipeProgress">How close to making the decision (0-1)</param>
    /// <param name="delta">Expected change amount (used for Minor/Normal/Major tier)</param>
    public void PlayHighlightPreview(float swipeProgress = 1f, int delta = 100)
    {
        _previewGlowTween?.Kill();
        _previewPulseTween?.Kill();
        
        _isPreviewActive = true;
        
        // Determine effect tier: Minor / Normal / Major
        int absDelta = Mathf.Abs(delta);
        float tierMultiplier;
        if (absDelta <= MinorThreshold)
            tierMultiplier = MinorMultiplier;      // Minor effect
        else if (absDelta > MajorThreshold)
            tierMultiplier = MajorMultiplier;      // Major effect
        else
            tierMultiplier = 1f;                   // Normal effect
        
        // Intensity based on swipe progress (0 = far, 1 = at threshold)
        float t = Mathf.Clamp01(swipeProgress);
        
        // Shake - interpolates from 0 to PreviewShakeBase, then scaled by tier
        float baseShake = Mathf.Lerp(0f, PreviewShakeBase, t);
        shakeIntensity = baseShake * tierMultiplier;
        
        // Liquid agitation - turbulence + bubbles (also scaled by tier)
        liquidTurbulence = Mathf.Lerp(0.2f, 0.8f, t) * tierMultiplier;
        bubbleIntensity = Mathf.Lerp(0.1f, 0.4f, t) * tierMultiplier;
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
        pulseIntensity = 0;
        glowIntensity = 0;
        shakeIntensity = 0;
    }
}
