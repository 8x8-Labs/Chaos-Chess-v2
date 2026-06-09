using TMPro;
using UnityEngine;

/// <summary>
/// 카드 이펙트 랩 씬의 중앙 오케스트레이터입니다.
/// 레지스트리에서 카드를 골라 즉석에서 instantiate → 실행하고,
/// 임의 FEN으로 보드를 세팅하며, AI 응수를 토글합니다.
/// </summary>
public class CardLabController : MonoBehaviour
{
    private const string DefaultFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Header("데이터")]
    [SerializeField] private CardLabRegistrySO registry;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown cardDropdown;
    [SerializeField] private TMP_InputField fenInput;
    [SerializeField] private TMP_Text aiToggleLabel;

    [Header("씬 참조")]
    [Tooltip("instantiate된 카드 인스턴스를 담을 부모입니다.")]
    [SerializeField] private Transform cardHolder;
    [SerializeField] private EffectorInspectorPanel inspector;

    private void Start()
    {
        PopulateDropdown();

        if (fenInput != null && string.IsNullOrEmpty(fenInput.text))
            fenInput.text = DefaultFen;

        UpdateAiToggleLabel();
    }

    private void PopulateDropdown()
    {
        if (cardDropdown == null || registry == null)
            return;

        cardDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>(registry.Count);
        for (int i = 0; i < registry.Count; i++)
            options.Add(registry.GetDisplayName(i));

        cardDropdown.AddOptions(options);
        cardDropdown.RefreshShownValue();
    }

    // ───────── 버튼 핸들러 (Inspector에서 OnClick 연결) ─────────

    /// <summary>현재 FEN 입력값으로 보드를 다시 세팅합니다. 기존 이펙터·기물을 먼저 정리합니다.</summary>
    public void SetupBoard()
    {
        if (BoardManager.Instance == null)
            return;

        // 1) 활성 이펙터 + 타일 이펙트 정리
        if (inspector != null)
            inspector.RevertAll();
        BoardManager.Instance.ClearAllTileEffectors();

        // 2) 현존 기물 GameObject 파괴 (LoadFEN은 내부 상태만 초기화하고 GameObject는 남김)
        Piece[] pieces = FindObjectsByType<Piece>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Piece p in pieces)
        {
            if (p != null)
                Destroy(p.gameObject);
        }

        // 3) FEN 로드
        string fen = fenInput != null && !string.IsNullOrWhiteSpace(fenInput.text)
            ? fenInput.text.Trim()
            : DefaultFen;
        BoardManager.Instance.LoadFEN(fen);
    }

    /// <summary>드롭다운에서 선택한 카드를 instantiate하고 타입에 맞게 실행합니다.</summary>
    public void TriggerCard()
    {
        if (registry == null || cardDropdown == null || registry.Count == 0)
            return;

        int index = cardDropdown.value;
        if (index < 0 || index >= registry.Count)
            return;

        CardData prefab = registry.Cards[index];
        if (prefab == null)
        {
            Debug.LogWarning("[CardLab] 선택된 카드 프리팹이 null입니다.");
            return;
        }

        CardData instance = Instantiate(prefab, cardHolder);
        CardDataSO so = instance.DataSO;

        if (so == null)
        {
            Debug.LogWarning($"[CardLab] {instance.name}에 DataSO가 없습니다.");
            return;
        }

        switch (so.Type)
        {
            case CardType.Piece:
                if (instance is IPieceCard pieceCard)
                    pieceCard.LoadPieceSelector();
                else
                    Debug.LogWarning($"[CardLab] {instance.name}이(가) IPieceCard를 구현하지 않습니다.");
                break;

            case CardType.Tile:
                if (instance is ITileCard tileCard)
                    tileCard.LoadTileSelector();
                else
                    Debug.LogWarning($"[CardLab] {instance.name}이(가) ITileCard를 구현하지 않습니다.");
                break;

            case CardType.Global:
            default:
                if (instance is ICard card)
                    card.Execute();
                else
                    Debug.LogWarning($"[CardLab] {instance.name}이(가) ICard를 구현하지 않습니다.");
                break;
        }
    }

    /// <summary>한 턴 진행합니다. AI가 꺼져 있으면 양쪽을 수동으로 두며 이펙터 카운트다운을 관찰할 수 있습니다.</summary>
    public void StepTurn()
    {
        GameManager.Instance?.NextTurn();
    }

    /// <summary>활성 이펙터를 모두 해제합니다.</summary>
    public void ClearEffects()
    {
        if (inspector != null)
            inspector.RevertAll();
        BoardManager.Instance?.ClearAllTileEffectors();
    }

    /// <summary>AI 자동 응수를 토글합니다.</summary>
    public void ToggleAi()
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.AiAutoMoveEnabled = !GameManager.Instance.AiAutoMoveEnabled;
        UpdateAiToggleLabel();
    }

    private void UpdateAiToggleLabel()
    {
        if (aiToggleLabel == null)
            return;

        bool on = GameManager.Instance != null && GameManager.Instance.AiAutoMoveEnabled;
        aiToggleLabel.text = on ? "AI: ON" : "AI: OFF";
    }
}
