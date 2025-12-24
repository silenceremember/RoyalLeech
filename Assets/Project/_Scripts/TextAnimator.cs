// TextAnimator.cs
// Royal Leech Ultimate Text Animation Constructor
// Минимальный компонент - только ссылка на preset + runtime state

using UnityEngine;
using UnityEngine.Events;
using TMPro;
using System.Collections;

/// <summary>
/// Состояния отдельной буквы.
/// </summary>
public enum LetterState
{
    Hidden,
    Appearing,
    Idle,
    Active,
    Selected,
    Disappearing
}

/// <summary>
/// Данные состояния одной буквы.
/// </summary>
[System.Serializable]
public struct LetterData
{
    public LetterState state;
    public float stateTime;
    public float currentScale;
    public float currentAlpha;
    public Vector2 currentOffset;
    public float currentRotation;
    
    public static LetterData Hidden => new LetterData
    {
        state = LetterState.Hidden,
        stateTime = 0f,
        currentScale = 0f,
        currentAlpha = 0f,
        currentOffset = Vector2.zero,
        currentRotation = 0f
    };
}

/// <summary>
/// Компонент для per-letter анимации текста.
/// ВСЕ настройки берутся из TextAnimatorPreset.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextAnimator : MonoBehaviour
{
    [Header("Preset")]
    [Tooltip("Пресет со всеми настройками и эффектами")]
    public TextAnimatorPreset preset;
    
    [Header("Runtime Progress")]
    [Range(0f, 1f)]
    public float progress = 0f;
    
    [Header("Events")]
    public UnityEvent<char> OnCharacterShown;
    public UnityEvent OnTextComplete;
    
    // Components
    private TMP_Text _textComponent;
    private string _textToAnimate;
    private Coroutine _typewriterCoroutine;
    private bool _isAnimating;
    
    // Per-letter data
    private LetterData[] _letterData;
    private float _animationTime = 0f;
    
    // Distance-based
    private float _currentVisibleCharsFloat = 0f;
    private int _targetCharCount = 0;
    private int _lastVisibleChars = 0;
    
    // Mesh cache
    private TMP_MeshInfo[] _cachedMeshInfo;
    
    // Properties
    public bool IsAnimating => _isAnimating;
    public string CurrentText => _textToAnimate;
    public int VisibleCharacters => Mathf.FloorToInt(_currentVisibleCharsFloat);
    
    // Helpers to get settings from preset
    private AnimationMode Mode => preset != null ? preset.mode : AnimationMode.TimeBasedTypewriter;
    private bool EnableEffects => preset != null && preset.enableEffects;
    private float CharactersPerSecond => preset != null ? preset.charactersPerSecond : 30f;
    private float Delay => preset != null ? preset.delay : 0f;
    private bool StartOnEnable => preset != null && preset.startOnEnable;
    private float InterpolationSpeed => preset != null ? preset.interpolationSpeed : 15f;
    private float AppearDuration => preset != null ? preset.appearDuration : 0.25f;
    private float AppearStagger => preset != null ? preset.appearStagger : 0.03f;
    private float DisappearDuration => preset != null ? preset.disappearDuration : 0.15f;
    private float EffectSmoothSpeed => preset != null ? preset.effectSmoothSpeed : 15f;

    void Awake()
    {
        _textComponent = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        if (Mode == AnimationMode.TimeBasedTypewriter && StartOnEnable && !string.IsNullOrEmpty(_textToAnimate))
        {
            StartWriter();
        }
    }

    void OnDisable()
    {
        if (Mode == AnimationMode.TimeBasedTypewriter)
        {
            StopWriter();
        }
    }

    void Update()
    {
        _animationTime += Time.deltaTime;
        
        if (Mode == AnimationMode.DistanceBased)
        {
            UpdateDistanceBasedAnimation();
        }
        
        if (EnableEffects && _letterData != null)
        {
            UpdateLetterEffects();
            ApplyMeshChanges();
        }
    }

    void LateUpdate()
    {
        if (EnableEffects && _letterData != null && _letterData.Length > 0)
        {
            ApplyMeshChanges();
        }
    }

    #region Public API

    public void SetText(string text)
    {
        _textToAnimate = text;
        
        int charCount = string.IsNullOrEmpty(text) ? 0 : text.Length;
        _letterData = new LetterData[charCount];
        for (int i = 0; i < charCount; i++)
        {
            _letterData[i] = LetterData.Hidden;
        }
        
        _textComponent.text = text;
        _textComponent.maxVisibleCharacters = charCount;
        _textComponent.ForceMeshUpdate();
        
        CacheMeshInfo();
        
        if (Mode == AnimationMode.TimeBasedTypewriter)
        {
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
                _isAnimating = false;
            }
            _textComponent.maxVisibleCharacters = 0;
        }
        else
        {
            _currentVisibleCharsFloat = 0f;
            _targetCharCount = 0;
        }
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
    }
    
    public void SetIntensity(float value) => SetProgress(value);

    public void SetTargetCharacterCount(int targetCount)
    {
        if (Mode != AnimationMode.DistanceBased) return;
        _targetCharCount = Mathf.Clamp(targetCount, 0, _textToAnimate?.Length ?? 0);
    }

    public void ResetProgress()
    {
        _currentVisibleCharsFloat = 0f;
        _targetCharCount = 0;
        _lastVisibleChars = 0;
        progress = 0f;
        
        if (_letterData != null)
        {
            for (int i = 0; i < _letterData.Length; i++)
            {
                _letterData[i] = LetterData.Hidden;
            }
        }
        
        if (_textComponent != null)
        {
            _textComponent.maxVisibleCharacters = 0;
        }
    }

    public void SetTextAndStart(string text)
    {
        SetText(text);
        if (Mode == AnimationMode.TimeBasedTypewriter)
        {
            StartWriter();
        }
    }

    #endregion

    #region Time-Based Typewriter

    public void StartWriter()
    {
        if (Mode != AnimationMode.TimeBasedTypewriter) return;
        if (string.IsNullOrEmpty(_textToAnimate)) return;

        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
        }

        _typewriterCoroutine = StartCoroutine(TypewriterCoroutine());
    }

    public void StopWriter()
    {
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
            _isAnimating = false;
        }
    }

    public void SkipToEnd()
    {
        StopWriter();
        if (_letterData != null)
        {
            for (int i = 0; i < _letterData.Length; i++)
            {
                _letterData[i].state = LetterState.Idle;
                _letterData[i].currentAlpha = 1f;
                _letterData[i].currentScale = 1f;
            }
            _textComponent.maxVisibleCharacters = _textToAnimate.Length;
            OnTextComplete?.Invoke();
        }
    }

    private IEnumerator TypewriterCoroutine()
    {
        _isAnimating = true;
        
        if (Delay > 0) yield return new WaitForSeconds(Delay);

        _textComponent.ForceMeshUpdate();
        int totalCharacters = _textComponent.textInfo.characterCount;
        
        for (int i = 0; i < totalCharacters; i++)
        {
            if (i < _letterData.Length)
            {
                _letterData[i].state = LetterState.Appearing;
                _letterData[i].stateTime = 0f;
                
                OnCharacterShown?.Invoke(_textComponent.textInfo.characterInfo[i].character);
            }
            
            _textComponent.maxVisibleCharacters = i + 1;
            
            yield return new WaitForSeconds(AppearStagger);
        }
        
        yield return new WaitForSeconds(AppearDuration);
        
        _isAnimating = false;
        _typewriterCoroutine = null;
        OnTextComplete?.Invoke();
    }

    #endregion

    #region Distance-Based

    private void UpdateDistanceBasedAnimation()
    {
        if (string.IsNullOrEmpty(_textToAnimate) || _letterData == null) return;

        int totalChars = _textToAnimate.Length;
        
        _currentVisibleCharsFloat = Mathf.MoveTowards(
            _currentVisibleCharsFloat,
            _targetCharCount,
            Time.deltaTime * InterpolationSpeed
        );
        
        int visibleChars = Mathf.Clamp(Mathf.FloorToInt(_currentVisibleCharsFloat), 0, totalChars);
        
        for (int i = 0; i < _letterData.Length; i++)
        {
            if (i < visibleChars)
            {
                if (_letterData[i].state == LetterState.Hidden || _letterData[i].state == LetterState.Disappearing)
                {
                    _letterData[i].state = LetterState.Appearing;
                    _letterData[i].stateTime = 0f;
                    
                    if (i > _lastVisibleChars - 1)
                    {
                        OnCharacterShown?.Invoke(_textToAnimate[i]);
                    }
                }
            }
            else
            {
                if (_letterData[i].state != LetterState.Hidden && _letterData[i].state != LetterState.Disappearing)
                {
                    _letterData[i].state = LetterState.Disappearing;
                    _letterData[i].stateTime = 0f;
                }
            }
        }
        
        _lastVisibleChars = visibleChars;
        _textComponent.maxVisibleCharacters = totalChars;
        
        if (visibleChars >= totalChars && _targetCharCount >= totalChars)
        {
            OnTextComplete?.Invoke();
        }
    }

    #endregion

    #region Per-Letter Effects

    private void CacheMeshInfo()
    {
        if (_textComponent == null) return;
        
        _textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = _textComponent.textInfo;
        
        if (textInfo?.meshInfo == null) return;
        
        _cachedMeshInfo = new TMP_MeshInfo[textInfo.meshInfo.Length];
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            _cachedMeshInfo[i].vertices = (Vector3[])textInfo.meshInfo[i].vertices.Clone();
            _cachedMeshInfo[i].colors32 = (Color32[])textInfo.meshInfo[i].colors32.Clone();
        }
    }

    private void UpdateLetterEffects()
    {
        if (_letterData == null || preset == null) return;
        
        float dt = Time.deltaTime;
        
        for (int i = 0; i < _letterData.Length; i++)
        {
            ref LetterData letter = ref _letterData[i];
            letter.stateTime += dt;
            
            EffectResult result = EffectResult.Identity;
            
            switch (letter.state)
            {
                case LetterState.Hidden:
                    result.alpha = 0f;
                    result.scale = 0f;
                    break;
                    
                case LetterState.Appearing:
                    float appearT = Mathf.Clamp01(letter.stateTime / AppearDuration);
                    result = preset.CalculateAppear(_animationTime, i, appearT);
                    
                    if (appearT >= 1f)
                    {
                        letter.state = progress > 0.01f ? LetterState.Active : LetterState.Idle;
                        letter.stateTime = 0f;
                    }
                    break;
                    
                case LetterState.Idle:
                case LetterState.Active:
                case LetterState.Selected:
                    letter.state = progress >= 1f ? LetterState.Selected : 
                                   (progress > 0.01f ? LetterState.Active : LetterState.Idle);
                    result = preset.CalculateIdle(_animationTime, i, progress);
                    break;
                    
                case LetterState.Disappearing:
                    float disappearT = Mathf.Clamp01(letter.stateTime / DisappearDuration);
                    result = preset.CalculateDisappear(_animationTime, i, disappearT);
                    
                    if (disappearT >= 1f)
                    {
                        letter.state = LetterState.Hidden;
                        letter.stateTime = 0f;
                    }
                    break;
            }
            
            // Interpolation
            letter.currentScale = Mathf.MoveTowards(letter.currentScale, result.scale, dt * EffectSmoothSpeed);
            letter.currentAlpha = Mathf.MoveTowards(letter.currentAlpha, result.alpha, dt * EffectSmoothSpeed);
            letter.currentOffset = Vector2.MoveTowards(letter.currentOffset, result.offset, dt * EffectSmoothSpeed * 10f);
            letter.currentRotation = Mathf.MoveTowards(letter.currentRotation, result.rotation, dt * EffectSmoothSpeed);
        }
    }

    private void ApplyMeshChanges()
    {
        if (_textComponent == null || _letterData == null || _cachedMeshInfo == null) return;
        
        _textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = _textComponent.textInfo;
        
        if (textInfo == null || textInfo.characterCount == 0) return;
        
        for (int i = 0; i < textInfo.characterCount && i < _letterData.Length; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;
            
            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            
            if (materialIndex >= textInfo.meshInfo.Length || materialIndex >= _cachedMeshInfo.Length) continue;
            
            Vector3[] sourceVertices = _cachedMeshInfo[materialIndex].vertices;
            Vector3[] destVertices = textInfo.meshInfo[materialIndex].vertices;
            Color32[] destColors = textInfo.meshInfo[materialIndex].colors32;
            
            if (vertexIndex + 3 >= destVertices.Length || vertexIndex + 3 >= sourceVertices.Length) continue;
            
            LetterData letter = _letterData[i];
            
            Vector3 center = (sourceVertices[vertexIndex] + sourceVertices[vertexIndex + 1] + 
                             sourceVertices[vertexIndex + 2] + sourceVertices[vertexIndex + 3]) * 0.25f;
            
            float scale = letter.currentScale;
            float rotation = letter.currentRotation * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(letter.currentOffset.x, letter.currentOffset.y, 0f);
            
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            
            for (int j = 0; j < 4; j++)
            {
                Vector3 vertex = sourceVertices[vertexIndex + j] - center;
                vertex *= scale;
                
                float rotX = vertex.x * cos - vertex.y * sin;
                float rotY = vertex.x * sin + vertex.y * cos;
                vertex.x = rotX;
                vertex.y = rotY;
                
                destVertices[vertexIndex + j] = vertex + center + offset;
            }
            
            byte alpha = (byte)(letter.currentAlpha * 255f);
            for (int j = 0; j < 4; j++)
            {
                Color32 c = destColors[vertexIndex + j];
                c.a = alpha;
                destColors[vertexIndex + j] = c;
            }
        }
        
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textInfo.meshInfo[i].mesh.colors32 = textInfo.meshInfo[i].colors32;
            _textComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }

    #endregion
}
