using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChaosChess.AI.Domain;
using ChaosChess.AI.Fen;
using ChaosChess.Unity.AIIntegration.Cards;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Mapping
{
    public sealed class UnityGameStateMappingResult
    {
        public UnityGameStateMappingResult(
            string fen,
            GameState gameState,
            IEnumerable<string> warnings)
        {
            Fen = fen ?? throw new ArgumentNullException(nameof(fen));
            GameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            Warnings = new List<string>(warnings ?? Array.Empty<string>()).AsReadOnly();
        }

        public string Fen { get; }
        public GameState GameState { get; }
        public ReadOnlyCollection<string> Warnings { get; }
    }

    public static class UnityGameStateMapper
    {
        public static UnityGameStateMappingResult Capture(
            global::BoardManager boardManager,
            IEnumerable<GameObject> availableCardPrefabs = null,
            int cardRemainingUses = 1)
        {
            if (boardManager == null)
                throw new ArgumentNullException(nameof(boardManager));

            var warnings = new List<string>();

            boardManager.UpdateFEN();
            string fen = boardManager.GetFEN();
            BoardState boardState = AddPieceMetadata(FenParser.Parse(fen), boardManager, warnings);
            IReadOnlyList<CardInfo> cards = UnityCardInfoMapper.MapCards(
                availableCardPrefabs,
                cardRemainingUses,
                warnings);
            IReadOnlyList<TileEffectInfo> tileEffects = UnityTileEffectMapper.MapTileEffects(
                boardManager,
                warnings);
            CapturedPieceState capturedPieces = MapCapturedPieces(boardManager, warnings);

            var gameState = new GameState(boardState, cards, tileEffects, capturedPieces);
            return new UnityGameStateMappingResult(fen, gameState, warnings);
        }

        private static BoardState AddPieceMetadata(
            BoardState parsedBoard,
            global::BoardManager boardManager,
            IList<string> warnings)
        {
            var pieces = new List<PieceInfo>();

            foreach (PieceInfo parsedPiece in parsedBoard.Pieces)
            {
                global::Piece unityPiece = boardManager.GetPiece(ToVector3Int(parsedPiece.Square));
                if (unityPiece == null)
                {
                    warnings?.Add($"FEN piece at {parsedPiece.Square} has no matching Unity piece metadata.");
                    pieces.Add(parsedPiece);
                    continue;
                }

                Square? startSquare = null;
                if (unityPiece.IsPromotioned)
                {
                    if (TryMapSquare(unityPiece.StartPos, out Square mappedStartSquare))
                        startSquare = mappedStartSquare;
                    else
                        warnings?.Add($"Promoted piece at {parsedPiece.Square} has invalid StartPos ({unityPiece.StartPos.x}, {unityPiece.StartPos.y}).");
                }

                pieces.Add(new PieceInfo(
                    parsedPiece.Kind,
                    parsedPiece.Color,
                    parsedPiece.Square,
                    parsedPiece.FenCode,
                    unityPiece.IsPromotioned,
                    startSquare));
            }

            return new BoardState(
                pieces,
                parsedBoard.SideToMove,
                parsedBoard.CastlingRights,
                parsedBoard.EnPassantTarget,
                parsedBoard.HalfmoveClock,
                parsedBoard.FullmoveNumber);
        }

        private static bool TryMapSquare(Vector3Int pos, out Square square)
        {
            square = default;

            try
            {
                square = new Square(pos.x, pos.y);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static Vector3Int ToVector3Int(Square square)
        {
            return new Vector3Int(square.File, square.Rank, 0);
        }

        private static CapturedPieceState MapCapturedPieces(
            global::BoardManager boardManager,
            IList<string> warnings)
        {
            return new CapturedPieceState(
                MapCapturedPieces(boardManager.WhiteDeadPieces, "white", warnings),
                MapCapturedPieces(boardManager.BlackDeadPieces, "black", warnings));
        }

        private static IReadOnlyList<PieceKind> MapCapturedPieces(
            IEnumerable<global::PieceType> pieces,
            string owner,
            IList<string> warnings)
        {
            var mapped = new List<PieceKind>();
            if (pieces == null)
                return mapped;

            foreach (global::PieceType piece in pieces)
            {
                if (TryMapPieceKind(piece, out PieceKind kind))
                    mapped.Add(kind);
                else
                    warnings?.Add($"Unsupported {owner} captured piece type '{piece}' was ignored.");
            }

            return mapped;
        }

        private static bool TryMapPieceKind(global::PieceType pieceType, out PieceKind kind)
        {
            switch (pieceType)
            {
                case global::PieceType.Pawn:
                    kind = PieceKind.Pawn;
                    return true;
                case global::PieceType.Knight:
                    kind = PieceKind.Knight;
                    return true;
                case global::PieceType.Bishop:
                    kind = PieceKind.Bishop;
                    return true;
                case global::PieceType.Rook:
                    kind = PieceKind.Rook;
                    return true;
                case global::PieceType.Queen:
                    kind = PieceKind.Queen;
                    return true;
                case global::PieceType.King:
                    kind = PieceKind.King;
                    return true;
                case global::PieceType.Wall:
                    kind = PieceKind.Wall;
                    return true;
                case global::PieceType.Amazon:
                    kind = PieceKind.Amazon;
                    return true;
                case global::PieceType.Chancellor:
                    kind = PieceKind.Chancellor;
                    return true;
                case global::PieceType.KnightRider:
                    kind = PieceKind.KnightRider;
                    return true;
                default:
                    kind = PieceKind.Unknown;
                    return false;
            }
        }

        public static UnityGameStateMappingResult Capture(
            global::BoardManager boardManager,
            AiCardHand aiCardHand)
        {
            return Capture(
                boardManager,
                aiCardHand != null ? aiCardHand.AvailableCards : null,
                aiCardHand != null ? aiCardHand.DefaultRemainingUses : 0);
        }
    }
}
