using UnityEngine;
using UnityEngine.UI;

namespace RoyalLeech.UI
{
    /// <summary>
    /// Controls the CardTear shader - procedural torn edges with angular, sharp tears.
    /// Attach to UI Image with CardTear material.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class CardTearEffect : MonoBehaviour
    {
        [Header("Tear Settings")]
        [Range(0f, 1f)]
        [Tooltip("Overall intensity of tears")]
        public float tearAmount = 0.3f;
        
        [Range(1f, 20f)]
        [Tooltip("Sharpness of tear edges")]
        public float tearSharpness = 10f;
        
        [Tooltip("Unique seed for this card's tear pattern")]
        public float tearSeed = 0f;
        
        [Header("Edge Noise")]
        [Tooltip("Scale of angular noise pattern")]
        public float edgeNoiseScale = 5f;
        
        [Range(0f, 0.2f)]
        [Tooltip("Amount of edge noise variation")]
        public float edgeNoiseAmount = 0.05f;
        
        [Header("Vertex Displacement")]
        [Range(0f, 0.1f)]
        [Tooltip("How much vertices jitter at edges")]
        public float vertexJitter = 0.02f;
        
        [Header("Animation")]
        [Tooltip("Frames per second for tear animation (1 = changes every second)")]
        public float framesPerSecond = 1f;
        
        [Header("Per-Edge Tear Bias")]
        [Tooltip("Use this to make different suits have different tear patterns")]
        [Range(0f, 1f)] public float topTear = 0.5f;
        [Range(0f, 1f)] public float bottomTear = 0.5f;
        [Range(0f, 1f)] public float leftTear = 0.5f;
        [Range(0f, 1f)] public float rightTear = 0.5f;
        
        [Header("Suit Presets")]
        [Tooltip("Apply a preset tear pattern based on card suit")]
        public CardSuit suitPreset = CardSuit.None;
        
        public enum CardSuit
        {
            None,
            Spades,   // ♠ Black - Left tears
            Clubs,    // ♣ Black - Left tears  
            Hearts,   // ♥ Red - Right tears
            Diamonds, // ♦ Red - Right tears
            Trump     // Козырь - All edges
        }
        
        private Image _image;
        private Material _materialInstance;
        
        // Shader property IDs
        private static readonly int PropTearAmount = Shader.PropertyToID("_TearAmount");
        private static readonly int PropTearSharpness = Shader.PropertyToID("_TearSharpness");
        private static readonly int PropTearSeed = Shader.PropertyToID("_TearSeed");
        private static readonly int PropEdgeNoiseScale = Shader.PropertyToID("_EdgeNoiseScale");
        private static readonly int PropEdgeNoiseAmount = Shader.PropertyToID("_EdgeNoiseAmount");
        private static readonly int PropVertexJitter = Shader.PropertyToID("_VertexJitter");
        private static readonly int PropAnimSpeed = Shader.PropertyToID("_AnimSpeed");
        private static readonly int PropTopTear = Shader.PropertyToID("_TopTear");
        private static readonly int PropBottomTear = Shader.PropertyToID("_BottomTear");
        private static readonly int PropLeftTear = Shader.PropertyToID("_LeftTear");
        private static readonly int PropRightTear = Shader.PropertyToID("_RightTear");
        
        private void Awake()
        {
            InitializeMaterial();
        }
        
        private void OnEnable()
        {
            InitializeMaterial();
        }
        
        private void InitializeMaterial()
        {
            _image = GetComponent<Image>();
            
            if (_image != null && _image.material != null)
            {
                // Create instance to avoid modifying shared material
                _materialInstance = new Material(_image.material);
                _image.material = _materialInstance;
            }
        }
        
        private void Update()
        {
            if (_materialInstance == null) return;
            
            // Apply suit preset if set
            ApplySuitPreset();
            
            // Update shader properties
            _materialInstance.SetFloat(PropTearAmount, tearAmount);
            _materialInstance.SetFloat(PropTearSharpness, tearSharpness);
            _materialInstance.SetFloat(PropTearSeed, tearSeed);
            _materialInstance.SetFloat(PropEdgeNoiseScale, edgeNoiseScale);
            _materialInstance.SetFloat(PropEdgeNoiseAmount, edgeNoiseAmount);
            _materialInstance.SetFloat(PropVertexJitter, vertexJitter);
            _materialInstance.SetFloat(PropAnimSpeed, framesPerSecond);
            _materialInstance.SetFloat(PropTopTear, topTear);
            _materialInstance.SetFloat(PropBottomTear, bottomTear);
            _materialInstance.SetFloat(PropLeftTear, leftTear);
            _materialInstance.SetFloat(PropRightTear, rightTear);
        }
        
        private void ApplySuitPreset()
        {
            switch (suitPreset)
            {
                case CardSuit.Spades:
                case CardSuit.Clubs:
                    // Black suits - tears on left side (Клинки и Сеятели)
                    topTear = 0.3f;
                    bottomTear = 0.2f;
                    leftTear = 0.8f;
                    rightTear = 0.1f;
                    break;
                    
                case CardSuit.Hearts:
                case CardSuit.Diamonds:
                    // Red suits - tears on right side (Дворянство и Златые)
                    topTear = 0.2f;
                    bottomTear = 0.3f;
                    leftTear = 0.1f;
                    rightTear = 0.8f;
                    break;
                    
                case CardSuit.Trump:
                    // Trumps - dramatic all-around tearing
                    topTear = 0.7f;
                    bottomTear = 0.7f;
                    leftTear = 0.6f;
                    rightTear = 0.6f;
                    break;
                    
                case CardSuit.None:
                default:
                    // Manual - don't override
                    break;
            }
        }
        
        /// <summary>
        /// Randomize the tear seed for a new pattern
        /// </summary>
        public void RandomizeSeed()
        {
            tearSeed = Random.Range(0f, 10000f);
        }
        
        /// <summary>
        /// Set the card suit, which affects tear pattern
        /// </summary>
        public void SetSuit(CardSuit suit)
        {
            suitPreset = suit;
        }
        
        private void OnDestroy()
        {
            if (_materialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(_materialInstance);
                else
                    DestroyImmediate(_materialInstance);
            }
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_materialInstance != null)
            {
                Update();
            }
        }
#endif
    }
}
