using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChaosChess.AI.Domain;
using ChaosChess.AI.Fen;
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
            BoardState boardState = FenParser.Parse(fen);
            IReadOnlyList<CardInfo> cards = UnityCardInfoMapper.MapCards(
                availableCardPrefabs,
                cardRemainingUses,
                warnings);
            IReadOnlyList<TileEffectInfo> tileEffects = UnityTileEffectMapper.MapTileEffects(
                boardManager,
                warnings);

            var gameState = new GameState(boardState, cards, tileEffects);
            return new UnityGameStateMappingResult(fen, gameState, warnings);
        }
    }
}
