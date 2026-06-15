using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 손패 입력 차단 패널(inputBlockPanel)에 부착합니다.
/// 차단 중 플레이어가 손패 영역을 클릭하면 그 사유(체크/상대 턴/투기장)를 토스트로 알립니다.
/// 차단은 턴 전환마다 자동으로 켜지므로, 알림은 패시브가 아니라 실제 클릭 시에만 띄웁니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CardInputBlockNotice : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        CardBlockNotifier.Notify(CardBlockNotifier.ResolveHandBlockReason());
    }
}
