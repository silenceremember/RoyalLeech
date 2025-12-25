using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Shader-based shadow for UI Image elements using mesh modification.
/// Adds shadow geometry directly to the Image mesh for single draw call rendering.
/// </summary>
[RequireComponent(typeof(Graphic))]
[ExecuteAlways]
public class ImageShadow : MonoBehaviour, IMeshModifier
{
    [Header("Shadow Settings")]
    public Color shadowColor = new Color(0, 0, 0, 0.5f);
    
    [Tooltip("Base shadow offset distance")]
    public float intensity = 15f;
    
    [Header("Scale Influence")]
    [Tooltip("Enable dynamic shadow distance based on scale")]
    public bool useScaleInfluence = false;
    
    [Tooltip("How much scale affects shadow distance")]
    public float scaleInfluence = 1f;
    
    [Tooltip("Transform to track for scale (optional)")]
    public Transform scaleReference;
    
    [Header("Debug")]
    [SerializeField] private Vector2 currentShadowOffset;
    [SerializeField] private float currentEffectiveIntensity;
    
    private Graphic _graphic;
    private Canvas _canvas;
    private Material _materialInstance;
    private float _lastScale;
    
    private static readonly int ShadowColorID = Shader.PropertyToID("_ShadowColor");
    
    void Awake()
    {
        _graphic = GetComponent<Graphic>();
    }
    
    void Start()
    {
        CreateMaterialInstance();
        _canvas = GetComponentInParent<Canvas>();
        _lastScale = GetCurrentScale();
    }
    
    void OnEnable()
    {
        if (_graphic != null)
            _graphic.SetVerticesDirty();
    }
    
    void OnDisable()
    {
        if (_graphic != null)
            _graphic.SetVerticesDirty();
    }
    
    void OnDestroy()
    {
        if (_materialInstance != null)
        {
            if (Application.isPlaying)
                Destroy(_materialInstance);
            else
                DestroyImmediate(_materialInstance);
        }
    }
    
    void CreateMaterialInstance()
    {
        if (_graphic == null) return;
        
        if (_graphic.material != null && _graphic.material.shader.name == "RoyalLeech/UI/ImageShadow")
        {
            _materialInstance = new Material(_graphic.material);
            _graphic.material = _materialInstance;
        }
    }
    
    void LateUpdate()
    {
        // Update shadow color in material
        if (_materialInstance != null)
        {
            _materialInstance.SetColor(ShadowColorID, shadowColor);
        }
        
        // Calculate effective intensity with scale influence
        currentEffectiveIntensity = intensity;
        if (useScaleInfluence)
        {
            float currentScale = GetCurrentScale();
            float scaleDelta = currentScale - 1f;
            currentEffectiveIntensity = intensity * (1f + scaleDelta * scaleInfluence);
            
            // Force mesh rebuild if scale changed
            if (Mathf.Abs(currentScale - _lastScale) > 0.001f)
            {
                _lastScale = currentScale;
                if (_graphic != null)
                    _graphic.SetVerticesDirty();
            }
        }
        
        Vector2 newOffset = CalculateShadowDirection() * currentEffectiveIntensity;
        
        // Rebuild mesh if offset changed significantly
        if (Vector2.Distance(newOffset, currentShadowOffset) > 0.1f)
        {
            currentShadowOffset = newOffset;
            if (_graphic != null)
                _graphic.SetVerticesDirty();
        }
    }
    
    float GetCurrentScale()
    {
        Transform refTransform = scaleReference != null ? scaleReference : transform;
        return (refTransform.localScale.x + refTransform.localScale.y) * 0.5f;
    }
    
    Vector2 CalculateShadowDirection()
    {
        if (ShadowLightSource.Instance == null)
            return new Vector2(1, -1).normalized;
        
        Vector2 screenPos = GetScreenPosition();
        return ShadowLightSource.Instance.GetShadowDirection(screenPos);
    }
    
    Vector2 GetScreenPosition()
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        
        Camera cam = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = _canvas.worldCamera;
        if (cam == null) cam = Camera.main;
        
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, transform.position);
        screenPos -= new Vector2(Screen.width / 2f, Screen.height / 2f);
        
        return screenPos;
    }
    
    // IMeshModifier implementation
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
        int originalIndexCount = vh.currentIndexCount;
        
        if (originalVertCount == 0)
            return;
        
        // Get original vertices
        List<UIVertex> originalVerts = new List<UIVertex>();
        for (int i = 0; i < originalVertCount; i++)
        {
            UIVertex v = new UIVertex();
            vh.PopulateUIVertex(ref v, i);
            originalVerts.Add(v);
        }
        
        // Get original triangles (indices)
        List<int> originalIndices = new List<int>();
        for (int i = 0; i < originalIndexCount; i++)
        {
            // VertexHelper stores triangles internally, we need to extract them
            // by reading the indices in groups of 3
        }
        
        // For UI meshes, we need to reconstruct triangles
        // Unity UI typically uses quads (4 verts, 6 indices per quad)
        // But Sliced/Tiled can have more complex geometry
        
        // Convert offset to local space
        Vector3 offset = new Vector3(currentShadowOffset.x, currentShadowOffset.y, 0);
        
        if (_canvas != null)
        {
            float scale = _canvas.scaleFactor;
            offset /= scale;
        }
        
        offset = transform.InverseTransformVector(offset);
        
        // We need to duplicate the entire mesh structure
        // Read all triangles by iterating through index buffer
        List<int> triangles = new List<int>();
        
        // Since we can't directly access indices, we'll reconstruct based on vertex count
        // Standard UI: quads with indices 0,1,2,2,3,0 pattern
        // For complex meshes (Sliced), there are multiple quads
        
        // Calculate number of quads
        int quadCount = originalVertCount / 4;
        for (int q = 0; q < quadCount; q++)
        {
            int baseIdx = q * 4;
            triangles.Add(baseIdx);
            triangles.Add(baseIdx + 1);
            triangles.Add(baseIdx + 2);
            triangles.Add(baseIdx + 2);
            triangles.Add(baseIdx + 3);
            triangles.Add(baseIdx);
        }
        
        // Handle remaining vertices (non-quad geometry)
        int remaining = originalVertCount % 4;
        if (remaining == 3)
        {
            int baseIdx = quadCount * 4;
            triangles.Add(baseIdx);
            triangles.Add(baseIdx + 1);
            triangles.Add(baseIdx + 2);
        }
        
        // Clear and rebuild mesh
        vh.Clear();
        
        // 1. Add shadow vertices (rendered first = behind)
        for (int i = 0; i < originalVerts.Count; i++)
        {
            UIVertex shadowVert = originalVerts[i];
            shadowVert.position += offset;
            shadowVert.uv1 = new Vector4(1, 0, 0, 0); // Shadow flag
            vh.AddVert(shadowVert);
        }
        
        // Add shadow triangles
        for (int i = 0; i < triangles.Count; i += 3)
        {
            vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
        
        // 2. Add main vertices (rendered second = on top)
        int mainBaseIdx = vh.currentVertCount;
        for (int i = 0; i < originalVerts.Count; i++)
        {
            UIVertex mainVert = originalVerts[i];
            mainVert.uv1 = new Vector4(0, 0, 0, 0); // Main flag
            vh.AddVert(mainVert);
        }
        
        // Add main triangles (offset by mainBaseIdx)
        for (int i = 0; i < triangles.Count; i += 3)
        {
            vh.AddTriangle(
                mainBaseIdx + triangles[i],
                mainBaseIdx + triangles[i + 1],
                mainBaseIdx + triangles[i + 2]
            );
        }
    }
    
    void OnValidate()
    {
        if (_graphic != null)
            _graphic.SetVerticesDirty();
            
        if (_materialInstance != null)
            _materialInstance.SetColor(ShadowColorID, shadowColor);
    }
}
