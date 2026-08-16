using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Cards
{
    [CreateAssetMenu(fileName = "AI Deck Config", menuName = "AI/AI Deck Config")]
    public sealed class AiDeckConfig : ScriptableObject
    {
        [Serializable]
        public sealed class CardEntry
        {
            public GameObject CardPrefab;
            [Min(1)] public int Count = 1;
        }

        [SerializeField] private string deckId;
        [SerializeField] private List<CardEntry> cards = new();
        [SerializeField, Min(0)] private int initialHandCount = AiCardHand.MaxCards;
        [SerializeField, Min(1)] private int drawIntervalTurns = 5;
        [SerializeField, Min(0)] private int drawCount = 1;
        [SerializeField, Range(1, AiCardHand.MaxCards)] private int maxHandCount = AiCardHand.MaxCards;
        [SerializeField, Min(0)] private int remainingUsesPerCard = 1;
        [SerializeField] private bool reshuffleUsedPileWhenEmpty;

        public string DeckId => string.IsNullOrWhiteSpace(deckId) ? name : deckId;
        public IReadOnlyList<CardEntry> Cards => cards;
        public int InitialHandCount => Mathf.Clamp(initialHandCount, 0, MaxHandCount);
        public int DrawIntervalTurns => Mathf.Max(1, drawIntervalTurns);
        public int DrawCount => Mathf.Max(0, drawCount);
        public int MaxHandCount => Mathf.Clamp(maxHandCount, 1, AiCardHand.MaxCards);
        public int RemainingUsesPerCard => Mathf.Max(0, remainingUsesPerCard);
        public bool ReshuffleUsedPileWhenEmpty => reshuffleUsedPileWhenEmpty;

        private void OnValidate()
        {
            initialHandCount = Mathf.Max(0, initialHandCount);
            drawIntervalTurns = Mathf.Max(1, drawIntervalTurns);
            drawCount = Mathf.Max(0, drawCount);
            maxHandCount = Mathf.Clamp(maxHandCount, 1, AiCardHand.MaxCards);
            remainingUsesPerCard = Mathf.Max(0, remainingUsesPerCard);

            if (cards == null)
                cards = new List<CardEntry>();

            foreach (CardEntry entry in cards)
            {
                if (entry != null)
                    entry.Count = Mathf.Max(1, entry.Count);
            }
        }
    }

    public sealed class AiDeckRuntime
    {
        private readonly List<GameObject> drawPile = new();
        private readonly List<GameObject> usedPile = new();
        private AiDeckConfig config;
        private bool runtimeConfigured;
        private int runtimeDrawIntervalTurns;
        private int runtimeDrawCount;
        private int runtimeMaxHandCount;
        private int runtimeRemainingUsesPerCard;
        private bool runtimeReshuffleUsedPileWhenEmpty;
        private int turnsSinceLastDraw;

        public bool IsConfigured => config != null || runtimeConfigured;
        public int DrawPileCount => drawPile.Count;
        public int UsedPileCount => usedPile.Count;
        public int RemainingUsesPerCard => config != null ? config.RemainingUsesPerCard : runtimeRemainingUsesPerCard;
        private int DrawIntervalTurns => config != null ? config.DrawIntervalTurns : runtimeDrawIntervalTurns;
        private int DrawCount => config != null ? config.DrawCount : runtimeDrawCount;
        private int MaxHandCount => config != null ? config.MaxHandCount : runtimeMaxHandCount;
        private bool ReshuffleUsedPileWhenEmpty => config != null ? config.ReshuffleUsedPileWhenEmpty : runtimeReshuffleUsedPileWhenEmpty;

        public void Initialize(AiDeckConfig deckConfig, List<GameObject> hand)
        {
            config = deckConfig;
            runtimeConfigured = false;
            turnsSinceLastDraw = 0;
            drawPile.Clear();
            usedPile.Clear();
            hand?.Clear();

            if (config == null)
                return;

            foreach (AiDeckConfig.CardEntry entry in config.Cards)
            {
                if (entry == null || entry.CardPrefab == null)
                    continue;

                int count = Mathf.Max(1, entry.Count);
                for (int i = 0; i < count; i++)
                    drawPile.Add(entry.CardPrefab);
            }

            Shuffle(drawPile);
            DrawCards(hand, config.InitialHandCount);
        }

        public void InitializeRuntimeDeck(
            IEnumerable<GameObject> cards,
            List<GameObject> hand,
            int initialHandCount,
            int drawIntervalTurns,
            int drawCount,
            int maxHandCount,
            int remainingUsesPerCard,
            bool reshuffleUsedPileWhenEmpty)
        {
            config = null;
            runtimeConfigured = true;
            runtimeDrawIntervalTurns = Mathf.Max(1, drawIntervalTurns);
            runtimeDrawCount = Mathf.Max(0, drawCount);
            runtimeMaxHandCount = Mathf.Clamp(maxHandCount, 1, AiCardHand.MaxCards);
            runtimeRemainingUsesPerCard = Mathf.Max(0, remainingUsesPerCard);
            runtimeReshuffleUsedPileWhenEmpty = reshuffleUsedPileWhenEmpty;
            turnsSinceLastDraw = 0;
            drawPile.Clear();
            usedPile.Clear();
            hand?.Clear();

            if (cards != null)
            {
                foreach (GameObject card in cards)
                {
                    if (card != null)
                        drawPile.Add(card);
                }
            }

            Shuffle(drawPile);
            DrawCards(hand, Mathf.Clamp(initialHandCount, 0, runtimeMaxHandCount));
        }

        public int HandleAiTurnStarted(List<GameObject> hand)
        {
            if (!IsConfigured)
                return 0;

            turnsSinceLastDraw++;
            if (turnsSinceLastDraw < DrawIntervalTurns)
                return 0;

            if (hand != null && hand.Count >= MaxHandCount)
                return 0;

            int drawn = DrawCards(hand, DrawCount);
            if (drawn > 0)
                turnsSinceLastDraw = 0;

            return drawn;
        }

        public void RecordUsedCard(GameObject cardPrefab)
        {
            if (!IsConfigured || cardPrefab == null)
                return;

            usedPile.Add(cardPrefab);
        }

        private int DrawCards(List<GameObject> hand, int count)
        {
            if (!IsConfigured || hand == null || count <= 0)
                return 0;

            int drawn = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(8, drawPile.Count + usedPile.Count + count + 8);
            var skippedDuplicates = new List<GameObject>();

            while (drawn < count && hand.Count < MaxHandCount && attempts < maxAttempts)
            {
                attempts++;

                if (!TryDrawOne(out GameObject card))
                    break;

                if (AiCardHand.ContainsSameCard(hand, card))
                {
                    skippedDuplicates.Add(card);
                    continue;
                }

                hand.Add(card);
                drawn++;
            }

            if (skippedDuplicates.Count > 0)
            {
                drawPile.AddRange(skippedDuplicates);
                Shuffle(drawPile);
            }

            return drawn;
        }

        private bool TryDrawOne(out GameObject card)
        {
            card = null;

            if (drawPile.Count == 0 && ReshuffleUsedPileWhenEmpty && usedPile.Count > 0)
            {
                drawPile.AddRange(usedPile);
                usedPile.Clear();
                Shuffle(drawPile);
            }

            while (drawPile.Count > 0)
            {
                int lastIndex = drawPile.Count - 1;
                card = drawPile[lastIndex];
                drawPile.RemoveAt(lastIndex);
                if (card != null)
                    return true;
            }

            return false;
        }

        private static void Shuffle(List<GameObject> cards)
        {
            if (cards == null)
                return;

            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }

    public sealed class AiCardHand : MonoBehaviour
    {
        public const int MaxCards = 4;

        [SerializeField] private List<GameObject> startingCards = new();
        [SerializeField] private int defaultRemainingUses = 1;
        [SerializeField] private bool useEditorConfiguredStartingCards;
        [SerializeField] private List<string> editorStartingCardPrefabPaths = new();
        [SerializeField] private bool useDeckRuntime = true;
        [SerializeField] private AiDeckConfig deckConfigOverride;
        [SerializeField] private bool useMapDeckConfig = true;
        [SerializeField] private bool useAllSupportedCardsWhenDeckMissing = true;
        [SerializeField, Min(0)] private int fallbackInitialHandCount = MaxCards;
        [SerializeField, Min(1)] private int fallbackDrawIntervalTurns = 5;
        [SerializeField, Min(0)] private int fallbackDrawCount = 1;
        [SerializeField] private bool fallbackReshuffleUsedPileWhenEmpty;
        [SerializeField] private bool logDeckState;

        private readonly List<GameObject> runtimeCards = new();
        private readonly AiDeckRuntime deckRuntime = new();
        private bool runtimeCardsInitialized;
        private bool deckRuntimeInitialized;
        private bool debugHandOverrideActive;
        private string activeDeckId;
        private int lastHandledAiTurn = -1;
        private int version;

        public IReadOnlyList<GameObject> AvailableCards => CurrentCards;
        public int DefaultRemainingUses => deckRuntimeInitialized && deckRuntime.IsConfigured
            ? deckRuntime.RemainingUsesPerCard
            : Mathf.Max(0, defaultRemainingUses);
        public int Version => version;
        public string ActiveDeckId => activeDeckId;
        public int DrawPileCount => deckRuntimeInitialized && deckRuntime.IsConfigured ? deckRuntime.DrawPileCount : 0;
        public int UsedPileCount => deckRuntimeInitialized && deckRuntime.IsConfigured ? deckRuntime.UsedPileCount : 0;
        public static event Action<AiCardHand> AnyChanged;
        public event Action Changed;

        private void Awake()
        {
            TrimStartingCards();
            RebuildRuntimeCardsFromConfiguration();
        }

        private IReadOnlyList<GameObject> CurrentCards
        {
            get
            {
                if (Application.isPlaying && runtimeCardsInitialized)
                    return runtimeCards;

                return startingCards;
            }
        }

#if UNITY_EDITOR
        private List<GameObject> ResolveEditorConfiguredCards()
        {
            var resolvedCards = new List<GameObject>();

            if (!useEditorConfiguredStartingCards)
                return resolvedCards;

            foreach (string path in editorStartingCardPrefabPaths)
            {
                if (resolvedCards.Count >= MaxCards)
                    break;

                if (string.IsNullOrWhiteSpace(path))
                    continue;

                GameObject cardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (cardPrefab != null)
                {
                    AddUniqueCard(resolvedCards, cardPrefab);
                    continue;
                }

                Debug.LogWarning($"[AI Card Hand] Could not load editor card prefab at '{path}'.");
            }

            return resolvedCards;
        }
#endif

        public void RebuildRuntimeCardsFromConfiguration()
        {
            runtimeCards.Clear();

#if UNITY_EDITOR
            List<GameObject> configuredCards = ResolveEditorConfiguredCards();
            IReadOnlyList<GameObject> source = configuredCards.Count > 0 ? configuredCards : startingCards;
#else
            IReadOnlyList<GameObject> source = startingCards;
#endif

            foreach (GameObject card in source)
            {
                if (runtimeCards.Count >= MaxCards)
                    break;

                AddUniqueCard(runtimeCards, card);
            }

            runtimeCardsInitialized = true;
            NotifyChanged();
        }

        public void ReplaceRuntimeCards(IEnumerable<GameObject> cards)
        {
            runtimeCards.Clear();

            if (cards != null)
            {
                foreach (GameObject card in cards)
                {
                    if (runtimeCards.Count >= MaxCards)
                        break;

                    AddUniqueCard(runtimeCards, card);
                }
            }

            runtimeCardsInitialized = true;
            debugHandOverrideActive = true;
            deckRuntimeInitialized = false;
            activeDeckId = null;
            NotifyChanged();
        }

        public void ClearDebugHandOverride()
        {
            if (!debugHandOverrideActive)
                return;

            debugHandOverrideActive = false;
            deckRuntimeInitialized = false;
            RebuildRuntimeCardsFromConfiguration();
        }

        public void PrepareForAiTurn(global::GameManager gameManager)
        {
            if (gameManager == null)
                return;

            if (lastHandledAiTurn == gameManager.CurrentTurn)
                return;

            lastHandledAiTurn = gameManager.CurrentTurn;

            if (!ShouldUseDeckRuntime())
                return;

            AiDeckConfig config = ResolveDeckConfig();
            if (config == null && !TryEnsureFallbackRuntimeDeck())
                return;

            if (config != null)
                EnsureDeckRuntime(config);

            int drawn = deckRuntime.HandleAiTurnStarted(runtimeCards);
            if (drawn > 0)
                NotifyChanged();

            if (logDeckState)
            {
                Debug.Log(
                    $"[AI Deck] turn={gameManager.CurrentTurn}, deck={activeDeckId}, hand={runtimeCards.Count}, " +
                    $"drawn={drawn}, drawPile={deckRuntime.DrawPileCount}, usedPile={deckRuntime.UsedPileCount}.");
            }
        }

        private void OnValidate()
        {
            defaultRemainingUses = Mathf.Max(0, defaultRemainingUses);
            fallbackInitialHandCount = Mathf.Clamp(fallbackInitialHandCount, 0, MaxCards);
            fallbackDrawIntervalTurns = Mathf.Max(1, fallbackDrawIntervalTurns);
            fallbackDrawCount = Mathf.Max(0, fallbackDrawCount);
            TrimStartingCards();
        }

        public bool Contains(global::CardDataSO cardSO)
        {
            if (cardSO == null)
                return false;

            foreach (GameObject cardPrefab in CurrentCards)
            {
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;

                if (cardData != null && cardData.DataSO == cardSO)
                    return true;
            }

            return false;
        }

        public bool TryFindByAiCardId(string aiCardId, out GameObject cardPrefab)
        {
            cardPrefab = null;

            if (string.IsNullOrWhiteSpace(aiCardId))
                return false;

            foreach (GameObject candidate in CurrentCards)
            {
                global::CardData cardData = candidate != null
                    ? candidate.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;

                if (dataSO != null &&
                    string.Equals(dataSO.AiCardId, aiCardId, System.StringComparison.OrdinalIgnoreCase))
                {
                    cardPrefab = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool Consume(global::CardDataSO cardSO)
        {
            if (cardSO == null)
                return false;

            List<GameObject> cards = Application.isPlaying && runtimeCardsInitialized
                ? runtimeCards
                : startingCards;

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject cardPrefab = cards[i];
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;

                if (cardData == null || !IsSameCardSO(cardData.DataSO, cardSO))
                    continue;

                cards.RemoveAt(i);
                RecordUsedCard(cardPrefab);
                NotifyChanged();
                return true;
            }

            return false;
        }

        public bool ConsumeAiCardId(string aiCardId)
        {
            if (string.IsNullOrWhiteSpace(aiCardId))
                return false;

            List<GameObject> cards = Application.isPlaying && runtimeCardsInitialized
                ? runtimeCards
                : startingCards;

            for (int i = 0; i < cards.Count; i++)
            {
                GameObject cardPrefab = cards[i];
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;

                if (dataSO == null ||
                    !string.Equals(dataSO.AiCardId, aiCardId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cards.RemoveAt(i);
                RecordUsedCard(cardPrefab);
                NotifyChanged();
                return true;
            }

            return false;
        }

        private void NotifyChanged()
        {
            version++;
            Changed?.Invoke();
            AnyChanged?.Invoke(this);
        }

        public bool ContainsAiCardId(string aiCardId)
        {
            if (string.IsNullOrWhiteSpace(aiCardId))
                return false;

            foreach (GameObject cardPrefab in CurrentCards)
            {
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;

                if (dataSO != null &&
                    string.Equals(dataSO.AiCardId, aiCardId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameCardSO(global::CardDataSO a, global::CardDataSO b)
        {
            if (a == null || b == null)
                return false;

            if (a == b)
                return true;

            return !string.IsNullOrWhiteSpace(a.AiCardId)
                && string.Equals(a.AiCardId, b.AiCardId, System.StringComparison.OrdinalIgnoreCase);
        }

        private void TrimStartingCards()
        {
            if (startingCards == null)
                startingCards = new List<GameObject>();

            RemoveDuplicateCards(startingCards);

            if (startingCards.Count > MaxCards)
                startingCards.RemoveRange(MaxCards, startingCards.Count - MaxCards);
        }

        private static void RemoveDuplicateCards(List<GameObject> cards)
        {
            if (cards == null)
                return;

            for (int i = cards.Count - 1; i >= 0; i--)
            {
                GameObject card = cards[i];
                if (card == null)
                {
                    cards.RemoveAt(i);
                    continue;
                }

                for (int j = 0; j < i; j++)
                {
                    if (IsSameCard(cards[j], card))
                    {
                        cards.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private static bool AddUniqueCard(List<GameObject> cards, GameObject card)
        {
            if (cards == null || card == null || ContainsSameCard(cards, card))
                return false;

            cards.Add(card);
            return true;
        }

        public static bool ContainsSameCard(IEnumerable<GameObject> cards, GameObject card)
        {
            if (cards == null || card == null)
                return false;

            foreach (GameObject existing in cards)
            {
                if (IsSameCard(existing, card))
                    return true;
            }

            return false;
        }

        public static bool IsSameCard(GameObject a, GameObject b)
        {
            if (a == null || b == null)
                return false;

            if (ReferenceEquals(a, b))
                return true;

            global::CardData aData = a.GetComponent<global::CardData>();
            global::CardData bData = b.GetComponent<global::CardData>();
            global::CardDataSO aSO = aData != null ? aData.DataSO : null;
            global::CardDataSO bSO = bData != null ? bData.DataSO : null;

            if (aSO == null || bSO == null)
                return false;

            if (aSO == bSO)
                return true;

            return !string.IsNullOrWhiteSpace(aSO.AiCardId)
                && string.Equals(aSO.AiCardId, bSO.AiCardId, System.StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldUseDeckRuntime()
        {
            return Application.isPlaying &&
                useDeckRuntime &&
                !debugHandOverrideActive &&
                !useEditorConfiguredStartingCards;
        }

        private AiDeckConfig ResolveDeckConfig()
        {
            if (deckConfigOverride != null)
                return deckConfigOverride;

            if (!useMapDeckConfig)
                return null;

            global::MapManager mapManager = global::MapManager.Instance;
            return mapManager != null ? mapManager.ResolveAiDeckConfig(mapManager.curMap) : null;
        }

        private void EnsureDeckRuntime(AiDeckConfig config)
        {
            string deckId = config != null ? config.DeckId : null;
            if (deckRuntimeInitialized && string.Equals(activeDeckId, deckId, StringComparison.Ordinal))
                return;

            runtimeCards.Clear();
            deckRuntime.Initialize(config, runtimeCards);
            runtimeCardsInitialized = true;
            deckRuntimeInitialized = config != null;
            activeDeckId = deckId;
            NotifyChanged();

            if (logDeckState && config != null)
            {
                Debug.Log(
                    $"[AI Deck] initialized deck={activeDeckId}, hand={runtimeCards.Count}, " +
                    $"drawPile={deckRuntime.DrawPileCount}, usedPile={deckRuntime.UsedPileCount}.");
            }
        }

        private bool TryEnsureFallbackRuntimeDeck()
        {
            if (!useAllSupportedCardsWhenDeckMissing)
                return false;

            string fallbackDeckId = ResolveFallbackRuntimeDeckId();
            if (deckRuntimeInitialized && string.Equals(activeDeckId, fallbackDeckId, StringComparison.Ordinal))
                return true;

            List<GameObject> fallbackCards = ResolveFallbackRuntimeDeckCards();
            if (fallbackCards.Count == 0)
                return false;

            runtimeCards.Clear();
            deckRuntime.InitializeRuntimeDeck(
                fallbackCards,
                runtimeCards,
                fallbackInitialHandCount,
                fallbackDrawIntervalTurns,
                fallbackDrawCount,
                MaxCards,
                defaultRemainingUses,
                fallbackReshuffleUsedPileWhenEmpty);
            runtimeCardsInitialized = true;
            deckRuntimeInitialized = true;
            activeDeckId = fallbackDeckId;
            NotifyChanged();

            if (logDeckState)
            {
                Debug.Log(
                    $"[AI Deck] initialized fallback deck={activeDeckId}, hand={runtimeCards.Count}, " +
                    $"drawPile={deckRuntime.DrawPileCount}, usedPile={deckRuntime.UsedPileCount}.");
            }

            return true;
        }

        private static string ResolveFallbackRuntimeDeckId()
        {
            global::MapManager mapManager = global::MapManager.Instance;
            if (mapManager != null && mapManager.curMap != null)
                return mapManager.GetRuntimeDeckKey(mapManager.curMap);

            return "runtime_all_supported_cards";
        }

        private static List<GameObject> ResolveFallbackRuntimeDeckCards()
        {
            global::MapManager mapManager = global::MapManager.Instance;
            if (mapManager != null && mapManager.curMap != null)
            {
                var nodeCards = new List<GameObject>();
                foreach (GameObject card in mapManager.ResolveAiRuntimeDeckCards(mapManager.curMap))
                {
                    if (card != null && !ContainsSameCard(nodeCards, card))
                        nodeCards.Add(card);
                }

                if (nodeCards.Count > 0)
                    return nodeCards;
            }

            return BuildAllSupportedCardPool();
        }

        private static List<GameObject> BuildAllSupportedCardPool()
        {
            var cards = new List<GameObject>();
            global::CardRandomizerManager manager = global::CardRandomizerManager.Instance;
            if (manager == null || manager.AllCards == null)
                return cards;

            foreach (GameObject cardPrefab in manager.AllCards)
            {
                global::CardData cardData = cardPrefab != null
                    ? cardPrefab.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;

                if (dataSO == null ||
                    !dataSO.AiSupported ||
                    string.IsNullOrWhiteSpace(dataSO.AiCardId) ||
                    dataSO.AiCategory == global::AiCardCategory.Unknown)
                {
                    continue;
                }

                cards.Add(cardPrefab);
            }

            return cards;
        }

        private void RecordUsedCard(GameObject cardPrefab)
        {
            if (deckRuntimeInitialized)
                deckRuntime.RecordUsedCard(cardPrefab);
        }
    }
}
