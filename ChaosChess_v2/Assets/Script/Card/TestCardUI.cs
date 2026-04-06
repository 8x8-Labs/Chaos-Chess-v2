using UnityEngine;

public class TestCardUI : MonoBehaviour
{
    [SerializeField] private CardType cardType;
    private CardData cardData;

    private void Start()
    {
        cardData = GetComponent<CardData>();
    }

    public void Execute()
    {
        switch (cardType)
        {
            case CardType.Piece:
                cardData.GetComponent<IPieceCard>().LoadPieceSelector(); break; 
            case CardType.Tile:
                cardData.GetComponent<ITileCard>().LoadTileSelector(); break;
            default:
                cardData.GetComponent<ICard>().Execute(); break;

        }
    }
}
