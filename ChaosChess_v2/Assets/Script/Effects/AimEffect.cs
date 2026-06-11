using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 분리된 VFX 리스너 인터페이스 사용 예시입니다.
/// 필요한 시점(여기서는 Apply/TurnTick/Revert)만 골라 구현하면 됩니다. (Hook은 구현하지 않음)
/// 이 컴포넌트는 효과의 LoopVFXPrefab에 올려두면 effector가 자동으로 호출합니다.
///
/// 스폰 시: rollSprite로 0.5초 회전 → 멈추면서 각 총이 ±settleAngle로 정착하고 normalSprite로 교체.
/// gunSpriteRenderers의 짝수 인덱스는 +settleAngle, 홀수 인덱스는 -settleAngle로 정착합니다.
/// </summary>
public class AimEffect : MonoBehaviour, IEffectApplyListener, IEffectRevertListener
{
    [SerializeField] private SpriteRenderer[] gunSpriteRenderers;
    [SerializeField] private Sprite rollSprite;
    [SerializeField] private Sprite normalSprite;

    [Header("연출 설정")]
    [Tooltip("도는 시간(초)")]
    [SerializeField] private float rollDuration = 0.5f;
    [Tooltip("rollDuration 동안 회전하는 바퀴 수")]
    [SerializeField] private int rollTurns = 2;
    [Tooltip("정착 각도(±). 짝수 인덱스는 +, 홀수 인덱스는 -")]
    [SerializeField] private float settleAngle = 45f;
    [Tooltip("정착 트윈 시간(초)")]
    [SerializeField] private float settleDuration = 0.2f;
    [Tooltip("정착 트윈 이징")]
    [SerializeField] private Ease settleEase = Ease.OutBack;

    [Header("퇴장 연출 (드롭 + 페이드)")]
    [Tooltip("아래로 떨어지는 거리(월드 단위)")]
    [SerializeField] private float dropDistance = 1.5f;
    [Tooltip("드롭+페이드 시간(초)")]
    [SerializeField] private float dropDuration = 0.6f;
    [Tooltip("떨어지는 이징(중력 가속 느낌은 InQuad/InCubic)")]
    [SerializeField] private Ease dropEase = Ease.InQuad;
    [Tooltip("떨어지며 서서히 추가 회전하는 각도. 짝/홀 인덱스가 좌우 대칭으로 회전")]
    [SerializeField] private float dropRotationAngle = 90f;

    private readonly List<Sequence> sequences = new();

    public void OnEffectApply(in EffectVFXContext ctx)
    {
        PlayAimSequence();
    }

    public void OnEffectRevert(in EffectVFXContext ctx)
    {
        // 진행 중인 회전/정착 트윈을 정리합니다.
        KillSequences();

        // 루프 VFX는 이 호출 직후 파괴되므로, 총 스프라이트를 분리해 살려두고
        // 분리된 오브젝트에서 드롭+페이드를 돌린 뒤 완료 시 스스로 파괴합니다.
        PlayHolsterSequence();
    }

    /// <summary>들고 있던 총 스프라이트를 아래로 떨구며 페이드 아웃시킵니다.</summary>
    private void PlayHolsterSequence()
    {
        if (gunSpriteRenderers == null) return;

        for (int i = 0; i < gunSpriteRenderers.Length; i++)
        {
            SpriteRenderer gun = gunSpriteRenderers[i];
            if (gun == null) continue;

            Transform t = gun.transform;
            // 부모(루프 VFX)가 곧 파괴되므로 월드 위치를 유지한 채 분리합니다.
            t.SetParent(null, worldPositionStays: true);

            // 짝/홀 인덱스를 좌우 대칭으로 회전시킵니다.
            float rotateDir = (i % 2 == 0) ? 1f : -1f;

            Sequence exit = DOTween.Sequence();
            exit.Join(t.DOMoveY(t.position.y - dropDistance, dropDuration).SetEase(dropEase));
            exit.Join(gun.DOFade(0f, dropDuration).SetEase(Ease.InQuad));
            exit.Join(
                t.DOLocalRotate(new Vector3(0f, 0f, dropRotationAngle * rotateDir), dropDuration, RotateMode.LocalAxisAdd)
                 .SetEase(dropEase));
            exit.OnComplete(() =>
            {
                if (gun != null)
                    Destroy(gun.gameObject);
            });
        }
    }

    private void OnDestroy()
    {
        KillSequences();
    }

    private void PlayAimSequence()
    {
        KillSequences();
        if (gunSpriteRenderers == null) return;

        for (int i = 0; i < gunSpriteRenderers.Length; i++)
        {
            SpriteRenderer gun = gunSpriteRenderers[i];
            if (gun == null) continue;

            Transform t = gun.transform;
            float targetAngle = (i % 2 == 0) ? settleAngle : -settleAngle;

            // 회전 시작 상태로 초기화
            gun.sprite = rollSprite;
            t.localRotation = Quaternion.identity;

            Sequence seq = DOTween.Sequence();

            // 1) rollDuration 동안 빠르게 회전 (루프처럼 보이도록 여러 바퀴)
            seq.Append(
                t.DOLocalRotate(new Vector3(0f, 0f, -360f * rollTurns), rollDuration, RotateMode.FastBeyond360)
                 .SetEase(Ease.Linear));

            // 2) 스프라이트를 총 모양으로 바꾸고 회전값을 정리(여러 바퀴 → 0°, 시각적으로 동일)한 뒤
            seq.AppendCallback(() =>
            {
                gun.sprite = normalSprite;
                t.localRotation = Quaternion.identity;
            });

            // 3) 목표 각도(±settleAngle)로 정착
            seq.Append(
                t.DOLocalRotate(new Vector3(0f, 0f, targetAngle), settleDuration, RotateMode.Fast)
                 .SetEase(settleEase));

            sequences.Add(seq);
        }
    }

    private void KillSequences()
    {
        foreach (Sequence seq in sequences)
            seq?.Kill();
        sequences.Clear();
    }
}
