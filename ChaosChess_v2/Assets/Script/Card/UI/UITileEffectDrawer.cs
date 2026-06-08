using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class UITileEffectDrawer : MonoBehaviour
{
    [SerializeField] private Tilemap effectTilemap;
    private readonly Dictionary<Vector3Int, TileEffectAnimationState> animationStates = new();

    private class TileEffectAnimationState
    {
        public CardDataSO DataSO;
        public int EffectTileIndex;
        public int InitialRemainingTurns;
        public int FrameIndex;
        public float Elapsed;

        public TileEffectAnimationState(CardDataSO dataSO, int effectTileIndex, int initialRemainingTurns, int initialFrame)
        {
            DataSO = dataSO;
            EffectTileIndex = effectTileIndex;
            InitialRemainingTurns = initialRemainingTurns;
            FrameIndex = initialFrame;
        }
    }

    private void Update()
    {
        if (effectTilemap == null || animationStates.Count == 0)
            return;

        foreach (var pair in animationStates)
        {
            TileEffectAnimationState state = pair.Value;
            CardDataSO dataSO = state.DataSO;
            if (dataSO == null || dataSO.EffectTileAnimationMode != TileEffectAnimationMode.Time)
                continue;

            float frameInterval = Mathf.Max(0.01f, dataSO.EffectTileFrameInterval);
            state.Elapsed += Time.deltaTime;
            if (state.Elapsed < frameInterval)
                continue;

            int framesToAdvance = Mathf.FloorToInt(state.Elapsed / frameInterval);
            state.Elapsed %= frameInterval;
            AdvanceFrame(pair.Key, state, framesToAdvance);
        }
    }

    public void SetTileEffect(Vector3Int pos, TileBase tile)
    {
        animationStates.Remove(pos);

        if (effectTilemap != null)
            effectTilemap.SetTile(pos, tile);
    }

    public void SetTileEffect(Vector3Int pos, CardDataSO dataSO, int effectTileIndex = 0, int remainingTurns = -1)
    {
        if (effectTilemap == null || dataSO == null)
            return;

        TileBase initialTile = dataSO.GetEffectTileBase(effectTileIndex);

        if (dataSO.EffectTileAnimationMode == TileEffectAnimationMode.None)
        {
            SetTileEffect(pos, initialTile);
            return;
        }

        int initialFrame = 0;
        var state = new TileEffectAnimationState(dataSO, effectTileIndex, remainingTurns, initialFrame);
        animationStates[pos] = state;
        effectTilemap.SetTile(pos, dataSO.GetEffectTileAnimationFrame(initialFrame, effectTileIndex));
    }

    public void TickTurnAnimation(Vector3Int pos, int remainingTurns = -1)
    {
        if (effectTilemap == null)
            return;

        if (!animationStates.TryGetValue(pos, out TileEffectAnimationState state))
            return;

        CardDataSO dataSO = state.DataSO;
        if (dataSO == null || dataSO.EffectTileAnimationMode != TileEffectAnimationMode.Turn)
            return;

        state.FrameIndex = GetTurnFrame(state, remainingTurns);

        effectTilemap.SetTile(pos, dataSO.GetEffectTileAnimationFrame(state.FrameIndex, state.EffectTileIndex));
    }

    public void ClearTileEffect(Vector3Int pos)
    {
        animationStates.Remove(pos);

        if (effectTilemap != null)
            effectTilemap.SetTile(pos, null);
    }

    /// <summary>현재 타일 이펙트 맵을 위치-타일 스냅샷으로 저장합니다.</summary>
    public Dictionary<Vector3Int, TileBase> CaptureTileEffects()
    {
        var snapshot = new Dictionary<Vector3Int, TileBase>();
        if (effectTilemap == null) return snapshot;

        foreach (Vector3Int pos in effectTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = effectTilemap.GetTile(pos);
            if (tile != null)
                snapshot[pos] = tile;
        }

        return snapshot;
    }

    /// <summary>저장된 스냅샷 기준으로 타일 이펙트를 복원합니다.</summary>
    public void RestoreTileEffects(Dictionary<Vector3Int, TileBase> snapshot)
    {
        if (effectTilemap == null) return;
        animationStates.Clear();
        effectTilemap.ClearAllTiles();

        if (snapshot == null)
            return;

        foreach (var pair in snapshot)
        {
            effectTilemap.SetTile(pair.Key, pair.Value);
        }
    }

    /// <summary>타일 이펙트 맵을 전체 초기화합니다.</summary>
    public void ClearAllTileEffects()
    {
        animationStates.Clear();

        if (effectTilemap != null)
            effectTilemap.ClearAllTiles();
    }

    private int GetTurnFrame(TileEffectAnimationState state, int remainingTurns)
    {
        CardDataSO dataSO = state.DataSO;
        int frameCount = dataSO.EffectTileAnimationFrames != null
            ? dataSO.EffectTileAnimationFrames.Length
            : 0;

        if (frameCount <= 0 || state.InitialRemainingTurns < 0 || remainingTurns < 0)
            return 0;

        int elapsedTurns = state.InitialRemainingTurns - remainingTurns;
        return Mathf.Clamp(elapsedTurns, 0, frameCount - 1);
    }

    private void AdvanceFrame(Vector3Int pos, TileEffectAnimationState state, int framesToAdvance = 1)
    {
        CardDataSO dataSO = state.DataSO;
        int frameCount = dataSO.EffectTileAnimationFrames != null
            ? dataSO.EffectTileAnimationFrames.Length
            : 0;

        if (frameCount <= 0)
            return;

        state.FrameIndex = (state.FrameIndex + framesToAdvance) % frameCount;
        effectTilemap.SetTile(pos, dataSO.GetEffectTileAnimationFrame(state.FrameIndex, state.EffectTileIndex));
    }
}
