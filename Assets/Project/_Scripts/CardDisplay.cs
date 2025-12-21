// CardDisplay.cs

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.InputSystem;

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
    
    [Header("Настройки Анимации")]
    public float hiddenY = 2500f; 
    public float fallDuration = 0.6f;     // Чуть ускорил падение для динамики
    public float interactionDelay = 0.5f; 

    [Header("Typewriter Effect")]
    public float typingSpeed = 0.02f; // Скорость появления одной буквы (сек)

    [Header("Сочность Текста")]
    public float minScale = 0.6f;
    public float maxScale = 1.3f;
    public float shakeSpeed = 30f;
    public float shakeAngle = 10f;
    public Color normalColor = Color.white;
    public Color snapColor = Color.yellow;

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

    // Твин для текста, чтобы можно было остановить
    private Tween _typewriterTween;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (actionText != null) _textRectTransform = actionText.GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Setup(CardData data, bool isFront)
    {
        CurrentData = data;
        characterImage.sprite = data.characterSprite;
        nameText.text = data.characterName;
        
        // 1. Устанавливаем текст, но скрываем все буквы
        questionText.text = data.dialogueText;
        questionText.maxVisibleCharacters = 0; 

        if (actionText) actionText.gameObject.SetActive(false);
        
        _isLocked = false;
        _isFront = isFront;
        _isInteractable = false;
        _safetyLock = false;
        _isUnlockAnimating = false; // Важно сбросить анимацию
        
        if (isFront)
        {
            _currentVerticalOffset = 0f;
            _currentAngularOffset = 0f;
            _rectTransform.anchoredPosition = Vector2.zero;
            _rectTransform.rotation = Quaternion.identity;
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
        
        DOTween.Kill(this); 
        // Убиваем старый твин текста, если он вдруг еще идет
        if (_typewriterTween != null) _typewriterTween.Kill();

        // Анимация падения
        DOTween.To(() => _currentVerticalOffset, x => _currentVerticalOffset = x, 0f, fallDuration)
            .SetEase(Ease.OutBack).SetTarget(this);

        DOTween.To(() => _currentAngularOffset, x => _currentAngularOffset = x, 0f, fallDuration)
            .SetEase(Ease.OutBack).SetTarget(this);
            
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
        int totalChars = questionText.text.Length;
        questionText.maxVisibleCharacters = 0;

        // Рассчитываем длительность: длина текста * скорость одной буквы
        float duration = totalChars * typingSpeed;

        // Анимируем число видимых символов от 0 до totalChars
        _typewriterTween = DOTween.To(x => questionText.maxVisibleCharacters = (int)x, 0, totalChars, duration)
            .SetEase(Ease.Linear)
            .SetTarget(this);
            // .OnUpdate(() => { PlayTypeSound(); }) // Сюда можно добавить звук "тук-тук"
    }

    void Update()
    {
        if (!_isFront || _isLocked) return;
        HandleMotion();
    }

    void HandleMotion()
    {
        float rawDiff = 0f;
        if (Mouse.current != null)
        {
            float screenCenter = Screen.width / 2f;
            rawDiff = (Mouse.current.position.ReadValue().x - screenCenter) * sensitivity;
        }

        // --- 1. ПРОВЕРКА НА FORCE UNLOCK (Клик во время блока) ---
        if (_safetyLock && _isInteractable && !_isUnlockAnimating)
        {
            // Если мышь далеко (за пределами лока) И нажат клик
            if (Mathf.Abs(rawDiff) > lockedLimit && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TriggerForceUnlock(rawDiff);
                return; // Прерываем кадр, дальше работает анимация
            }
        }

        // --- 2. ЕСЛИ ИДЕТ АНИМАЦИЯ РАЗБЛОКИРОВКИ ---
        if (_isUnlockAnimating)
        {
            // Мы не управляем позицией X (это делает Tween), но мы должны обновлять:
            // 1. Позицию Y (если карта еще падает)
            // 2. Вращение (зависит от текущего X)
            // 3. Визуал текста
            
            float currentX = _rectTransform.anchoredPosition.x;
            _rectTransform.anchoredPosition = new Vector2(currentX, _currentVerticalOffset);
            
            float rot = -currentX * 0.05f;
            _rectTransform.rotation = Quaternion.Euler(0, 0, rot + _currentAngularOffset);
            
            UpdateVisuals(currentX); // Обновляем текст, чтобы он появился в конце замаха
            return;
        }

        // --- 3. ОБЫЧНОЕ ДВИЖЕНИЕ ---
        
        // Автоматическое снятие лока при возврате в центр
        if (_safetyLock && Mathf.Abs(rawDiff) < unlockDistance)
        {
            _safetyLock = false;
        }

        // Ограничение движения
        float currentLimit = _safetyLock ? lockedLimit : movementLimit;
        float appliedDiff = Mathf.Clamp(rawDiff, -currentLimit, currentLimit);

        // Физика
        float smoothX = Mathf.Lerp(_rectTransform.anchoredPosition.x, appliedDiff, Time.deltaTime * 20f);
        _rectTransform.anchoredPosition = new Vector2(smoothX, _currentVerticalOffset);

        float mouseRotation = -smoothX * 0.05f;
        _rectTransform.rotation = Quaternion.Euler(0, 0, mouseRotation + _currentAngularOffset);

        UpdateVisuals(appliedDiff);
        
        // Клик для выбора (Только если разблокировано)
        if (_isInteractable && !_safetyLock)
        {
            HandleInput(appliedDiff);
        }
    }

        // Тот самый "Маятник"
    void TriggerForceUnlock(float targetMouseX)
    {
        _isUnlockAnimating = true; // Отбираем контроль у Update
        _safetyLock = false;       // Снимаем логический замок

        // Определяем направление отката (противоположное мыши)
        // Если мышь справа (>0), откат влево. И наоборот.
        // Откат небольшой, например 20 пикселей в противоположную сторону.
        float recoilX = (targetMouseX > 0) ? -20f : 20f;
        
        // Финальная точка - это позиция мыши (но в пределах лимита)
        float finalX = Mathf.Clamp(targetMouseX, -movementLimit, movementLimit);

        // Создаем последовательность
        Sequence seq = DOTween.Sequence();
        
        // 1. Откат к центру/назад (быстро) -> "1-2"
        seq.Append(_rectTransform.DOAnchorPosX(recoilX, 0.1f).SetEase(Ease.OutQuad));
        
        // 2. Рывок к мыши (сочно) -> "5-6"
        seq.Append(_rectTransform.DOAnchorPosX(finalX, 0.25f).SetEase(Ease.OutBack));

        seq.OnComplete(() => 
        {
            _isUnlockAnimating = false; // Возвращаем контроль игроку
            // Теперь карта там, где курсор, и игрок может кликнуть еще раз для выбора
        });
    }

    void UpdateVisuals(float diff)
    {
        // Скрываем если падает ИЛИ если ЗАБЛОКИРОВАНО (но не во время анимации разблокировки!)
        if (_currentVerticalOffset > 150f || (_safetyLock && !_isUnlockAnimating)) 
        {
            if (actionText && actionText.gameObject.activeSelf) actionText.gameObject.SetActive(false);
            GameManager.Instance.ResetHighlights();
            return;
        }

        float absDiff = Mathf.Abs(diff);
        
        // Deadzone
        if (absDiff < lockedLimit + 10f)
        {
             if (actionText && actionText.gameObject.activeSelf) actionText.gameObject.SetActive(false);
             GameManager.Instance.ResetHighlights();
             return;
        }

        if (actionText && !actionText.gameObject.activeSelf) actionText.gameObject.SetActive(true);

        bool isRight = diff > 0;
        actionText.text = isRight ? CurrentData.rightChoiceText : CurrentData.leftChoiceText;

        float fadeRange = 150f;
        float alpha = Mathf.Clamp01((absDiff - (lockedLimit + 10f)) / fadeRange);
        
        Color targetColor = normalColor;
        
        float progress = absDiff / choiceThreshold;
        float clampedProgress = Mathf.Clamp01(progress);

        float targetScale = Mathf.Lerp(minScale, maxScale, clampedProgress);
        if (progress > 1.0f) targetScale += Mathf.Sin(Time.time * 20f) * 0.1f;
        _textRectTransform.localScale = Vector3.one * targetScale;

        float baseAngle = isRight ? -5f : 5f;
        float currentShake = Mathf.Sin(Time.time * shakeSpeed) * (shakeAngle * clampedProgress);
        _textRectTransform.localRotation = Quaternion.Euler(0, 0, baseAngle + currentShake);

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
        
        targetColor.a = alpha;
        actionText.color = targetColor;
    }

    void HandleInput(float diff)
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (diff > choiceThreshold) MakeChoice(true);
            else if (diff < -choiceThreshold) MakeChoice(false);
        }
    }

void MakeChoice(bool isRight)
    {
        _isLocked = true;
        _isInteractable = false;
        
        if (actionText) actionText.gameObject.SetActive(false);

        if (isRight) GameManager.Instance.ApplyCardEffect(CurrentData.rightCrown, CurrentData.rightChurch, CurrentData.rightMob, CurrentData.rightPlague);
        else GameManager.Instance.ApplyCardEffect(CurrentData.leftCrown, CurrentData.leftChurch, CurrentData.leftMob, CurrentData.leftPlague);

        float endX = isRight ? 1500f : -1500f;
        float endRotation = isRight ? -45f : 45f;

        Sequence seq = DOTween.Sequence();
        seq.Append(_rectTransform.DOAnchorPosX(endX, 0.4f).SetEase(Ease.InBack));
        seq.Join(_rectTransform.DORotate(new Vector3(0, 0, endRotation), 0.4f));

        seq.OnComplete(() => 
        {
            GameManager.Instance.OnCardAnimationComplete();
        });
    }
}
