using UnityEngine;
using UnityEngine.UI;
using TMPro;
// TMPEffects replaced with TextAnimator

/// <summary>
/// Adds a dynamic shadow to the object.
/// Creates a separate shadow object as a sibling (rendered behind).
/// Supports Image, SpriteRenderer, and TextMeshPro (UI & World).
/// For text, applies per-character shadow offset for maximum quality.
/// </summary>
public class Shadow : MonoBehaviour
{
    [Header("Shadow Settings")]
    public Color shadowColor = new Color(0, 0, 0, 0.5f);
    
    [Tooltip("Base shadow offset distance at scale 1.0")]
    public float intensity = 15f;
    
    [Header("Scale Influence")]
    [Tooltip("Enable dynamic shadow distance based on scale (physically correct light simulation)")]
    public bool useScaleInfluence = false;
    
    [Tooltip("How much scale affects shadow distance. 1.0 = linear, 2.0 = doubled effect, etc.")]
    public float scaleInfluence = 1f;
    
    [Header("Transform Reference")]
    [Tooltip("Transform to track for scale (optional). If null, uses own transform.")]
    public Transform scaleReference;
    
    [Header("Runtime Info (Read-only)")]
    [Tooltip("Current effective intensity based on scale")]
    public float currentEffectiveIntensity;
    
    [Header("Quality Settings")]
    [Tooltip("Enable per-character shadow for text (higher quality, each letter casts its own shadow)")]
    public bool perCharacterShadow = true;
    
    [Header("Targets (Auto-filled)")]
    public Graphic targetGraphic;
    public SpriteRenderer targetSpriteRenderer;
    public TMP_Text targetText; // Supports both TextMeshPro and TextMeshProUGUI

    // Internal
    private GameObject _shadowObject;
    private RectTransform _parentRect;
    private RectTransform _shadowRect;
    
    private Image _shadowImage;
    private SpriteRenderer _shadowSprite;
    private TMP_Text _shadowText;
    private TextAnimator _shadowAnimator; // TextAnimator для синхронизации с оригиналом
    
    private bool _hasInitialized = false;

    void Start()
    {
        if (!_hasInitialized) Initialize();
    }

    void OnValidate()
    {
        // Update effective intensity in Inspector even in Edit mode
        if (useScaleInfluence)
        {
            Transform refTransform = scaleReference != null ? scaleReference : transform;
            if (refTransform != null)
            {
                float scaleAverage = (refTransform.localScale.x + refTransform.localScale.y) * 0.5f;
                float scaleDelta = scaleAverage - 1f;
                currentEffectiveIntensity = intensity * (1f + scaleDelta * scaleInfluence);
            }
        }
        else
        {
            currentEffectiveIntensity = intensity;
        }
    }

    void OnEnable()
    {
        if (_shadowObject != null) _shadowObject.SetActive(true);
    }

    void OnDisable()
    {
        if (_shadowObject != null) _shadowObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_shadowObject != null) Destroy(_shadowObject);
    }

    void Initialize()
    {
        // Auto-detect targets
        if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
        if (targetSpriteRenderer == null) targetSpriteRenderer = GetComponent<SpriteRenderer>();
        if (targetText == null) targetText = GetComponent<TMP_Text>();

        // Cleanup existing
        Transform existing = transform.parent != null 
            ? transform.parent.Find($"{gameObject.name}_Shadow") 
            : null;
        if (existing != null) Destroy(existing.gameObject);

        CreateShadowObject();
        _hasInitialized = true;
    }

    void CreateShadowObject()
    {
        if (_shadowObject != null) Destroy(_shadowObject);
        _shadowObject = new GameObject($"{gameObject.name}_Shadow");
        
        // Hierarchy: Sibling Behind
        if (transform.parent != null)
        {
            _shadowObject.transform.SetParent(transform.parent, false);
            _shadowObject.transform.SetSiblingIndex(Mathf.Max(0, transform.GetSiblingIndex()));
        }
        else
        {
             _shadowObject.transform.SetParent(null, false);
        }

        // Transforms
        _shadowObject.transform.localPosition = transform.localPosition;
        _shadowObject.transform.localScale = transform.localScale;
        _shadowObject.transform.localRotation = transform.localRotation;

        // Setup Components
        _parentRect = GetComponent<RectTransform>();
        if (_parentRect != null)
        {
            _shadowRect = _shadowObject.AddComponent<RectTransform>();
            _shadowRect.anchorMin = _parentRect.anchorMin;
            _shadowRect.anchorMax = _parentRect.anchorMax;
            _shadowRect.pivot = _parentRect.pivot;
            _shadowRect.sizeDelta = _parentRect.sizeDelta;
        }

        // Visuals
        if (targetText != null)
        {
            SetupTextShadow();
        }
        else if (targetGraphic is Image img)
        {
            SetupImageShadow(img);
        }
        else if (targetSpriteRenderer != null)
        {
            SetupSpriteShadow();
        }
        else
        {
             // Fallback: Check if targetGraphic is generic (not Image)
        }
    }

    void SetupTextShadow()
    {
        // Add same component type (UGUI or World)
        if (targetText is TextMeshProUGUI)
            _shadowText = _shadowObject.AddComponent<TextMeshProUGUI>();
        else
            _shadowText = _shadowObject.AddComponent<TextMeshPro>();

        _shadowText.raycastTarget = false;
        _shadowText.color = shadowColor;
        
        // Проверяем, есть ли TextAnimator на оригинале
        TextAnimator targetAnimator = targetText.GetComponent<TextAnimator>();
        if (targetAnimator != null)
        {
            // Добавляем TextAnimator и на shadow для синхронизации
            _shadowAnimator = _shadowObject.AddComponent<TextAnimator>();
            _shadowAnimator.enabled = false; // Отключаем, будем синхронизировать вручную
        }
        
        SyncText();
    }

    void SetupImageShadow(Image srcImg)
    {
        _shadowImage = _shadowObject.AddComponent<Image>();
        _shadowImage.sprite = srcImg.sprite;
        _shadowImage.type = srcImg.type;
        _shadowImage.preserveAspect = srcImg.preserveAspect;
        _shadowImage.raycastTarget = false;
        _shadowImage.color = shadowColor;
    }

    void SetupSpriteShadow()
    {
        _shadowSprite = _shadowObject.AddComponent<SpriteRenderer>();
        _shadowSprite.sprite = targetSpriteRenderer.sprite;
        _shadowSprite.sortingLayerID = targetSpriteRenderer.sortingLayerID;
        _shadowSprite.sortingOrder = targetSpriteRenderer.sortingOrder - 1;
        _shadowSprite.color = shadowColor;
    }

    void LateUpdate()
    {
        if (ShadowLightSource.Instance == null) return;
        if (_shadowObject == null) 
        {
            if (_hasInitialized && Application.isPlaying) CreateShadowObject();
            return;
        }

        // Calculate and update current effective intensity for Inspector display
        if (useScaleInfluence)
        {
            Transform refTransform = scaleReference != null ? scaleReference : transform;
            float scaleAverage = (refTransform.localScale.x + refTransform.localScale.y) * 0.5f;
            float scaleDelta = scaleAverage - 1f;
            currentEffectiveIntensity = intensity * (1f + scaleDelta * scaleInfluence);
        }
        else
        {
            currentEffectiveIntensity = intensity;
        }

        // 1. Hierarchy Maintain
        if (transform.parent != null && _shadowObject.transform.parent == transform.parent)
        {
            if (_shadowObject.transform.GetSiblingIndex() >= transform.GetSiblingIndex())
            {
                 _shadowObject.transform.SetSiblingIndex(Mathf.Max(0, transform.GetSiblingIndex()));
            }
        }

        // 2. Sync Visuals
        Color finalColor = shadowColor;
        float targetAlpha = 1f;

        if (targetText != null && _shadowText != null)
        {
            SyncText();
            
            // Если текст пустой или нет видимых символов - скрываем тень
            if (string.IsNullOrEmpty(targetText.text) || targetText.maxVisibleCharacters == 0)
            {
                finalColor.a = 0; // Полностью прозрачная тень
                _shadowText.color = finalColor;
                return; // Не применяем offset/per-character shadow
            }
            
            targetAlpha = targetText.alpha;
            finalColor.a = shadowColor.a * targetAlpha;
            _shadowText.color = finalColor;
            
            // Apply per-character shadow if enabled
            if (perCharacterShadow)
            {
                ApplyPerCharacterShadow();
            }
            else
            {
                ApplySimpleShadowOffset();
            }
        }
        else if (targetGraphic is Image srcImg && _shadowImage != null)
        {
            if (_shadowImage.sprite != srcImg.sprite) _shadowImage.sprite = srcImg.sprite;
            if (_parentRect != null) _shadowRect.sizeDelta = _parentRect.sizeDelta;
            
            targetAlpha = srcImg.color.a;
            finalColor.a = shadowColor.a * targetAlpha;
            _shadowImage.color = finalColor;
            
            ApplySimpleShadowOffset();
        }
        else if (targetSpriteRenderer != null && _shadowSprite != null)
        {
            if (_shadowSprite.sprite != targetSpriteRenderer.sprite) _shadowSprite.sprite = targetSpriteRenderer.sprite;
            
            targetAlpha = targetSpriteRenderer.color.a;
            finalColor.a = shadowColor.a * targetAlpha;
            _shadowSprite.color = finalColor;
            
            ApplySimpleShadowOffset();
        }

        // 3. Transform (scale and rotation sync)
        _shadowObject.transform.localScale = transform.localScale;
        _shadowObject.transform.localRotation = transform.localRotation;
        
        // Z Flat for UI elements
        if (_parentRect != null && targetText != null && perCharacterShadow)
        {
            // For per-character shadow, keep position synced
            _shadowObject.transform.position = transform.position;
            Vector3 lc = _shadowObject.transform.localPosition;
            lc.z = transform.localPosition.z;
            _shadowObject.transform.localPosition = lc;
        }
    }
    
    void ApplySimpleShadowOffset()
    {
        // Use pre-calculated effective intensity from LateUpdate
        float effectiveIntensity = currentEffectiveIntensity;
        
        // For non-text objects or simple shadow mode
        Vector2 screenPos = GetScreenPosition();
        Vector2 directionFactor = ShadowLightSource.Instance.GetShadowDirection(screenPos);
        Vector2 offset = directionFactor * effectiveIntensity;
        _shadowObject.transform.position = transform.position + (Vector3)offset;
        
        // Z Flat
        if (_parentRect != null)
        {
             Vector3 lc = _shadowObject.transform.localPosition;
             lc.z = transform.localPosition.z;
             _shadowObject.transform.localPosition = lc;
        }
    }
    
    void ApplyPerCharacterShadow()
    {
        if (_shadowText == null || targetText == null) return;
        
        // Use pre-calculated effective intensity from LateUpdate
        float effectiveIntensity = currentEffectiveIntensity;
        
        // Force mesh update
        targetText.ForceMeshUpdate();
        _shadowText.ForceMeshUpdate();
        
        TMP_TextInfo textInfo = _shadowText.textInfo;
        if (textInfo == null) return;
        
        int characterCount = textInfo.characterCount;
        
        // Process each visible character
        for (int i = 0; i < characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            
            // Skip invisible characters
            if (!charInfo.isVisible) continue;
            
            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            
            // Safety check
            if (materialIndex >= textInfo.meshInfo.Length) continue;
            
            // Get the vertices for this character
            Vector3[] sourceVertices = targetText.textInfo.meshInfo[materialIndex].vertices;
            Vector3[] destinationVertices = textInfo.meshInfo[materialIndex].vertices;
            
            // Safety check
            if (vertexIndex + 3 >= destinationVertices.Length || vertexIndex + 3 >= sourceVertices.Length) continue;
            
            // Calculate character center in local space
            Vector3 charCenter = (sourceVertices[vertexIndex + 0] + 
                                 sourceVertices[vertexIndex + 1] + 
                                 sourceVertices[vertexIndex + 2] + 
                                 sourceVertices[vertexIndex + 3]) * 0.25f;
            
            // Convert character center to world space
            Vector3 charWorldPos = targetText.transform.TransformPoint(charCenter);
            
            // Get screen position for this character
            Vector2 charScreenPos = GetScreenPositionForPoint(charWorldPos);
            
            // Get shadow direction for this specific character
            Vector2 directionFactor = ShadowLightSource.Instance.GetShadowDirection(charScreenPos);
            Vector2 offsetScreen = directionFactor * effectiveIntensity;
            
            // Convert screen space offset to local space of the shadow text
            Vector3 offsetLocal;
            if (_parentRect != null) // UI Element
            {
                // For UI, screen space offset needs to be divided by canvas scale factor
                Canvas canvas = GetComponentInParent<Canvas>();
                float scaleFactor = (canvas != null) ? canvas.scaleFactor : 1f;
                
                // Convert screen pixels to canvas units
                offsetLocal = new Vector3(offsetScreen.x / scaleFactor, offsetScreen.y / scaleFactor, 0);
            }
            else // World Space Text
            {
                // For world space, treat offset as world units
                offsetLocal = _shadowText.transform.InverseTransformDirection(new Vector3(offsetScreen.x, offsetScreen.y, 0));
            }
            
            // Apply offset to all 4 vertices of this character
            destinationVertices[vertexIndex + 0] = sourceVertices[vertexIndex + 0] + offsetLocal;
            destinationVertices[vertexIndex + 1] = sourceVertices[vertexIndex + 1] + offsetLocal;
            destinationVertices[vertexIndex + 2] = sourceVertices[vertexIndex + 2] + offsetLocal;
            destinationVertices[vertexIndex + 3] = sourceVertices[vertexIndex + 3] + offsetLocal;
        }
        
        // Update all meshes
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            _shadowText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }

    void SyncText()
    {
        // Sync vital text properties
        if (_shadowText.text != targetText.text) _shadowText.text = targetText.text;
        
        _shadowText.font = targetText.font;
        _shadowText.fontSize = targetText.fontSize;
        _shadowText.alignment = targetText.alignment;
        _shadowText.textWrappingMode = targetText.textWrappingMode;
        
        // Проверяем наличие TextAnimator для корректной синхронизации per-letter эффектов
        TextAnimator textAnimator = targetText.GetComponent<TextAnimator>();
        if (textAnimator != null)
        {
            // NEW: Копируем ВСЕ mesh данные (vertices + colors) для полной синхронизации
            // с per-letter эффектами TextAnimator (scale, rotation, offset, alpha)
            targetText.ForceMeshUpdate();
            _shadowText.ForceMeshUpdate();
            
            if (targetText.textInfo != null && _shadowText.textInfo != null)
            {
                for (int i = 0; i < targetText.textInfo.meshInfo.Length; i++)
                {
                    if (i >= _shadowText.textInfo.meshInfo.Length) break;
                    
                    // Копируем vertices (включая per-letter scale/rotation/offset)
                    var sourceVerts = targetText.textInfo.meshInfo[i].vertices;
                    var targetVerts = _shadowText.textInfo.meshInfo[i].vertices;
                    
                    if (sourceVerts != null && targetVerts != null)
                    {
                        int count = Mathf.Min(sourceVerts.Length, targetVerts.Length);
                        System.Array.Copy(sourceVerts, targetVerts, count);
                    }
                    
                    // NEW: Копируем alpha из colors32 для синхронизации прозрачности букв
                    var sourceColors = targetText.textInfo.meshInfo[i].colors32;
                    var targetColors = _shadowText.textInfo.meshInfo[i].colors32;
                    
                    if (sourceColors != null && targetColors != null)
                    {
                        int colorCount = Mathf.Min(sourceColors.Length, targetColors.Length);
                        for (int c = 0; c < colorCount; c++)
                        {
                            // Сохраняем RGB тени, но берем alpha из оригинала
                            Color32 shadowCol = targetColors[c];
                            shadowCol.a = (byte)(sourceColors[c].a * (shadowColor.a));
                            targetColors[c] = shadowCol;
                        }
                    }
                }
                
                // Обновляем mesh с новыми данными
                _shadowText.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
            }
        }
        
        // ВСЕГДА синхронизируем maxVisibleCharacters независимо от наличия TextAnimator
        // Это критично для корректной работы с динамическим typewriter
        _shadowText.maxVisibleCharacters = targetText.maxVisibleCharacters;
        
        _shadowText.characterSpacing = targetText.characterSpacing;
        
        // Rect Size sync is crucial for text alignment
        if (_parentRect != null) _shadowRect.sizeDelta = _parentRect.sizeDelta;
    }

    Vector2 GetScreenPosition()
    {
        return GetScreenPositionForPoint(transform.position);
    }
    
    Vector2 GetScreenPositionForPoint(Vector3 worldPos)
    {
        Vector2 screenPos = Vector2.zero;
        
        if (_parentRect != null) // UI
        {
            Camera cam = null;
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) cam = c.worldCamera;
            if (cam == null) cam = Camera.main;

            if (cam != null)
                 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
            else
                 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);
            
            screenPos -= new Vector2(Screen.width / 2f, Screen.height / 2f);
        }
        else // World
        {
             if (Camera.main != null)
             {
                 Vector2 vp = Camera.main.WorldToViewportPoint(worldPos);
                 screenPos = (vp - new Vector2(0.5f, 0.5f)) * new Vector2(Screen.width, Screen.height);
             }
        }
        return screenPos;
    }
}
