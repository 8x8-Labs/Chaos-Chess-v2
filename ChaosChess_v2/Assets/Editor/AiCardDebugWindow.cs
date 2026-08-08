using System;
using System.Collections.Generic;
using System.Text;
using ChaosChess.AI.Decision;
using ChaosChess.AI.Decision.CardTargeting;
using ChaosChess.AI.Decision.TurnPlanning;
using ChaosChess.AI.Domain;
using ChaosChess.AI.Fen;
using ChaosChess.Unity.AIIntegration.Cards;
using ChaosChess.Unity.AIIntegration.Engine;
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
    [SerializeField] private int analysisDepth = 12;
    [SerializeField] private int variationCount = 3;
    [SerializeField] private int cardCandidateCount = MaxHandCards;
    [SerializeField] private int targetCandidateCount = 32;
    [SerializeField] private int maximumEngineCallCount = 64;
    [SerializeField] private bool allowCoarseCardEffects = true;
    [SerializeField] private int cardUseScoreTolerance = 25;
    [SerializeField] private bool useEnemyColorAsActor = true;
    [SerializeField] private global::PieceColor manualActorColor = global::PieceColor.Black;
    [SerializeField] private bool executeNormalSelectedCard;
    [SerializeField] private bool deterministicScanOrder = true;
    [SerializeField] private int seed;
    [SerializeField] private string presetName = "default";
    [SerializeField] private int selectedTab;
    [SerializeField] private bool showSceneReferences;
    [SerializeField] private bool showAdvancedSettings;
    [SerializeField] private bool showPreset;
    [SerializeField] private bool showSelectedCardDetails;

    private readonly AiCardTargetPlanner targetPlanner = new AiCardTargetPlanner();
    private readonly AiCardExecutor cardExecutor = new AiCardExecutor();
    private readonly CardTargetingModule cardTargetingModule = new CardTargetingModule();
    private readonly List<CardData> catalog = new List<CardData>();
    private readonly List<GameObject> handCards = new List<GameObject>();
    private readonly StringBuilder logBuilder = new StringBuilder(8192);
    private Vector2 scroll;
    private Vector2 catalogScroll;
    private Vector2 handScroll;
    private bool isAnalysisRunning;
    private static readonly string[] Tabs = { "Hand", "Run", "Cards", "Log" };

    [Serializable]
    private sealed class PresetData
    {
        public List<string> paths = new List<string>();
    }

    [MenuItem("Tools/Chaos Chess/AI Card Debugger")]
    public static void Open()
    {
        GetWindow<AiCardDebugWindow>("AI Card Debugger");
    }

    private void OnEnable()
    {
        ResolveSceneReferences();
        LoadRegistryIfNeeded();
        RefreshCatalog();
        RefreshHandSnapshot();
    }

    private void OnGUI()
    {
        DrawCompactHeader();

        selectedTab = GUILayout.Toolbar(selectedTab, Tabs, GUILayout.Height(28f));
        EditorGUILayout.Space(6f);

        switch (selectedTab)
        {
            case 0:
                DrawCompactHandView();
                break;
            case 1:
                DrawCompactRunView();
                break;
            case 2:
                DrawCompactCatalogView();
                break;
            case 3:
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

                if (GUILayout.Button("Find", GUILayout.Width(64f)))
                {
                    ResolveSceneReferences();
                    RefreshHandSnapshot();
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
                {
                    RefreshCatalog();
                    RefreshHandSnapshot();
                }
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
            EditorGUILayout.LabelField("Actor", GetActorColor().ToString());
            if (aiTurnController != null && aiTurnController.HasQueuedForcedCard)
                EditorGUILayout.LabelField("Queued", aiTurnController.QueuedForcedCardId);

            showSceneReferences = EditorGUILayout.Foldout(showSceneReferences, "Scene References", true);
            if (showSceneReferences)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    registry = (CardLabRegistrySO)EditorGUILayout.ObjectField("Registry", registry, typeof(CardLabRegistrySO), false);
                    aiCardHand = (AiCardHand)EditorGUILayout.ObjectField("AI Hand", aiCardHand, typeof(AiCardHand), true);
                    boardManager = (BoardManager)EditorGUILayout.ObjectField("Board", boardManager, typeof(BoardManager), true);
                    gameManager = (GameManager)EditorGUILayout.ObjectField("Game", gameManager, typeof(GameManager), true);
                    aiTurnController = (AiTurnController)EditorGUILayout.ObjectField("AI Turn", aiTurnController, typeof(AiTurnController), true);
                }
            }
        }
    }

    private void DrawCompactHandView()
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

            handScroll = EditorGUILayout.BeginScrollView(handScroll, GUILayout.MinHeight(220f));
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

                using (new EditorGUI.DisabledScope(selectedHandIndex < 0 || selectedHandIndex >= handCards.Count))
                {
                    string label = gameManager != null && gameManager.IsPlayerTurn
                        ? "Queue For AI Turn"
                        : "Force Selected";
                    if (GUILayout.Button(label, GUILayout.Height(30f)))
                        ForceSelectedCard();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Candidates", GUILayout.Height(28f)))
                    ScanCurrentHandCandidates();

                if (GUILayout.Button("Capture Board", GUILayout.Height(28f)))
                    CaptureBoardSummary();
            }
        }

        showPreset = EditorGUILayout.Foldout(showPreset, "Preset", true);
        if (showPreset)
        {
            using (new EditorGUI.IndentLevelScope())
                DrawPresetControls();
        }
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

    private void DrawCompactRunView()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Run", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(isAnalysisRunning))
            {
                if (GUILayout.Button("Run Normal AI Selection", GUILayout.Height(36f)))
                    RunNormalAiSelection();
            }

            using (new EditorGUI.DisabledScope(selectedHandIndex < 0 || selectedHandIndex >= handCards.Count))
            {
                string label = gameManager != null && gameManager.IsPlayerTurn
                    ? "Queue Selected For AI Turn"
                    : "Force Selected Card";
                if (GUILayout.Button(label, GUILayout.Height(36f)))
                    ForceSelectedCard();
            }

            using (new EditorGUI.DisabledScope(aiTurnController == null || !aiTurnController.HasQueuedForcedCard))
            {
                if (GUILayout.Button("Clear Queued Forced Card", GUILayout.Height(26f)))
                    aiTurnController.ClearQueuedForcedCard();
            }

            if (GUILayout.Button("Scan Current Hand Candidates", GUILayout.Height(30f)))
                ScanCurrentHandCandidates();

            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
            if (showAdvancedSettings)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    analysisDepth = EditorGUILayout.IntField("Analysis Depth", Mathf.Max(1, analysisDepth));
                    variationCount = EditorGUILayout.IntField("Variation Count", Mathf.Max(1, variationCount));
                    cardCandidateCount = EditorGUILayout.IntSlider("Card Candidates", cardCandidateCount, 1, MaxHandCards);
                    targetCandidateCount = EditorGUILayout.IntField("Target Candidates", Mathf.Max(1, targetCandidateCount));
                    maximumEngineCallCount = EditorGUILayout.IntField("Max Engine Calls", Mathf.Max(1, maximumEngineCallCount));
                    allowCoarseCardEffects = EditorGUILayout.Toggle("Allow Coarse Effects", allowCoarseCardEffects);
                    cardUseScoreTolerance = EditorGUILayout.IntField("Card Use Tolerance", Mathf.Max(0, cardUseScoreTolerance));
                    useEnemyColorAsActor = EditorGUILayout.Toggle("Use Enemy As Actor", useEnemyColorAsActor);
                    using (new EditorGUI.DisabledScope(useEnemyColorAsActor))
                    {
                        manualActorColor = (global::PieceColor)EditorGUILayout.EnumPopup("Manual Actor", manualActorColor);
                    }
                    deterministicScanOrder = EditorGUILayout.Toggle("Deterministic", deterministicScanOrder);
                    seed = EditorGUILayout.IntField("Seed / Run Id", seed);
                    executeNormalSelectedCard = EditorGUILayout.Toggle("Execute Normal Card", executeNormalSelectedCard);
                    EditorGUILayout.HelpBox(
                        "강제 실행은 기존 AiCardExecutor.ExecutePlan 경로를 사용하므로 AI/Unity 실행 조건을 우회하지 않습니다.",
                        MessageType.Info);
                }
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

                if (GUILayout.Button("Scan", GUILayout.Width(72f)))
                {
                    ScanRegistry();
                    RefreshCatalog();
                }
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

    private void DrawToolbar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                registry = (CardLabRegistrySO)EditorGUILayout.ObjectField("Registry", registry, typeof(CardLabRegistrySO), false);
                aiCardHand = (AiCardHand)EditorGUILayout.ObjectField("AI Hand", aiCardHand, typeof(AiCardHand), true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                boardManager = (BoardManager)EditorGUILayout.ObjectField("Board", boardManager, typeof(BoardManager), true);
                gameManager = (GameManager)EditorGUILayout.ObjectField("Game", gameManager, typeof(GameManager), true);
                aiTurnController = (AiTurnController)EditorGUILayout.ObjectField("AI Turn", aiTurnController, typeof(AiTurnController), true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find Scene Objects", GUILayout.Width(140f)))
                {
                    ResolveSceneReferences();
                    RefreshHandSnapshot();
                }

                if (GUILayout.Button("Scan Card Prefabs", GUILayout.Width(140f)))
                {
                    ScanRegistry();
                    RefreshCatalog();
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
                {
                    RefreshCatalog();
                    RefreshHandSnapshot();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, GUILayout.Width(220f));
            }
        }
    }

    private void DrawCatalogPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(position.width * 0.32f)))
        {
            EditorGUILayout.LabelField("Card Catalog", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Prefabs", catalog.Count.ToString());

            catalogScroll = EditorGUILayout.BeginScrollView(catalogScroll, GUILayout.Height(260f));
            for (int i = 0; i < catalog.Count; i++)
            {
                CardData card = catalog[i];
                CardDataSO so = card != null ? card.DataSO : null;
                bool selected = i == selectedCatalogIndex;
                string label = FormatCardLabel(card);

                using (new EditorGUILayout.HorizontalScope(selected ? EditorStyles.helpBox : GUIStyle.none))
                {
                    if (GUILayout.Toggle(selected, GUIContent.none, GUILayout.Width(18f)))
                        selectedCatalogIndex = i;

                    EditorGUILayout.LabelField(label);
                    using (new EditorGUI.DisabledScope(so == null || !so.AiSupported))
                    {
                        if (GUILayout.Button("+", GUILayout.Width(28f)))
                            AddCardToHand(card);
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(!HasSelectedCatalogCard()))
            {
                if (GUILayout.Button("Add Selected To AI Hand"))
                    AddCardToHand(catalog[selectedCatalogIndex]);
            }

            if (HasSelectedCatalogCard())
                DrawCardDetails(catalog[selectedCatalogIndex]);
        }
    }

    private void DrawHandPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(position.width * 0.30f)))
        {
            EditorGUILayout.LabelField("AI Hand", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Cards", handCards.Count.ToString());

            handScroll = EditorGUILayout.BeginScrollView(handScroll, GUILayout.Height(220f));
            for (int i = 0; i < handCards.Count; i++)
            {
                GameObject cardObject = handCards[i];
                CardData card = cardObject != null ? cardObject.GetComponent<CardData>() : null;
                bool selected = i == selectedHandIndex;

                using (new EditorGUILayout.HorizontalScope(selected ? EditorStyles.helpBox : GUIStyle.none))
                {
                    if (GUILayout.Toggle(selected, GUIContent.none, GUILayout.Width(18f)))
                        selectedHandIndex = i;

                    EditorGUILayout.LabelField(FormatCardLabel(card));
                    if (GUILayout.Button("X", GUILayout.Width(28f)))
                    {
                        RemoveHandCard(i);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedHandIndex <= 0 || selectedHandIndex >= handCards.Count))
                {
                    if (GUILayout.Button("Priority Up"))
                        MoveHandCard(selectedHandIndex, selectedHandIndex - 1);
                }

                using (new EditorGUI.DisabledScope(selectedHandIndex < 0 || selectedHandIndex >= handCards.Count))
                {
                    if (GUILayout.Button("Force Selected"))
                        ForceSelectedCard();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Hand"))
                    ClearHand();

                if (GUILayout.Button("Scan Candidates"))
                    ScanCurrentHandCandidates();
            }

            DrawPresetControls();
        }
    }

    private void DrawRunPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Selection / Execution", EditorStyles.boldLabel);
            analysisDepth = EditorGUILayout.IntField("Analysis Depth", Mathf.Max(1, analysisDepth));
            variationCount = EditorGUILayout.IntField("Variation Count", Mathf.Max(1, variationCount));
            cardCandidateCount = EditorGUILayout.IntSlider("Card Candidates", cardCandidateCount, 1, MaxHandCards);
            targetCandidateCount = EditorGUILayout.IntField("Target Candidates", Mathf.Max(1, targetCandidateCount));
            maximumEngineCallCount = EditorGUILayout.IntField("Max Engine Calls", Mathf.Max(1, maximumEngineCallCount));
            allowCoarseCardEffects = EditorGUILayout.Toggle("Allow Coarse Effects", allowCoarseCardEffects);
            cardUseScoreTolerance = EditorGUILayout.IntField("Card Use Tolerance", Mathf.Max(0, cardUseScoreTolerance));
            useEnemyColorAsActor = EditorGUILayout.Toggle("Use Enemy As Actor", useEnemyColorAsActor);
            using (new EditorGUI.DisabledScope(useEnemyColorAsActor))
            {
                manualActorColor = (global::PieceColor)EditorGUILayout.EnumPopup("Manual Actor", manualActorColor);
            }
            deterministicScanOrder = EditorGUILayout.Toggle("Deterministic", deterministicScanOrder);
            seed = EditorGUILayout.IntField("Seed / Run Id", seed);
            executeNormalSelectedCard = EditorGUILayout.Toggle("Execute Normal Card", executeNormalSelectedCard);

            using (new EditorGUI.DisabledScope(isAnalysisRunning))
            {
                if (GUILayout.Button("Run Normal AI Selection"))
                    RunNormalAiSelection();
            }

            if (GUILayout.Button("Capture Board Summary"))
                CaptureBoardSummary();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Forced execution uses AiCardExecutor.ExecutePlan and still passes AI plan validation, Unity target validation, and ICard.Execute.",
                MessageType.Info);
        }
    }

    private void DrawPresetControls()
    {
        EditorGUILayout.Space(8f);
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
        if (aiCardHand == null)
            aiCardHand = UnityEngine.Object.FindFirstObjectByType<AiCardHand>();

        if (boardManager == null)
            boardManager = BoardManager.Instance != null ? BoardManager.Instance : UnityEngine.Object.FindFirstObjectByType<BoardManager>();

        if (gameManager == null)
            gameManager = GameManager.Instance != null ? GameManager.Instance : UnityEngine.Object.FindFirstObjectByType<GameManager>();

        if (aiTurnController == null)
            aiTurnController = UnityEngine.Object.FindFirstObjectByType<AiTurnController>();
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

    private void ScanRegistry()
    {
        if (registry == null)
        {
            AppendLog("Registry is not assigned. Create or assign CardLabRegistrySO first.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { CardPrefabFolder });
        var found = new List<CardData>();
        var seen = new HashSet<CardData>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CardData card = prefab != null ? prefab.GetComponent<CardData>() : null;
            if (card != null && seen.Add(card))
                found.Add(card);
        }

        found.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        Undo.RecordObject(registry, "Scan AI Card Debug Registry");
        registry.Cards = found;
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssetIfDirty(registry);
        AppendLog("Scanned card prefabs: " + found.Count);
    }

    private void RefreshCatalog()
    {
        catalog.Clear();

        if (registry != null && registry.Cards != null)
        {
            foreach (CardData card in registry.Cards)
            {
                if (card != null)
                    catalog.Add(card);
            }
        }

        if (catalog.Count == 0)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { CardPrefabFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                CardData card = prefab != null ? prefab.GetComponent<CardData>() : null;
                if (card != null)
                    catalog.Add(card);
            }
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

        selectedHandIndex = Mathf.Clamp(selectedHandIndex, 0, Mathf.Max(0, handCards.Count - 1));
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
        selectedHandIndex = handCards.Count - 1;
        AppendLog("Added card to AI hand: " + FormatCardLabel(card));
    }

    private void RemoveHandCard(int index)
    {
        if (index < 0 || index >= handCards.Count)
            return;

        GameObject removed = handCards[index];
        handCards.RemoveAt(index);
        ApplyHandCardsToComponent();
        selectedHandIndex = Mathf.Clamp(index, 0, Mathf.Max(0, handCards.Count - 1));
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
        selectedHandIndex = to;
        AppendLog("Moved card priority: " + FormatCardLabel(GetCardData(card)) + " -> " + to);
    }

    private void ClearHand()
    {
        handCards.Clear();
        ApplyHandCardsToComponent();
        AppendLog("Cleared AI hand.");
    }

    private void ApplyHandCardsToComponent()
    {
        if (aiCardHand == null)
        {
            AppendLog("AI Card Hand is not assigned.");
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
    }

    private void ScanCurrentHandCandidates()
    {
        if (!EnsureRuntimeContext())
            return;

        AiPieceColor actor = GetActor();
        UnityGameStateMappingResult mapping = CaptureMapping(actor);
        if (mapping == null)
            return;

        AppendLog("=== AI card candidate scan ===");
        AppendLog(FormatStateSummary(mapping));
        AppendLog("deterministic=" + deterministicScanOrder + ", seed=" + seed);

        IEnumerable<GameObject> cards = deterministicScanOrder
            ? SortedHandCards()
            : handCards;

        foreach (GameObject cardObject in cards)
            LogCandidatePlan(cardObject, mapping.GameState, actor);
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

        if (gameManager != null && gameManager.IsPlayerTurn)
        {
            QueueForcedCard(dataSO);
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

    private void RunNormalAiSelection()
    {
        if (!EnsureRuntimeContext())
            return;

        AiPieceColor perspective = GetActor();
        UnityGameStateMappingResult mapping = CaptureMapping(perspective);
        if (mapping == null)
            return;

        if (FairyStockfishBridge.Instance == null)
        {
            AppendLog("FairyStockfishBridge is missing.");
            return;
        }

        isAnalysisRunning = true;
        AppendLog("=== Normal AI selection ===");
        AppendLog(FormatStateSummary(mapping));
        AppendLog("analysisDepth=" + analysisDepth + ", variationCount=" + variationCount + ", actor=" + perspective + ", executeCard=" + executeNormalSelectedCard);

        foreach (GameObject cardObject in SortedHandCards())
            LogCandidatePlan(cardObject, mapping.GameState, perspective);

        FairyStockfishBridge.Instance.AnalyzePositionAsync(
            mapping.Fen,
            analysisDepth,
            variationCount,
            perspective,
            onComplete: (_, snapshot) =>
            {
                isAnalysisRunning = false;

                try
                {
                    HandleNormalAnalysisComplete(mapping, snapshot, perspective);
                }
                catch (Exception ex)
                {
                    AppendLog("normalSelection=failed, reason=" + ex.Message);
                }

                Repaint();
            },
            onError: (_, error) =>
            {
                isAnalysisRunning = false;
                AppendLog("normalSelection=analysisFailed, reason=" + error);
                Repaint();
            });
    }

    private void HandleNormalAnalysisComplete(
        UnityGameStateMappingResult mapping,
        UciAnalysisSnapshot snapshot,
        AiPieceColor actor)
    {
        if (snapshot == null || !snapshot.HasMoves)
        {
            AppendLog("normalSelection=no engine move candidates.");
            return;
        }

        UnifiedTurnPlanner planner = CreateTurnPlanner(mapping.Fen, snapshot);
        TurnPlannerResult result = planner.PlanTurn(mapping.GameState);
        LogTurnPlannerTrace(result);

        TurnPlan selectedPlan = SelectCardBiasedPlan(result);
        if (selectedPlan == null)
        {
            AppendLog("selectedPlan=null");
            return;
        }

        AppendLog(
            "selectedPlan rank=" + selectedPlan.DeterministicRankKey +
            ", usesCard=" + selectedPlan.UsesCard +
            ", hasMove=" + selectedPlan.HasMove +
            ", score=" + selectedPlan.Score.Total);

        if (selectedPlan.MovePlan != null)
            AppendLog("selectedMove=" + selectedPlan.MovePlan.UciMove);

        if (!selectedPlan.UsesCard || selectedPlan.CardPlan == null)
        {
            AppendLog("selectedCard=none");
            return;
        }

        AppendLog("selectedCard=" + selectedPlan.CardPlan.CardId + ", target=" + FormatTarget(selectedPlan.CardPlan.Target));

        GameObject selectedCardObject = FindHandCardByAiId(selectedPlan.CardPlan.CardId);
        if (selectedCardObject == null)
        {
            AppendLog("selectedCardExecution=blocked, status=CardNotInHand");
            return;
        }

        if (!targetPlanner.TryCreatePlan(
                selectedPlan.CardPlan,
                selectedCardObject,
                boardManager,
                mapping.GameState,
                actor,
                out AiCardTargetPlan plan,
                out AiCardExecutionStatus failureStatus,
                out string reason))
        {
            AppendLog("selectedTargetPlan=rejected, status=" + failureStatus + ", kind=" + ClassifyFailure(reason) + ", reason=" + reason);
            return;
        }

        AppendLog("selectedTargetPlan=accepted, " + FormatPlan(plan));

        if (!executeNormalSelectedCard)
            return;

        AiCardExecutionResult execution = cardExecutor.ExecutePlan(
            selectedPlan.CardPlan,
            aiCardHand,
            boardManager,
            mapping.GameState,
            actor);

        AppendLog("normalExecution=" + FormatExecution(execution));
        RefreshHandSnapshot();
    }

    private UnifiedTurnPlanner CreateTurnPlanner(string fen, UciAnalysisSnapshot snapshot)
    {
        var snapshotEngine = new FairyStockfishSnapshotEngine(
            fen,
            snapshot,
            FairyStockfishBridge.Instance.IsInCheck());
        var moveFilter = new MoveFilter(snapshotEngine);
        var options = new TurnPlannerOptions(
            noCardMoveCandidateCount: variationCount,
            cardCandidateCount: Mathf.Clamp(cardCandidateCount, 1, MaxHandCards),
            targetCandidateCount: Mathf.Max(1, targetCandidateCount),
            postCardMoveCandidateCount: variationCount,
            opponentReplyCandidateCount: 0,
            beamWidth: Mathf.Max(1, variationCount),
            maximumEngineCallCount: Mathf.Max(1, maximumEngineCallCount),
            allowCoarseCardEffects: allowCoarseCardEffects,
            seed: deterministicScanOrder ? seed : null);

        return new UnifiedTurnPlanner(
            moveFilter,
            cardTargetingModule,
            options: options);
    }

    private TurnPlan SelectCardBiasedPlan(TurnPlannerResult result)
    {
        if (result == null || !result.HasPlan)
            return null;

        TurnPlan selectedPlan = result.SelectedPlan;
        if (selectedPlan == null || selectedPlan.UsesCard)
            return selectedPlan;

        TurnPlan bestCardPlan = null;
        foreach (TurnPlanCandidate candidate in result.Candidates)
        {
            if (candidate == null || !candidate.HasPlan || candidate.Plan == null || !candidate.Plan.UsesCard)
                continue;

            if (bestCardPlan == null || candidate.Plan.Score.Total > bestCardPlan.Score.Total)
                bestCardPlan = candidate.Plan;
        }

        if (bestCardPlan == null)
            return selectedPlan;

        int tolerance = Mathf.Max(0, cardUseScoreTolerance);
        int scoreGap = selectedPlan.Score.Total - bestCardPlan.Score.Total;
        if (scoreGap > tolerance)
            return selectedPlan;

        AppendLog(
            "cardBias=selectedCardPlan, noCardScore=" + selectedPlan.Score.Total +
            ", cardScore=" + bestCardPlan.Score.Total +
            ", gap=" + scoreGap +
            ", tolerance=" + tolerance +
            ", card=" + (bestCardPlan.CardPlan != null ? bestCardPlan.CardPlan.CardId : "<none>"));
        return bestCardPlan;
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

    private void CaptureBoardSummary()
    {
        if (!EnsureRuntimeContext())
            return;

        UnityGameStateMappingResult mapping = CaptureMapping(GetActor());
        if (mapping != null)
            AppendLog(FormatStateSummary(mapping));
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
        if (useEnemyColorAsActor && gameManager != null)
            return gameManager.EnemyColor;

        return manualActorColor;
    }

    private IEnumerable<GameObject> SortedHandCards()
    {
        var sorted = new List<GameObject>(handCards);
        sorted.Sort((a, b) => string.Compare(FormatCardLabel(GetCardData(a)), FormatCardLabel(GetCardData(b)), StringComparison.Ordinal));
        return sorted;
    }

    private GameObject FindHandCardByAiId(string aiCardId)
    {
        if (string.IsNullOrWhiteSpace(aiCardId))
            return null;

        foreach (GameObject cardObject in handCards)
        {
            CardData cardData = GetCardData(cardObject);
            CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
            if (dataSO != null && string.Equals(dataSO.AiCardId, aiCardId, StringComparison.OrdinalIgnoreCase))
                return cardObject;
        }

        return null;
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
               ", tileEffects=" + mapping.GameState.TileEffects.Count +
               ", pieces=" + mapping.GameState.BoardState.Pieces.Count +
               ", actor=" + mapping.GameState.BoardState.SideToMove;
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

    private void LogTurnPlannerTrace(TurnPlannerResult result)
    {
        if (result == null)
        {
            AppendLog("plannerTrace=<null>");
            return;
        }

        TurnPlannerTraceSummary trace = result.TraceSummary;
        AppendLog(
            "plannerTrace selected=" + trace.SelectedCandidateCount +
            ", skipped=" + trace.SkippedCandidateCount +
            ", rootMoves=" + trace.RootNoCardMoveCandidateCount +
            ", consideredCards=" + trace.ConsideredCardCandidateCount +
            ", postCardMoves=" + trace.PostCardMoveCandidateCount +
            ", engineCalls=" + trace.EngineCallCount + "/" + trace.MaximumEngineCallCount +
            ", beamPruned=" + trace.BeamPrunedCandidateCount);
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
