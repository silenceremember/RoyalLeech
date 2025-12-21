using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a dynamic shadow to the object.
/// Creates a separate GameObject for the shadow and maintains it as a sibling rendered BEHIND this object.
/// </summary>
public class Shadow : MonoBehaviour
{
    [Header("Shadow Settings")]
    public Color shadowColor = new Color(0, 0, 0, 0.5f);
    public float scaleMultiplier = 1.0f;
    
    [Tooltip("Local offset applied on top of global light calculation")]
    public Vector2 localShift = Vector2.zero;

    [Tooltip("Assign the background image here to cast the shadow from it.")]
    public Graphic targetGraphic;
    public SpriteRenderer targetSpriteRenderer;

    private GameObject _shadowObject;
    private RectTransform _parentRect;
    private RectTransform _shadowRect;
    private Image _shadowImage;
    private SpriteRenderer _shadowSprite;
    
    // To prevent double shadows, we track if we created one
    private bool _hasInitialized = false;

    void Start()
    {
        if (_hasInitialized) return;
        Initialize();
    }

    void Initialize()
    {
        // Auto-detect targets
        if (targetGraphic == null) targetGraphic = GetComponent<Image>();
        if (targetSpriteRenderer == null) targetSpriteRenderer = GetComponent<SpriteRenderer>();

        // Clean up any existing loose shadows that might have been left over (by name check)
        // This fixes the "Double Shadow" if Scene wasn't cleaned
        Transform existing = transform.parent != null 
            ? transform.parent.Find($"{gameObject.name}_Shadow_Generated") 
            : null;
            
        if (existing != null) Destroy(existing.gameObject);

        CreateShadowObject();
        _hasInitialized = true;
    }

    void CreateShadowObject()
    {
        if (_shadowObject != null) Destroy(_shadowObject);

        // Name it uniquely so we can find it
        _shadowObject = new GameObject($"{gameObject.name}_Shadow_Generated");
        
        // HIERARCHY: Sibling, Index = MyIndex - 1 (Behind)
        // We use Sibling approach because "Parenting" the Card to the Shadow breaks movement logic (DoTween, etc)
        // The user asked for "ShadowParent", but that likely implies "Visual Layering".
        // Sibling Index < My Index == Rendered Behind (in UI).
        
        if (transform.parent != null)
        {
            _shadowObject.transform.SetParent(transform.parent, false);
            int myIndex = transform.GetSiblingIndex();
            _shadowObject.transform.SetSiblingIndex(Mathf.Max(0, myIndex)); // Initially same, Update will fix
        }
        else
        {
             // Root object case
             _shadowObject.transform.SetParent(null, false);
        }

        // Init Transforms
        _shadowObject.transform.localPosition = transform.localPosition;
        _shadowObject.transform.localScale = transform.localScale;
        _shadowObject.transform.localRotation = transform.localRotation;

        // Setup Components
        if (targetGraphic != null)
        {
            SetupUIShadow();
        }
        else if (targetSpriteRenderer != null)
        {
            SetupSpriteShadow();
        }
    }

    void SetupUIShadow()
    {
        _shadowRect = _shadowObject.AddComponent<RectTransform>();
        _shadowImage = _shadowObject.AddComponent<Image>();
        _parentRect = GetComponent<RectTransform>();

        // Match anchors of the object we are shadowing
        if (_parentRect != null)
        {
            _shadowRect.anchorMin = _parentRect.anchorMin;
            _shadowRect.anchorMax = _parentRect.anchorMax;
            _shadowRect.pivot = _parentRect.pivot;
            _shadowRect.sizeDelta = _parentRect.sizeDelta;
        }

        // Visuals from Target
        Image srcImg = targetGraphic as Image;
        if (srcImg != null)
        {
            _shadowImage.sprite = srcImg.sprite;
            _shadowImage.type = srcImg.type;
            _shadowImage.preserveAspect = srcImg.preserveAspect;
            _shadowImage.raycastTarget = false;
        }

        _shadowImage.color = shadowColor;
    }

    void SetupSpriteShadow()
    {
        _shadowSprite = _shadowObject.AddComponent<SpriteRenderer>();
        _shadowSprite.sprite = targetSpriteRenderer.sprite;
        _shadowSprite.sortingLayerID = targetSpriteRenderer.sortingLayerID;
        _shadowSprite.sortingOrder = targetSpriteRenderer.sortingOrder - 1; // Behind
        _shadowSprite.color = shadowColor;
        
        _shadowObject.transform.localScale = transform.localScale;
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

    void LateUpdate()
    {
        if (ShadowLightSource.Instance == null) return;
        
        // Safety: If shadow was deleted externally, recreate
        if (_shadowObject == null) 
        {
            if (_hasInitialized && Application.isPlaying) CreateShadowObject();
            return;
        }

        // 1. Force Sibling Order (Render Behind)
        if (transform.parent != null && _shadowObject.transform.parent == transform.parent)
        {
            int myIndex = transform.GetSiblingIndex();
            // We want shadow to be at myIndex - 1
            // Only update if needed to avoid overhead
            if (_shadowObject.transform.GetSiblingIndex() >= myIndex)
            {
                _shadowObject.transform.SetSiblingIndex(Mathf.Max(0, myIndex)); 
                // Note: SetSiblingIndex(x) puts it at x. 
                // If I am at 5, Shadow at 5 -> Shadow pushes me to 6. Shadow is behind? 
                // In Unity UI: Higher Index = On Top.
                // So Shadow (Index 0) is Bottom. Card (Index 1) is Top.
                // So Shadow Index MUST BE < My Index.
                _shadowObject.transform.SetSiblingIndex(Mathf.Max(0, transform.GetSiblingIndex()));
            }
        }

        // 2. Sync Visuals
        if (targetGraphic is Image srcImg && _shadowImage != null)
        {
            if (_shadowImage.sprite != srcImg.sprite) _shadowImage.sprite = srcImg.sprite;
            if (_parentRect != null) _shadowRect.sizeDelta = _parentRect.sizeDelta;
        }

        // 3. Scale Logic
        _shadowObject.transform.localScale = transform.localScale * scaleMultiplier;
        _shadowObject.transform.localRotation = transform.localRotation;

        // 4. Calculate Shadow Position
        Vector2 screenPos = GetScreenPosition();
        Vector2 offset = ShadowLightSource.Instance.GetShadowOffset(screenPos);
        offset += localShift;

        // Apply simple offset to World Position
        _shadowObject.transform.position = transform.position + (Vector3)offset;
        
        // Z correction (UI)
        if (targetGraphic != null)
        {
            Vector3 lc = _shadowObject.transform.localPosition;
            lc.z = transform.localPosition.z;
            _shadowObject.transform.localPosition = lc;
        }
    }

    Vector2 GetScreenPosition()
    {
        Vector2 screenPos = Vector2.zero;
        if (targetGraphic != null)
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
        else
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
