using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class CardGrantStatusUI : MonoBehaviour
{
    private const float GrantMessageDuration = 0.8f;
    private const int EmphasisSize = 84;
    private const string WaitingHex = "#FFE633";
    private const string GrantedHex = "#75D6FF";
    private const string NoticeHex = "#FF9F43";

    [SerializeField] private RectTransform statusRoot;
    [SerializeField] private TMP_Text labelText;

    private Player player;
    private Sequence grantSequence;
    private Coroutine bindCoroutine;

    private void OnEnable()
    {
        bindCoroutine = StartCoroutine(BindPlayerWhenReady());
    }

    private void OnDisable()
    {
        if (bindCoroutine != null)
        {
            StopCoroutine(bindCoroutine);
            bindCoroutine = null;
        }

        UnbindPlayer();
        grantSequence?.Kill();
        grantSequence = null;
    }

    private IEnumerator BindPlayerWhenReady()
    {
        while (true)
        {
            while (player == null)
            {
                player = FindFirstObjectByType<Player>();
                yield return null;
            }

            while (player != null && !player.IsCardGrantInitialized)
                yield return null;

            if (player != null)
            {
                player.OnCardGrantStateChanged += Refresh;
                player.OnCardGranted += PlayGrantedFeedback;
                bindCoroutine = null;
                Refresh();
                yield break;
            }
        }
    }

    private void UnbindPlayer()
    {
        if (player == null) return;

        player.OnCardGrantStateChanged -= Refresh;
        player.OnCardGranted -= PlayGrantedFeedback;
        player = null;
    }

    private void Refresh()
    {
        if (player == null || labelText == null) return;

        if (grantSequence != null && grantSequence.IsActive() && grantSequence.IsPlaying()) return;

        if (player.IsCardGrantPaused)
        {
            SetStatus($"{Emphasize("대기", NoticeHex)} 중");
            return;
        }

        if (player.IsCardHandFull)
        {
            SetStatus($"손패 {Emphasize("가득 참", NoticeHex)}");
            return;
        }

        int remainingTurns = player.RemainingTurnsUntilCardGrant;
        SetStatus($"{Emphasize($"{remainingTurns}턴", WaitingHex)} 후 지급");
    }

    private void PlayGrantedFeedback()
    {
        if (statusRoot == null || labelText == null) return;

        grantSequence?.Kill();
        SetStatus($"카드 {Emphasize("지급!", GrantedHex)}");
        statusRoot.localScale = Vector3.one;

        grantSequence = DOTween.Sequence()
            .Append(statusRoot.DOScale(1.08f, 0.12f))
            .Append(statusRoot.DOScale(1f, 0.12f))
            .AppendInterval(GrantMessageDuration)
            .OnComplete(() =>
            {
                grantSequence = null;
                Refresh();
            });
    }

    private void SetStatus(string text)
    {
        labelText.text = text;
        labelText.color = Color.white;
    }

    private static string Emphasize(string text, string colorHex) =>
        $"<size={EmphasisSize}><color={colorHex}>{text}</color></size>";
}
