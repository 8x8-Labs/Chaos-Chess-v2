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

            if (!TryMapSquare(effector.TilePos, warnings, out Square square))
                return false;

            string effectType;
            AiPieceColor? owner = null;
            Square? destination = null;
            int? sharedUses = null;
            TileEffectLifetimeKind lifetimeKind = effector.RemainingTurns < 0
                ? TileEffectLifetimeKind.PersistentUntilTriggered
                : TileEffectLifetimeKind.TurnLimited;

            if (effector is global::ATMineEffector)
            {
                effectType = "Mine";
                AddCoverageWarning(warnings, effectType, effector, "Exact", "AI simulates heavy-piece path blast and removes the mine.");
            }
            else if (effector is global::PeaceZoneCardEffect)
            {
                effectType = "Peace";
                AddCoverageWarning(warnings, effectType, effector, "Exact", "AI simulates capture cancellation and one-shot removal.");
            }
            else if (effector is global::FireEffect)
            {
                effectType = "Fire";
                AddCoverageWarning(warnings, effectType, effector, "Heuristic", "AI scores fire entry risk, but the DTO has no delayed removal target/residency state.");
            }
            else if (effector is global::BlessingEffect)
            {
                effectType = "Blessing";
                AddCoverageWarning(warnings, effectType, effector, "Heuristic", "AI scores blessing entry promotion gain, but the DTO has no residency state for delayed promotion.");
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

                AddCoverageWarning(warnings, effectType, effector, "Exact", "AI simulates owner-gated teleport and shared uses when destination/use metadata is present.");
            }
            else if (effector is global::SyncEffect sync)
            {
                effectType = "Sync";
                sharedUses = 1;

                if (sync.child != null && TryMapSquare(sync.child.TilePos, warnings, out Square destinationSquare))
                    destination = destinationSquare;
                else
                    AddWarning(warnings, "Mapped Sync tile without linked child destination.");

                AddCoverageWarning(warnings, effectType, effector, "Exact", "AI simulates linked-tile exit and one-shot removal when pair metadata is present.");
            }
            else if (effector is global::SyncChild syncChild)
            {
                effectType = "Sync";
                sharedUses = 1;

                if (syncChild.parent != null && TryMapSquare(syncChild.parent.TilePos, warnings, out Square destinationSquare))
                    destination = destinationSquare;
                else
                    AddWarning(warnings, "Mapped Sync child tile without linked parent destination.");

                AddCoverageWarning(warnings, effectType, effector, "Exact", "AI simulates linked-tile exit and one-shot removal when pair metadata is present.");
            }
            else if (effector is global::JumpingPlatformEffect)
            {
                effectType = "JumpingPlatform";
                AddCoverageWarning(warnings, effectType, effector, "Exact", "AI simulates post-entry jump displacement when the landing square is valid.");
            }
            else if (effector is global::CobwebEffector)
            {
                effectType = "Cobweb";
                AddCoverageWarning(warnings, effectType, effector, "Deferred", "AI v0.8.0 carries this DTO but does not apply path stop or movement lock.");
            }
            else if (effector is global::PsilocybinMushroomTileEffect)
            {
                effectType = "PsilocybinMushroom";
                AddCoverageWarning(warnings, effectType, effector, "Deferred", "AI v0.8.0 carries this DTO but does not apply movement override.");
            }
            else if (effector is global::ObeyOrderEffect)
            {
                effectType = "ObeyOrder";
                AddCoverageWarning(warnings, effectType, effector, "Deferred", "AI v0.8.0 carries this DTO but does not apply command state.");
            }
            else if (effector is global::ObeyDestEffect)
            {
                AddWarning(warnings, "Skipped ObeyDestEffect because it is derived runtime state of ObeyOrder, not a card-placement tile.");
                return false;
            }
            else if (effector is global::TimeBombEffector)
            {
                effectType = "TimeBomb";
                AddCoverageWarning(warnings, effectType, effector, "Exact", "AI simulates delayed cross explosion on turn-end expiry.");
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
                sharedUses,
                lifetimeKind);
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

        private static void AddCoverageWarning(
            IList<string> warnings,
            string effectType,
            global::TileEffector effector,
            string coverage,
            string reason)
        {
            if (string.Equals(coverage, "Exact", StringComparison.OrdinalIgnoreCase))
                return;

            AddWarning(
                warnings,
                $"Mapped tile effect '{effectType}' from {effector.GetType().Name} as {coverage}. {reason}");
        }
    }
}
