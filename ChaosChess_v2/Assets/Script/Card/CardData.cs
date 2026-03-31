using System.Collections.Generic;
using UnityEngine;

public abstract class CardData : MonoBehaviour
{
    public CardDataSO DataSO;
}

public class CardEffectArgs
{
    public List<Piece> Targets;             // 선택된 기물들
    public List<Vector3Int> TargetPos;      // 선택된 좌표
    public int LimitTurn;                   // 가변 수치 등
}