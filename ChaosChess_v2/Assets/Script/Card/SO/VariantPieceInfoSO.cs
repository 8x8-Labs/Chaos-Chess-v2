using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 엘리트 노드에서 등장하는 변형 기물(Amazon/Chancellor/KnightRider)의 설명 데이터베이스입니다.
/// 기물 종류별로 이름/기물 이미지/행마법 이미지/설명을 보관하며, 설명 UI가 이를 조회해 표시합니다.
/// (필드 구성은 CardDataSO의 기물 부가 설명 필드와 동일한 의미로 맞춰, 기존 자산을 그대로 재사용할 수 있습니다.)
/// </summary>
[CreateAssetMenu(fileName = "VariantPieceInfo", menuName = "ChaosChess/Variant Piece Info DB")]
public class VariantPieceInfoSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("이 설명이 대응하는 변형 기물 종류")]
        public PieceType pieceType;
        [Tooltip("기물 이름 (예: 아마존)")]
        public string pieceName;
        [Tooltip("기물 이미지")]
        public Sprite pieceImage;
        [Tooltip("행마법 이미지")]
        public Sprite movementImage;
        [TextArea]
        [Tooltip("기물 설명")]
        public string description;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    /// <summary>주어진 기물 종류의 설명 엔트리를 반환합니다. 없으면 null.</summary>
    public Entry Get(PieceType type)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].pieceType == type) return entries[i];
        }
        return null;
    }
}
