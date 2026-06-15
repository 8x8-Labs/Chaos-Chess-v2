using DG.Tweening;
using UnityEngine;

public class CheckmateSengenEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private float duration;
    [SerializeField] private Ease ease;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sprite.DOFade(0f, duration).SetEase(ease).OnComplete(() => Destroy(gameObject));
    }
}
