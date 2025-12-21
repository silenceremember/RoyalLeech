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

    [Header("Ресурсы (0-100)")]
    public int crown = 50;
    public int church = 50;
    public int mob = 50;
    public int plague = 50;

    [Header("UI Иконки (Filled Images)")]
    public Image crownIcon;
    public Image churchIcon;
    public Image mobIcon;
    public Image plagueIcon;
    
    [Header("UI Текст")]
    public TextMeshProUGUI dayText;

    [Header("Настройки Визуала")]
    public Color normalColor = Color.white;    // Обычный цвет иконки
    public Color highlightColor = Color.yellow; // Цвет предсказания

    private int _currentDay = 1;

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

        // 3. Запоминаем базовый цвет иконок
        if (crownIcon) normalColor = crownIcon.color;
        
        // 4. Обновляем UI (статы и день)
        UpdateUI();

        // 5. Запускаем игру (Спавн карт)
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

        int randomIndex = Random.Range(0, _activeDeck.Count);
        CardData card = _activeDeck[randomIndex];
        
        // Удаляем из текущей, чтобы не повторялась сразу
        _activeDeck.RemoveAt(randomIndex);
        
        return card;
    }

    // Применение эффектов выбора
    public void ApplyCardEffect(int dCrown, int dChurch, int dMob, int dPlague)
    {
        // Ограничиваем статы от 0 до 100
        crown = Mathf.Clamp(crown + dCrown, 0, 100);
        church = Mathf.Clamp(church + dChurch, 0, 100);
        mob = Mathf.Clamp(mob + dMob, 0, 100);
        plague = Mathf.Clamp(plague + dPlague, 0, 100);

        if (CheckGameOver()) return;

        _currentDay++;
        UpdateUI();
    }

    void UpdateUI()
    {
        // Обновляем заполнение иконок
        if (crownIcon) crownIcon.fillAmount = crown / 100f;
        if (churchIcon) churchIcon.fillAmount = church / 100f;
        if (mobIcon) mobIcon.fillAmount = mob / 100f;
        if (plagueIcon) plagueIcon.fillAmount = plague / 100f;

        if (dayText != null) dayText.text = "День " + _currentDay;
    }

    // Подсветка иконок (Предсказание)
    public void HighlightResources(int dCrown, int dChurch, int dMob, int dPlague)
    {
        // Красим иконку, если её ресурс изменится
        if (crownIcon) crownIcon.color = (dCrown != 0) ? highlightColor : normalColor;
        if (churchIcon) churchIcon.color = (dChurch != 0) ? highlightColor : normalColor;
        if (mobIcon) mobIcon.color = (dMob != 0) ? highlightColor : normalColor;
        if (plagueIcon) plagueIcon.color = (dPlague != 0) ? highlightColor : normalColor;
    }

    // Сброс цветов
    public void ResetHighlights()
    {
        if (crownIcon) crownIcon.color = normalColor;
        if (churchIcon) churchIcon.color = normalColor;
        if (mobIcon) mobIcon.color = normalColor;
        if (plagueIcon) plagueIcon.color = normalColor;
    }

    bool CheckGameOver()
    {
        // Упрощенная проверка смерти (в будущем здесь будет вызов экрана GameOver)
        if (crown <= 0 || crown >= 100) { Debug.Log("Game Over: Crown"); return true; }
        if (church <= 0 || church >= 100) { Debug.Log("Game Over: Church"); return true; }
        if (mob <= 0 || mob >= 100) { Debug.Log("Game Over: Mob"); return true; }
        if (plague >= 100) { Debug.Log("Game Over: Plague"); return true; }

        return false;
    }
}