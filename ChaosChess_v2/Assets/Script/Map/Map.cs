using System.Collections.Generic;
using UnityEngine;

// 노드 종류: Normal(일반 전투), Elite(강화 전투), Boss(보스전)
public enum NodeType { Normal, Elite, Boss }

// 맵 그래프의 노드 하나를 나타내는 데이터 클래스
[System.Serializable]
public class Map
{
    public int ELO;             // 이 노드의 AI 강도 (Fairy Stockfish SetElo에 직접 전달)
    public string FEN;          // 이 노드의 초기 보드 배치 (FEN 문자열)
    public string MapName;      // 맵 이름 (UI 표시용)
    public bool isCleared;      // 플레이어가 클리어했는지 여부
    public int floor;           // 층 번호 (0-based)

    public NodeType nodeType;
    public int column;          // 같은 층 내 열 인덱스
    public List<int> nextColumns = new(); // 다음 층에서 연결된 노드들의 열 인덱스
    public bool isAccessible;   // 현재 선택 가능한 노드인지 여부
    public Vector2 uiPosition;  // MapUI에서 버튼을 배치할 앵커 기준 좌표
}
