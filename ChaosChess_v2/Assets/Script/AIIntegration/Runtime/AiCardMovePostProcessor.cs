using System;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Runtime
{
    public sealed class AiCardMovePostProcessor
    {
        public delegate bool MoveAllowedPredicate(string uciMove);

        public bool TrySelectMove(
            string engineMove,
            BoardManager boardManager,
            FairyStockfishBridge stockfish,
            Piece lockedPiece,
            int extraActions,
            MoveAllowedPredicate isMoveAllowed,
            out string selectedMove)
        {
            selectedMove = null;

            if (boardManager == null || stockfish == null || isMoveAllowed == null)
                return false;

            if (TrySelectDesperadoFinalMove(
                engineMove,
                boardManager,
                stockfish,
                lockedPiece,
                extraActions,
                isMoveAllowed,
                out selectedMove))
            {
                return true;
            }

            return false;
        }

        public bool ShouldRejectMove(
            string uciMove,
            BoardManager boardManager,
            Piece lockedPiece,
            int extraActions)
        {
            if (IsDesperadoFinalAction(lockedPiece, extraActions) &&
                !IsDesperadoCaptureMove(boardManager, lockedPiece, uciMove))
            {
                return true;
            }

            return IsAwakenedMoveExposesTrade(boardManager, uciMove);
        }

        private static bool TrySelectDesperadoFinalMove(
            string engineMove,
            BoardManager boardManager,
            FairyStockfishBridge stockfish,
            Piece lockedPiece,
            int extraActions,
            MoveAllowedPredicate isMoveAllowed,
            out string selectedMove)
        {
            selectedMove = null;

            if (extraActions <= 0 ||
                lockedPiece == null ||
                !lockedPiece ||
                lockedPiece.GetComponent<DesperadoEffect>() == null)
            {
                return false;
            }

            string[] legalMoves = stockfish.GetLegalMoves();
            if (legalMoves == null || legalMoves.Length == 0)
                return false;

            string bestMove = null;
            int bestScore = int.MinValue;
            foreach (string move in legalMoves)
            {
                if (!boardManager.IsValidUciMove(move) || !isMoveAllowed(move))
                    continue;

                if (!IsMoveFromLockedPiece(boardManager, lockedPiece, move))
                    continue;

                int score = ScoreDesperadoFinalMove(boardManager, lockedPiece, move);
                if (score <= 0)
                    continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }

            if (bestMove == null)
                return false;

            int engineScore = boardManager.IsValidUciMove(engineMove) && isMoveAllowed(engineMove)
                && IsMoveFromLockedPiece(boardManager, lockedPiece, engineMove)
                && IsDesperadoCaptureMove(boardManager, lockedPiece, engineMove)
                ? ScoreDesperadoFinalMove(boardManager, lockedPiece, engineMove)
                : int.MinValue;
            selectedMove = bestScore > engineScore ? bestMove : engineMove;

            Debug.Log(
                $"[AI] Desperado final move selected '{selectedMove}' " +
                $"best={bestMove}:{bestScore}, engine={engineMove}:{engineScore}.");
            return true;
        }

        private static int ScoreDesperadoFinalMove(
            BoardManager boardManager,
            Piece lockedPiece,
            string uciMove)
        {
            if (string.IsNullOrWhiteSpace(uciMove) || uciMove.Length < 4)
                return int.MinValue;

            Vector3Int destination = boardManager.UCIToGrid(uciMove.Substring(2, 2));
            Piece capturedPiece = boardManager.GetPiece(destination);
            int captureValue = capturedPiece != null && capturedPiece.Color != lockedPiece.Color
                ? GetPieceValue(capturedPiece.Type)
                : 0;

            return captureValue;
        }

        private static bool IsDesperadoFinalAction(Piece lockedPiece, int extraActions)
        {
            return extraActions > 0 &&
                lockedPiece != null &&
                lockedPiece &&
                lockedPiece.GetComponent<DesperadoEffect>() != null;
        }

        private static bool IsDesperadoCaptureMove(
            BoardManager boardManager,
            Piece lockedPiece,
            string uciMove)
        {
            if (boardManager == null ||
                !IsMoveFromLockedPiece(boardManager, lockedPiece, uciMove) ||
                string.IsNullOrWhiteSpace(uciMove) ||
                uciMove.Length < 4)
            {
                return false;
            }

            Vector3Int destination = boardManager.UCIToGrid(uciMove.Substring(2, 2));
            Piece capturedPiece = boardManager.GetPiece(destination);
            return capturedPiece != null && capturedPiece.Color != lockedPiece.Color;
        }

        private static bool IsMoveFromLockedPiece(
            BoardManager boardManager,
            Piece lockedPiece,
            string uciMove)
        {
            if (boardManager == null ||
                lockedPiece == null ||
                !lockedPiece ||
                string.IsNullOrWhiteSpace(uciMove) ||
                uciMove.Length < 2)
            {
                return false;
            }

            string lockedSource = boardManager.GridTOUCI(lockedPiece.Pos);
            return string.Equals(
                uciMove.Substring(0, 2),
                lockedSource,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetMoveInfo(
            BoardManager boardManager,
            string uciMove,
            out Vector3Int source,
            out Vector3Int destination,
            out Piece movingPiece,
            out Piece capturedPiece)
        {
            source = default;
            destination = default;
            movingPiece = null;
            capturedPiece = null;

            if (boardManager == null ||
                string.IsNullOrWhiteSpace(uciMove) ||
                uciMove.Length < 4 ||
                !boardManager.IsValidUciMove(uciMove))
            {
                return false;
            }

            source = boardManager.UCIToGrid(uciMove.Substring(0, 2));
            destination = boardManager.UCIToGrid(uciMove.Substring(2, 2));
            movingPiece = boardManager.GetPiece(source);
            if (movingPiece == null)
                return false;

            capturedPiece = boardManager.GetPiece(destination);
            return capturedPiece == null || capturedPiece.Color != movingPiece.Color;
        }

        private static bool IsAwakenedMoveExposesTrade(
            BoardManager boardManager,
            string uciMove)
        {
            if (string.IsNullOrWhiteSpace(uciMove) || uciMove.Length < 4)
                return false;

            Vector3Int source = boardManager.UCIToGrid(uciMove.Substring(0, 2));
            Vector3Int destination = boardManager.UCIToGrid(uciMove.Substring(2, 2));
            Piece movingPiece = boardManager.GetPiece(source);
            if (movingPiece == null || !movingPiece.IsAwakened)
                return false;

            Piece capturedPiece = boardManager.GetPiece(destination);
            if (capturedPiece != null && capturedPiece.Color == movingPiece.Color)
                return false;

            return IsSquareAttackedByOpponentAfterMove(boardManager, movingPiece, source, destination);
        }

        private static bool IsSquareAttackedByOpponentAfterMove(
            BoardManager boardManager,
            Piece movingPiece,
            Vector3Int source,
            Vector3Int destination)
        {
            foreach (Piece attacker in boardManager.GetAllPieces())
            {
                if (attacker == null ||
                    attacker == movingPiece ||
                    attacker.Color == movingPiece.Color ||
                    attacker.Type == PieceType.Wall ||
                    attacker.Pos == destination)
                {
                    continue;
                }

                if (CanAttackAfterMove(boardManager, attacker, movingPiece, source, destination))
                    return true;
            }

            return false;
        }

        private static bool CanAttackAfterMove(
            BoardManager boardManager,
            Piece attacker,
            Piece movingPiece,
            Vector3Int source,
            Vector3Int destination)
        {
            Vector3Int attackerPos = attacker.Pos;
            int dx = destination.x - attackerPos.x;
            int dy = destination.y - attackerPos.y;
            int absX = Mathf.Abs(dx);
            int absY = Mathf.Abs(dy);
            PieceType attackerType = GetEffectivePieceType(attacker);

            switch (attackerType)
            {
                case PieceType.Pawn:
                    int pawnDirection = attacker.Color == PieceColor.White ? 1 : -1;
                    return absX == 1 && dy == pawnDirection;
                case PieceType.Knight:
                    return IsKnightStep(absX, absY);
                case PieceType.King:
                    return Mathf.Max(absX, absY) == 1;
                case PieceType.Bishop:
                    return IsClearDiagonal(boardManager, attackerPos, destination, source, movingPiece);
                case PieceType.Rook:
                    return IsClearOrthogonal(boardManager, attackerPos, destination, source, movingPiece);
                case PieceType.Queen:
                    return IsClearDiagonal(boardManager, attackerPos, destination, source, movingPiece) ||
                        IsClearOrthogonal(boardManager, attackerPos, destination, source, movingPiece);
                case PieceType.Amazon:
                    return IsKnightStep(absX, absY) ||
                        IsClearDiagonal(boardManager, attackerPos, destination, source, movingPiece) ||
                        IsClearOrthogonal(boardManager, attackerPos, destination, source, movingPiece);
                case PieceType.Chancellor:
                    return IsKnightStep(absX, absY) ||
                        IsClearOrthogonal(boardManager, attackerPos, destination, source, movingPiece);
                case PieceType.KnightRider:
                    return IsClearKnightRider(boardManager, attackerPos, destination, source, movingPiece);
                default:
                    return false;
            }
        }

        private static bool IsKnightStep(int absX, int absY)
        {
            return (absX == 1 && absY == 2) || (absX == 2 && absY == 1);
        }

        private static bool IsClearDiagonal(
            BoardManager boardManager,
            Vector3Int from,
            Vector3Int to,
            Vector3Int movedSource,
            Piece movedPiece)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if (Mathf.Abs(dx) != Mathf.Abs(dy) || dx == 0)
                return false;

            return IsClearRay(boardManager, from, to, Math.Sign(dx), Math.Sign(dy), movedSource, movedPiece);
        }

        private static bool IsClearOrthogonal(
            BoardManager boardManager,
            Vector3Int from,
            Vector3Int to,
            Vector3Int movedSource,
            Piece movedPiece)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if ((dx == 0) == (dy == 0))
                return false;

            return IsClearRay(boardManager, from, to, Math.Sign(dx), Math.Sign(dy), movedSource, movedPiece);
        }

        private static bool IsClearKnightRider(
            BoardManager boardManager,
            Vector3Int from,
            Vector3Int to,
            Vector3Int movedSource,
            Piece movedPiece)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            int absX = Mathf.Abs(dx);
            int absY = Mathf.Abs(dy);

            if (absX == 0 || absY == 0)
                return false;

            int stepX;
            int stepY;
            if (absX * 2 == absY)
            {
                stepX = Math.Sign(dx);
                stepY = Math.Sign(dy) * 2;
            }
            else if (absY * 2 == absX)
            {
                stepX = Math.Sign(dx) * 2;
                stepY = Math.Sign(dy);
            }
            else
            {
                return false;
            }

            return IsClearRay(boardManager, from, to, stepX, stepY, movedSource, movedPiece);
        }

        private static bool IsClearRay(
            BoardManager boardManager,
            Vector3Int from,
            Vector3Int to,
            int stepX,
            int stepY,
            Vector3Int movedSource,
            Piece movedPiece)
        {
            Vector3Int cursor = new Vector3Int(from.x + stepX, from.y + stepY, 0);
            while (cursor != to)
            {
                if (!boardManager.IsInside(cursor))
                    return false;

                if (GetPieceAfterMove(boardManager, cursor, movedSource, to, movedPiece) != null)
                    return false;

                cursor = new Vector3Int(cursor.x + stepX, cursor.y + stepY, 0);
            }

            return true;
        }

        private static Piece GetPieceAfterMove(
            BoardManager boardManager,
            Vector3Int position,
            Vector3Int movedSource,
            Vector3Int movedDestination,
            Piece movedPiece)
        {
            if (position == movedSource)
                return null;

            if (position == movedDestination)
                return movedPiece;

            return boardManager.GetPiece(position);
        }

        private static PieceType GetEffectivePieceType(Piece piece)
        {
            if (piece == null)
                return PieceType.None;

            string fen = piece.GetFen();
            if (string.IsNullOrEmpty(fen))
                return piece.Type;

            switch (char.ToLowerInvariant(fen[0]))
            {
                case 'p':
                    return PieceType.Pawn;
                case 'n':
                    return PieceType.Knight;
                case 'b':
                    return PieceType.Bishop;
                case 'r':
                    return PieceType.Rook;
                case 'q':
                    return PieceType.Queen;
                case 'k':
                    return PieceType.King;
                case 's':
                    return PieceType.Amazon;
                case 'y':
                    return PieceType.Chancellor;
                case 'z':
                    return PieceType.KnightRider;
                case 'a':
                    return PieceType.Wall;
                default:
                    return piece.Type;
            }
        }

        private static int GetPieceValue(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn:
                    return 100;
                case PieceType.Knight:
                case PieceType.Bishop:
                case PieceType.King:
                    return 320;
                case PieceType.Rook:
                    return 500;
                case PieceType.KnightRider:
                    return 700;
                case PieceType.Queen:
                case PieceType.Chancellor:
                    return 900;
                case PieceType.Amazon:
                    return 1300;
                default:
                    return 0;
            }
        }
    }
}
