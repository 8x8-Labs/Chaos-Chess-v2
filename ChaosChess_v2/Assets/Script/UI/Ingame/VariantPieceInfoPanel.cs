using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엘리트 노드 진입 시 새로 등장한 변형 기물들을 순차 카드 형식으로 설명하는 모달 패널입니다.
/// 여러 종류가 등장하면 "다음" 버튼으로 한 종류씩 넘기고, 마지막에서 닫으면 게임 입력이 재개됩니다.
/// 표시 중에는 PromotionPanel 등과 동일하게 GameManager.IsGameInput을 false로 막습니다.
/// </summary>
public class VariantPieceInfoPanel : ButtonPanel
{
    private GameManager gameManager = GameManager.Instance;

    [Header("Data")]
    [SerializeField] private VariantPieceInfoSO infoDatabase;

    [Header("UI Elements")]
    [SerializeField] private TMP_Text pieceNameText;
    [SerializeField] private Image pieceImage;
    [SerializeField] private Image movementImage;
    [SerializeField] private TMP_Text descriptionText;
    [Tooltip("\"1 / 2\" 형태의 페이지 표시 (선택)")]
    [SerializeField] private TMP_Text pageIndicatorText;
    [Tooltip("마지막 페이지에서 '닫기'로 바꾸고 싶을 때 라벨 갱신용 (선택)")]
    [SerializeField] private TMP_Text nextButtonLabel;
    [SerializeField] private string nextLabel = "다음";
    [SerializeField] private string closeLabel = "확인";

    private readonly List<VariantPieceInfoSO.Entry> queue = new List<VariantPieceInfoSO.Entry>();
    private int index;

    /// <summary>주어진 변형 기물 종류들을 순서대로 설명합니다. 데이터가 없는 종류는 건너뜁니다.</summary>
    public void Show(IReadOnlyList<PieceType> types)
    {
        if (infoDatabase == null || types == null || types.Count == 0) return;

        queue.Clear();
        for (int i = 0; i < types.Count; i++)
        {
            VariantPieceInfoSO.Entry entry = infoDatabase.Get(types[i]);
            if (entry != null) queue.Add(entry);
        }
        if (queue.Count == 0) return;

        index = 0;
        EnablePanel();
        RenderCurrent();
    }

    public override void EnablePanel()
    {
        base.EnablePanel();
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gameManager != null) gameManager.IsGameInput = false;
    }

    public override void DisablePanel()
    {
        base.DisablePanel();
        RestoreGameInput();
    }

    // "확인" 버튼을 거치지 않고 씬 전환·부모 비활성화 등으로 패널이 닫히면 DisablePanel이 호출되지
    // 않아 IsGameInput이 false로 남는 소프트락이 발생할 수 있으므로, OnDisable에서 안전하게 복구한다.
    private void OnDisable()
    {
        RestoreGameInput();
    }

    private void RestoreGameInput()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gameManager != null) gameManager.IsGameInput = true;
    }

    /// <summary>"다음" 버튼 OnClick에 연결합니다. 마지막 페이지에서는 패널을 닫습니다.</summary>
    public void OnClickNext()
    {
        index++;
        if (index >= queue.Count)
        {
            DisablePanel();
            return;
        }
        RenderCurrent();
    }

    private void RenderCurrent()
    {
        VariantPieceInfoSO.Entry entry = queue[index];

        if (pieceNameText != null) pieceNameText.text = entry.pieceName;
        if (pieceImage != null) pieceImage.sprite = entry.pieceImage;
        if (movementImage != null) movementImage.sprite = entry.movementImage;
        if (descriptionText != null) descriptionText.text = entry.description;
        if (pageIndicatorText != null) pageIndicatorText.text = $"{index + 1} / {queue.Count}";

        if (nextButtonLabel != null)
            nextButtonLabel.text = (index == queue.Count - 1) ? closeLabel : nextLabel;
    }
}
