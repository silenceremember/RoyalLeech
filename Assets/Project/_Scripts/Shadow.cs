using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TMPEffects.Components;

/// <summary>
/// Adds a dynamic shadow to the object.
/// Creates a separate shadow object as a sibling (rendered behind).
/// Supports Image, SpriteRenderer, and TextMeshPro (UI & World).
/// </summary>
public class Shadow : MonoBehaviour
{
    [Header("Shadow Settings")]
    public Color shadowColor = new Color(0, 0, 0, 0.5f);
    
    [Tooltip("How far the shadow is cast. Replaces global intensity.")]
    public float intensity = 15f; 

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
    private TMPWriter _shadowWriter; // TMPWriter для синхронизации с оригиналом
    
    private bool _hasInitialized = false;

    void Start()
    {
        if (!_hasInitialized) Initialize();
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
        
        // Проверяем, есть ли TMPWriter на оригинале
        TMPWriter targetWriter = targetText.GetComponent<TMPWriter>();
        if (targetWriter != null)
        {
            // Добавляем TMPWriter и на shadow для синхронизации
            _shadowWriter = _shadowObject.AddComponent<TMPWriter>();
            _shadowWriter.enabled = false; // Отключаем, будем синхронизировать вручную
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
            targetAlpha = targetText.alpha;
            // TMP specific: multiply vertex color alpha? Or just use .color?
            // TMP usually uses .alpha or .color.a. Let's use simple color assignment.
             finalColor.a = shadowColor.a * targetAlpha;
            _shadowText.color = finalColor;
        }
        else if (targetGraphic is Image srcImg && _shadowImage != null)
        {
            if (_shadowImage.sprite != srcImg.sprite) _shadowImage.sprite = srcImg.sprite;
            if (_parentRect != null) _shadowRect.sizeDelta = _parentRect.sizeDelta;
            
            targetAlpha = srcImg.color.a;
            finalColor.a = shadowColor.a * targetAlpha;
            _shadowImage.color = finalColor;
        }
        else if (targetSpriteRenderer != null && _shadowSprite != null)
        {
            if (_shadowSprite.sprite != targetSpriteRenderer.sprite) _shadowSprite.sprite = targetSpriteRenderer.sprite;
            
            targetAlpha = targetSpriteRenderer.color.a;
            finalColor.a = shadowColor.a * targetAlpha;
            _shadowSprite.color = finalColor;
        }

        // 3. Transform
        _shadowObject.transform.localScale = transform.localScale; // No multiplier
        _shadowObject.transform.localRotation = transform.localRotation;

        // 4. Position
        Vector2 screenPos = GetScreenPosition();
        
        // Get Direction Factor from LightSource (Perspective)
        Vector2 directionFactor = ShadowLightSource.Instance.GetShadowDirection(screenPos);
        
        // Apply local Intensity
        Vector2 offset = directionFactor * intensity;

        _shadowObject.transform.position = transform.position + (Vector3)offset;
        
        // Z Flat
        if (_parentRect != null)
        {
             Vector3 lc = _shadowObject.transform.localPosition;
             lc.z = transform.localPosition.z;
             _shadowObject.transform.localPosition = lc;
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
        
        // Проверяем наличие TMPWriter для корректной синхронизации видимых символов
        TMPWriter tmpWriter = targetText.GetComponent<TMPWriter>();
        if (tmpWriter != null && _shadowWriter != null)
        {
            // ЛУЧШИЙ ПОДХОД: Копируем mesh data напрямую от оригинала
            // Это гарантирует 100% синхронизацию, включая все эффекты TMPWriter
            targetText.ForceMeshUpdate();
            _shadowText.ForceMeshUpdate();
            
            // Копируем vertex data для точной синхронизации
            if (targetText.textInfo != null && _shadowText.textInfo != null)
            {
                for (int i = 0; i < targetText.textInfo.meshInfo.Length; i++)
                {
                    if (i >= _shadowText.textInfo.meshInfo.Length) break;
                    
                    // Копируем только vertices для синхронизации видимости
                    // (цвета не копируем, т.к. у тени свой цвет)
                    var sourceVerts = targetText.textInfo.meshInfo[i].vertices;
                    var targetVerts = _shadowText.textInfo.meshInfo[i].vertices;
                    
                    if (sourceVerts != null && targetVerts != null)
                    {
                        int count = Mathf.Min(sourceVerts.Length, targetVerts.Length);
                        System.Array.Copy(sourceVerts, targetVerts, count);
                    }
                }
                
                // Обновляем mesh с новыми вертексами
                _shadowText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }
        }
        else
        {
            // Обычная синхронизация для текста без TMPWriter
            _shadowText.maxVisibleCharacters = targetText.maxVisibleCharacters;
        }
        
        _shadowText.characterSpacing = targetText.characterSpacing;
        
        // Rect Size sync is crucial for text alignment
        if (_parentRect != null) _shadowRect.sizeDelta = _parentRect.sizeDelta;
    }

    Vector2 GetScreenPosition()
    {
        Vector2 screenPos = Vector2.zero;
        
        if (_parentRect != null) // UI
        {
            Camera cam = null;
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) cam = c.worldCamera;
            if (cam == null) cam = Camera.main;

            if (cam != null)
                 screenPos = RectTransformUtility.WorldToScreenPoint(cam, transform.position);
            else
                 screenPos = RectTransformUtility.WorldToScreenPoint(null, transform.position);
            
            screenPos -= new Vector2(Screen.width / 2f, Screen.height / 2f);
        }
        else // World
        {
             if (Camera.main != null)
             {
                 Vector2 vp = Camera.main.WorldToViewportPoint(transform.position);
                 screenPos = (vp - new Vector2(0.5f, 0.5f)) * new Vector2(Screen.width, Screen.height);
             }
        }
        return screenPos;
    }
}
