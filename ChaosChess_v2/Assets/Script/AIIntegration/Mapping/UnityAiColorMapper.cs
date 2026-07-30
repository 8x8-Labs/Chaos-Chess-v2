using AiPieceColor = ChaosChess.AI.Domain.PieceColor;

namespace ChaosChess.Unity.AIIntegration.Mapping
{
    public static class UnityAiColorMapper
    {
        public static AiPieceColor ToAiColor(global::PieceColor color)
        {
            return color == global::PieceColor.White
                ? AiPieceColor.White
                : AiPieceColor.Black;
        }
    }
}
