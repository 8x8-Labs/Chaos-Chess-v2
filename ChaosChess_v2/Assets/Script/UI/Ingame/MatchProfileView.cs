using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 헤더의 플레이어/상대 프로필이 실제 진영을 따라가도록 맞춥니다.
///
/// 슬롯 자체는 "나 / 상대"라는 상대적 배치라 그대로 두면 되지만, 슬롯에 칠해진 색은
/// 씬에 백/흑으로 고정되어 있습니다. 플레이어가 흑을 잡으면 내 슬롯이 백으로,
/// 상대 슬롯이 흑으로 보여 실제 진영과 어긋납니다.
///
/// 보드는 BoardManager.ApplyBoardView가 뒤집어 주므로, 헤더도 같은 기준으로 맞춥니다.
/// 대국 중에는 진영이 바뀌지 않으므로 시작할 때 한 번만 정리합니다.
/// </summary>
public class MatchProfileView : MonoBehaviour
{
    [Tooltip("내 프로필 슬롯의 Image입니다. (Header > Player)")]
    [SerializeField] private Image playerProfile;

    [Tooltip("상대 프로필 슬롯의 Image입니다. (Header > Enemy)")]
    [SerializeField] private Image enemyProfile;

    [Tooltip("씬에 칠해진 색이 어느 진영 기준인지. 기본은 내 슬롯이 백입니다.")]
    [SerializeField] private PieceColor authoredPlayerColor = PieceColor.White;

    private void Start()
    {
        if (playerProfile == null || enemyProfile == null)
        {
            Debug.LogWarning("[Profile] 프로필 Image가 연결되지 않아 진영 표시를 맞추지 못했습니다.");
            return;
        }

        if (GameManager.Instance == null)
            return;

        // 씬에 칠해진 기준과 이번 매치의 진영이 같으면 손댈 것이 없습니다.
        if (GameManager.Instance.PlayerColor == authoredPlayerColor)
            return;

        SwapVisuals();
    }

    /// <summary>두 슬롯의 표시만 맞바꿉니다. 슬롯의 의미(나 / 상대)는 그대로입니다.</summary>
    private void SwapVisuals()
    {
        (playerProfile.color, enemyProfile.color) = (enemyProfile.color, playerProfile.color);
        (playerProfile.sprite, enemyProfile.sprite) = (enemyProfile.sprite, playerProfile.sprite);

        Debug.Log($"[Profile] 플레이어가 {GameManager.Instance.PlayerColor}라서 프로필 표시를 맞바꿨습니다.");
    }
}
