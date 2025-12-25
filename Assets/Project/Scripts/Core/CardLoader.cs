// CardLoader.cs

using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class CardJsonData
{
    public string id;
    public string characterName;
    public string characterId;      // ID персонажа для системы отношений
    public string dialogueText;
    public string leftChoice;
    public string rightChoice;
    
    // Новые поля
    public string suit;             // "spades", "hearts", "diamonds", "clubs", "joker", "none"
    public string rarity;           // "common", "uncommon", "rare", "epic", "legendary"
    public int minShuffle;          // Минимальная Тасовка для появления
    public bool requiresUnlock;     // Требует разблокировки?
    
    // Ресурсы: [Армия, Народ, Казна, Хаос]
    public int[] leftStats;
    public int[] rightStats;
    
    // Цепочки
    public string leftNextCardId;
    public string rightNextCardId;
    
    // Флаги
    public bool isShuffleTransition;
    public bool isJokerCard;
}

[System.Serializable]
public class CardCollection
{
    public CardJsonData[] cards;
}

public class CardLoader : MonoBehaviour
{
    public string jsonFileName = "Cards/cards"; // Имя файла без расширения в папке Resources
    
    // Этот метод будет вызываться из GameManager
    public List<CardData> LoadCardsFromJson()
    {
        List<CardData> loadedCards = new List<CardData>();

        // 1. Загружаем текст из Resources
        TextAsset jsonText = Resources.Load<TextAsset>(jsonFileName);
        
        if (jsonText == null)
        {
            Debug.LogError("Не найден файл JSON: " + jsonFileName);
            return loadedCards;
        }

        // 2. Парсим текст в объекты
        CardCollection collection = JsonUtility.FromJson<CardCollection>(jsonText.text);

        // 3. Конвертируем JSON-объекты в наши ScriptableObject (CardData)
        foreach (CardJsonData jsonData in collection.cards)
        {
            // Создаем экземпляр ScriptableObject в памяти
            CardData newCard = ScriptableObject.CreateInstance<CardData>();
            
            // Основные поля
            newCard.name = jsonData.id;
            newCard.cardId = jsonData.id;
            newCard.characterName = jsonData.characterName;
            newCard.characterId = jsonData.characterId ?? jsonData.id;
            newCard.dialogueText = jsonData.dialogueText;
            
            newCard.leftChoiceText = jsonData.leftChoice;
            newCard.rightChoiceText = jsonData.rightChoice;
            
            // Парсим масть
            newCard.suit = ParseSuit(jsonData.suit);
            newCard.rarity = ParseRarity(jsonData.rarity);
            newCard.minShuffle = jsonData.minShuffle;
            newCard.requiresUnlock = jsonData.requiresUnlock;

            // Раскидываем статы: [♠Пики, ♥Черви, ♦Бубны, ♣Трефы]
            if (jsonData.leftStats != null && jsonData.leftStats.Length >= 4)
            {
                newCard.leftSpades = jsonData.leftStats[0];
                newCard.leftHearts = jsonData.leftStats[1];
                newCard.leftDiamonds = jsonData.leftStats[2];
                newCard.leftClubs = jsonData.leftStats[3];
            }

            if (jsonData.rightStats != null && jsonData.rightStats.Length >= 4)
            {
                newCard.rightSpades = jsonData.rightStats[0];
                newCard.rightHearts = jsonData.rightStats[1];
                newCard.rightDiamonds = jsonData.rightStats[2];
                newCard.rightClubs = jsonData.rightStats[3];
            }
            
            // Цепочки и флаги
            newCard.leftNextCardId = jsonData.leftNextCardId;
            newCard.rightNextCardId = jsonData.rightNextCardId;
            newCard.isShuffleTransition = jsonData.isShuffleTransition;
            newCard.isJokerCard = jsonData.isJokerCard;
            
            // Спрайты грузим отдельно по имени
            // TODO: Resources.Load<Sprite>("Sprites/" + jsonData.characterId);
            
            loadedCards.Add(newCard);
        }

        Debug.Log($"[CardLoader] Загружено карт: {loadedCards.Count}");
        return loadedCards;
    }
    
    // Парсинг масти из строки
    CardSuit ParseSuit(string suitStr)
    {
        if (string.IsNullOrEmpty(suitStr)) return CardSuit.None;
        
        switch (suitStr.ToLower())
        {
            case "spades": return CardSuit.Spades;
            case "hearts": return CardSuit.Hearts;
            case "diamonds": return CardSuit.Diamonds;
            case "clubs": return CardSuit.Clubs;
            case "joker": return CardSuit.Joker;
            default: return CardSuit.None;
        }
    }
    
    // Парсинг редкости из строки
    CardRarity ParseRarity(string rarityStr)
    {
        if (string.IsNullOrEmpty(rarityStr)) return CardRarity.Common;
        
        switch (rarityStr.ToLower())
        {
            case "common": return CardRarity.Common;
            case "uncommon": return CardRarity.Uncommon;
            case "rare": return CardRarity.Rare;
            case "epic": return CardRarity.Epic;
            case "legendary": return CardRarity.Legendary;
            default: return CardRarity.Common;
        }
    }
}
