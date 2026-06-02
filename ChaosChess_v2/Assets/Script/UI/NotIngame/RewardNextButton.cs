using UnityEngine;

public class RewardNextButton : MonoBehaviour
{
    [SerializeField] private UIButton nextButton;
    [SerializeField] private CanvasGroup nextButtonCanvasGroup;
    [SerializeField] private int totalRewardCount = 3;

    private int claimedRewardCount;

    private void Awake()
    {
        claimedRewardCount = 0;
        RefreshState();
    }

    public void OnRewardClaimed()
    {
        if (claimedRewardCount >= totalRewardCount)
            return;

        claimedRewardCount++;
        RefreshState();
    }

    private void RefreshState()
    {
        bool canGoNext = claimedRewardCount >= totalRewardCount;

        if (nextButtonCanvasGroup != null)
        {
            nextButtonCanvasGroup.alpha = canGoNext ? 1.0f : 0.4f;
            nextButtonCanvasGroup.interactable = canGoNext;
            nextButtonCanvasGroup.blocksRaycasts = canGoNext;
        }

    }
}
