using System;

/// <summary>매치 중 양쪽이 주고받는 행동의 종류입니다.</summary>
public enum MatchMessageKind
{
    Move = 0,
    Card = 1,
    Resign = 2
}

/// <summary>
/// 매치 중 한 번의 행동을 나타내는 메시지입니다.
///
/// 이 게임은 상태를 복제하지 않고 행동만 주고받는 커맨드 릴레이 방식이라,
/// 양쪽이 같은 초기 상태에서 같은 행동을 같은 순서로 적용하면 같은 결과에 도달합니다.
/// 그래서 한 턴에 오가는 데이터가 수십 바이트로 끝납니다.
///
/// JsonUtility로 직렬화할 수 있도록 필드만 두고 프로퍼티는 쓰지 않습니다.
/// </summary>
[Serializable]
public class MatchMessage
{
    public MatchMessageKind Kind;

    /// <summary>이 행동이 몇 번째 턴의 것인지. 중복·역순 도착을 걸러내는 데 씁니다.</summary>
    public int Turn;

    /// <summary>Move일 때의 착수(UCI). 예: "e2e4", 승격은 "e7e8q".</summary>
    public string Uci;

    /// <summary>Card일 때 사용한 카드 식별자.</summary>
    public string CardId;

    /// <summary>Card일 때 지정한 대상 칸들(UCI 좌표).</summary>
    public string[] Targets;

    public static MatchMessage CreateMove(int turn, string uci)
    {
        return new MatchMessage
        {
            Kind = MatchMessageKind.Move,
            Turn = turn,
            Uci = uci
        };
    }

    public static MatchMessage CreateCard(int turn, string cardId, string[] targets)
    {
        return new MatchMessage
        {
            Kind = MatchMessageKind.Card,
            Turn = turn,
            CardId = cardId,
            Targets = targets
        };
    }

    public static MatchMessage CreateResign(int turn)
    {
        return new MatchMessage
        {
            Kind = MatchMessageKind.Resign,
            Turn = turn
        };
    }

    public override string ToString()
    {
        switch (Kind)
        {
            case MatchMessageKind.Move: return $"[T{Turn}] Move {Uci}";
            case MatchMessageKind.Card: return $"[T{Turn}] Card {CardId}";
            default: return $"[T{Turn}] Resign";
        }
    }
}
