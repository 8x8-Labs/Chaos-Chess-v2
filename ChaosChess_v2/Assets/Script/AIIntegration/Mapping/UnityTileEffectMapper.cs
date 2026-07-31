using System;
using System.Collections.Generic;
using ChaosChess.AI.Domain;
using UnityEngine;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;

namespace ChaosChess.Unity.AIIntegration.Mapping
{
    public static class UnityTileEffectMapper
    {
        public static IReadOnlyList<TileEffectInfo> MapTileEffects(
            global::BoardManager boardManager,
            IList<string> warnings)
        {
            var effects = new List<TileEffectInfo>();

            if (boardManager == null)
            {
                AddWarning(warnings, "BoardManager is null; tile effects were not mapped.");
                return effects.AsReadOnly();
            }

            foreach (global::TileEffector effector in boardManager.GetAllTileEffectors())
            {
                if (TryMapTileEffect(effector, warnings, out TileEffectInfo effect))
                    effects.Add(effect);
            }

            return effects.AsReadOnly();
        }

        public static bool TryMapTileEffect(
            global::TileEffector effector,
            IList<string> warnings,
            out TileEffectInfo effect)
        {
            effect = null;

            if (effector == null)
            {
                AddWarning(warnings, "Skipped null tile effector.");
                return false;
            }

            if (effector.IsSuspended)
            {
                AddWarning(warnings, $"Skipped suspended tile effector '{effector.GetType().Name}'.");
                return false;
            }

            if (effector.RemainingTurns < 0)
            {
                AddWarning(warnings, $"Skipped permanent tile effector '{effector.GetType().Name}' because AI DTO requires non-negative remaining turns.");
                return false;
            }

            if (!TryMapSquare(effector.TilePos, warnings, out Square square))
                return false;

            string effectType;
            AiPieceColor? owner = null;
            Square? destination = null;
            int? sharedUses = null;

            if (effector is global::ATMineEffector)
            {
                effectType = "Mine";
                AddWarning(warnings, "Mapped Mine tile without owner because ATMineEffector does not expose caster color.");
            }
            else if (effector is global::PeaceZoneCardEffect)
            {
                effectType = "Peace";
                AddWarning(warnings, "Mapped Peace tile without owner because PeaceZoneCardEffect does not expose caster color.");
            }
            else if (effector is global::FireEffect)
            {
                effectType = "Fire";
                AddWarning(warnings, "Mapped Fire tile without delayed removal target because current AI DTO cannot represent it.");
            }
            else if (effector is global::BlessingEffect)
            {
                effectType = "Blessing";
                AddWarning(warnings, "Mapped Blessing tile without residency state because current AI DTO cannot represent it.");
            }
            else if (effector is global::PortalEffect portal)
            {
                effectType = "Portal";
                owner = UnityAiColorMapper.ToAiColor(portal.CasterColor);

                if (portal.Dest != null && TryMapSquare(portal.Dest.TilePos, warnings, out Square destinationSquare))
                    destination = destinationSquare;
                else
                    AddWarning(warnings, "Mapped Portal tile without destination.");

                if (portal.SharedUses != null)
                    sharedUses = Math.Max(0, portal.SharedUses.Uses);
                else
                    AddWarning(warnings, "Mapped Portal tile without shared remaining uses.");
            }
            else
            {
                AddWarning(warnings, $"Skipped unsupported tile effector '{effector.GetType().Name}'.");
                return false;
            }

            effect = new TileEffectInfo(
                CreateEffectId(effector, square),
                effectType,
                square,
                owner,
                effector.RemainingTurns,
                destination,
                sharedUses);
            return true;
        }

        private static bool TryMapSquare(Vector3Int pos, IList<string> warnings, out Square square)
        {
            square = default;

            try
            {
                square = new Square(pos.x, pos.y);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                AddWarning(warnings, $"Skipped tile effect outside board at ({pos.x}, {pos.y}).");
                return false;
            }
        }

        private static string CreateEffectId(global::TileEffector effector, Square square)
        {
            return effector.GetType().Name + ":" + square;
        }

        private static void AddWarning(IList<string> warnings, string message)
        {
            warnings?.Add(message);
        }
    }
}
