// GameManager.cs

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using DG.Tweening; // Нужно для таймеров (DOVirtual)

public class GameManager : MonoBehaviour
{
    // Синглтон
    public static GameManager Instance;

    [Header("Система Карт")]
    public CardDisplay cardTemplate; // Ссылка на объект-шаблон в сцене
    public CardLoader cardLoader;    // Ссылка на загрузчик JSON

    private CardDisplay _activeCard;

    [Header("Данные")]
    public List<CardData> allCards = new List<CardData>();
    private List<CardData> _activeDeck;

    [Header("Ресурсы (0-100) — Четыре Масти")]
    [Tooltip("♠ Пики — Армия/Клинки")]
    public int spades = 50;
    [Tooltip("♥ Черви — Народ/Алый Двор")]
    public int hearts = 50;
    [Tooltip("♦ Бубны — Казна/Златая Гильдия")]
    public int diamonds = 50;
    [Tooltip("♣ Трефы — Хаос/Дикий Рост")]
    public int clubs = 50;

    [Header("UI Icons (LiquidFillIcon)")]
    public LiquidFillIcon spadesIcon;
    public LiquidFillIcon heartsIcon;
    public LiquidFillIcon diamondsIcon;
    public LiquidFillIcon clubsIcon;
    
    [Header("UI Текст")]
    public TextMeshProUGUI shuffleText;

    [Header("Настройки Визуала")]
    public Color normalColor = Color.white;
    public Color highlightColor = Color.yellow;
    
    [Header("Критические Уровни")]
    [Tooltip("Ниже этого значения включается Critical Pulse")]
    public int criticalLowThreshold = 20;
    [Tooltip("Выше этого значения включается Critical Pulse")]
    public int criticalHighThreshold = 80;

    // Прогрессия
    private int _currentShuffle = 1;
    private int _cardsInShuffle = 0;
    
    [Header("Настройки Прогрессии")]
    [Tooltip("Сколько карт до перехода к следующей Тасовке")]
    public int cardsPerShuffle = 15;

    void Awake()
    {
        // Инициализация Синглтона
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // 1. Загрузка Карт из JSON
        if (cardLoader != null)
        {
            var jsonCards = cardLoader.LoadCardsFromJson();
            if (jsonCards != null && jsonCards.Count > 0)
            {
                allCards.AddRange(jsonCards);
                Debug.Log($"[GameManager] Загружено {jsonCards.Count} карт.");
            }
        }

        if (allCards.Count == 0)
        {
            Debug.LogError("[GameManager] ОШИБКА: Колода пуста! Проверь JSON файл.");
            return; // Останавливаем выполнение, чтобы не было ошибок дальше
        }
        
        // 2. Инициализация колоды
        _activeDeck = new List<CardData>(allCards);

        // 3. Обновляем UI (статы и тасовку)
        UpdateUI();

        // 4. Запускаем игру (Спавн карт)
        InitCards();
    }

    void InitCards()
    {
        if (cardTemplate == null) return;

        // 1. Создаем единственную карту
        cardTemplate.gameObject.SetActive(true);
        _activeCard = Instantiate(cardTemplate, cardTemplate.transform.parent);
        _activeCard.name = "Card_Active";
        cardTemplate.gameObject.SetActive(false);

        // 2. Получаем данные
        CardData data = GetNextCardData();

        // 3. Настраиваем как "Скрытую наверху" (isFront = false)
        // Это телепортирует её на hiddenY
        _activeCard.Setup(data, false);

        // 4. Падение через паузу
        DOVirtual.DelayedCall(0.5f, () => 
        {
            _activeCard.AnimateToFront();
        });
    }

    public void OnCardAnimationComplete()
    {
        // 1. Получаем новые данные
        CardData newData = GetNextCardData();

        // 2. Перезагружаем ТУ ЖЕ карту
        // Setup с false мгновенно телепортирует её наверх и обновляет текст/картинку
        _activeCard.Setup(newData, false);

        // 3. Сразу роняем её вниз
        // (Без задержки, чтобы динамика была быстрее, или с минимальной)
        _activeCard.AnimateToFront();
    }

    // Получение следующей карты из колоды с авто-решаффлом
    CardData GetNextCardData()
    {
        // Если колода кончилась - наполняем её заново
        if (_activeDeck == null || _activeDeck.Count == 0)
        {
            Debug.Log("[GameManager] Колода закончилась. Перемешиваем сброс.");
            _activeDeck = new List<CardData>(allCards);
        }

        // TODO: Фильтрация карт по текущей Тасовке (прогрессия)
        // Чем выше _currentShuffle, тем "серьёзнее" карты

        int randomIndex = Random.Range(0, _activeDeck.Count);
        CardData card = _activeDeck[randomIndex];
        
        // Удаляем из текущей, чтобы не повторялась сразу
        _activeDeck.RemoveAt(randomIndex);
        
        return card;
    }

    // Применение эффектов выбора
    public void ApplyCardEffect(int dSpades, int dHearts, int dDiamonds, int dClubs)
    {
        // Сохраняем старые значения для определения gain/loss
        int oldSpades = spades;
        int oldHearts = hearts;
        int oldDiamonds = diamonds;
        int oldClubs = clubs;
        
        // Ограничиваем статы от 0 до 100
        spades = Mathf.Clamp(spades + dSpades, 0, 100);
        hearts = Mathf.Clamp(hearts + dHearts, 0, 100);
        diamonds = Mathf.Clamp(diamonds + dDiamonds, 0, 100);
        clubs = Mathf.Clamp(clubs + dClubs, 0, 100);

        if (CheckGameOver()) return;

        // Увеличиваем счётчик карт в текущей тасовке
        _cardsInShuffle++;
        
        // Проверяем переход к новой Тасовке
        if (_cardsInShuffle >= cardsPerShuffle)
        {
            _currentShuffle++;
            _cardsInShuffle = 0;
            OnNewShuffle();
        }
        
        // JUICY анимации для каждого изменённого ресурса
        ApplyJuicyEffect(spadesIcon, oldSpades, spades, dSpades);
        ApplyJuicyEffect(heartsIcon, oldHearts, hearts, dHearts);
        ApplyJuicyEffect(diamondsIcon, oldDiamonds, diamonds, dDiamonds);
        ApplyJuicyEffect(clubsIcon, oldClubs, clubs, dClubs);
        
        // Обновляем текст тасовки
        if (shuffleText != null) shuffleText.text = "Тасовка " + _currentShuffle;
    }
    
    void ApplyJuicyEffect(LiquidFillIcon icon, int oldValue, int newValue, int delta)
    {
        if (icon == null) return;
        
        float newFill = newValue / 100f;
        int absDelta = Mathf.Abs(delta);
        
        if (delta > 0)
        {
            // GAIN - punch + glow (minor or major based on delta)
            icon.PlayGainEffect(newFill, absDelta);
        }
        else if (delta < 0)
        {
            // LOSS - shake + shrink punch (minor or major based on delta)
            icon.PlayLossEffect(newFill, absDelta);
        }
        else
        {
            // No change - just animate fill
            icon.AnimateFillTo(newFill);
        }
        
        // Critical glow is handled automatically by UpdateCriticalGlow in LiquidFillIcon.Update()
    }
    
    void CheckCriticalLevel(LiquidFillIcon icon, int value)
    {
        if (icon == null) return;
        
        if (value <= criticalLowThreshold || value >= criticalHighThreshold)
        {
            // Включаем пульс предупреждения
            icon.StartCriticalPulse();
        }
        else
        {
            // Выключаем пульс
            icon.StopCriticalPulse();
        }
    }

    void UpdateUI()
    {
        // Обновляем заполнение иконок (без анимации - для инициализации)
        if (spadesIcon) spadesIcon.SetFill(spades / 100f);
        if (heartsIcon) heartsIcon.SetFill(hearts / 100f);
        if (diamondsIcon) diamondsIcon.SetFill(diamonds / 100f);
        if (clubsIcon) clubsIcon.SetFill(clubs / 100f);

        if (shuffleText != null) shuffleText.text = "Тасовка " + _currentShuffle;
    }

    // Вызывается при переходе к новой Тасовке
    void OnNewShuffle()
    {
        Debug.Log($"[GameManager] Новая Тасовка #{_currentShuffle}! Карты становятся серьёзнее...");
        // TODO: Здесь можно показать особую карту-разделитель (Шут комментирует)
        // TODO: Изменить пул доступных карт
        // TODO: Возможно изменить визуал (фон темнеет?)
    }

    // Подсветка иконок (Предсказание) - magnitude + progress based, без подсказки gain/loss
    // Вызывается постоянно во время свайпа с текущим swipeProgress
    public void HighlightResources(int dSpades, int dHearts, int dDiamonds, int dClubs, float swipeProgress, Vector3 cardWorldPosition)
    {
        const float maxMagnitude = 30f; // Normalize against this
        
        // Set card position and magnitude for all icons (3D look-at effect)
        // Bigger magnitude = more intense stare at the card
        if (spadesIcon) { 
            spadesIcon.SetCardPosition(cardWorldPosition);
            spadesIcon.SetMagnitude(Mathf.Abs(dSpades) / maxMagnitude);
        }
        if (heartsIcon) { 
            heartsIcon.SetCardPosition(cardWorldPosition);
            heartsIcon.SetMagnitude(Mathf.Abs(dHearts) / maxMagnitude);
        }
        if (diamondsIcon) { 
            diamondsIcon.SetCardPosition(cardWorldPosition);
            diamondsIcon.SetMagnitude(Mathf.Abs(dDiamonds) / maxMagnitude);
        }
        if (clubsIcon) { 
            clubsIcon.SetCardPosition(cardWorldPosition);
            clubsIcon.SetMagnitude(Mathf.Abs(dClubs) / maxMagnitude);
        }
        
        // Показываем magnitude-based preview на иконках, которые изменятся
        // swipeProgress: 0 = far from center, 1 = at choice threshold
        HighlightIcon(spadesIcon, Mathf.Abs(dSpades), swipeProgress);
        HighlightIcon(heartsIcon, Mathf.Abs(dHearts), swipeProgress);
        HighlightIcon(diamondsIcon, Mathf.Abs(dDiamonds), swipeProgress);
        HighlightIcon(clubsIcon, Mathf.Abs(dClubs), swipeProgress);
    }
    
    void HighlightIcon(LiquidFillIcon icon, int magnitude, float swipeProgress)
    {
        if (icon == null) return;
        
        if (magnitude > 0 && swipeProgress > 0.1f)
        {
            // Preview effect - intensity based on swipeProgress AND magnitude (Minor/Normal/Major)
            icon.PlayHighlightPreview(swipeProgress, magnitude);
        }
        else
        {
            icon.StopHighlightPreview();
        }
    }

    // Сброс подсветки
    public void ResetHighlights()
    {
        // Reset tracking and preview
        if (spadesIcon) { spadesIcon.StopHighlightPreview(); spadesIcon.ResetCardTracking(); }
        if (heartsIcon) { heartsIcon.StopHighlightPreview(); heartsIcon.ResetCardTracking(); }
        if (diamondsIcon) { diamondsIcon.StopHighlightPreview(); diamondsIcon.ResetCardTracking(); }
        if (clubsIcon) { clubsIcon.StopHighlightPreview(); clubsIcon.ResetCardTracking(); }
    }

    bool CheckGameOver()
    {
        // Проверка смерти с информацией о типе
        // TODO: Здесь будут уникальные концовки для каждого типа смерти
        
        if (spades <= 0) { Debug.Log("Game Over: ♠ Spades = 0 (НОЧЬ ДЛИННЫХ КЛИНКОВ)"); return true; }
        if (spades >= 100) { Debug.Log("Game Over: ♠ Spades = 100 (МАРШ НА ТРОН)"); return true; }
        
        if (hearts <= 0) { Debug.Log("Game Over: ♥ Hearts = 0 (МОЛЧАНИЕ ПЛОЩАДЕЙ)"); return true; }
        if (hearts >= 100) { Debug.Log("Game Over: ♥ Hearts = 100 (КАРНАВАЛ ОБОЖАНИЯ)"); return true; }
        
        if (diamonds <= 0) { Debug.Log("Game Over: ♦ Diamonds = 0 (БАНКРОТСТВО КОРОНЫ)"); return true; }
        if (diamonds >= 100) { Debug.Log("Game Over: ♦ Diamonds = 100 (ЗОЛОТАЯ ТЮРЬМА)"); return true; }
        
        if (clubs <= 0) { Debug.Log("Game Over: ♣ Clubs = 0 (ПОСЛЕДНИЙ ЛИСТ)"); return true; }
        if (clubs >= 100) { Debug.Log("Game Over: ♣ Clubs = 100 (ЗЕЛЁНЫЙ ПОТОП)"); return true; }

        return false;
    }
}
