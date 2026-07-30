using System;
using ChaosChess.AI.Domain;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;

namespace ChaosChess.Unity.AIIntegration.Engine
{
    public static class UciMultiPvParserSelfTest
    {
        public static void Run()
        {
            ParsesCompletedDepthSet();
            FallsBackToPartialDepthWithWarning();
            SkipsInvalidPv();
        }

        private static void ParsesCompletedDepthSet()
        {
            string output =
                "info depth 8 multipv 1 score cp 34 nodes 10 pv e2e4 e7e5\n" +
                "info depth 8 multipv 2 score mate -3 nodes 11 pv g1f3 b8c6\n" +
                "info depth 9 multipv 1 score cp 41 nodes 12 pv d2d4 d7d5\n" +
                "info depth 9 multipv 2 score cp 17 lowerbound nodes 13 pv c2c4 e7e6\n" +
                "bestmove d2d4";

            UciAnalysisSnapshot snapshot = UciMultiPvParser.Parse(output, 2, AiPieceColor.Black);

            Assert(snapshot.SelectedDepth == 9, "Expected deepest completed depth.");
            Assert(snapshot.Moves.Count == 2, "Expected two moves.");
            Assert(snapshot.Moves[0].UciMove == "d2d4", "Expected multipv 1 first.");
            Assert(snapshot.Moves[1].Score.Bound == UciScoreBound.Lower, "Expected lowerbound flag.");
            Assert(snapshot.ToPositionEvaluation().Perspective == AiPieceColor.Black, "Expected perspective to be preserved.");
        }

        private static void FallsBackToPartialDepthWithWarning()
        {
            string output =
                "info depth 5 multipv 1 score cp 10 pv e2e4\n" +
                "bestmove none";

            UciAnalysisSnapshot snapshot = UciMultiPvParser.Parse(output, 3, AiPieceColor.White);

            Assert(snapshot.SelectedDepth == 5, "Expected partial depth fallback.");
            Assert(snapshot.Moves.Count == 1, "Expected one parsed move.");
            Assert(snapshot.BestMove == "none", "Expected bestmove none.");
            Assert(snapshot.Warnings.Count >= 2, "Expected partial and bestmove warnings.");
        }

        private static void SkipsInvalidPv()
        {
            string output =
                "info depth 6 multipv 1 score cp 10 pv invalid\n" +
                "info depth 6 multipv 2 score cp 5 pv e2e4";

            UciAnalysisSnapshot snapshot = UciMultiPvParser.Parse(output, 2, AiPieceColor.White);

            Assert(snapshot.Moves.Count == 1, "Expected invalid pv to be skipped.");
            Assert(snapshot.Moves[0].VariationIndex == 2, "Expected remaining multipv index.");
            Assert(snapshot.Warnings.Count >= 2, "Expected invalid pv and partial warnings.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
