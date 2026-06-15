using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SplashPanelAnim : MonoBehaviour
{
    [SerializeField] private Image logoImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float splashImageDuration = 0.5f;
    [SerializeField] private float canvasFadeDuration = 0.5f;

    private void Start()
    {
        // 초기 상태 설정
        logoImage.color = Color.clear;
        canvasGroup.alpha = 1f;
        // 스플래시 동안 뒤쪽 UI로 터치/클릭이 새지 않도록 입력을 막는다
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        Sequence seq = DOTween.Sequence();

        // 1) 로고 페이드 인
        seq.Append(logoImage.DOColor(Color.white, fadeDuration));
        // 2) 잠깐 유지
        seq.AppendInterval(splashImageDuration);
        // 3) 로고 페이드 아웃
        seq.Append(logoImage.DOColor(Color.clear, fadeDuration));
        // 4) 캔버스 전체 페이드 아웃
        seq.Append(canvasGroup.DOFade(0f, canvasFadeDuration));
        // 5) 끝나면 입력 차단을 풀고 비활성화
        seq.AppendCallback(() =>
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            canvasGroup.gameObject.SetActive(false);
        });

        seq.Play();
    }
}
