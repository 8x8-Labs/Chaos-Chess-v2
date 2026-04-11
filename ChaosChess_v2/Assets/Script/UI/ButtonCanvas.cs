using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonCanvas : ButtonParent
{
    public bool MainCanvas
    {
        get
        {
            return isMainParent;
        }
    }
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float uiAnimDelay = 0.2f;
    [SerializeField] private List<BasicUIAnimation> animationButtons;

    private CanvasGroup canvasGroup;
    private ScrollRect scrollRect;
    private Canvas canvas;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        // Debug.Log($"{gameObject.name}에 있는 캔버스 그룹 오브젝트: {canvasGroup.gameObject.name}");
        canvas = GetComponent<Canvas>();
        scrollRect = GetComponentInChildren<ScrollRect>();
    }


    // 캔버스를 활성화시키고 효과 및 첫 선택 버튼을 설정
    public override void EnableParent()
    {
        canvas.enabled = true;
        if(scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1;
            scrollRect.horizontalNormalizedPosition = 1;
        }

        canvasGroup.alpha = 0f;
        FadeOut();
    }

    // 캔버스를 비활성화 시키고 알파를 0으로 바꿈
    public override void DisableParent()
    {
        canvas.enabled = false;
        canvasGroup.alpha = 0f;
    }
    
    public void FadeOut()
    {
        canvasGroup.DOFade(1f, fadeDuration);
        for(int i = 0; i < animationButtons.Count; i++)
        {
            animationButtons[i].StartAnimation(i * uiAnimDelay);
        }
    }
    public void FadeIn()
    {
        // Debug.Log($"{gameObject.name} 캔버스 그룹 인스턴스 아이디: {canvasGroup.GetInstanceID()} ");
        canvasGroup.DOFade(0f, fadeDuration)
            .OnComplete(() => 
            {
                canvas.enabled = false;
            });
    }
}