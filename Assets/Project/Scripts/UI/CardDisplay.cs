// CardDisplay.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.InputSystem;
using System.Collections;

public class CardDisplay : MonoBehaviour
{
    [Header("UI Компоненты")]
    public Image characterImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI questionText;
    public TextMeshProUGUI actionText; 
    public CanvasGroup canvasGroup;

    [Header("Настройки Управления")]
    public float movementLimit = 450f;
    public float lockedLimit = 80f;      
    public float unlockDistance = 50f;   
    public float choiceThreshold = 300f;
    public float sensitivity = 1.0f;
    public float lockedDampening = 0.6f;
    
    [Header("Настройки Анимации")]
    public float hiddenY = 2500f; 
    public float fallDuration = 0.6f;     // Чуть ускорил падение для динамики
    public float interactionDelay = 0.5f; 

    [Header("Text Animation")]
    private TextAnimator questionAnimator; // Time-based typewriter
    private TextAnimator actionAnimator;   // Distance-based animation

    // Насколько сильно карта реагирует при локе (настройка)
    [Header("Настройки Лока")]
    public float lockedInputScale = 0.15f;

    [Header("3D Tilt & Juice")]
    public Vector2 tiltStrength = new Vector2(20f, 15f); // X = Vertical, Y = Horizontal
    public float tiltSpeed = 15f; // Увеличил скорость тильта для отзывчивости
    public float velocityTiltMultiplier = 2.5f; // Множитель тильта от скорости мыши
    public float grabScale = 0.95f; // Размер карты при нажатии
    public float grabScaleSpeed = 15f; // Скорость сжатия

    [Header("Сочность Текста")]
    public float minScale = 0.6f;
    public float maxScale = 1.3f;
    public float shakeSpeed = 30f;
    public float shakeAngle = 10f;
    public Color normalColor = Color.white;
    public Color snapColor = Color.yellow;
    
    [Header("Настройки Typewriter для ActionText")]
    [Tooltip("Расстояние от центра, когда начинает показываться первый символ")]
    public float textShowStartDistance = 90f;
    [Tooltip("Расстояние от центра, когда показывается весь текст полностью")]
    public float textShowFullDistance = 300f;
    [Tooltip("Скорость интерполяции количества символов")]
    public float typewriterSmoothSpeed = 15f;
    [Tooltip("Угол наклона текста в сторону свайпа")]
    public float actionTextTiltAngle = 8f;
    [Tooltip("Скорость интерполяции наклона текста")]
    public float actionTextTiltSpeed = 10f;
    [Tooltip("Минимальный масштаб блока текста")]
    public float actionTextScaleMin = 1.0f;
    [Tooltip("Максимальный масштаб блока текста (при полном выборе)")]
    public float actionTextScaleMax = 1.25f;
    [Tooltip("Амплитуда пульсации при полном прогрессе")]
    public float actionTextPulseAmount = 0.05f;
    [Tooltip("Амплитуда покачивания при полном прогрессе (градусы)")]
    public float actionTextWobbleAngle = 3f;
    [Tooltip("Дистанция для нарастания прозрачности текста (0 = текст невидим)")]
    public float actionTextFadeDistance = 50f;

    [Header("Idle Effect")]
    public bool enableIdleRotation = true;
    public float idleRotationAmount = 3f; // Угол качания в градусах
    public float idleRotationSpeed = 1.5f; // Скорость качания
    public Vector2 idleTiltAmount = new Vector2(2f, 1.5f); // X/Y тильт при idle

    public CardData CurrentData { get; private set; }
    
    private RectTransform _rectTransform;
    private RectTransform _textRectTransform;
    private bool _isLocked;       
    private bool _isFront;        
    private bool _isInteractable; 
    
    // ПРЕДОХРАНИТЕЛЬ
    private bool _safetyLock = false; 
    private bool _isUnlockAnimating = false; // Блокирует Update во время "встряски"


    private float _currentVerticalOffset = 0f; 
    private float _currentAngularOffset = 0f;

    private float _shakeOffset = 0f;

    // Коэффициент передачи движения (0.15 = вяло, 1.0 = 1 к 1)
    private float _inputScale = 1.0f;

    // Juice vars
    private Vector3 _currentScaleVec = Vector3.one;
    private float _lastMouseX;
    private float _mouseVelocityX;
    private bool _wasHeld = false;

    // Idle effect vars
    private float _idleTime = 0f;
    
    // Disappear animation tracking
    private Coroutine _disappearCoroutine = null;
    private string _pendingText = null;
    private bool _isWaitingForDisappear = false;
    
    // Pending setup (waiting for explosion to complete)
    private CardData _pendingSetupData = null;
    private bool _pendingSetupIsFront = false;
    private bool _pendingAnimateToFront = false;
    
    // Pending explosion fill data (applied after all letters arrive)
    private int[] _pendingExplosionChanges = null;
    private float[] _pendingFinalFillValues = null;
    private bool[] _pendingIsIncrease = null;
    
    // Canvas group for action text opacity control
    private CanvasGroup _actionTextCanvasGroup;
    
    // Debug: flag to prevent repeated logging when ready
    private bool _debugLoggedReady = false;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (actionText != null) _textRectTransform = actionText.GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        
        // Получаем TextAnimator компоненты
        questionAnimator = questionText.GetComponent<TextAnimator>();
        if (questionAnimator == null)
        {
            Debug.LogWarning("TextAnimator component not found on questionText! Please add it.");
        }
        
        if (actionText != null)
        {
            actionAnimator = actionText.GetComponent<TextAnimator>();
            if (actionAnimator == null)
            {
                Debug.LogWarning("TextAnimator component not found on actionText! Please add it.");
            }
            
            // Получаем или создаём CanvasGroup для управления opacity
            _actionTextCanvasGroup = actionText.GetComponent<CanvasGroup>();
            if (_actionTextCanvasGroup == null)
            {
                _actionTextCanvasGroup = actionText.gameObject.AddComponent<CanvasGroup>();
            }
            
            // Подписываемся на завершение взрыва
            if (actionAnimator != null)
            {
                actionAnimator.OnExplosionComplete += OnExplosionCompleteHandler;
            }
        }
    }
    
    void OnDestroy()
    {
        // Отписываемся
        if (actionAnimator != null)
        {
            actionAnimator.OnExplosionComplete -= OnExplosionCompleteHandler;
        }
    }
    
    private void OnExplosionCompleteHandler()
    {
        // Взрыв завершился - применяем fill ко ВСЕМ ресурсам с изменениями
        if (_pendingExplosionChanges != null && _pendingFinalFillValues != null && _pendingIsIncrease != null)
        {
            var gm = GameManager.Instance;
            LiquidFillIcon[] icons = new LiquidFillIcon[] { gm.spadesIcon, gm.heartsIcon, gm.diamondsIcon, gm.clubsIcon };
            
            for (int i = 0; i < 4; i++)
            {
                if (_pendingExplosionChanges[i] != 0 && icons[i] != null)
                {
                    // Apply FINAL fill value with FULL effect (arithmetically exact)
                    icons[i].ApplyFinalFillWithEffect(_pendingFinalFillValues[i], _pendingIsIncrease[i], Mathf.Abs(_pendingExplosionChanges[i]));
                }
            }
            
            // Clear pending data
            _pendingExplosionChanges = null;
            _pendingFinalFillValues = null;
            _pendingIsIncrease = null;
        }
        
        // Выполняем отложенный Setup если есть
        if (_pendingSetupData != null)
        {
            var data = _pendingSetupData;
            var isFront = _pendingSetupIsFront;
            var shouldAnimate = _pendingAnimateToFront;
            
            _pendingSetupData = null;
            _pendingAnimateToFront = false;
            
            ExecuteSetup(data, isFront);
            
            // Выполняем отложенный AnimateToFront
            if (shouldAnimate)
            {
                AnimateToFront();
            }
        }
    }

    public void Setup(CardData data, bool isFront)
    {
        // Если взрыв в процессе - откладываем Setup
        if (actionAnimator != null && actionAnimator.IsExploding)
        {
            _pendingSetupData = data;
            _pendingSetupIsFront = isFront;
            return;
        }
        
        ExecuteSetup(data, isFront);
    }
    
    private void ExecuteSetup(CardData data, bool isFront)
    {
        CurrentData = data;
        characterImage.sprite = data.characterSprite;
        nameText.text = data.characterName;
        
        // 1. Устанавливаем текст через TextAnimator
        if (questionAnimator != null)
        {
            questionAnimator.SetText(data.dialogueText);
        }
        else
        {
            questionText.text = data.dialogueText;
        } 

        // ActionText теперь управляется через TextAnimator в distance-based режиме
        if (actionAnimator != null)
        {
            actionAnimator.SetText("");
            actionAnimator.ResetProgress();
        }
        else if (actionText)
        {
            actionText.text = "";
            actionText.maxVisibleCharacters = 0;
        }
        

        
        _isLocked = false;
        _isFront = isFront;
        _isInteractable = false;
        _safetyLock = false;
        _isUnlockAnimating = false; // Важно сбросить анимацию
        _inputScale = 1.0f; // По умолчанию полный контроль
        _currentScaleVec = Vector3.one;
        _idleTime = Random.Range(0f, 100f); // Случайное начальное смещение для разнообразия
        
        if (isFront)
        {
            _currentVerticalOffset = 0f;
            _currentAngularOffset = 0f;
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.rotation = Quaternion.identity;
            _rectTransform.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
        }
        else
        {
            _currentVerticalOffset = hiddenY;
            _currentAngularOffset = 5f;
            _rectTransform.anchoredPosition = new Vector2(0, hiddenY);
            _rectTransform.rotation = Quaternion.Euler(0, 0, _currentAngularOffset);
            canvasGroup.alpha = 1f;
        }
    }

    public void AnimateToFront()
    {
        // Если есть отложенный Setup (ждём взрыва) - также откладываем анимацию
        if (_pendingSetupData != null)
        {
            _pendingAnimateToFront = true;
            return;
        }
        
        _isFront = true;
        _isLocked = false;
        _isInteractable = false;

        _safetyLock = true;
        
        // ВКЛЮЧАЕМ "ВЯЛОСТЬ"
        // Карта будет двигаться, но с маленькой амплитудой
        _inputScale = lockedInputScale;
        
        DOTween.Kill(this);

        // Анимация падения
        Sequence dropSeq = DOTween.Sequence();
        
        dropSeq.Append(DOTween.To(() => _currentVerticalOffset, x => _currentVerticalOffset = x, 0f, fallDuration)
            .SetEase(Ease.OutBack).SetTarget(this));
        
        dropSeq.Join(DOTween.To(() => _currentAngularOffset, x => _currentAngularOffset = x, 0f, fallDuration)
            .SetEase(Ease.OutBack).SetTarget(this));

        // PUNCH эффект при приземлении
        dropSeq.Append(transform.DOPunchScale(new Vector3(0.05f, -0.05f, 0), 0.2f, 10, 1)
            .SetTarget(this));

        // ЗАПУСК ТЕКСТА
        // Мы запускаем текст чуть раньше, чем закончится падение (на 80%), чтобы было динамичнее
        DOVirtual.DelayedCall(fallDuration * 0.8f, () => 
        {
            StartTypewriter();
        }).SetTarget(this);

        // Разблокировка управления
        DOVirtual.DelayedCall(interactionDelay, () => 
        {
            _isInteractable = true;
        }).SetTarget(this);
    }

    void StartTypewriter()
    {
        if (questionAnimator != null)
        {
            // TextAnimator автоматически анимирует появление текста
            questionAnimator.StartWriter();
            // Можно подписаться на события:
            // questionAnimator.OnCharacterShown.AddListener((char c) => PlayTypeSound());
        }
    }

    void Update()
    {
        // Обновляем idle время для всех карт на фронте (даже если заблокированы)
        if (_isFront && enableIdleRotation)
        {
            _idleTime += Time.deltaTime;
        }
        
        // Если карта заблокирована но на фронте, применяем только idle эффект
        if (_isFront && _isLocked && _safetyLock)
        {
            ApplyIdleEffect();
            return;
        }
        
        if (!_isFront || _isLocked) return;
        
        HandleMotion();
    }

    void ApplyIdleEffect()
    {
        // Применяем только idle rotation для заблокированной карты
        Vector3 idleRotation = GetIdleRotation();
        Quaternion targetRotation = Quaternion.Euler(idleRotation.x, idleRotation.y, idleRotation.z);
        _rectTransform.rotation = Quaternion.Slerp(_rectTransform.rotation, targetRotation, Time.deltaTime * tiltSpeed);
    }

    void HandleMotion()
    {
        // 1. СЫРОЙ ВВОД
        Vector2 mousePos = Vector2.zero;
        float rawDiff = 0f;
        
        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
            float screenCenter = Screen.width / 2f;
            rawDiff = (mousePos.x - screenCenter) * sensitivity;

            // Расчет скорости мыши для тильта
            _mouseVelocityX = (mousePos.x - _lastMouseX) / Time.deltaTime;
            _lastMouseX = mousePos.x;
        }

        bool isClickFrame = Mouse.current.leftButton.wasPressedThisFrame;
        bool isHeld = Mouse.current.leftButton.isPressed;

        // 2. ЛОГИКА ЛОКА (КЛИК)
        if (_safetyLock && _isInteractable)
        {
            if (isClickFrame)
            {
                TriggerWobbleUnlock();
                isClickFrame = false; 
                isHeld = false; // Сбрасываем hold для этого кадра, чтобы не было конфликтов
            }
        }

        // 3. МАТЕМАТИКА ПОЗИЦИИ (X)
        float currentLimit = _safetyLock ? lockedLimit : movementLimit;
        float scaledDiff = rawDiff * _inputScale;
        float targetX = Mathf.Clamp(scaledDiff, -movementLimit, movementLimit);

        // 4. ФИЗИКА ПОЗИЦИИ
        float smoothX = Mathf.Lerp(_rectTransform.anchoredPosition.x, targetX, Time.deltaTime * 20f);
        _rectTransform.anchoredPosition = new Vector2(smoothX + _shakeOffset, _currentVerticalOffset);

        // --- 5. ФИЗИКА ВРАЩЕНИЯ (3D TILT + 2D SWAY + VELOCITY) ---

        float rotZ = -_rectTransform.anchoredPosition.x * 0.05f;
        rotZ += _currentAngularOffset;

        // Добавляем тильт от скорости мыши (инерция)
        // Если мышь резко дернули вправо, карта должна наклониться
        float velocityTilt = Mathf.Clamp(_mouseVelocityX * -0.005f, -10f, 10f) * velocityTiltMultiplier;
        if (_safetyLock) velocityTilt = 0; // В локе не тильтуем от скорости

        float nMouseX = (mousePos.x / Screen.width - 0.5f) * 2f;
        float nMouseY = (mousePos.y / Screen.height - 0.5f) * 2f;

        nMouseX = Mathf.Clamp(nMouseX, -1f, 1f);
        nMouseY = Mathf.Clamp(nMouseY, -1f, 1f);

        // Используем разные силы для осей
        float targetTiltX = -nMouseY * tiltStrength.x; 
        float targetTiltY = (nMouseX * tiltStrength.y) + velocityTilt; 

        // Добавляем idle эффект (только когда не держим карту)
        Vector3 idleRotation = Vector3.zero;
        if (!isHeld)
        {
            idleRotation = GetIdleRotation();
        }

        Quaternion targetRotation = Quaternion.Euler(targetTiltX + idleRotation.x, targetTiltY + idleRotation.y, rotZ + idleRotation.z);

        _rectTransform.rotation = Quaternion.Slerp(_rectTransform.rotation, targetRotation, Time.deltaTime * tiltSpeed);

        // 6. SCALE "GRAB" EFFECT
        // Если держим карту (и она не залочена), она чуть сжимается
        bool isGrabbed = _isInteractable && !_safetyLock && isHeld;

        // Если только что схватили - убиваем все твины (особенно PunchScale от падения)
        // и синхронизируем скейл, чтобы не было скачка
        if (isGrabbed && !_wasHeld)
        {
            DOTween.Kill(transform); // Убиваем PunchScale и всё что висит на трансформе
            _currentScaleVec = _rectTransform.localScale; // Синхронизируем с ТЕКУЩИМ (возможно сплюснутым) вектором
        }

        float targetS = isGrabbed ? grabScale : 1.0f;
        Vector3 targetVec = Vector3.one * targetS;
        
        _currentScaleVec = Vector3.Lerp(_currentScaleVec, targetVec, Time.deltaTime * grabScaleSpeed);
        
        // Применяем масштаб, если мы контролируем его (т.е. если мы держим карту ИЛИ если нет активных твинов)
        // Если идет твин падения (Punch), и мы НЕ держим карту - пусть играет твин.
        // Но если мы схватили - мы убили твин выше, и теперь полностью управляем скейлом.
        if (isGrabbed || !DOTween.IsTweening(transform)) 
        {
            _rectTransform.localScale = _currentScaleVec;
        }

        _wasHeld = isGrabbed;

        // 7. ВИЗУАЛ
        UpdateVisuals(targetX);

        
        // Проверка свайпа должна быть постоянной или на отпускании. 
        // Если это "Tinder", то карта летает за мышкой, и если отпускаешь в зоне - выбор.
        if (_isInteractable && !_safetyLock && Mouse.current.leftButton.wasReleasedThisFrame)
        {
             if (targetX > choiceThreshold) MakeChoice(true);
             else if (targetX < -choiceThreshold) MakeChoice(false);
        }
    }

    Vector3 GetIdleRotation()
    {
        if (!enableIdleRotation)
            return Vector3.zero;

        // Используем синусоиды с разными частотами для более органичного движения
        float rotZ = Mathf.Sin(_idleTime * idleRotationSpeed) * idleRotationAmount;
        float rotX = Mathf.Sin(_idleTime * idleRotationSpeed * 0.7f) * idleTiltAmount.x;
        float rotY = Mathf.Cos(_idleTime * idleRotationSpeed * 0.5f) * idleTiltAmount.y;

        return new Vector3(rotX, rotY, rotZ);
    }

    void TriggerWobbleUnlock()
    {
        _safetyLock = false; 
        
        // Очищаем текст после взрыва - теперь игрок разблокировал карту!
        if (actionAnimator != null)
        {
            actionAnimator.ClearExplosionText();
        }

        // 1. ОПРЕДЕЛЯЕМ НАПРАВЛЕНИЕ
        float mouseX = 0f;
        if (Mouse.current != null)
        {
            mouseX = Mouse.current.position.ReadValue().x - Screen.width / 2f;
        }

        // Если мышь справа (> 0), то первый рывок влево (-1).
        // Если мышь слева или в центре, то первый рывок вправо (+1).
        // Это делает анимацию логичной физически.
        float dir = (mouseX > 0) ? -1f : 1f;
        
        float power = 10f; // Амплитуда, как ты просил

        DOTween.Kill(this, "shake"); 
        DOTween.Kill(this, "inputScale");

        Sequence seq = DOTween.Sequence();
        seq.SetId("shake");
        
        // --- АНИМАЦИЯ ПРОБУЖДЕНИЯ ---
        
        // 1. Рывок "ПРОТИВ" (Замах) -> 30px
        seq.Append(DOTween.To(() => _shakeOffset, x => _shakeOffset = x, dir * power, 0.08f)
            .SetEase(Ease.OutSine));
        
        // 2. Рывок "ЗА" (Перехлест) -> -30px (в другую сторону)
        seq.Append(DOTween.To(() => _shakeOffset, x => _shakeOffset = x, -dir * power, 0.1f)
            .SetEase(Ease.InOutSine));
        
        // 3. Возврат в ноль (Успокоение)
        seq.Append(DOTween.To(() => _shakeOffset, x => _shakeOffset = x, 0f, 0.3f)
            .SetEase(Ease.OutBack)); // Легкая пружинка в конце

        // --- ФИЗИКА (РАЗГОН) ---
        // Включаем следование за мышью сразу, но плавно разгоняем чувствительность
        DOTween.To(() => _inputScale, x => _inputScale = x, 1.0f, 0.3f)
            .SetEase(Ease.OutCubic) // Быстрый старт, плавный финиш - ощущается как "сразу работает"
            .SetId("inputScale")
            .SetTarget(this);
    }

    void UpdateVisuals(float diff)
    {
        // Скрываем если падает ИЛИ если ЗАБЛОКИРОВАНО (но не во время анимации разблокировки!)
        if (_currentVerticalOffset > 150f || (_safetyLock && !_isUnlockAnimating)) 
        {
            TriggerReturnToCenterWithDelay();
            GameManager.Instance.ResetHighlights();
            return;
        }

        float absDiff = Mathf.Abs(diff);
        
        // Если мы близко к центру, сбрасываем всё с анимацией
        if (absDiff < 5f)
        {
            TriggerReturnToCenterWithDelay();
            GameManager.Instance.ResetHighlights();
            return;
        }
        
        // Определяем направление и текст
        bool isRight = diff > 0;
        string fullChoiceText = isRight ? CurrentData.rightChoiceText : CurrentData.leftChoiceText;
        
        // При смене направления запускаем disappear и ждём
        if (actionAnimator != null)
        {
            if (actionAnimator.CurrentText != fullChoiceText && !_isWaitingForDisappear)
            {
                // Если текст уже есть, запускаем пропадание перед сменой
                if (!string.IsNullOrEmpty(actionAnimator.CurrentText))
                {
                    StartDisappearAndChangeText(fullChoiceText, DisappearMode.Return);
                }
                else
                {
                    actionAnimator.SetText(fullChoiceText);
                    actionAnimator.ResetProgress();
                }
            }
        }
        else if (actionText)
        {
            actionText.text = fullChoiceText;
        }

        Color targetColor = normalColor;
        float progress = absDiff / choiceThreshold;
        float clampedProgress = Mathf.Clamp01(progress);
        
        // Вычисляем целевое количество символов на основе расстояния
        float activeRange = textShowFullDistance - textShowStartDistance;
        int totalChars = fullChoiceText.Length;
        int targetIntChars = 0;
        
        if (activeRange > 0 && totalChars > 0)
        {
            float distancePerChar = activeRange / totalChars;
            float charsFromDistance = (absDiff - textShowStartDistance) / distancePerChar;
            targetIntChars = Mathf.FloorToInt(charsFromDistance);
            targetIntChars = Mathf.Clamp(targetIntChars, 0, totalChars);
        }
        
        // Передаем целевое количество символов и прогресс в TextAnimator
        // НО только если НЕ ждём завершения disappear анимации
        if (actionAnimator != null)
        {
            if (!_isWaitingForDisappear)
            {
                actionAnimator.SetTargetCharacterCount(targetIntChars);
                
                // Передаем прогресс для управления интенсивностью per-letter эффектов
                actionAnimator.SetProgress(clampedProgress);
            }
            // Если ждём disappear - ничего не делаем, ждём завершения анимации
        }
        else if (actionText)
        {
            // Fallback для старой системы (только если нет actionAnimator)
            actionText.maxVisibleCharacters = targetIntChars;
            
            // Оставляем старую анимацию скейла только для fallback
            float targetScale = Mathf.Lerp(minScale, maxScale, clampedProgress);
            if (progress > 1.0f) targetScale += Mathf.Sin(Time.time * 20f) * 0.1f;
            _textRectTransform.localScale = Vector3.one * targetScale;
            _textRectTransform.localRotation = Quaternion.identity;
        }

        // Подсветка ресурсов - вызываем ПОСТОЯННО с текущим прогрессом
        // Чем ближе к выбору, тем сильнее тряска
        // Pass card world position for 3D look-at effect
        Vector3 cardWorldPos = transform.position;
        if (isRight) 
            GameManager.Instance.HighlightResources(CurrentData.rightSpades, CurrentData.rightHearts, CurrentData.rightDiamonds, CurrentData.rightClubs, clampedProgress, cardWorldPos);
        else 
            GameManager.Instance.HighlightResources(CurrentData.leftSpades, CurrentData.leftHearts, CurrentData.leftDiamonds, CurrentData.leftClubs, clampedProgress, cardWorldPos);
        
        // Цвет текста при полном выборе
        if (progress >= 1.0f)
        {
            targetColor = snapColor;
            
            // Debug: вывод последствий выбора когда текст становится желтым
            if (!_debugLoggedReady)
            {
                _debugLoggedReady = true;
                string dir = isRight ? "RIGHT" : "LEFT";
                int spades = isRight ? CurrentData.rightSpades : CurrentData.leftSpades;
                int hearts = isRight ? CurrentData.rightHearts : CurrentData.leftHearts;
                int diamonds = isRight ? CurrentData.rightDiamonds : CurrentData.leftDiamonds;
                int clubs = isRight ? CurrentData.rightClubs : CurrentData.leftClubs;
                Debug.Log($"[READY] {dir}: {fullChoiceText} → ♠{spades:+#;-#;0} ♥{hearts:+#;-#;0} ♦{diamonds:+#;-#;0} ♣{clubs:+#;-#;0}");
            }
        }
        else
        {
            _debugLoggedReady = false;
        }
        
        // Прозрачность блока текста через CanvasGroup (0 -> 50)
        // Не меняем пока ждём disappear анимацию
        if (!_isWaitingForDisappear && _actionTextCanvasGroup != null)
        {
            float targetAlpha = Mathf.Clamp01(absDiff / actionTextFadeDistance);
            // Плавная интерполяция alpha
            _actionTextCanvasGroup.alpha = Mathf.Lerp(_actionTextCanvasGroup.alpha, targetAlpha, Time.deltaTime * 12f);
        }
        
        actionText.color = targetColor;
        
        // Наклон блока текста в сторону свайпа (фиксированный угол)
        // Не меняем наклон пока текст исчезает
        if (_textRectTransform != null && !_isWaitingForDisappear)
        {
            // Фиксированный угол в зависимости от направления
            float targetTilt = isRight ? actionTextTiltAngle : -actionTextTiltAngle;
            
            // Покачивание при полном выборе (желтый цвет)
            if (progress >= 1.0f)
            {
                targetTilt += Mathf.Sin(Time.time * 10f) * actionTextWobbleAngle;
            }
            
            Quaternion targetRot = Quaternion.Euler(0f, 0f, -targetTilt);
            _textRectTransform.localRotation = Quaternion.Slerp(
                _textRectTransform.localRotation, 
                targetRot, 
                Time.deltaTime * actionTextTiltSpeed
            );
            
            // Масштаб блока текста в зависимости от прогресса
            float targetScale = Mathf.Lerp(actionTextScaleMin, actionTextScaleMax, clampedProgress);
            
            // Пульсация при полном выборе
            if (progress >= 1.0f)
            {
                targetScale += Mathf.Sin(Time.time * 8f) * actionTextPulseAmount;
            }
            
            _textRectTransform.localScale = Vector3.Lerp(
                _textRectTransform.localScale,
                Vector3.one * targetScale,
                Time.deltaTime * 10f
            );
        }
    }

    // HandleInput(float diff) удаляем, перенесли логику в Update для Release

    void MakeChoice(bool isRight)
    {
        _isLocked = true;
        _isInteractable = false;
        
        // Get resource changes
        int[] changes = isRight 
            ? new int[] { CurrentData.rightSpades, CurrentData.rightHearts, CurrentData.rightDiamonds, CurrentData.rightClubs }
            : new int[] { CurrentData.leftSpades, CurrentData.leftHearts, CurrentData.leftDiamonds, CurrentData.leftClubs };
        
        // Check if ANY resource changes
        bool hasChanges = false;
        foreach (var c in changes) if (c != 0) { hasChanges = true; break; }
        
        if (actionAnimator != null)
        {
            if (hasChanges && actionAnimator.preset?.explosionPreset != null)
            {
                // Get icon positions from GameManager
                var gm = GameManager.Instance;
                Vector2[] iconPositions = new Vector2[]
                {
                    gm.spadesIcon != null ? (Vector2)gm.spadesIcon.transform.position : Vector2.zero,
                    gm.heartsIcon != null ? (Vector2)gm.heartsIcon.transform.position : Vector2.zero,
                    gm.diamondsIcon != null ? (Vector2)gm.diamondsIcon.transform.position : Vector2.zero,
                    gm.clubsIcon != null ? (Vector2)gm.clubsIcon.transform.position : Vector2.zero
                };
                
                // Calculate how many letters will fly to each icon
                int totalLetters = actionAnimator.VisibleCharacters;
                if (totalLetters <= 0) totalLetters = actionAnimator.CurrentText?.Length ?? 1;
                
                // Calculate total absolute change for proportional distribution
                int totalChange = 0;
                foreach (var c in changes) totalChange += Mathf.Abs(c);
                
                // Letter counts per icon (same algorithm as in TextAnimator.DistributeLettersToIcons)
                int[] letterCounts = new int[4];
                int assigned = 0;
                for (int i = 0; i < 4; i++)
                {
                    float proportion = totalChange > 0 ? (float)Mathf.Abs(changes[i]) / totalChange : 0f;
                    letterCounts[i] = Mathf.RoundToInt(proportion * totalLetters);
                    assigned += letterCounts[i];
                }
                // Fix rounding
                while (assigned != totalLetters && totalChange > 0)
                {
                    int maxIdx = 0;
                    for (int i = 1; i < 4; i++)
                        if (Mathf.Abs(changes[i]) > Mathf.Abs(changes[maxIdx])) maxIdx = i;
                    if (assigned < totalLetters) { letterCounts[maxIdx]++; assigned++; }
                    else if (letterCounts[maxIdx] > 0) { letterCounts[maxIdx]--; assigned--; }
                    else break;
                }
                
                // Calculate tier-based effect multiplier for each icon
                float[] effectMultiplierPerLetter = new float[4];
                float[] finalFillValues = new float[4]; // Final fill values
                bool[] isIncrease = new bool[4]; // Track direction for each icon
                int[] currentResourceValues = new int[] { gm.spades, gm.hearts, gm.diamonds, gm.clubs };
                
                // Store icon references (needed for tier calculation and callback)
                LiquidFillIcon[] icons = new LiquidFillIcon[] { gm.spadesIcon, gm.heartsIcon, gm.diamondsIcon, gm.clubsIcon };
                
                for (int i = 0; i < 4; i++)
                {
                    // Calculate final fill value after all changes
                    int newValue = Mathf.Clamp(currentResourceValues[i] + changes[i], 0, 100);
                    finalFillValues[i] = newValue / 100f;
                    isIncrease[i] = changes[i] > 0;
                    
                    if (letterCounts[i] > 0)
                    {
                        // Calculate tier multiplier based on absolute change
                        LiquidFillIcon icon = icons[i];
                        if (icon != null && icon.effectPreset != null)
                        {
                            var preset = icon.effectPreset;
                            int absDelta = Mathf.Abs(changes[i]);
                            float tierMultiplier;
                            
                            // Use separate multipliers for increase vs decrease
                            if (isIncrease[i])
                            {
                                // Increase effect - use increase multipliers
                                if (absDelta <= preset.minorThreshold)
                                    tierMultiplier = preset.increaseMinorMultiplier;
                                else if (absDelta > preset.majorThreshold)
                                    tierMultiplier = preset.increaseMajorMultiplier;
                                else
                                    tierMultiplier = 1f; // Normal
                            }
                            else
                            {
                                // Decrease effect - use decrease multipliers
                                if (absDelta <= preset.minorThreshold)
                                    tierMultiplier = preset.decreaseMinorMultiplier;
                                else if (absDelta > preset.majorThreshold)
                                    tierMultiplier = preset.decreaseMajorMultiplier;
                                else
                                    tierMultiplier = 1f; // Normal
                            }
                            
                            // Each letter applies FULL tier effect
                            effectMultiplierPerLetter[i] = tierMultiplier;
                        }
                        else
                        {
                            effectMultiplierPerLetter[i] = 1f; // Fallback
                        }
                    }
                }
                
                // Store pending data for OnExplosionCompleteHandler
                // This ensures fill is applied for ALL resources after explosion, not just those with letters
                _pendingExplosionChanges = changes;
                _pendingFinalFillValues = finalFillValues;
                _pendingIsIncrease = isIncrease;
                
                // Fade out bubbles on all icons before explosion
                foreach (var icon in icons)
                {
                    if (icon != null) icon.FadeOutBubbles();
                }
                
                // Trigger explosion with callback that plays per-letter VISUAL effect only (no fill change)
                actionAnimator.TriggerExplosion(changes, iconPositions, (iconIndex) =>
                {
                    // Letter arrived - play visual effect only (no fill change)
                    LiquidFillIcon icon = icons[iconIndex];
                    if (icon != null && changes[iconIndex] != 0)
                    {
                        icon.ReceiveLetterEffect(isIncrease[iconIndex], effectMultiplierPerLetter[iconIndex]);
                    }
                    // Fill will be applied in OnExplosionCompleteHandler after ALL letters arrive
                });
                
                // Update game state immediately (values will animate visually)
                gm.ApplyCardEffectInstant(changes[0], changes[1], changes[2], changes[3]);
            }
            else
            {
                // No changes or no explosion preset - normal disappear
                actionAnimator.TriggerSelected();
                
                // Apply effect normally
                if (isRight) GameManager.Instance.ApplyCardEffect(CurrentData.rightSpades, CurrentData.rightHearts, CurrentData.rightDiamonds, CurrentData.rightClubs);
                else GameManager.Instance.ApplyCardEffect(CurrentData.leftSpades, CurrentData.leftHearts, CurrentData.leftDiamonds, CurrentData.leftClubs);
            }
        }
        else if (actionText)
        {
            actionText.text = "";
            actionText.maxVisibleCharacters = 0;
            
            // Apply effect normally
            if (isRight) GameManager.Instance.ApplyCardEffect(CurrentData.rightSpades, CurrentData.rightHearts, CurrentData.rightDiamonds, CurrentData.rightClubs);
            else GameManager.Instance.ApplyCardEffect(CurrentData.leftSpades, CurrentData.leftHearts, CurrentData.leftDiamonds, CurrentData.leftClubs);
        }

        float endX = isRight ? 1500f : -1500f;
        float endRotation = isRight ? -45f : 45f;

        // АНТИЦИПАЦИЯ (замах перед улетом)
        Sequence seq = DOTween.Sequence();
        
        // 1. Короткий отскок назад (0.1 сек)
        float anticipationX = isRight ? -50f : 50f; 
        seq.Append(_rectTransform.DOAnchorPosX(_rectTransform.anchoredPosition.x + anticipationX, 0.1f).SetEase(Ease.OutQuad));
        seq.Join(_rectTransform.DORotate(new Vector3(0, 0, isRight ? 5f : -5f), 0.1f));
        seq.Join(_rectTransform.DOScale(1f, 0.1f)); // Гарантируем возврат к нормальному размеру, если карту держали

        // 2. Вылет с ускорением
        seq.Append(_rectTransform.DOAnchorPosX(endX, 0.5f).SetEase(Ease.InBack)); // InBack сам дает замах, но мы усилили его ручным
        seq.Join(_rectTransform.DORotate(new Vector3(0, 0, endRotation), 0.4f).SetEase(Ease.InQuad));

        seq.OnComplete(() => 
        {
            GameManager.Instance.OnCardAnimationComplete();
        });
    }
    
    /// <summary>
    /// Запускает анимацию пропадания при возврате к центру.
    /// </summary>
    void TriggerReturnToCenterWithDelay()
    {
        if (actionAnimator != null && !string.IsNullOrEmpty(actionAnimator.CurrentText))
        {
            // Если текст уже пропадает или уже пустой, ничего не делаем
            if (_isWaitingForDisappear) return;
            
            actionAnimator.TriggerReturnToCenter();
            
            // Запускаем корутину для очистки после анимации
            if (_disappearCoroutine != null)
            {
                StopCoroutine(_disappearCoroutine);
            }
            _disappearCoroutine = StartCoroutine(WaitForDisappearAndClear());
        }
        else if (actionText)
        {
            actionText.text = "";
            actionText.maxVisibleCharacters = 0;
        }
    }
    
    /// <summary>
    /// Запускает анимацию пропадания и затем меняет текст.
    /// </summary>
    void StartDisappearAndChangeText(string newText, DisappearMode mode)
    {
        if (actionAnimator == null) return;
        if (_isWaitingForDisappear) return;
        
        _pendingText = newText;
        _isWaitingForDisappear = true;
        
        if (mode == DisappearMode.Return)
        {
            actionAnimator.TriggerReturnToCenter();
        }
        else if (mode == DisappearMode.Selected)
        {
            actionAnimator.TriggerSelected();
        }
        else
        {
            actionAnimator.TriggerDisappear(mode);
        }
        
        if (_disappearCoroutine != null)
        {
            StopCoroutine(_disappearCoroutine);
        }
        _disappearCoroutine = StartCoroutine(WaitForDisappearAndChangeText());
    }
    
    IEnumerator WaitForDisappearAndClear()
    {
        _isWaitingForDisappear = true;
        
        // Получаем длительность анимации из preset
        float waitTime = 0.15f; // fallback
        if (actionAnimator != null && actionAnimator.preset != null)
        {
            waitTime = actionAnimator.preset.disappearReturnDuration * 1.2f + 0.03f;
        }
        
        // Плавно гасим текст
        float fadeOutTime = 0.1f;
        float elapsed = 0f;
        float startAlpha = _actionTextCanvasGroup != null ? _actionTextCanvasGroup.alpha : 1f;
        
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            if (_actionTextCanvasGroup != null)
            {
                _actionTextCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutTime);
            }
            yield return null;
        }
        
        if (_actionTextCanvasGroup != null)
        {
            _actionTextCanvasGroup.alpha = 0f;
        }
        
        // Ждём завершения disappear анимации
        yield return new WaitForSeconds(Mathf.Max(0f, waitTime - fadeOutTime));
        
        if (actionAnimator != null)
        {
            actionAnimator.SetText("");
            actionAnimator.ResetProgress();
        }
        
        // alpha остаётся 0 так как текст пустой
        
        _isWaitingForDisappear = false;
        _disappearCoroutine = null;
    }
    
    IEnumerator WaitForDisappearAndChangeText()
    {
        // Получаем длительность анимации из preset
        float waitTime = 0.15f; // fallback
        if (actionAnimator != null && actionAnimator.preset != null)
        {
            waitTime = actionAnimator.preset.disappearReturnDuration * 1.2f + 0.03f;
        }
        
        // Плавно гасим текст
        float fadeTime = 0.1f;
        float elapsed = 0f;
        float startAlpha = _actionTextCanvasGroup != null ? _actionTextCanvasGroup.alpha : 1f;
        
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            if (_actionTextCanvasGroup != null)
            {
                _actionTextCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeTime);
            }
            yield return null;
        }
        
        if (_actionTextCanvasGroup != null)
        {
            _actionTextCanvasGroup.alpha = 0f;
        }
        
        // Ждём завершения disappear анимации
        yield return new WaitForSeconds(Mathf.Max(0f, waitTime - fadeTime));
        
        // Устанавливаем новый текст
        if (actionAnimator != null && !string.IsNullOrEmpty(_pendingText))
        {
            actionAnimator.SetText(_pendingText);
            actionAnimator.ResetProgress();
        }
        
        // Плавно показываем новый текст
        elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            if (_actionTextCanvasGroup != null)
            {
                _actionTextCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeTime);
            }
            yield return null;
        }
        
        if (_actionTextCanvasGroup != null)
        {
            _actionTextCanvasGroup.alpha = 1f;
        }
        
        _pendingText = null;
        _isWaitingForDisappear = false;
        _disappearCoroutine = null;
    }
}
