using System.Collections.Generic;
using System.Text;

/// <summary>
/// 입장 반전 - 전역
/// 자신의 기물 전체와 상대 기물 전체를 교환하며 모든 기물, 타일 효과가 사라집니다.
/// </summary>
public class PositionSwapCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        BoardManager bm = BoardManager.Instance;

        string fen = bm.GetFEN();

        string flipped = FlipFEN(fen);

        bm.DestroyPieces(bm.GetAllPieces(), false);
        bm.LoadFEN(flipped);
        bm.ClearAllTileEffectors();
        bm.RefreshMoves();

        GameManager.Instance.NextTurn();
        if (!GameManager.Instance.IsPlayerTurn)
            GameManager.Instance.RequestAIMove();
    }

    private string FlipFEN(string fen)
    {
        string[] parts = fen.Split(' ');

        string[] ranks = parts[0].Split('/');
        List<string> newRanks = new List<string>();

        for (int i = 7; i >= 0; i--)
        {
            string rank = ranks[i];
            List<char> expanded = new List<char>();

            foreach (char c in rank)
            {
                if (char.IsDigit(c))
                {
                    int count = c - '0';
                    for (int k = 0; k < count; k++)
                        expanded.Add('.');
                }
                else expanded.Add(c);
            }

            for (int j = 0; j < expanded.Count; j++)
            {
                char c = expanded[j];
                if (c == '.') continue;

                expanded[j] = char.IsUpper(c)
                    ? char.ToLower(c)
                    : char.ToUpper(c);
            }

            StringBuilder newRank = new StringBuilder();
            int empty = 0;

            foreach (char c in expanded)
            {
                if (c == '.') empty++;
                else
                {
                    if (empty > 0)
                    {
                        newRank.Append(empty);
                        empty = 0;
                    }
                    newRank.Append(c);
                }
            }
            if (empty > 0) newRank.Append(empty);

            newRanks.Add(newRank.ToString());
        }

        string newBoard = string.Join("/", newRanks);

        // 캐슬링 반전
        string castling = parts[2];
        string newCastling = "-";
        if (castling != "-")
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in castling)
            {
                if (c == 'K') sb.Append('k');
                else if (c == 'Q') sb.Append('q');
                else if (c == 'k') sb.Append('K');
                else if (c == 'q') sb.Append('Q');
            }
            newCastling = sb.Length == 0 ? "-" : sb.ToString();
        }

        // 앙파상 반전
        string ep = parts[3];
        string newEP = "-";
        if (ep != "-")
        {
            char file = ep[0];
            char rank = ep[1];

            char newFile = (char)('h' - (file - 'a'));
            char newRank = (char)('8' - (rank - '1'));

            newEP = $"{newFile}{newRank}";
        }

        return $"{newBoard} {parts[1]} {newCastling} {newEP} {parts[4]} {parts[5]}";
    }
}
