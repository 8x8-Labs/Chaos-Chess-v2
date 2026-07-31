using System;
using System.Collections.Generic;
using System.Globalization;
using ChaosChess.AI.Domain;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;

namespace ChaosChess.Unity.AIIntegration.Engine
{
    public static class UciMultiPvParser
    {
        public static UciAnalysisSnapshot Parse(
            string uciOutput,
            int requestedVariationCount,
            AiPieceColor perspective)
        {
            if (uciOutput == null)
                throw new ArgumentNullException(nameof(uciOutput));

            return ParseLines(
                uciOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None),
                requestedVariationCount,
                perspective);
        }

        public static UciAnalysisSnapshot ParseLines(
            IEnumerable<string> lines,
            int requestedVariationCount,
            AiPieceColor perspective)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            if (requestedVariationCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedVariationCount), requestedVariationCount, "Requested variation count must be positive.");

            var warnings = new List<string>();
            var byDepth = new Dictionary<int, Dictionary<int, UciMoveAnalysis>>();
            string bestMove = null;

            foreach (string line in lines)
            {
                string trimmed = line == null ? string.Empty : line.Trim();
                if (trimmed.Length == 0)
                    continue;

                if (TryParseBestMove(trimmed, out string parsedBestMove))
                {
                    bestMove = parsedBestMove;
                    continue;
                }

                if (!trimmed.StartsWith("info ", StringComparison.Ordinal))
                    continue;

                if (!TryParseInfoLine(trimmed, out UciMoveAnalysis analysis, out string warning))
                {
                    if (!string.IsNullOrEmpty(warning))
                        warnings.Add(warning);
                    continue;
                }

                if (!byDepth.TryGetValue(analysis.Depth, out Dictionary<int, UciMoveAnalysis> depthMoves))
                {
                    depthMoves = new Dictionary<int, UciMoveAnalysis>();
                    byDepth.Add(analysis.Depth, depthMoves);
                }

                depthMoves[analysis.VariationIndex] = analysis;
            }

            int selectedDepth = SelectDepth(byDepth, requestedVariationCount);
            var selectedMoves = new List<UciMoveAnalysis>();

            if (selectedDepth > 0 && byDepth.TryGetValue(selectedDepth, out Dictionary<int, UciMoveAnalysis> selectedByIndex))
            {
                for (int index = 1; index <= requestedVariationCount; index++)
                {
                    if (selectedByIndex.TryGetValue(index, out UciMoveAnalysis analysis))
                        selectedMoves.Add(analysis);
                }

                if (selectedMoves.Count < requestedVariationCount)
                {
                    warnings.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "Requested {0} variations but parsed {1} at depth {2}.",
                        requestedVariationCount,
                        selectedMoves.Count,
                        selectedDepth));
                }
            }

            if (bestMove == "none")
                warnings.Add("Engine reported bestmove none.");

            return new UciAnalysisSnapshot(
                perspective,
                requestedVariationCount,
                selectedDepth,
                selectedMoves,
                bestMove,
                warnings);
        }

        public static bool IsValidUciMove(string move)
        {
            if (string.IsNullOrEmpty(move) || (move.Length != 4 && move.Length != 5))
                return false;

            return move[0] >= 'a' && move[0] <= 'h'
                && move[1] >= '1' && move[1] <= '8'
                && move[2] >= 'a' && move[2] <= 'h'
                && move[3] >= '1' && move[3] <= '8'
                && (move.Length == 4 || IsAsciiLetter(move[4]));
        }

        private static bool TryParseBestMove(string line, out string bestMove)
        {
            bestMove = null;
            if (!line.StartsWith("bestmove", StringComparison.Ordinal))
                return false;

            string[] parts = SplitTokens(line);
            bestMove = parts.Length > 1 ? parts[1] : "none";
            return true;
        }

        private static bool TryParseInfoLine(
            string line,
            out UciMoveAnalysis analysis,
            out string warning)
        {
            analysis = null;
            warning = null;

            string[] tokens = SplitTokens(line);
            int depth = 0;
            int variationIndex = 1;
            int? scoreCentipawns = null;
            int? mateIn = null;
            UciScoreBound bound = UciScoreBound.Exact;
            int pvIndex = -1;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];

                if (token == "depth" && i + 1 < tokens.Length)
                {
                    int.TryParse(tokens[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out depth);
                }
                else if (token == "multipv" && i + 1 < tokens.Length)
                {
                    int.TryParse(tokens[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out variationIndex);
                }
                else if (token == "score" && i + 2 < tokens.Length)
                {
                    string scoreKind = tokens[++i];
                    string scoreValue = tokens[++i];
                    if (!int.TryParse(scoreValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedScore))
                    {
                        warning = "Skipped info line with invalid score: " + line;
                        return false;
                    }

                    if (scoreKind == "cp")
                        scoreCentipawns = parsedScore;
                    else if (scoreKind == "mate")
                        mateIn = parsedScore;
                    else
                    {
                        warning = "Skipped info line with unknown score type: " + line;
                        return false;
                    }
                }
                else if (token == "lowerbound")
                {
                    bound = UciScoreBound.Lower;
                }
                else if (token == "upperbound")
                {
                    bound = UciScoreBound.Upper;
                }
                else if (token == "pv")
                {
                    pvIndex = i + 1;
                    break;
                }
            }

            if (depth <= 0)
            {
                warning = "Skipped info line without positive depth: " + line;
                return false;
            }

            if (variationIndex <= 0)
            {
                warning = "Skipped info line without positive multipv index: " + line;
                return false;
            }

            if (scoreCentipawns.HasValue == mateIn.HasValue)
            {
                warning = "Skipped info line without exactly one score: " + line;
                return false;
            }

            if (mateIn == 0)
            {
                warning = "Skipped info line with mate 0 score: " + line;
                return false;
            }

            if (pvIndex < 0 || pvIndex >= tokens.Length)
            {
                warning = "Skipped info line without pv moves: " + line;
                return false;
            }

            var pv = new List<string>();
            for (int i = pvIndex; i < tokens.Length; i++)
            {
                if (!IsValidUciMove(tokens[i]))
                {
                    warning = "Skipped info line with invalid pv move: " + line;
                    return false;
                }

                pv.Add(tokens[i]);
            }

            try
            {
                var score = new UciAnalysisScore(scoreCentipawns, mateIn, bound);
                analysis = new UciMoveAnalysis(variationIndex, depth, score, pv);
                return true;
            }
            catch (Exception ex)
            {
                warning = "Skipped info line: " + ex.Message + " Line: " + line;
                return false;
            }
        }

        private static int SelectDepth(
            Dictionary<int, Dictionary<int, UciMoveAnalysis>> byDepth,
            int requestedVariationCount)
        {
            int bestCompleteDepth = 0;
            int bestPartialDepth = 0;

            foreach (KeyValuePair<int, Dictionary<int, UciMoveAnalysis>> entry in byDepth)
            {
                bool complete = true;
                for (int index = 1; index <= requestedVariationCount; index++)
                {
                    if (!entry.Value.ContainsKey(index))
                    {
                        complete = false;
                        break;
                    }
                }

                if (complete && entry.Key > bestCompleteDepth)
                    bestCompleteDepth = entry.Key;

                if (entry.Value.Count > 0 && entry.Key > bestPartialDepth)
                    bestPartialDepth = entry.Key;
            }

            return bestCompleteDepth > 0 ? bestCompleteDepth : bestPartialDepth;
        }

        private static string[] SplitTokens(string value)
        {
            return value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool IsAsciiLetter(char value)
        {
            return (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');
        }
    }
}
