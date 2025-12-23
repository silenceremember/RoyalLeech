// CardDisplay.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.InputSystem;
using TMPEffects.Components;

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

    [Header("TMPEffects Components")]
    private TMPWriter tmpWriter; // Будет автоматически найден в Awake

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
    
    // Typewriter vars
    private string _currentChoiceText = "";
    private float _currentVisibleCharsFloat = 0f;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (actionText != null) _textRectTransform = actionText.GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        
        // Получаем TMPWriter компонент
        tmpWriter = questionText.GetComponent<TMPWriter>();
        if (tmpWriter == null)
        {
            Debug.LogWarning("TMPWriter component not found on questionText! Please add it.");
        }
    }

    public void Setup(CardData data, bool isFront)
    {
        CurrentData = data;
        characterImage.sprite = data.characterSprite;
        nameText.text = data.characterName;
        
        // 1. Устанавливаем текст через TMPWriter
        if (tmpWriter != null)
        {
            tmpWriter.SetText(data.dialogueText);
        }
        else
        {
            questionText.text = data.dialogueText;
        } 

        if (actionText) 
        {
            actionText.text = "";
            actionText.maxVisibleCharacters = 0;
        }
        
        _currentChoiceText = "";
        _currentVisibleCharsFloat = 0f;
        
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
        if (tmpWriter != null)
        {
            // TMPWriter автоматически анимирует появление текста
            tmpWriter.StartWriter();
            // Можно подписаться на события:
            // tmpWriter.OnCharacterShown.AddListener((CharData data) => PlayTypeSound());
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
            if (actionText)
            {
                actionText.text = "";
                actionText.maxVisibleCharacters = 0;
            }
            _currentVisibleCharsFloat = 0f;
            _currentChoiceText = "";
            GameManager.Instance.ResetHighlights();
            return;
        }

        float absDiff = Mathf.Abs(diff);
        
        // Определяем направление и текст
        bool isRight = diff > 0;
        string fullChoiceText = "";
        
        // Если мы близко к центру, определяем текст по предыдущему направлению или обнуляем
        if (absDiff < 5f)
        {
            // В центре - сбрасываем всё
            if (actionText)
            {
                actionText.text = "";
                actionText.maxVisibleCharacters = 0;
            }
            _currentVisibleCharsFloat = 0f;
            _currentChoiceText = "";
            GameManager.Instance.ResetHighlights();
            return;
        }
        
        fullChoiceText = isRight ? CurrentData.rightChoiceText : CurrentData.leftChoiceText;
        
        // При смене направления сбрасываем прогресс
        if (_currentChoiceText != fullChoiceText)
        {
            _currentChoiceText = fullChoiceText;
            _currentVisibleCharsFloat = 0f;
        }

        Color targetColor = normalColor;
        
        float progress = absDiff / choiceThreshold;
        float clampedProgress = Mathf.Clamp01(progress);
        
        // Вычисляем прогресс показа текста (от 0 до 1)
        // Текст появляется линейно от textShowStartDistance до textShowFullDistance
        float activeRange = textShowFullDistance - textShowStartDistance;
        int totalChars = fullChoiceText.Length;
        
        // Целевое количество символов на основе расстояния
        int targetIntChars = 0;
        
        if (activeRange > 0 && totalChars > 0)
        {
            // Расстояние, которое нужно пройти для одного символа
            float distancePerChar = activeRange / totalChars;
            
            // Сколько символов соответствует текущему расстоянию
            float charsFromDistance = (absDiff - textShowStartDistance) / distancePerChar;
            targetIntChars = Mathf.FloorToInt(charsFromDistance);
            targetIntChars = Mathf.Clamp(targetIntChars, 0, totalChars);
        }
        
        // Текущее количество символов (целое)
        int currentIntChars = Mathf.FloorToInt(_currentVisibleCharsFloat);
        
        // СТРОГОЕ ОГРАНИЧЕНИЕ: максимум +1 символ за кадр при движении вперёд
        if (targetIntChars > currentIntChars)
        {
            // Движение вперёд: добавляем ровно +1 символ
            _currentVisibleCharsFloat = currentIntChars + 1;
        }
        else if (targetIntChars < currentIntChars)
        {
            // Движение назад: мгновенно уменьшаем до target
            _currentVisibleCharsFloat = targetIntChars;
        }
        // Если targetIntChars == currentIntChars - остаёмся на месте
        
        int visibleChars = Mathf.FloorToInt(_currentVisibleCharsFloat);
        visibleChars = Mathf.Clamp(visibleChars, 0, totalChars);
        
        // ДИНАМИЧЕСКОЕ ЗАПОЛНЕНИЕ: используем substring для правильного центрирования
        string displayText = visibleChars > 0 ? fullChoiceText.Substring(0, visibleChars) : "";
        if (actionText.text != displayText)
        {
            actionText.text = displayText;
        }
        actionText.maxVisibleCharacters = visibleChars;

        float targetScale = Mathf.Lerp(minScale, maxScale, clampedProgress);
        if (progress > 1.0f) targetScale += Mathf.Sin(Time.time * 20f) * 0.1f;
        _textRectTransform.localScale = Vector3.one * targetScale;

        // Покачивание отключено
        _textRectTransform.localRotation = Quaternion.identity;

        if (progress >= 1.0f)
        {
            targetColor = snapColor;
             if (isRight) GameManager.Instance.HighlightResources(CurrentData.rightCrown, CurrentData.rightChurch, CurrentData.rightMob, CurrentData.rightPlague);
            else GameManager.Instance.HighlightResources(CurrentData.leftCrown, CurrentData.leftChurch, CurrentData.leftMob, CurrentData.leftPlague);
        }
        else
        {
            GameManager.Instance.ResetHighlights();
        }
        
        // Opacity (alpha) отключен - текст всегда полностью видим
        actionText.color = targetColor;
    }

    // HandleInput(float diff) удаляем, перенесли логику в Update для Release

    void MakeChoice(bool isRight)
    {
        _isLocked = true;
        _isInteractable = false;
        
        if (actionText) 
        {
            actionText.text = "";
            actionText.maxVisibleCharacters = 0;
        }
        
        _currentChoiceText = "";
        _currentVisibleCharsFloat = 0f;

        if (isRight) GameManager.Instance.ApplyCardEffect(CurrentData.rightCrown, CurrentData.rightChurch, CurrentData.rightMob, CurrentData.rightPlague);
        else GameManager.Instance.ApplyCardEffect(CurrentData.leftCrown, CurrentData.leftChurch, CurrentData.leftMob, CurrentData.leftPlague);

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
}
