using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 보드에 적용된 활성 Effector를 실시간으로 표시하는 디버그 패널입니다.
/// Effector의 정적 라이프사이클 이벤트만 구독하므로 폴링이 없습니다.
/// </summary>
public class EffectorInspectorPanel : MonoBehaviour
{
    [Tooltip("활성 이펙터 목록을 표시할 멀티라인 텍스트입니다.")]
    [SerializeField] private TMP_Text listText;

    private readonly List<Effector> active = new List<Effector>();
    private readonly StringBuilder sb = new StringBuilder();

    /// <summary>컨트롤러가 일괄 해제(Revert)에 사용할 수 있는 현재 활성 이펙터 목록입니다.</summary>
    public IReadOnlyList<Effector> Active => active;

    private void OnEnable()
    {
        Effector.OnAnyEffectApplied += HandleApplied;
        Effector.OnAnyEffectReverted += HandleReverted;
        Effector.OnAnyEffectTurnTicked += HandleTicked;
        Refresh();
    }

    private void OnDisable()
    {
        Effector.OnAnyEffectApplied -= HandleApplied;
        Effector.OnAnyEffectReverted -= HandleReverted;
        Effector.OnAnyEffectTurnTicked -= HandleTicked;
    }

    private void HandleApplied(Effector e)
    {
        if (e != null && !active.Contains(e))
            active.Add(e);
        Refresh();
    }

    private void HandleReverted(Effector e)
    {
        active.Remove(e);
        Refresh();
    }

    private void HandleTicked(Effector e)
    {
        Refresh();
    }

    private void Refresh()
    {
        // 파괴되었지만 이벤트가 누락된 항목 정리
        active.RemoveAll(e => e == null);

        if (listText == null)
            return;

        sb.Clear();
        sb.AppendLine($"<b>활성 이펙터: {active.Count}개</b>");

        foreach (Effector e in active)
        {
            if (e == null)
                continue;

            string cardName = e.CardSO != null && !string.IsNullOrEmpty(e.CardSO.CardName)
                ? e.CardSO.CardName
                : e.GetType().Name;
            string kind = GetKind(e);
            string turns = e.IsPermanent ? "∞" : e.RemainingTurns.ToString();
            string suspended = e.IsSuspended ? " (정지)" : "";

            sb.AppendLine($"· {cardName} [{kind}] - {turns}턴{suspended}");
        }

        listText.text = sb.ToString();
    }

    private static string GetKind(Effector e)
    {
        if (e is PieceEffector) return "Piece";
        if (e is TileEffector) return "Tile";
        if (e is GlobalEffector) return "Global";
        return "Effector";
    }

    /// <summary>현재 활성 이펙터를 모두 해제합니다.</summary>
    public void RevertAll()
    {
        // Revert가 목록을 수정하므로 복사본을 순회합니다.
        // 이벤트 구독을 임시로 해제하여 루프 내에서 Refresh()가 반복 호출되는 것을 방지하고,
        // 마지막에 한 번만 갱신합니다.
        Effector.OnAnyEffectReverted -= HandleReverted;
        try
        {
            Effector[] snapshot = active.ToArray();
            foreach (Effector e in snapshot)
            {
                if (e != null)
                    e.Revert();
            }
            active.Clear();
        }
        finally
        {
            Effector.OnAnyEffectReverted += HandleReverted;
        }
        Refresh();
    }
}
