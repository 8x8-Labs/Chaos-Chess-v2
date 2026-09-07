using System;
using UnityEngine;

/// <summary>
/// 한 턴을 누가 두는지 결정하는 주체입니다.
/// GameManager는 이 프로바이더에게 먼저 턴을 넘기고, 처리되지 않으면 기본 엔진 수 요청으로 넘어갑니다.
///
/// 현재 구현체는 AI 카드 사용을 판단하는 AiTurnController 하나이며,
/// 멀티플레이에서는 네트워크 응답을 기다리는 원격 프로바이더가 같은 자리에 들어갑니다.
///
/// 인터페이스가 아니라 MonoBehaviour 추상 클래스인 이유는 Unity가 [SerializeField]로
/// 인터페이스를 직렬화하지 못하기 때문입니다. 이렇게 두면 인스펙터 연결이 타입 안전해집니다.
/// </summary>
public abstract class TurnProvider : MonoBehaviour
{
    /// <summary>
    /// 상대 턴을 원격에서 받아오는 프로바이더인지 여부입니다.
    /// GameManager가 게임 모드에 맞는 프로바이더를 고르는 기준이며,
    /// 덕분에 한 씬에 AI용과 원격용을 함께 두어도 안전합니다.
    /// </summary>
    public virtual bool IsRemote => false;

    /// <summary>
    /// 이번 턴 처리를 시도합니다.
    /// </summary>
    /// <param name="gameManager">턴을 요청한 게임 매니저</param>
    /// <param name="boardManager">현재 보드 상태</param>
    /// <param name="fallbackMoveRequest">프로바이더가 수를 두지 않기로 했을 때 사용할 기본 착수 요청</param>
    /// <returns>이 프로바이더가 턴을 맡았으면 true. false면 호출측이 기본 착수를 요청합니다.</returns>
    public abstract bool TryRequestTurn(
        GameManager gameManager,
        BoardManager boardManager,
        Action fallbackMoveRequest);

    /// <summary>
    /// 로컬 플레이어가 확정한 행동을 상대에게 알립니다.
    /// 상대에게 전할 곳이 없는 프로바이더(예: AI 대전)는 그대로 무시하면 됩니다.
    /// </summary>
    public virtual void SendLocalAction(MatchMessage message) { }
}
