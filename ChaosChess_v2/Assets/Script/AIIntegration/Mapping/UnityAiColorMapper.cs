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

        public static global::PieceColor ToUnityColor(AiPieceColor color)
        {
            return color == AiPieceColor.White
                ? global::PieceColor.White
                : global::PieceColor.Black;
        }
    }
}
