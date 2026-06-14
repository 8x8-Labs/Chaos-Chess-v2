using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using DG.Tweening;

public class UITileEffectDrawer : MonoBehaviour
{
    [SerializeField] private Tilemap effectTilemap;
    private readonly Dictionary<Vector3Int, TileEffectAnimationState> animationStates = new();

    // 떨어지는 중인(착지 전) 셀별 등장 트윈을 추적합니다.
    private readonly Dictionary<Vector3Int, Tween> appearTweens = new();

    private class TileEffectAnimationState
    {
        public CardDataSO DataSO;
        public int EffectTileIndex;
        public int InitialRemainingTurns;
        public int FrameIndex;

        public TileEffectAnimationState(CardDataSO dataSO, int effectTileIndex, int initialRemainingTurns, int initialFrame)
        {
            DataSO = dataSO;
            EffectTileIndex = effectTileIndex;
            InitialRemainingTurns = initialRemainingTurns;
            FrameIndex = initialFrame;
        }
    }

    public void SetTileEffect(Vector3Int pos, TileBase tile)
    {
        CancelAppearAnimation(pos);
        animationStates.Remove(pos);

        if (effectTilemap != null)
            effectTilemap.SetTile(pos, tile);
    }

    /// <param name="playAppear">true이고 카드가 Drop 등장 모드면, 즉시 배치 대신 위에서 떨어지는 연출을 재생한 뒤 착지 시 배치합니다.</param>
    public void SetTileEffect(Vector3Int pos, CardDataSO dataSO, int effectTileIndex = 0, int remainingTurns = -1, bool playAppear = false)
    {
        if (effectTilemap == null || dataSO == null)
            return;

        CancelAppearAnimation(pos);

        if (playAppear && dataSO.TileAppearMode != TileAppearAnimationMode.None
            && TryStartAppearAnimation(pos, dataSO, effectTileIndex, remainingTurns))
            return;

        PlaceTileEffect(pos, dataSO, effectTileIndex, remainingTurns);
    }

    /// <summary>등장 연출 없이 타일 이펙트를 실제로 타일맵에 배치하고 애니메이션 상태를 등록합니다.</summary>
    private void PlaceTileEffect(Vector3Int pos, CardDataSO dataSO, int effectTileIndex, int remainingTurns)
    {
        if (effectTilemap == null || dataSO == null)
            return;

        if (dataSO.EffectTileAnimationMode != TileEffectAnimationMode.Turn)
        {
            animationStates.Remove(pos);
            effectTilemap.SetTile(pos, dataSO.GetEffectTileBase(effectTileIndex));
            return;
        }

        int initialFrame = 0;
        var state = new TileEffectAnimationState(dataSO, effectTileIndex, remainingTurns, initialFrame);
        animationStates[pos] = state;
        effectTilemap.SetTile(pos, dataSO.GetEffectTileAnimationFrame(initialFrame, effectTileIndex));
    }

    /// <summary>실제 타일을 셀에 깐 뒤 셀별 transform 행렬로 등장 연출(Drop/Scale)을 재생하고, 완료 시 행렬을 원위치합니다.</summary>
    /// <returns>표시할 타일이 없으면 false(호출측이 즉시 배치로 대체).</returns>
    private bool TryStartAppearAnimation(Vector3Int pos, CardDataSO dataSO, int effectTileIndex, int remainingTurns)
    {
        TileBase initialTile = dataSO.EffectTileAnimationMode != TileEffectAnimationMode.Turn
            ? dataSO.GetEffectTileBase(effectTileIndex)
            : dataSO.GetEffectTileAnimationFrame(0, effectTileIndex);

        if (initialTile == null)
            return false;

        // 연출 동안 보여줄 타일을 셀에 깔고, 셀 transform 잠금을 풀어 행렬을 적용 가능하게 한다.
        effectTilemap.SetTile(pos, initialTile);
        effectTilemap.SetTileFlags(pos, TileFlags.None);

        Tween tween;
        if (dataSO.TileAppearMode == TileAppearAnimationMode.Scale)
        {
            // 타일 메시는 이미 셀 로컬 원점(타일 중심)을 기준으로 그려지므로,
            // pivot 보정 없이 순수 스케일만 적용해야 가운데에서 확대된다.
            float from = dataSO.TileAppearScaleFrom;
            effectTilemap.SetTransformMatrix(pos, BuildScaleMatrix(from));

            tween = DOVirtual.Float(from, 1f, dataSO.TileAppearScaleDuration, scale =>
                {
                    if (effectTilemap != null && effectTilemap.HasTile(pos))
                        effectTilemap.SetTransformMatrix(pos, BuildScaleMatrix(scale));
                })
                .SetEase(dataSO.TileAppearScaleEase)
                .SetTarget(effectTilemap);
        }
        else // Drop
        {
            // 행렬 translation은 타일맵 로컬 공간 기준이므로 높이(셀)를 셀 크기로 환산한다.
            float startOffset = dataSO.TileAppearDropHeight * effectTilemap.cellSize.y;
            effectTilemap.SetTransformMatrix(pos, Matrix4x4.Translate(new Vector3(0f, startOffset, 0f)));

            tween = DOVirtual.Float(startOffset, 0f, dataSO.TileAppearDropDuration, offset =>
                {
                    if (effectTilemap != null && effectTilemap.HasTile(pos))
                        effectTilemap.SetTransformMatrix(pos, Matrix4x4.Translate(new Vector3(0f, offset, 0f)));
                })
                .SetEase(dataSO.TileAppearDropEase)
                .SetTarget(effectTilemap);
        }

        tween.OnComplete(() =>
        {
            appearTweens.Remove(pos);
            if (effectTilemap == null) return;
            effectTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
            PlaceTileEffect(pos, dataSO, effectTileIndex, remainingTurns);
        });

        appearTweens[pos] = tween;
        return true;
    }

    /// <summary>타일 중심(셀 로컬 원점)을 기준으로 균일 확대하는 transform 행렬을 만듭니다.</summary>
    private static Matrix4x4 BuildScaleMatrix(float scale)
    {
        return Matrix4x4.Scale(new Vector3(scale, scale, 1f));
    }

    private void CancelAppearAnimation(Vector3Int pos)
    {
        if (!appearTweens.TryGetValue(pos, out Tween tween))
            return;

        appearTweens.Remove(pos);
        tween?.Kill();
        if (effectTilemap != null && effectTilemap.HasTile(pos))
            effectTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
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
        CancelAppearAnimation(pos);
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
        CancelAllAppearAnimations();
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
        CancelAllAppearAnimations();
        animationStates.Clear();

        if (effectTilemap != null)
            effectTilemap.ClearAllTiles();
    }

    private void CancelAllAppearAnimations()
    {
        foreach (var pair in appearTweens)
        {
            pair.Value?.Kill();
            if (effectTilemap != null && effectTilemap.HasTile(pair.Key))
                effectTilemap.SetTransformMatrix(pair.Key, Matrix4x4.identity);
        }
        appearTweens.Clear();
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

}
