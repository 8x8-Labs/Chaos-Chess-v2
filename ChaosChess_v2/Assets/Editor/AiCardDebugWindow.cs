using System;
using System.Collections.Generic;
using System.Text;
using ChaosChess.AI.Domain;
using ChaosChess.AI.Fen;
using ChaosChess.Unity.AIIntegration.Cards;
using ChaosChess.Unity.AIIntegration.Mapping;
using ChaosChess.Unity.AIIntegration.Runtime;
using UnityEditor;
using UnityEngine;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;

public sealed class AiCardDebugWindow : EditorWindow
{
    private const string CardPrefabFolder = "Assets/Prefab/Skill";
    private const string PresetPrefix = "ChaosChess.AiCardDebugWindow.Preset.";
    private const int MaxHandCards = AiCardHand.MaxCards;

    [SerializeField] private CardLabRegistrySO registry;
    [SerializeField] private AiCardHand aiCardHand;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AiTurnController aiTurnController;
    [SerializeField] private int selectedCatalogIndex;
    [SerializeField] private int selectedHandIndex;
    [SerializeField] private string presetName = "default";
    [SerializeField] private int selectedTab;
    [SerializeField] private bool showSceneReferences;
    [SerializeField] private bool showPreset;
    [SerializeField] private bool showSelectedCardDetails;
    [SerializeField] private int allCardsBatchIndex;

    private readonly AiCardTargetPlanner targetPlanner = new AiCardTargetPlanner();
    private readonly AiCardExecutor cardExecutor = new AiCardExecutor();
    private readonly List<CardData> catalog = new List<CardData>();
    private readonly List<GameObject> handCards = new List<GameObject>();
    private readonly StringBuilder logBuilder = new StringBuilder(8192);
    private Vector2 scroll;
    private Vector2 catalogScroll;
    private Vector2 handScroll;
    private Vector2 handPageScroll;
    private int observedHandVersion = -1;
    private string lastReferenceWarning;
    private bool shouldAutoResolveSceneReferences = true;
    private AiCardHand subscribedHand;
    private bool autoScanDirty = true;
    private string lastAutoScanFingerprint;
    private double nextAutoScanAt;
    private static readonly string[] Tabs = { "Hand", "Cards", "Log" };
    private static readonly TestHandPreset[] TestHandPresets =
    {
        new TestHandPreset(
            "Likely Broken",
            "Definitions or generated moves look likely to diverge from Unity behavior",
            new[] { "charge", "gods_move", "thunderclap_flash", "overbearing" }),
        new TestHandPreset(
            "Coarse Piece",
            "Piece-attached effects are not represented exactly in AI GameState",
            new[] { "desperado", "sunset_blade", "giant", "father_enemy" }),
        new TestHandPreset(
            "Coarse Move",
            "Movement overrides that do not have immediate post-card move generation",
            new[] { "agile", "caterpillar", "concentration", "limitless" }),
        new TestHandPreset(
            "Immediate Move",
            "One-turn movement overrides with direct generated post-card moves",
            new[] { "sneak_pawn", "aim", "fast_march", "thunderclap_flash" }),
        new TestHandPreset(
            "Random Coarse",
            "Random or expected-value cards where actual result can diverge after execution",
            new[] { "gaslighting", "magnet", "arena", "honey_trap" }),
        new TestHandPreset(
            "Global Coarse",
            "Ongoing global effects that are planned coarsely",
            new[] { "checkmate_declaration", "mutiny", "stag_fight", "windmill" }),
        new TestHandPreset(
            "Board Coarse",
            "Board-wide effects with coarse/random planning",
            new[] { "democracy", "destroyer_tank_cards", "shuffle_board", "position_swap" }),
        new TestHandPreset(
            "Deferred Tiles",
            "Tile effects with heuristic or deferred behavior",
            new[] { "cobweb", "psilocybin_mushroom", "obey_order", "fire" }),
        new TestHandPreset(
            "Tile Verify",
            "Tile effects that should be checked against actual movement follow-up",
            new[] { "jumping_platform", "time_bomb", "blessing", "peace_zone" }),
        new TestHandPreset(
            "Target Edge",
            "Cards with stricter target constraints or multi-target behavior",
            new[] { "dark_hand", "dimension_disturbance", "transmigration", "weird_castling" })
    };

    [Serializable]
    private sealed class PresetData
    {
        public List<string> paths = new List<string>();
    }

    private sealed class TestHandPreset
    {
        public TestHandPreset(string name, string description, string[] cardIds)
        {
            Name = name;
            Description = description;
            CardIds = cardIds;
        }

        public string Name { get; }
        public string Description { get; }
        public string[] CardIds { get; }
    }

    [MenuItem("Tools/Chaos Chess/AI Card Debugger")]
    public static void Open()
    {
        GetWindow<AiCardDebugWindow>("AI Card Debugger");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AiCardHand.AnyChanged += OnAnyAiCardHandChanged;
        shouldAutoResolveSceneReferences = true;
        ResolveSceneReferences();
        LoadRegistryIfNeeded();
        RefreshCatalog();
        RefreshHandSnapshot();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        AiCardHand.AnyChanged -= OnAnyAiCardHandChanged;
        UnsubscribeHandChanged();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        shouldAutoResolveSceneReferences = true;
        UnsubscribeHandChanged();
        EditorApplication.delayCall += () =>
        {
            if (this == null)
                return;

            ResolveSceneReferences();
            LoadRegistryIfNeeded();
            RefreshCatalog();
            RefreshHandSnapshot();
            Repaint();
        };
    }

    private void OnHierarchyChange()
    {
        shouldAutoResolveSceneReferences = true;
    }

    private void OnProjectChange()
    {
        LoadRegistryIfNeeded();
        RefreshCatalog();
        QueueAutoScan();
        Repaint();
    }

    private void OnInspectorUpdate()
    {
        EnsureAutoSceneReferences();

        if (!EditorApplication.isPlaying || aiCardHand == null)
            return;

        if (observedHandVersion == aiCardHand.Version && AreHandSnapshotsEqual(aiCardHand.AvailableCards))
        {
            TryAutoScanCandidates();
            return;
        }

        RefreshHandSnapshot();
        QueueAutoScan();
        TryAutoScanCandidates(force: true);
        Repaint();
    }

    private void OnGUI()
    {
        EnsureAutoSceneReferences();

        DrawCompactHeader();

        selectedTab = Mathf.Clamp(selectedTab, 0, Tabs.Length - 1);
        selectedTab = GUILayout.Toolbar(selectedTab, Tabs, GUILayout.Height(28f));
        EditorGUILayout.Space(6f);

        switch (selectedTab)
        {
            case 0:
                DrawCompactHandView();
                break;
            case 1:
                DrawCompactCatalogView();
                break;
            case 2:
                DrawLogPanel();
                break;
        }
    }

    private void DrawCompactHeader()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("AI Card Debugger", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                EditorGUILayout.LabelField("Hand", handCards.Count + "/" + MaxHandCards, GUILayout.Width(80f));
            }

            string selectedCard = selectedHandIndex >= 0 && selectedHandIndex < handCards.Count
                ? FormatCardLabel(GetCardData(handCards[selectedHandIndex]))
                : "none";
            EditorGUILayout.LabelField("Selected", selectedCard);
            EditorGUILayout.LabelField("Turn", FormatTurnStatus());
            EditorGUILayout.LabelField("Actor", GetActorColor().ToString());
            if (aiTurnController != null && aiTurnController.HasQueuedForcedCard)
                EditorGUILayout.LabelField("Queued", aiTurnController.QueuedForcedCardId);

            showSceneReferences = EditorGUILayout.Foldout(showSceneReferences, "Scene References", true);
            if (showSceneReferences)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    registry = (CardLabRegistrySO)EditorGUILayout.ObjectField("Registry", registry, typeof(CardLabRegistrySO), false);
                    aiCardHand = (AiCardHand)EditorGUILayout.ObjectField("AI Hand", aiCardHand, typeof(AiCardHand), true);
                    boardManager = (BoardManager)EditorGUILayout.ObjectField("Board", boardManager, typeof(BoardManager), true);
                    gameManager = (GameManager)EditorGUILayout.ObjectField("Game", gameManager, typeof(GameManager), true);
                    aiTurnController = (AiTurnController)EditorGUILayout.ObjectField("AI Turn", aiTurnController, typeof(AiTurnController), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        UpdateHandChangedSubscription();
                        RefreshHandSnapshot();
                    }
                }
            }
        }
    }

    private void DrawCompactHandView()
    {
        handPageScroll = EditorGUILayout.BeginScrollView(handPageScroll);
        using (new EditorGUILayout.VerticalScope())
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("AI Hand", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(handCards.Count + "/" + MaxHandCards, GUILayout.Width(48f));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Clear", GUILayout.Width(72f)))
                        ClearHand();
                }

                handScroll = EditorGUILayout.BeginScrollView(handScroll, GUILayout.MinHeight(160f), GUILayout.MaxHeight(240f));
                for (int i = 0; i < handCards.Count; i++)
                    DrawHandRow(i);
                EditorGUILayout.EndScrollView();

                if (handCards.Count == 0)
                    EditorGUILayout.HelpBox("Cards 탭에서 AI 손패에 카드를 추가하세요.", MessageType.Info);
                else if (handCards.Count >= MaxHandCards)
                    EditorGUILayout.HelpBox("AI 손패는 최대 4장입니다.", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(selectedHandIndex <= 0 || selectedHandIndex >= handCards.Count))
                    {
                        if (GUILayout.Button("Priority Up", GUILayout.Height(30f)))
                            MoveHandCard(selectedHandIndex, selectedHandIndex - 1);
                    }
                }

                DrawSelectedCardExecutionControls(30f);
                DrawForceStatusBox();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(aiTurnController == null || !aiTurnController.HasQueuedForcedCard))
                {
                    if (GUILayout.Button("Clear Queued", GUILayout.Height(26f)))
                            aiTurnController.ClearQueuedForcedCard();
                    }
                }
            }

            showPreset = EditorGUILayout.Foldout(showPreset, "Preset", true);
            if (showPreset)
            {
                using (new EditorGUI.IndentLevelScope())
                    DrawPresetControls();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawHandRow(int index)
    {
        GameObject cardObject = handCards[index];
        CardData card = GetCardData(cardObject);
        bool selected = index == selectedHandIndex;

        using (new EditorGUILayout.HorizontalScope(selected ? EditorStyles.helpBox : GUIStyle.none))
        {
            if (GUILayout.Toggle(selected, GUIContent.none, GUILayout.Width(18f)))
                selectedHandIndex = index;

            EditorGUILayout.LabelField(FormatCardLabel(card));

            if (GUILayout.Button("X", GUILayout.Width(28f)))
            {
                RemoveHandCard(index);
                GUIUtility.ExitGUI();
            }
        }
    }

    private void DrawCompactCatalogView()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cards", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(catalog.Count.ToString(), GUILayout.Width(40f));
            }

            catalogScroll = EditorGUILayout.BeginScrollView(catalogScroll, GUILayout.MinHeight(280f));
            for (int i = 0; i < catalog.Count; i++)
                DrawCatalogRow(i);
            EditorGUILayout.EndScrollView();
        }

        showSelectedCardDetails = EditorGUILayout.Foldout(showSelectedCardDetails, "Selected Card Details", true);
        if (showSelectedCardDetails && HasSelectedCatalogCard())
        {
            using (new EditorGUI.IndentLevelScope())
                DrawCardDetails(catalog[selectedCatalogIndex]);
        }
    }

    private void DrawCatalogRow(int index)
    {
        CardData card = catalog[index];
        CardDataSO so = card != null ? card.DataSO : null;
        bool selected = index == selectedCatalogIndex;

        using (new EditorGUILayout.HorizontalScope(selected ? EditorStyles.helpBox : GUIStyle.none))
        {
            if (GUILayout.Toggle(selected, GUIContent.none, GUILayout.Width(18f)))
                selectedCatalogIndex = index;

            EditorGUILayout.LabelField(FormatCardLabel(card));

            using (new EditorGUI.DisabledScope(so == null || !so.AiSupported || handCards.Count >= MaxHandCards))
            {
                if (GUILayout.Button("+", GUILayout.Width(32f)))
                    AddCardToHand(card);
            }
        }
    }

    private void DrawSelectedCardExecutionControls(float height)
    {
        bool hasSelectedCard = selectedHandIndex >= 0 && selectedHandIndex < handCards.Count;
        bool isAiTurn = IsCurrentAiTurn();

        using (new EditorGUI.DisabledScope(!hasSelectedCard))
        {
            if (!isAiTurn)
            {
                if (GUILayout.Button("Queue For AI Turn", GUILayout.Height(height)))
                    QueueSelectedCardForAiTurn();
                return;
            }

            if (GUILayout.Button("Force Now", GUILayout.Height(height)))
                ForceSelectedCard();
        }
    }

    private void DrawPresetControls()
    {
        EditorGUILayout.Space(8f);
        DrawQuickTestHandControls();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
        presetName = EditorGUILayout.TextField("Name", presetName);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save"))
                SavePreset();

            if (GUILayout.Button("Load"))
                LoadPreset();
        }
    }

    private void DrawQuickTestHandControls()
    {
        EditorGUILayout.LabelField("Quick Test Hands", EditorStyles.boldLabel);
        foreach (TestHandPreset preset in TestHandPresets)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(preset.Name, preset.Description), GUILayout.Width(110f));
                if (GUILayout.Button("Load", GUILayout.Width(72f)))
                    LoadTestHandPreset(preset);
            }
        }

        EditorGUILayout.Space(8f);
        DrawAllCardsBatchControls();
    }

    private void DrawAllCardsBatchControls()
    {
        List<CardData> testableCards = GetTestableCatalogCards();
        int batchCount = Mathf.Max(1, Mathf.CeilToInt(testableCards.Count / (float)MaxHandCards));
        allCardsBatchIndex = Mathf.Clamp(allCardsBatchIndex, 0, batchCount - 1);

        EditorGUILayout.LabelField("All Supported Cards", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(testableCards.Count + " cards", GUILayout.Width(80f));
            EditorGUILayout.LabelField((allCardsBatchIndex + 1) + "/" + batchCount, GUILayout.Width(56f));

            using (new EditorGUI.DisabledScope(testableCards.Count == 0 || allCardsBatchIndex <= 0))
            {
                if (GUILayout.Button("Prev", GUILayout.Width(56f)))
                    LoadAllCardsBatch(allCardsBatchIndex - 1);
            }

            using (new EditorGUI.DisabledScope(testableCards.Count == 0))
            {
                if (GUILayout.Button("Load", GUILayout.Width(56f)))
                    LoadAllCardsBatch(allCardsBatchIndex);
            }

            using (new EditorGUI.DisabledScope(testableCards.Count == 0 || allCardsBatchIndex >= batchCount - 1))
            {
                if (GUILayout.Button("Next", GUILayout.Width(56f)))
                    LoadAllCardsBatch(allCardsBatchIndex + 1);
            }
        }
    }

    private void DrawLogPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Debug Output", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Copy", GUILayout.Width(80f)))
                    EditorGUIUtility.systemCopyBuffer = logBuilder.ToString();

                if (GUILayout.Button("Clear", GUILayout.Width(80f)))
                    logBuilder.Length = 0;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(180f));
            EditorGUILayout.TextArea(logBuilder.ToString(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawCardDetails(CardData card)
    {
        CardDataSO so = card != null ? card.DataSO : null;
        if (so == null)
            return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Selected", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Name", so.CardName);
        EditorGUILayout.LabelField("AI Id", so.AiCardId);
        EditorGUILayout.LabelField("Type", so.Type.ToString());
        EditorGUILayout.LabelField("Category", so.AiCategory.ToString());
        EditorGUILayout.LabelField("Supported", so.AiSupported.ToString());
        EditorGUILayout.LabelField("Piece Targets", so.RequiredPieceCount.ToString());
        EditorGUILayout.LabelField("Tile Targets", so.TileCount.ToString());
    }

    private void ResolveSceneReferences()
    {
        if (boardManager == null)
            boardManager = BoardManager.Instance != null ? BoardManager.Instance : UnityEngine.Object.FindFirstObjectByType<BoardManager>();

        if (gameManager == null)
            gameManager = GameManager.Instance != null ? GameManager.Instance : UnityEngine.Object.FindFirstObjectByType<GameManager>();

        if (aiTurnController == null)
            aiTurnController = UnityEngine.Object.FindFirstObjectByType<AiTurnController>();

        AiCardHand controllerHand = aiTurnController != null ? aiTurnController.CardHand : null;
        if (controllerHand != null)
        {
            if (HasDontSaveFlags(controllerHand))
            {
                RefreshHandSnapshot(controllerHand);
            }
            else
            {
                if (aiCardHand != null && aiCardHand != controllerHand)
                {
                    string warning = "Editor AI Hand reference differed from AiTurnController.CardHand. Using controller hand.";
                    if (!string.Equals(lastReferenceWarning, warning, StringComparison.Ordinal))
                    {
                        AppendLog(warning);
                        lastReferenceWarning = warning;
                    }
                }

                aiCardHand = controllerHand;
            }
        }
        else if (aiCardHand == null)
        {
            AiCardHand foundHand = UnityEngine.Object.FindFirstObjectByType<AiCardHand>();
            if (foundHand != null && !HasDontSaveFlags(foundHand))
                aiCardHand = foundHand;
            else if (foundHand != null)
                RefreshHandSnapshot(foundHand);
        }

        UpdateHandChangedSubscription();
    }

    private void EnsureAutoSceneReferences()
    {
        if (!shouldAutoResolveSceneReferences && !HasMissingSceneReferences())
            return;

        ResolveSceneReferences();

        if (aiCardHand != null)
            RefreshHandSnapshot();

        shouldAutoResolveSceneReferences = HasMissingSceneReferences();
    }

    private bool HasMissingSceneReferences()
    {
        return aiCardHand == null ||
               boardManager == null ||
               gameManager == null ||
               aiTurnController == null;
    }

    private void LoadRegistryIfNeeded()
    {
        if (registry != null)
            return;

        string[] guids = AssetDatabase.FindAssets("t:CardLabRegistrySO");
        if (guids.Length == 0)
            return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        registry = AssetDatabase.LoadAssetAtPath<CardLabRegistrySO>(path);
    }

    private void RefreshCatalog()
    {
        catalog.Clear();
        var seen = new HashSet<CardData>();

        if (registry != null && registry.Cards != null)
        {
            foreach (CardData card in registry.Cards)
            {
                if (card != null && seen.Add(card))
                    catalog.Add(card);
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { CardPrefabFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CardData card = prefab != null ? prefab.GetComponent<CardData>() : null;
            if (card != null && seen.Add(card))
                catalog.Add(card);
        }

        catalog.Sort((a, b) => string.Compare(FormatCardLabel(a), FormatCardLabel(b), StringComparison.Ordinal));
        selectedCatalogIndex = Mathf.Clamp(selectedCatalogIndex, 0, Mathf.Max(0, catalog.Count - 1));
    }

    private void RefreshHandSnapshot()
    {
        handCards.Clear();

        if (aiCardHand != null && aiCardHand.AvailableCards != null)
        {
            foreach (GameObject card in aiCardHand.AvailableCards)
            {
                if (handCards.Count >= MaxHandCards)
                    break;

                handCards.Add(card);
            }
        }

        observedHandVersion = aiCardHand != null ? aiCardHand.Version : -1;
        selectedHandIndex = ClampHandIndex(selectedHandIndex);
    }

    private void UpdateHandChangedSubscription()
    {
        if (subscribedHand == aiCardHand)
            return;

        UnsubscribeHandChanged();

        subscribedHand = aiCardHand;
        if (subscribedHand != null)
            subscribedHand.Changed += OnAiCardHandChanged;
    }

    private void UnsubscribeHandChanged()
    {
        if (subscribedHand == null)
            return;

        subscribedHand.Changed -= OnAiCardHandChanged;
        subscribedHand = null;
    }

    private void OnAiCardHandChanged()
    {
        RefreshHandSnapshot();
        QueueAutoScan();
        EditorApplication.QueuePlayerLoopUpdate();
        Repaint();
    }

    private void OnAnyAiCardHandChanged(AiCardHand changedHand)
    {
        if (changedHand == null)
            return;

        if (HasDontSaveFlags(changedHand))
        {
            if (aiCardHand == null || aiCardHand == changedHand)
            {
                RefreshHandSnapshot(changedHand);
                QueueAutoScan();
            }

            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
            return;
        }

        if (aiCardHand != null && aiCardHand != changedHand)
            return;

        if (aiCardHand == null)
            aiCardHand = changedHand;

        UpdateHandChangedSubscription();
        RefreshHandSnapshot();
        QueueAutoScan();
        EditorApplication.QueuePlayerLoopUpdate();
        Repaint();
    }

    private void RefreshHandSnapshot(AiCardHand sourceHand)
    {
        handCards.Clear();

        if (sourceHand != null && sourceHand.AvailableCards != null)
        {
            foreach (GameObject card in sourceHand.AvailableCards)
            {
                if (handCards.Count >= MaxHandCards)
                    break;

                handCards.Add(card);
            }
        }

        observedHandVersion = sourceHand != null ? sourceHand.Version : -1;
        selectedHandIndex = ClampHandIndex(selectedHandIndex);
    }

    private bool AreHandSnapshotsEqual(IReadOnlyList<GameObject> runtimeCards)
    {
        if (runtimeCards == null)
            return handCards.Count == 0;

        int count = Mathf.Min(runtimeCards.Count, MaxHandCards);
        if (handCards.Count != count)
            return false;

        for (int i = 0; i < count; i++)
        {
            if (!IsSameCard(handCards[i], runtimeCards[i]))
                return false;
        }

        return true;
    }

    private int ClampHandIndex(int index)
    {
        return handCards.Count > 0
            ? Mathf.Clamp(index, 0, handCards.Count - 1)
            : -1;
    }

    private void AddCardToHand(CardData card)
    {
        if (card == null)
            return;

        if (handCards.Count >= MaxHandCards)
        {
            AppendLog("AI hand is full. Max cards: " + MaxHandCards);
            return;
        }

        string path = AssetDatabase.GetAssetPath(card.gameObject);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            AppendLog("Could not resolve prefab for card: " + card.name);
            return;
        }

        if (ContainsSameCard(handCards, prefab))
        {
            AppendLog("AI hand already contains card: " + FormatCardLabel(card));
            return;
        }

        handCards.Add(prefab);
        ApplyHandCardsToComponent();
        RefreshHandSnapshot();
        QueueAutoScan();
        TryAutoScanCandidates(force: true);
        selectedHandIndex = handCards.Count - 1;
        AppendLog("Added card to AI hand: " + FormatCardLabel(card));
    }

    private void LoadTestHandPreset(TestHandPreset preset)
    {
        if (preset == null)
            return;

        if (catalog.Count == 0)
            RefreshCatalog();

        handCards.Clear();
        var missing = new List<string>();

        foreach (string cardId in preset.CardIds)
        {
            GameObject prefab = FindCardPrefabByAiCardId(cardId);
            if (prefab == null)
            {
                missing.Add(cardId);
                continue;
            }

            if (handCards.Count >= MaxHandCards)
                break;

            handCards.Add(prefab);
        }

        selectedHandIndex = ClampHandIndex(0);
        ApplyHandCardsToComponent();
        RefreshHandSnapshot();
        QueueAutoScan();
        TryAutoScanCandidates(force: true);

        AppendLog(
            "Loaded quick test hand '" + preset.Name + "': " +
            FormatHandList(handCards) +
            (missing.Count > 0 ? ", missing=" + string.Join(", ", missing) : string.Empty));
    }

    private void LoadAllCardsBatch(int batchIndex)
    {
        List<CardData> testableCards = GetTestableCatalogCards();
        if (testableCards.Count == 0)
        {
            AppendLog("No AI-supported cards with AiCardId were found.");
            return;
        }

        int batchCount = Mathf.Max(1, Mathf.CeilToInt(testableCards.Count / (float)MaxHandCards));
        allCardsBatchIndex = Mathf.Clamp(batchIndex, 0, batchCount - 1);
        int start = allCardsBatchIndex * MaxHandCards;

        handCards.Clear();
        for (int i = start; i < testableCards.Count && handCards.Count < MaxHandCards; i++)
        {
            GameObject prefab = GetPrefabObject(testableCards[i]);
            if (prefab != null && !ContainsSameCard(handCards, prefab))
                handCards.Add(prefab);
        }

        selectedHandIndex = ClampHandIndex(0);
        ApplyHandCardsToComponent();
        RefreshHandSnapshot();
        QueueAutoScan();
        TryAutoScanCandidates(force: true);

        AppendLog(
            "Loaded all-card test batch " + (allCardsBatchIndex + 1) + "/" + batchCount +
            ": " + FormatHandList(handCards));
    }

    private List<CardData> GetTestableCatalogCards()
    {
        if (catalog.Count == 0)
            RefreshCatalog();

        var cards = new List<CardData>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (CardData card in catalog)
        {
            CardDataSO so = card != null ? card.DataSO : null;
            if (so == null ||
                !so.AiSupported ||
                string.IsNullOrWhiteSpace(so.AiCardId) ||
                !seen.Add(so.AiCardId))
            {
                continue;
            }

            cards.Add(card);
        }

        cards.Sort((a, b) => string.Compare(FormatCardLabel(a), FormatCardLabel(b), StringComparison.Ordinal));
        return cards;
    }

    private GameObject FindCardPrefabByAiCardId(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;

        foreach (CardData card in catalog)
        {
            CardDataSO so = card != null ? card.DataSO : null;
            if (so == null ||
                string.IsNullOrWhiteSpace(so.AiCardId) ||
                !string.Equals(so.AiCardId, cardId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string path = AssetDatabase.GetAssetPath(card.gameObject);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        return null;
    }

    private static GameObject GetPrefabObject(CardData card)
    {
        if (card == null)
            return null;

        string path = AssetDatabase.GetAssetPath(card.gameObject);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private void RemoveHandCard(int index)
    {
        if (index < 0 || index >= handCards.Count)
            return;

        GameObject removed = handCards[index];
        handCards.RemoveAt(index);
        ApplyHandCardsToComponent();
        RefreshHandSnapshot();
        QueueAutoScan();
        TryAutoScanCandidates(force: true);
        selectedHandIndex = ClampHandIndex(index);
        AppendLog("Removed card from AI hand: " + FormatCardLabel(GetCardData(removed)));
    }

    private void MoveHandCard(int from, int to)
    {
        if (from < 0 || from >= handCards.Count || to < 0 || to >= handCards.Count)
            return;

        GameObject card = handCards[from];
        handCards.RemoveAt(from);
        handCards.Insert(to, card);
        ApplyHandCardsToComponent();
        RefreshHandSnapshot();
        QueueAutoScan();
        TryAutoScanCandidates(force: true);
        selectedHandIndex = ClampHandIndex(to);
        AppendLog("Moved card priority: " + FormatCardLabel(GetCardData(card)) + " -> " + to);
    }

    private void ClearHand()
    {
        handCards.Clear();
        ApplyHandCardsToComponent();
        RefreshHandSnapshot();
        QueueAutoScan();
        TryAutoScanCandidates(force: true);
        AppendLog("Cleared AI hand.");
    }

    private void ApplyHandCardsToComponent()
    {
        if (aiCardHand == null)
        {
            AppendLog("AI Card Hand is not assigned.");
            return;
        }

        if (HasDontSaveFlags(aiCardHand))
        {
            if (EditorApplication.isPlaying)
            {
                aiCardHand.ReplaceRuntimeCards(handCards);
                RefreshHandSnapshot();
                ClearQueuedForcedCardIfMissing();
                AppendLog("Applied runtime AI hand: " + FormatHandList(aiCardHand.AvailableCards));
            }
            else
            {
                AppendLog("AI Card Hand cannot be edited because it is marked DontSave.");
            }

            return;
        }

        TrimHandCardsToMax();
        RemoveDuplicateHandCards();

        Undo.RecordObject(aiCardHand, "Configure AI Card Hand");

        SerializedObject serialized = new SerializedObject(aiCardHand);
        SerializedProperty startingCards = serialized.FindProperty("startingCards");
        SerializedProperty useEditorConfigured = serialized.FindProperty("useEditorConfiguredStartingCards");
        SerializedProperty paths = serialized.FindProperty("editorStartingCardPrefabPaths");

        startingCards.ClearArray();
        paths.ClearArray();

        for (int i = 0; i < handCards.Count && i < MaxHandCards; i++)
        {
            startingCards.InsertArrayElementAtIndex(i);
            startingCards.GetArrayElementAtIndex(i).objectReferenceValue = handCards[i];

            paths.InsertArrayElementAtIndex(i);
            paths.GetArrayElementAtIndex(i).stringValue = AssetDatabase.GetAssetPath(handCards[i]);
        }

        useEditorConfigured.boolValue = true;
        serialized.ApplyModifiedProperties();

        if (EditorApplication.isPlaying)
            aiCardHand.ReplaceRuntimeCards(handCards);

        EditorUtility.SetDirty(aiCardHand);
        observedHandVersion = aiCardHand.Version;
        ClearQueuedForcedCardIfMissing();
        AppendLog("Applied AI hand: " + FormatHandList(aiCardHand.AvailableCards));
    }

    private void ClearQueuedForcedCardIfMissing()
    {
        if (aiTurnController == null || !aiTurnController.HasQueuedForcedCard)
            return;

        string queuedCardId = aiTurnController.QueuedForcedCardId;
        if (aiCardHand != null && aiCardHand.ContainsAiCardId(queuedCardId))
            return;

        aiTurnController.ClearQueuedForcedCard();
        AppendLog("clearedQueuedForcedCard=notInHand, card=" + queuedCardId);
    }

    private void QueueAutoScan()
    {
        autoScanDirty = true;
    }

    private void TryAutoScanCandidates(bool force = false)
    {
        if (!EditorApplication.isPlaying ||
            aiCardHand == null ||
            boardManager == null ||
            gameManager == null ||
            handCards.Count == 0)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (!force && now < nextAutoScanAt)
            return;

        string fingerprint = CreateAutoScanFingerprint();
        if (!force &&
            !autoScanDirty &&
            string.Equals(fingerprint, lastAutoScanFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        lastAutoScanFingerprint = fingerprint;
        autoScanDirty = false;
        nextAutoScanAt = now + 0.75d;
        ScanCurrentHandCandidates("auto");
    }

    private string CreateAutoScanFingerprint()
    {
        string fen = boardManager != null ? boardManager.GetFEN() : string.Empty;
        return FormatTurnStatus() + "|" +
               GetActorColor() + "|" +
               fen + "|" +
               FormatHandList(handCards);
    }

    private void ScanCurrentHandCandidates(string source = "manual")
    {
        if (!EnsureRuntimeContext())
            return;

        AiPieceColor actor = GetActor();
        UnityGameStateMappingResult mapping = CaptureMapping(actor);
        if (mapping == null)
            return;

        AppendLog("=== AI card candidate scan (" + source + ") ===");
        AppendLog(FormatStateSummary(mapping));

        foreach (GameObject cardObject in handCards)
            LogCandidatePlan(cardObject, mapping.GameState, actor);
    }

    private void DrawForceStatusBox()
    {
        MessageType messageType;
        string message = GetForceStatusMessage(out messageType);
        EditorGUILayout.HelpBox(message, messageType);
    }

    private string GetForceStatusMessage(out MessageType messageType)
    {
        messageType = MessageType.Info;

        if (!EditorApplication.isPlaying)
        {
            messageType = MessageType.Warning;
            return "Force unavailable: Play Mode에서만 실행/예약할 수 있습니다.";
        }

        if (aiCardHand == null)
        {
            messageType = MessageType.Warning;
            return "Force unavailable: AiCardHand 참조가 없습니다.";
        }

        if (boardManager == null)
        {
            messageType = MessageType.Warning;
            return "Force unavailable: BoardManager 참조가 없습니다.";
        }

        if (gameManager == null)
        {
            messageType = MessageType.Warning;
            return "Force unavailable: GameManager 참조가 없습니다.";
        }

        if (handCards.Count == 0)
        {
            messageType = MessageType.Warning;
            return "Force unavailable: AI 손패가 비어 있습니다.";
        }

        if (selectedHandIndex < 0 || selectedHandIndex >= handCards.Count)
        {
            messageType = MessageType.Warning;
            return "Force unavailable: 손패에서 카드를 선택해야 합니다.";
        }

        CardData cardData = GetCardData(handCards[selectedHandIndex]);
        CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
        if (dataSO == null)
        {
            messageType = MessageType.Warning;
            return "Force unavailable: 선택 카드에 CardDataSO가 없습니다.";
        }

        if (string.IsNullOrWhiteSpace(dataSO.AiCardId))
        {
            messageType = MessageType.Warning;
            return "Force unavailable: 선택 카드에 AiCardId가 없습니다.";
        }

        if (!IsCurrentAiTurn())
        {
            if (aiTurnController == null)
            {
                messageType = MessageType.Warning;
                return "Force unavailable: AI 턴 예약에는 AiTurnController가 필요합니다.";
            }

            return "Ready to queue: 현재 턴 색이 AI 색이 아니라 즉시 실행하지 않고 다음 AI 턴에 예약합니다. 실제 AI 턴에 손패/카드 조건/타겟 검증을 다시 통과해야 실행됩니다.";
        }

        return "Ready to force: 즉시 targetPlan 검증 후 실행합니다. 실패하면 Log에 CardCondition/TargetUnavailable 같은 reject reason이 표시됩니다.";
    }

    private void ForceSelectedCard()
    {
        if (!EnsureRuntimeContext())
            return;

        if (selectedHandIndex < 0 || selectedHandIndex >= handCards.Count)
        {
            AppendLog("No selected hand card.");
            return;
        }

        GameObject cardObject = handCards[selectedHandIndex];
        CardData cardData = GetCardData(cardObject);
        CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
        if (dataSO == null || string.IsNullOrWhiteSpace(dataSO.AiCardId))
        {
            AppendLog("Selected card has no AI card id.");
            return;
        }

        AppendLog("forceRequest=selected, card=" + FormatCardLabel(cardData));

        if (!IsCurrentAiTurn())
        {
            AppendLog("forceNow=blocked, reason=NotAiTurn, " + FormatTurnStatus() + ". Use Queue For AI Turn instead.");
            return;
        }

        AiPieceColor actor = GetActor();
        UnityGameStateMappingResult mapping = CaptureMapping(actor);
        if (mapping == null)
            return;

        AppendLog("=== Forced AI card execution ===");
        AppendLog(FormatStateSummary(mapping));
        AppendLog("forcedCard=" + FormatCardLabel(cardData) + ", actor=" + actor);

        if (!targetPlanner.TryCreatePlan(
                dataSO.AiCardId,
                cardObject,
                boardManager,
                mapping.GameState,
                actor,
                out AiCardTargetPlan plan,
                out AiCardExecutionStatus failureStatus,
                out string reason))
        {
            AppendLog("targetPlan=rejected, status=" + failureStatus + ", kind=" + ClassifyFailure(reason) + ", reason=" + reason);
            return;
        }

        AppendLog("targetPlan=accepted, " + FormatPlan(plan));
        AiCardExecutionResult execution = cardExecutor.ExecutePlan(
            plan.UsePlan,
            aiCardHand,
            boardManager,
            mapping.GameState,
            actor);

        AppendLog("execution=" + FormatExecution(execution));
        RefreshHandSnapshot();
    }

    private void QueueSelectedCardForAiTurn()
    {
        if (!EnsureRuntimeContext())
            return;

        if (selectedHandIndex < 0 || selectedHandIndex >= handCards.Count)
        {
            AppendLog("No selected hand card.");
            return;
        }

        GameObject cardObject = handCards[selectedHandIndex];
        CardData cardData = GetCardData(cardObject);
        CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
        if (dataSO == null || string.IsNullOrWhiteSpace(dataSO.AiCardId))
        {
            AppendLog("Selected card has no AI card id.");
            return;
        }

        AppendLog("queueRequest=selected, card=" + FormatCardLabel(cardData));
        QueueForcedCard(dataSO);
    }

    private void QueueForcedCard(CardDataSO dataSO)
    {
        if (dataSO == null || string.IsNullOrWhiteSpace(dataSO.AiCardId))
        {
            AppendLog("Cannot queue forced card because AI card id is empty.");
            return;
        }

        if (aiTurnController == null)
            aiTurnController = UnityEngine.Object.FindFirstObjectByType<AiTurnController>();

        if (aiTurnController == null)
        {
            AppendLog("AiTurnController is missing. Cannot queue forced card.");
            return;
        }

        aiTurnController.QueueForcedCardForNextAiTurn(dataSO.AiCardId);
        AppendLog("queuedForcedCard=" + dataSO.CardName + " [" + dataSO.AiCardId + "], will execute on next AI turn.");
    }

    private void LogCandidatePlan(
        GameObject cardObject,
        GameState gameState,
        AiPieceColor actor)
    {
        CardData cardData = GetCardData(cardObject);
        CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
        if (dataSO == null)
        {
            AppendLog("candidate=<missing CardDataSO>, rejectedReason=MissingCardData");
            return;
        }

        if (!dataSO.AiSupported || string.IsNullOrWhiteSpace(dataSO.AiCardId))
        {
            AppendLog("candidate=" + FormatCardLabel(cardData) + ", rejectedReason=AI metadata disabled or missing");
            return;
        }

        if (targetPlanner.TryCreatePlan(
                dataSO.AiCardId,
                cardObject,
                boardManager,
                gameState,
                actor,
                out AiCardTargetPlan plan,
                out AiCardExecutionStatus failureStatus,
                out string reason))
        {
            AppendLog("candidate=" + FormatCardLabel(cardData) + ", targetPlan=accepted, " + FormatPlan(plan));
            return;
        }

        AppendLog(
            "candidate=" + FormatCardLabel(cardData) +
            ", targetPlan=rejected, status=" + failureStatus +
            ", kind=" + ClassifyFailure(reason) +
            ", rejectedReason=" + reason);
    }

    private UnityGameStateMappingResult CaptureMapping(AiPieceColor actor)
    {
        try
        {
            UnityGameStateMappingResult mapping = UnityGameStateMapper.Capture(boardManager, aiCardHand);
            foreach (string warning in mapping.Warnings)
                AppendLog("mappingWarning=" + warning);
            return EnsureMappingSideToMove(mapping, actor);
        }
        catch (Exception ex)
        {
            AppendLog("mapping=failed, reason=" + ex.Message);
            return null;
        }
    }

    private UnityGameStateMappingResult EnsureMappingSideToMove(
        UnityGameStateMappingResult mapping,
        AiPieceColor actor)
    {
        if (mapping == null || mapping.GameState == null || mapping.GameState.BoardState.SideToMove == actor)
            return mapping;

        BoardState sourceBoard = mapping.GameState.BoardState;
        var boardState = new BoardState(
            sourceBoard.Pieces,
            actor,
            sourceBoard.CastlingRights,
            sourceBoard.EnPassantTarget,
            sourceBoard.HalfmoveClock,
            sourceBoard.FullmoveNumber);
        var gameState = new GameState(
            boardState,
            mapping.GameState.AvailableCards,
            mapping.GameState.TileEffects,
            mapping.GameState.CapturedPieces,
            mapping.GameState.TimeReversals);
        string fen = FenParser.Serialize(boardState);

        AppendLog(
            "stateActorOverride=" + sourceBoard.SideToMove +
            "->" + actor +
            ", fen=" + fen);
        return new UnityGameStateMappingResult(fen, gameState, mapping.Warnings);
    }

    private bool EnsureRuntimeContext()
    {
        ResolveSceneReferences();
        RefreshHandSnapshot();

        if (!EditorApplication.isPlaying)
        {
            AppendLog("Enter Play Mode with MainGameScene_AITest before running AI card execution.");
            return false;
        }

        if (aiCardHand == null)
        {
            AppendLog("AiCardHand is missing in the active scene.");
            return false;
        }

        if (boardManager == null)
        {
            AppendLog("BoardManager is missing in the active scene.");
            return false;
        }

        if (gameManager == null)
        {
            AppendLog("GameManager is missing in the active scene.");
            return false;
        }

        if (handCards.Count == 0)
        {
            AppendLog("AI hand is empty.");
            return false;
        }

        return true;
    }

    private AiPieceColor GetActor()
    {
        return UnityAiColorMapper.ToAiColor(GetActorColor());
    }

    private global::PieceColor GetActorColor()
    {
        if (gameManager != null)
            return gameManager.EnemyColor;

        return global::PieceColor.Black;
    }

    private bool IsCurrentAiTurn()
    {
        return gameManager != null && gameManager.turnColor == gameManager.EnemyColor;
    }

    private string FormatTurnStatus()
    {
        if (gameManager == null)
            return "<missing GameManager>";

        string side = gameManager.turnColor == gameManager.EnemyColor
            ? "AI"
            : gameManager.turnColor == gameManager.PlayerColor
                ? "Player"
                : "Unknown";

        return gameManager.turnColor + " / " + side;
    }

    private static CardData GetCardData(GameObject cardObject)
    {
        return cardObject != null ? cardObject.GetComponent<CardData>() : null;
    }

    private void RemoveDuplicateHandCards()
    {
        for (int i = handCards.Count - 1; i >= 0; i--)
        {
            GameObject card = handCards[i];
            if (card == null)
            {
                handCards.RemoveAt(i);
                continue;
            }

            for (int j = 0; j < i; j++)
            {
                if (IsSameCard(handCards[j], card))
                {
                    handCards.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static bool ContainsSameCard(IEnumerable<GameObject> cards, GameObject card)
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

    private static bool IsSameCard(GameObject a, GameObject b)
    {
        if (a == null || b == null)
            return false;

        if (ReferenceEquals(a, b))
            return true;

        CardDataSO aSO = GetCardData(a)?.DataSO;
        CardDataSO bSO = GetCardData(b)?.DataSO;

        if (aSO == null || bSO == null)
            return false;

        if (aSO == bSO)
            return true;

        return !string.IsNullOrWhiteSpace(aSO.AiCardId)
            && string.Equals(aSO.AiCardId, bSO.AiCardId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDontSaveFlags(UnityEngine.Object obj)
    {
        if (obj == null)
            return false;

        return (obj.hideFlags & HideFlags.DontSave) != 0 ||
               (obj.hideFlags & HideFlags.DontSaveInEditor) != 0 ||
               (obj.hideFlags & HideFlags.DontSaveInBuild) != 0;
    }

    private static string FormatCardLabel(CardData card)
    {
        if (card == null)
            return "<null>";

        CardDataSO so = card.DataSO;
        if (so == null)
            return card.name + " <DataSO missing>";

        string name = string.IsNullOrWhiteSpace(so.CardName) ? card.name : so.CardName;
        string aiId = string.IsNullOrWhiteSpace(so.AiCardId) ? "no-ai-id" : so.AiCardId;
        return name + " [" + so.Type + ", " + aiId + "]";
    }

    private static string FormatStateSummary(UnityGameStateMappingResult mapping)
    {
        if (mapping == null || mapping.GameState == null)
            return "state=<null>";

        return "state fen=" + mapping.Fen +
               ", hand=" + mapping.GameState.AvailableCards.Count +
               " [" + FormatAvailableCards(mapping.GameState.AvailableCards) + "]" +
               ", tileEffects=" + mapping.GameState.TileEffects.Count +
               ", pieces=" + mapping.GameState.BoardState.Pieces.Count +
               ", actor=" + mapping.GameState.BoardState.SideToMove;
    }

    private static string FormatAvailableCards(IReadOnlyList<CardInfo> cards)
    {
        if (cards == null || cards.Count == 0)
            return "<none>";

        var parts = new List<string>(cards.Count);
        foreach (CardInfo card in cards)
        {
            if (card == null)
                continue;

            parts.Add(card.Id + "x" + card.RemainingUses);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "<none>";
    }

    private static string FormatHandList(IReadOnlyList<GameObject> cards)
    {
        if (cards == null || cards.Count == 0)
            return "<empty>";

        var parts = new List<string>(cards.Count);
        foreach (GameObject cardObject in cards)
        {
            CardData cardData = GetCardData(cardObject);
            CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
            if (dataSO == null)
                continue;

            parts.Add(string.IsNullOrWhiteSpace(dataSO.AiCardId) ? dataSO.CardName : dataSO.AiCardId);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "<empty>";
    }

    private static string FormatPlan(AiCardTargetPlan plan)
    {
        if (plan == null)
            return "plan=<null>";

        return "card=" + plan.UsePlan.CardId +
               ", actor=" + plan.UsePlan.Actor +
               ", target=" + FormatTarget(plan.UsePlan.Target) +
               ", unityPieces=" + FormatUnityPieces(plan.TargetPieces) +
               ", unityTiles=" + FormatUnityTiles(plan.TargetPositions);
    }

    private static string FormatTarget(CardTargetSelection target)
    {
        if (target == null)
            return "<null>";

        switch (target.Kind)
        {
            case CardTargetKind.None:
                return "None";
            case CardTargetKind.PieceAtSquare:
                return "PieceAtSquare(" + FormatPieceTarget(target.Piece) + ")";
            case CardTargetKind.PieceAndSquare:
                return "PieceAndSquare(piece=" + FormatPieceTarget(target.Piece) + ", squares=" + FormatSquares(target.Squares) + ")";
            case CardTargetKind.OrderedPieces:
                return "OrderedPieces(" + FormatPieceTargets(target.Pieces) + ")";
            case CardTargetKind.BoardSquare:
            case CardTargetKind.OrderedSquares:
                return target.Kind + "(" + FormatSquares(target.Squares) + ")";
            default:
                return target.Kind.ToString();
        }
    }

    private static string FormatPieceTarget(PieceTargetSnapshot target)
    {
        if (target == null)
            return "<null>";

        return target.ExpectedColor + " " + target.ExpectedKind + "@" + target.Square;
    }

    private static string FormatPieceTargets(IReadOnlyList<PieceTargetSnapshot> targets)
    {
        if (targets == null || targets.Count == 0)
            return "none";

        var values = new string[targets.Count];
        for (int i = 0; i < targets.Count; i++)
            values[i] = FormatPieceTarget(targets[i]);

        return string.Join(", ", values);
    }

    private static string FormatSquares(IReadOnlyList<Square> squares)
    {
        if (squares == null || squares.Count == 0)
            return "none";

        var values = new string[squares.Count];
        for (int i = 0; i < squares.Count; i++)
            values[i] = squares[i].ToString();

        return string.Join(", ", values);
    }

    private static string FormatUnityPieces(IReadOnlyList<Piece> pieces)
    {
        if (pieces == null || pieces.Count == 0)
            return "none";

        var values = new string[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
        {
            Piece piece = pieces[i];
            values[i] = piece != null
                ? piece.Color + " " + piece.Type + "@" + piece.Pos
                : "<null>";
        }

        return string.Join(", ", values);
    }

    private static string FormatUnityTiles(IReadOnlyList<Vector3Int> positions)
    {
        if (positions == null || positions.Count == 0)
            return "none";

        var values = new string[positions.Count];
        for (int i = 0; i < positions.Count; i++)
            values[i] = positions[i].ToString();

        return string.Join(", ", values);
    }

    private static string FormatExecution(AiCardExecutionResult execution)
    {
        if (execution == null)
            return "<null>";

        return "status=" + execution.Status +
               ", applied=" + execution.Executed +
               ", kind=" + ClassifyFailure(execution.Reason) +
               ", card=" + (execution.CardSO != null ? execution.CardSO.CardName : "<unknown>") +
               ", reason=" + execution.Reason;
    }

    private static string ClassifyFailure(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "None";

        string lower = reason.ToLowerInvariant();
        if (lower.Contains("no piece") || lower.Contains("piece target"))
            return "PieceTarget";
        if (lower.Contains("tile target") || lower.Contains("square"))
            return "TileTarget";
        if (lower.Contains("target count") || lower.Contains("requires exactly") || lower.Contains("needs"))
            return "TargetCount";
        if (lower.Contains("different color") || lower.Contains("different kind") || lower.Contains("selectable"))
            return "TargetRelation";
        if (lower.Contains("validation failed"))
            return "CardCondition";
        if (lower.Contains("not supported"))
            return "UnsupportedCard";
        if (lower.Contains("not contain"))
            return "CardNotInHand";
        return "ExecutionBoundary";
    }

    private bool HasSelectedCatalogCard()
    {
        return selectedCatalogIndex >= 0 && selectedCatalogIndex < catalog.Count && catalog[selectedCatalogIndex] != null;
    }

    private void SavePreset()
    {
        var data = new PresetData();
        foreach (GameObject cardObject in handCards)
        {
            if (data.paths.Count >= MaxHandCards)
                break;

            string path = AssetDatabase.GetAssetPath(cardObject);
            if (!string.IsNullOrWhiteSpace(path))
                data.paths.Add(path);
        }

        EditorPrefs.SetString(PresetKey(), JsonUtility.ToJson(data));
        AppendLog("Saved preset '" + presetName + "' with " + data.paths.Count + " card(s).");
    }

    private void LoadPreset()
    {
        string key = PresetKey();
        if (!EditorPrefs.HasKey(key))
        {
            AppendLog("Preset not found: " + presetName);
            return;
        }

        PresetData data = JsonUtility.FromJson<PresetData>(EditorPrefs.GetString(key));
        handCards.Clear();

        if (data != null && data.paths != null)
        {
            foreach (string path in data.paths)
            {
                if (handCards.Count >= MaxHandCards)
                    break;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    handCards.Add(prefab);
                else
                    AppendLog("Preset skipped missing prefab: " + path);
            }
        }

        ApplyHandCardsToComponent();
        RefreshHandSnapshot();
        AppendLog("Loaded preset '" + presetName + "' with " + handCards.Count + " card(s).");
    }

    private void TrimHandCardsToMax()
    {
        if (handCards.Count <= MaxHandCards)
            return;

        int removed = handCards.Count - MaxHandCards;
        handCards.RemoveRange(MaxHandCards, removed);
        selectedHandIndex = Mathf.Clamp(selectedHandIndex, 0, Mathf.Max(0, handCards.Count - 1));
        AppendLog("Trimmed AI hand to max " + MaxHandCards + " card(s). Removed: " + removed);
    }

    private string PresetKey()
    {
        string safeName = string.IsNullOrWhiteSpace(presetName) ? "default" : presetName.Trim();
        return PresetPrefix + safeName;
    }

    private void AppendLog(string message)
    {
        string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
        logBuilder.AppendLine(line);
        Debug.Log("[AI Card Debug] " + message);
    }
}
