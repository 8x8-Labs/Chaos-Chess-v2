using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardTargetUI : MonoBehaviour
{
    [SerializeField] private Image[] pieceIconSlots;
    [SerializeField] private TMP_Text typeLabel;
    [SerializeField] private PieceIconSetSO pieceIconSet;

    private static readonly PieceType[] PieceOrder =
    {
        PieceType.Pawn, PieceType.Knight, PieceType.Bishop,
        PieceType.Rook, PieceType.Queen, PieceType.King,
        PieceType.Chancellor, PieceType.Amazon, PieceType.KnightRider, PieceType.Wall
    };

    public void SetData(CardDataSO data)
    {
        if (data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        switch (data.Type)
        {
            case CardType.Piece:
                ShowLabel("적용 대상");
                ShowPieceIcons(data);
                break;
            case CardType.Tile:
                ShowLabel("타일 설치");
                break;
            case CardType.Global:
                ShowLabel("전역 효과");
                break;
        }
    }

    private void ShowPieceIcons(CardDataSO data)
    {
        int slotIndex = 0;
        foreach (PieceType flag in PieceOrder)
        {
            if (slotIndex >= pieceIconSlots.Length) break;
            if ((data.PieceType & flag) == 0) continue;

            Sprite icon = pieceIconSet != null ? pieceIconSet.GetIcon(flag, data.PieceTargetColor) : null;
            if (icon == null) continue;

            if (pieceIconSlots[slotIndex] != null)
            {
                pieceIconSlots[slotIndex].sprite = icon;
                pieceIconSlots[slotIndex].gameObject.SetActive(true);
            }
            slotIndex++;
        }

        for (int i = slotIndex; i < pieceIconSlots.Length; i++)
        {
            if (pieceIconSlots[i] != null)
                pieceIconSlots[i].gameObject.SetActive(false);
        }
    }

    private void ShowLabel(string text)
    {
        foreach (var slot in pieceIconSlots)
        {
            if (slot != null)
                slot.gameObject.SetActive(false);
        }

        if (typeLabel != null)
        {
            typeLabel.gameObject.SetActive(true);
            typeLabel.text = text;
        }
    }
}
