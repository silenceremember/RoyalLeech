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
    public LetterState previousState;
    public float stateTime;
    public float transitionProgress; // 0 = still blending from previous, 1 = fully in new state
    public float currentScale;
    public float currentAlpha;
    public Vector2 currentOffset;
    public float currentRotation;
    
    // Сохранённые значения из предыдущего состояния для кроссфейда
    public float prevScale;
    public float prevAlpha;
    public Vector2 prevOffset;
    public float prevRotation;
    
    public static LetterData Hidden => new LetterData
    {
        state = LetterState.Hidden,
        previousState = LetterState.Hidden,
        stateTime = 0f,
        transitionProgress = 1f,
        currentScale = 0f,
        currentAlpha = 0f,
        currentOffset = Vector2.zero,
        currentRotation = 0f,
        prevScale = 0f,
        prevAlpha = 0f,
        prevOffset = Vector2.zero,
        prevRotation = 0f
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
    private float StateTransitionDuration => preset != null ? preset.stateTransitionDuration : 0.15f;

    void Awake()
    {
        _textComponent = GetComponent<TMP_Text>();
        
        // CRITICAL: Инициализируем _textToAnimate пустой строкой
        // Это гарантирует что CurrentText вернет "", а не null
        // И Shadow будет знать что текст ещё не установлен через SetText
        _textToAnimate = "";
        
        // Если в TMP есть заранее установленный текст в Inspector (placeholder),
        // мы должны скрыть его до вызова SetText
        if (_textComponent != null && !string.IsNullOrEmpty(_textComponent.text))
        {
            // Устанавливаем alpha=0 для всего mesh чтобы предотвратить отображение placeholder текста
            _textComponent.ForceMeshUpdate();
            TMP_TextInfo textInfo = _textComponent.textInfo;
            if (textInfo != null)
            {
                for (int m = 0; m < textInfo.meshInfo.Length; m++)
                {
                    Color32[] colors = textInfo.meshInfo[m].colors32;
                    if (colors != null)
                    {
                        for (int c = 0; c < colors.Length; c++)
                        {
                            colors[c].a = 0;
                        }
                    }
                }
                _textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
        }
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
        
        // CRITICAL: Сразу применяем alpha=0 ко всему тексту, чтобы избежать 1-кадрового "вспыхивания"
        // когда Shadow считывает mesh до того как ApplyMeshChanges() отработает
        if (charCount > 0)
        {
            TMP_TextInfo textInfo = _textComponent.textInfo;
            if (textInfo != null)
            {
                for (int m = 0; m < textInfo.meshInfo.Length; m++)
                {
                    Color32[] colors = textInfo.meshInfo[m].colors32;
                    if (colors != null)
                    {
                        for (int c = 0; c < colors.Length; c++)
                        {
                            colors[c].a = 0;
                        }
                    }
                }
                _textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
        }
        
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
        float transitionSpeed = StateTransitionDuration > 0.001f ? 1f / StateTransitionDuration : 100f;
        
        for (int i = 0; i < _letterData.Length; i++)
        {
            ref LetterData letter = ref _letterData[i];
            letter.stateTime += dt;
            
            // Обновляем прогресс transition
            if (letter.transitionProgress < 1f)
            {
                letter.transitionProgress = Mathf.MoveTowards(letter.transitionProgress, 1f, dt * transitionSpeed);
            }
            
            EffectResult result = EffectResult.Identity;
            LetterState newState = letter.state;
            
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
                        newState = progress > 0.01f ? LetterState.Active : LetterState.Idle;
                    }
                    break;
                    
                case LetterState.Idle:
                case LetterState.Active:
                case LetterState.Selected:
                    LetterState targetState = progress >= 1f ? LetterState.Selected : 
                                   (progress > 0.01f ? LetterState.Active : LetterState.Idle);
                    newState = targetState;
                    result = preset.CalculateIdle(_animationTime, i, progress);
                    break;
                    
                case LetterState.Disappearing:
                    float disappearT = Mathf.Clamp01(letter.stateTime / DisappearDuration);
                    result = preset.CalculateDisappear(_animationTime, i, disappearT);
                    
                    if (disappearT >= 1f)
                    {
                        newState = LetterState.Hidden;
                    }
                    break;
            }
            
            // Проверяем смену состояния (кроме переходов между Idle/Active/Selected - они плавные через progress)
            bool isIdleGroup = letter.state == LetterState.Idle || letter.state == LetterState.Active || letter.state == LetterState.Selected;
            bool newIsIdleGroup = newState == LetterState.Idle || newState == LetterState.Active || newState == LetterState.Selected;
            
            if (newState != letter.state && !(isIdleGroup && newIsIdleGroup))
            {
                // Сохраняем текущие значения для кроссфейда
                letter.prevScale = letter.currentScale;
                letter.prevAlpha = letter.currentAlpha;
                letter.prevOffset = letter.currentOffset;
                letter.prevRotation = letter.currentRotation;
                letter.previousState = letter.state;
                letter.transitionProgress = 0f;
                letter.stateTime = 0f;
            }
            
            letter.state = newState;
            
            // Применяем кроссфейд
            float t = letter.transitionProgress;
            float smoothT = t * t * (3f - 2f * t); // Smooth step для более плавного перехода
            
            // Целевые значения с интерполяцией через EffectSmoothSpeed
            float targetScale = Mathf.Lerp(letter.prevScale, result.scale, smoothT);
            float targetAlpha = Mathf.Lerp(letter.prevAlpha, result.alpha, smoothT);
            Vector2 targetOffset = Vector2.Lerp(letter.prevOffset, result.offset, smoothT);
            float targetRotation = Mathf.Lerp(letter.prevRotation, result.rotation, smoothT);
            
            // Дополнительная интерполяция для сглаживания резких изменений в самих эффектах
            letter.currentScale = Mathf.Lerp(letter.currentScale, targetScale, dt * EffectSmoothSpeed);
            letter.currentAlpha = Mathf.Lerp(letter.currentAlpha, targetAlpha, dt * EffectSmoothSpeed);
            letter.currentOffset = Vector2.Lerp(letter.currentOffset, targetOffset, dt * EffectSmoothSpeed);
            letter.currentRotation = Mathf.Lerp(letter.currentRotation, targetRotation, dt * EffectSmoothSpeed);
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
