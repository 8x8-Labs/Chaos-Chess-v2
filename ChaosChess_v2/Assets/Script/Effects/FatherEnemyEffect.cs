using DG.Tweening;
using UnityEngine;

public class FatherEnemyEffect : MonoBehaviour, IEffectApplyListener
{
    [SerializeField] private Vector2 startPos;
    [SerializeField] private SpriteRenderer sword;
    [SerializeField] private float swordDownDuration;
    [SerializeField] private Ease animEase;

    public void OnEffectApply(in EffectVFXContext ctx)
    {
        sword.transform.localPosition = startPos;
        sword.transform.DOLocalMove(Vector2.zero, swordDownDuration).SetEase(animEase);
    }
}
