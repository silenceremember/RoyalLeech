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
    DisappearingNormal,   // Обычное пропадание при свайпе назад
    DisappearingReturn,   // Быстрое пропадание при возврате к центру
    DisappearingSelected, // Пропадание после выбора
    Exploding             // Flying to resource icon
}

/// <summary>
/// Режим пропадания (для внешнего вызова).
/// </summary>
public enum DisappearMode
{
    Normal,   // При свайпе назад
    Return,   // При возврате к центру
    Selected  // После выбора
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
    
    // Explosion flight data
    public Vector2 startWorldPos;       // Where letter started
    public Vector2 targetWorldPos;      // Target icon position
    public Vector2 controlPoint;        // Bezier control point
    public float flightDuration;        // Individual flight duration
    public float flightDelay;           // Stagger delay
    public float rotationDirection;     // +1 or -1
    public int targetIconIndex;         // 0=Spades, 1=Hearts, 2=Diamonds, 3=Clubs
    public bool arrivalCallbackFired;   // Prevent double-firing
    
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
        prevRotation = 0f,
        arrivalCallbackFired = false
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
    private float EffectSmoothSpeed => preset != null ? preset.effectSmoothSpeed : 15f;
    private float StateTransitionDuration => preset != null ? preset.stateTransitionDuration : 0.15f;
    
    // Current disappear mode for active disappearing
    private DisappearMode _currentDisappearMode = DisappearMode.Normal;
    
    // Flag to block distance-based updates during fast disappear (Return/Selected)
    private bool _isFastDisappearing = false;
    
    // Flag for explosion in progress
    private bool _isExploding = false;
    
    // Time when explosion animation should be fully complete
    private float _explosionEndTime = 0f;
    
    // Explosion completion callback
    private System.Action _onExplosionComplete;
    
    // Public property for external check
    public bool IsFastDisappearing => _isFastDisappearing;
    public bool IsExploding => _isExploding || Time.time < _explosionEndTime;
    
    /// <summary>
    /// Event fired when explosion animation completes (all letters reached destination)
    /// </summary>
    public event System.Action OnExplosionComplete;

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
        // Если идёт взрыв - игнорируем ЛЮБЫЕ изменения текста
        // Текст очистится сам когда все буквы станут Hidden
        if (_isExploding)
        {
            return;
        }
        
        // Сбрасываем флаги при установке нового текста
        _isFastDisappearing = false;
        _isExploding = false;
        
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
    
    // Global alpha multiplier for the entire text (for fade effects)
    private float _globalAlpha = 1f;
    
    public void SetGlobalAlpha(float alpha)
    {
        _globalAlpha = Mathf.Clamp01(alpha);
    }
    
    public void SetIntensity(float value) => SetProgress(value);

    public void SetTargetCharacterCount(int targetCount)
    {
        if (Mode != AnimationMode.DistanceBased) return;
        _targetCharCount = Mathf.Clamp(targetCount, 0, _textToAnimate?.Length ?? 0);
    }

    public void ResetProgress()
    {
        // Не сбрасываем во время взрыва - анимация должна доиграть
        if (_isExploding)
        {
            return;
        }
        
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
    
    /// <summary>
    /// Запустить пропадание всех видимых букв с указанным режимом.
    /// </summary>
    public void TriggerDisappear(DisappearMode mode)
    {
        if (_letterData == null) return;
        
        _currentDisappearMode = mode;
        LetterState targetState = GetDisappearStateFromMode(mode);
        
        // Для Return и Selected блокируем distance-based обновления
        _isFastDisappearing = (mode == DisappearMode.Return || mode == DisappearMode.Selected);
        
        // Сбрасываем explosion состояние для обычного пропадания
        _isExploding = false;
        
        // Устанавливаем время окончания анимации для Return/Selected
        // чтобы флаги не сбрасывались до завершения disappear
        if (_isFastDisappearing && preset != null)
        {
            float disappearDuration = preset.GetDisappearDuration(mode);
            _explosionEndTime = Time.time + disappearDuration + 0.05f; // небольшой буфер
        }
        else
        {
            _explosionEndTime = 0f;
        }
        
        for (int i = 0; i < _letterData.Length; i++)
        {
            // Принудительно переключаем ВСЕ видимые буквы на disappear
            if (_letterData[i].state != LetterState.Hidden)
            {
                // Сохраняем текущие значения для кроссфейда
                _letterData[i].prevScale = _letterData[i].currentScale;
                _letterData[i].prevAlpha = _letterData[i].currentAlpha;
                _letterData[i].prevOffset = _letterData[i].currentOffset;
                _letterData[i].prevRotation = _letterData[i].currentRotation;
                _letterData[i].previousState = _letterData[i].state;
                _letterData[i].transitionProgress = 0f;
                
                _letterData[i].state = targetState;
                _letterData[i].stateTime = 0f;
            }
        }
        
        // Сбрасываем target чтобы новые буквы не появлялись
        _targetCharCount = 0;
        _currentVisibleCharsFloat = 0f;
    }
    
    /// <summary>
    /// Быстрое пропадание при возврате к центру.
    /// </summary>
    public void TriggerReturnToCenter()
    {
        TriggerDisappear(DisappearMode.Return);
    }
    
    /// <summary>
    /// Пропадание после выбора.
    /// </summary>
    public void TriggerSelected()
    {
        TriggerDisappear(DisappearMode.Selected);
    }
    
    // Helper methods
    private static bool IsDisappearingState(LetterState state)
    {
        return state == LetterState.DisappearingNormal || 
               state == LetterState.DisappearingReturn || 
               state == LetterState.DisappearingSelected;
    }
    
    private static LetterState GetDisappearStateFromMode(DisappearMode mode)
    {
        switch (mode)
        {
            case DisappearMode.Return: return LetterState.DisappearingReturn;
            case DisappearMode.Selected: return LetterState.DisappearingSelected;
            default: return LetterState.DisappearingNormal;
        }
    }
    
    private static DisappearMode GetDisappearModeFromState(LetterState state)
    {
        switch (state)
        {
            case LetterState.DisappearingReturn: return DisappearMode.Return;
            case LetterState.DisappearingSelected: return DisappearMode.Selected;
            default: return DisappearMode.Normal;
        }
    }
    
    // Explosion callback storage
    private System.Action<int> _explosionArrivalCallback;
    
    /// <summary>
    /// Trigger letter explosion effect. Letters fly to resource icons proportionally.
    /// </summary>
    /// <param name="resourceChanges">Array of 4 ints: [spades, hearts, diamonds, clubs]</param>
    /// <param name="iconPositions">World positions of 4 icons</param>
    /// <param name="onLetterArrival">Callback(iconIndex) when each letter arrives</param>
    public void TriggerExplosion(int[] resourceChanges, Vector2[] iconPositions, System.Action<int> onLetterArrival)
    {
        if (_letterData == null || preset?.explosionPreset == null) 
        {
            Debug.LogWarning($"[TextAnimator] TriggerExplosion fallback to Selected: letterData={_letterData != null}, preset={preset != null}, explosionPreset={preset?.explosionPreset != null}");
            TriggerSelected();
            return;
        }
        
        // Check if ANY resource changes
        int totalChange = 0;
        foreach (var c in resourceChanges) totalChange += Mathf.Abs(c);
        
        if (totalChange == 0)
        {
            // No changes - use normal Selected disappear
            TriggerSelected();
            return;
        }
        
        _explosionArrivalCallback = onLetterArrival;
        var expPreset = preset.explosionPreset;
        
        // Distribute letters proportionally to resource changes
        int[] letterAssignments = DistributeLettersToIcons(resourceChanges);
        
        // Get start positions for all letters
        Vector2[] letterWorldPositions = GetLetterWorldPositions();
        
        // Initialize explosion for each letter
        int currentLetterIndex = 0;
        for (int iconIdx = 0; iconIdx < 4; iconIdx++)
        {
            int letterCount = letterAssignments[iconIdx];
            for (int j = 0; j < letterCount; j++)
            {
                if (currentLetterIndex >= _letterData.Length) break;
                
                ref LetterData letter = ref _letterData[currentLetterIndex];
                
                // Skip if letter is hidden or not visible
                if (letter.state == LetterState.Hidden)
                {
                    currentLetterIndex++;
                    continue;
                }
                
                // Save prev state for crossfade
                letter.prevScale = letter.currentScale;
                letter.prevAlpha = letter.currentAlpha;
                letter.prevOffset = letter.currentOffset;
                letter.prevRotation = letter.currentRotation;
                letter.previousState = letter.state;
                
                // Set explosion state
                letter.state = LetterState.Exploding;
                letter.stateTime = 0f;
                letter.transitionProgress = 1f; // No crossfade needed
                letter.arrivalCallbackFired = false;
                
                // Flight data
                letter.startWorldPos = letterWorldPositions[currentLetterIndex];
                letter.targetWorldPos = iconPositions[iconIdx];
                letter.targetIconIndex = iconIdx;
                
                // Bezier control point (arc with side offset for curve)
                Vector2 midPoint = (letter.startWorldPos + letter.targetWorldPos) * 0.5f;
                float arcOffset = expPreset.arcHeight * (1f + Random.Range(-expPreset.arcRandomness, expPreset.arcRandomness));
                float sideOffset = Random.Range(-expPreset.arcSideOffset, expPreset.arcSideOffset);
                letter.controlPoint = midPoint + Vector2.up * arcOffset + Vector2.right * sideOffset;
                
                // Timing - all letters fly simultaneously (no stagger)
                // Randomness ONLY DECREASES duration to ensure all letters finish within flightDuration
                letter.flightDuration = expPreset.flightDuration * (1f - Random.Range(0f, expPreset.flightDurationRandomness));
                letter.flightDelay = 0f; // All letters start at the same time
                
                // Rotation direction
                letter.rotationDirection = expPreset.randomRotationDirection ? (Random.value > 0.5f ? 1f : -1f) : 1f;
                
                currentLetterIndex++;
            }
        }
        
        _isFastDisappearing = true;
        _isExploding = true;
        _targetCharCount = 0;
        _currentVisibleCharsFloat = 0f;
        
        // Рассчитываем время окончания взрыва:
        // All letters now finish within base flightDuration (randomness only decreases)
        int totalExplodingLetters = currentLetterIndex;
        float buffer = 0.05f; // Small buffer for safety
        _explosionEndTime = Time.time + expPreset.flightDuration + buffer;
        
        Debug.Log($"[TextAnimator] Explosion started: {totalExplodingLetters} letters, endTime={_explosionEndTime - Time.time}s from now");
        
        // Убедимся что все символы видимы во время взрыва
        if (_textComponent != null)
        {
            _textComponent.maxVisibleCharacters = _textToAnimate?.Length ?? 99999;
        }
    }
    
    /// <summary>
    /// Distribute letters proportionally to resource changes.
    /// </summary>
    private int[] DistributeLettersToIcons(int[] changes)
    {
        int totalLetters = 0;
        if (_letterData != null)
        {
            foreach (var ld in _letterData)
            {
                if (ld.state != LetterState.Hidden) totalLetters++;
            }
        }
        
        if (totalLetters == 0) return new int[4];
        
        // Calculate total absolute change
        int totalChange = 0;
        foreach (var c in changes) totalChange += Mathf.Abs(c);
        
        if (totalChange == 0) return new int[4];
        
        // Proportional distribution
        int[] result = new int[4];
        int assigned = 0;
        
        for (int i = 0; i < 4; i++)
        {
            float proportion = (float)Mathf.Abs(changes[i]) / totalChange;
            result[i] = Mathf.RoundToInt(proportion * totalLetters);
            assigned += result[i];
        }
        
        // Fix rounding errors - add/remove from largest change
        while (assigned != totalLetters)
        {
            int maxIdx = 0;
            for (int i = 1; i < 4; i++)
                if (Mathf.Abs(changes[i]) > Mathf.Abs(changes[maxIdx])) maxIdx = i;
                
            if (assigned < totalLetters) { result[maxIdx]++; assigned++; }
            else if (result[maxIdx] > 0) { result[maxIdx]--; assigned--; }
            else break; // Safety
        }
        
        return result;
    }
    
    /// <summary>
    /// Get world positions of all visible letters.
    /// </summary>
    private Vector2[] GetLetterWorldPositions()
    {
        Vector2[] positions = new Vector2[_letterData.Length];
        
        _textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = _textComponent.textInfo;
        
        for (int i = 0; i < _letterData.Length && i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) 
            {
                positions[i] = (Vector2)transform.position;
                continue;
            }
            
            // Get character center in world space
            int vertIdx = charInfo.vertexIndex;
            int matIdx = charInfo.materialReferenceIndex;
            Vector3[] verts = textInfo.meshInfo[matIdx].vertices;
            
            if (vertIdx + 3 < verts.Length)
            {
                Vector3 localCenter = (verts[vertIdx] + verts[vertIdx + 1] + verts[vertIdx + 2] + verts[vertIdx + 3]) * 0.25f;
                // Apply current letter offset
                localCenter += new Vector3(_letterData[i].currentOffset.x, _letterData[i].currentOffset.y, 0);
                positions[i] = transform.TransformPoint(localCenter);
            }
            else
            {
                positions[i] = (Vector2)transform.position;
            }
        }
        
        return positions;
    }
    
    /// <summary>
    /// Quadratic Bezier curve interpolation.
    /// </summary>
    private static Vector2 BezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
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
        
        // Блокируем обновление во время быстрого пропадания
        if (_isFastDisappearing)
        {
            // Проверяем, завершилось ли пропадание всех букв
            bool allHidden = true;
            for (int i = 0; i < _letterData.Length; i++)
            {
                if (_letterData[i].state != LetterState.Hidden)
                {
                    allHidden = false;
                    break;
                }
            }
            
            // Не сбрасываем флаги пока не прошло достаточно времени И все буквы не скрыты
            if (allHidden && Time.time >= _explosionEndTime)
            {
                bool wasExploding = _isExploding;
                _isFastDisappearing = false;
                _isExploding = false;
                _explosionEndTime = 0f;
                
                // Теперь можно безопасно очистить текст
                _textToAnimate = "";
                _textComponent.text = "";
                
                Debug.Log($"[TextAnimator] Explosion complete, text cleared");
                
                // Вызываем callback если был взрыв
                if (wasExploding)
                {
                    OnExplosionComplete?.Invoke();
                }
            }
            return;
        }

        int totalChars = _textToAnimate.Length;
        
        _currentVisibleCharsFloat = Mathf.MoveTowards(
            _currentVisibleCharsFloat,
            _targetCharCount,
            Time.deltaTime * InterpolationSpeed
        );
        
        int visibleChars = Mathf.Clamp(Mathf.FloorToInt(_currentVisibleCharsFloat), 0, totalChars);
        
        for (int i = 0; i < _letterData.Length; i++)
        {
            bool isDisappearing = IsDisappearingState(_letterData[i].state);
            
            if (i < visibleChars)
            {
                if (_letterData[i].state == LetterState.Hidden || isDisappearing)
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
                if (_letterData[i].state != LetterState.Hidden && !isDisappearing)
                {
                    // Сохраняем текущие значения для кроссфейда
                    _letterData[i].prevScale = _letterData[i].currentScale;
                    _letterData[i].prevAlpha = _letterData[i].currentAlpha;
                    _letterData[i].prevOffset = _letterData[i].currentOffset;
                    _letterData[i].prevRotation = _letterData[i].currentRotation;
                    _letterData[i].previousState = _letterData[i].state;
                    _letterData[i].transitionProgress = 0f;
                    
                    // При обычном свайпе используем Normal режим
                    _letterData[i].state = LetterState.DisappearingNormal;
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
                    
                case LetterState.DisappearingNormal:
                case LetterState.DisappearingReturn:
                case LetterState.DisappearingSelected:
                    DisappearMode mode = GetDisappearModeFromState(letter.state);
                    float disappearDuration = preset.GetDisappearDuration(mode);
                    float disappearT = Mathf.Clamp01(letter.stateTime / disappearDuration);
                    result = preset.CalculateDisappear(_animationTime, i, disappearT, mode);
                    
                    // ОБЯЗАТЕЛЬНО применяем fade alpha на основе прогресса disappear
                    // Если effects содержит Fade - оно применится через CalculateDisappear
                    // Но мы всё равно умножаем на (1 - disappearT) чтобы гарантировать исчезновение
                    result.alpha *= (1f - disappearT);
                    result.scale *= Mathf.Lerp(1f, 0f, disappearT * disappearT); // scale тоже уменьшается
                    
                    // Для Selected и Return disappear: сохраняем позиции из предыдущего состояния
                    // Буквы должны исчезать "на месте", а не выстраиваться в линию
                    if (letter.state == LetterState.DisappearingSelected || 
                        letter.state == LetterState.DisappearingReturn)
                    {
                        // Добавляем сохранённые смещения к результату disappear
                        result.offset += letter.prevOffset;
                        result.rotation += letter.prevRotation;
                    }
                    
                    if (disappearT >= 1f)
                    {
                        newState = LetterState.Hidden;
                    }
                    break;
                    
                case LetterState.Exploding:
                    if (preset?.explosionPreset != null)
                    {
                        var exp = preset.explosionPreset;
                        
                        // Account for stagger delay
                        float activeTime = letter.stateTime - letter.flightDelay;
                        
                        if (activeTime < 0)
                        {
                            // Still waiting - keep at current position with current values
                            result.scale = letter.currentScale;
                            result.alpha = letter.currentAlpha;
                            result.offset = letter.currentOffset;
                            result.rotation = letter.currentRotation;
                        }
                        else
                        {
                            float flightT = Mathf.Clamp01(activeTime / letter.flightDuration);
                            float curvedT = exp.positionCurve.Evaluate(flightT);
                            
                            // Bezier curve interpolation for world position
                            Vector2 worldPos = BezierPoint(letter.startWorldPos, letter.controlPoint, letter.targetWorldPos, curvedT);
                            
                            // Calculate offset by converting world delta to local delta
                            // worldPos is where the letter should be in world space
                            // startWorldPos is where it started in world space
                            // The delta in world space needs to be converted to local space
                            Vector2 worldDelta = worldPos - letter.startWorldPos;
                            
                            // Convert world delta to local delta (accounting for parent rotation/scale)
                            Vector3 localDelta = transform.InverseTransformVector(new Vector3(worldDelta.x, worldDelta.y, 0));
                            
                            // Add delta to the previous offset (where letter was when explosion started)
                            result.offset = letter.prevOffset + new Vector2(localDelta.x, localDelta.y);
                            
                            // Scale
                            result.scale = Mathf.Lerp(exp.initialScale, exp.finalScale, exp.scaleCurve.Evaluate(flightT));
                            
                            // Alpha
                            result.alpha = exp.alphaCurve.Evaluate(flightT);
                            
                            // Rotation
                            result.rotation = letter.prevRotation + activeTime * exp.rotationSpeed * letter.rotationDirection;
                            
                            // Check arrival and fire callback
                            if (flightT >= 1f)
                            {
                                if (!letter.arrivalCallbackFired)
                                {
                                    letter.arrivalCallbackFired = true;
                                    _explosionArrivalCallback?.Invoke(letter.targetIconIndex);
                                }
                                newState = LetterState.Hidden;
                            }
                        }
                    }
                    else
                    {
                        // No preset - just hide
                        newState = LetterState.Hidden;
                    }
                    break;
            }
            
            // Проверяем смену состояния (кроме переходов между Idle/Active/Selected - они плавные через progress)
            bool isIdleGroup = letter.state == LetterState.Idle || letter.state == LetterState.Active || letter.state == LetterState.Selected;
            bool newIsIdleGroup = newState == LetterState.Idle || newState == LetterState.Active || newState == LetterState.Selected;
            bool isDisappearing = IsDisappearingState(letter.state);
            bool newIsDisappearing = IsDisappearingState(newState);
            
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
            
            // Для быстрых режимов (Return/Selected/Exploding) используем более быструю интерполяцию
            // Для Exploding - прямое применение без сглаживания для точного следования траектории
            bool isExploding = letter.state == LetterState.Exploding;
            bool isFastDisappear = letter.state == LetterState.DisappearingReturn || 
                                   letter.state == LetterState.DisappearingSelected;
            
            if (isExploding)
            {
                // Прямое применение значений для точного следования траектории Bezier
                letter.currentScale = result.scale;
                letter.currentAlpha = result.alpha;
                letter.currentOffset = result.offset;
                letter.currentRotation = result.rotation;
            }
            else
            {
                float effectiveSmooth = isFastDisappear ? 50f : EffectSmoothSpeed;
                
                // Дополнительная интерполяция для сглаживания резких изменений в самих эффектах
                letter.currentScale = Mathf.Lerp(letter.currentScale, targetScale, dt * effectiveSmooth);
                letter.currentAlpha = Mathf.Lerp(letter.currentAlpha, targetAlpha, dt * effectiveSmooth);
                letter.currentOffset = Vector2.Lerp(letter.currentOffset, targetOffset, dt * effectiveSmooth);
                letter.currentRotation = Mathf.Lerp(letter.currentRotation, targetRotation, dt * effectiveSmooth);
            }
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
            
            byte alpha = (byte)(letter.currentAlpha * _globalAlpha * 255f);
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
