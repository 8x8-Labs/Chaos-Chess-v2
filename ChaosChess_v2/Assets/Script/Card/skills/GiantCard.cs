using UnityEngine;
using DG.Tweening;

/// <summary>
/// 거대화 - 기물 전용 (무력화)
/// 기물 이동 시 1칸 반경의 기물들을 1턴 동안 무력화시킵니다.
/// 무력화 시 카드 적용은 되지만 이동할 수는 없습니다.
/// </summary>
public class GiantCard : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        Piece piece = args.Targets[0];
        var effector = CreatePieceEffector<GiantEffector>(piece);
        effector.Apply();
    }
}
public class GiantEffector : PieceEffector
{
    // 거대화 시 기물 스프라이트를 키우는 배율과 트윈 시간(초)
    private const float ScaleMultiplier = 1.4f;
    private const float ScaleTweenDuration = 0.2f;

    private Vector3 originalScale;
    private Tween scaleTween;

    protected override void OnApply()
    {
        if (target == null) return;

        originalScale = target.transform.localScale;
        scaleTween?.Kill();
        // 즉시 키운다. Apply()는 OnApply() 직후 PlayApplyVFX()에서 같은 transform에
        // DOPunchScale 펀치를 거는데, 펀치는 시작 시점의 localScale을 "기준"으로 캐싱한 뒤
        // 완료 시 그 값으로 되돌린다(VFXSpawner.PlayPunch). 여기서 미리 크게 만들어 두면
        // 펀치가 거대화된 스케일을 기준으로 잡아 "뿅" 커지는 연출을 그대로 살리면서 원복되지 않는다.
        target.transform.localScale = originalScale * ScaleMultiplier;
    }
    int[] dx = { -1, -1, -1, 0, 1, 1, 1, 0 };
    int[] dy = { -1, 0, 1, 1, 1, 0, -1, -1 };

    public override void OnPieceMove(Vector3Int dest)
    {
        for(int i = 0; i < 8; i++)
        {
            int x = dx[i];
            int y = dy[i];
            Vector3Int pos = new Vector3Int(dest.x + x, dest.y + y, dest.z);
            if (BoardManager.Instance.IsInside(pos))
            {
                Piece target = BoardManager.Instance.GetPiece(pos);
                if (target != null)
                {
                    if (PieceEffector.HasActiveMovementOverride(target))
                        continue;

                    GiantStunEffector stun = target.gameObject.AddComponent<GiantStunEffector>();
                    stun.CardSO = CardSO;
                    stun.SetVFXConfig(CardSO.PieceEffectVFX);
                    stun.InitForHalfTurns(target, 2);
                    stun.Apply(true);
                }
            }
        }

        Revert();
    }

    protected override void OnRevert()
    {
        scaleTween?.Kill();
        if (target != null)
            scaleTween = target.transform.DOScale(originalScale, ScaleTweenDuration).SetEase(Ease.OutQuad);

        Destroy(this);
    }

}

public class GiantStunEffector : PieceEffector, IMovementOverrideEffect
{
    private int remainingHalfTurns;

    public void InitForHalfTurns(Piece piece, int halfTurns)
    {
        Init(piece, -1);
        remainingHalfTurns = Mathf.Max(1, halfTurns);
    }

    protected override void OnApply()
    {
        target.MoveFenOverride = "a";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        if (target != null && target.MoveFenOverride?.ToLower() == "a")
            target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }

    protected override void OnHalfTurnChanged()
    {
        remainingHalfTurns--;
        if (remainingHalfTurns <= 0)
            Revert();
    }
}
