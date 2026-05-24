using TMPro;
using UnityEngine;

public class BuffPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    private BuffSO buff;
    private BuffSide side;
    private int rolledMagnitude;

    public void Init(BuffSO randomBuff, BuffSide randomSide)
    {
        buff = randomBuff;
        side = randomSide;

        if (buff == null)
        {
            text.text = "선택 가능한 버프가 없습니다.";
            return;
        }

        rolledMagnitude = buff.RollMagnitude(side);
        text.text = buff.GetDescription(side, rolledMagnitude);
    }

    public void OnClick()
    {
        if (PlayerState.Instance != null && buff != null)
            PlayerState.Instance.AddBuff(buff, side, rolledMagnitude);

        gameObject.SetActive(false);
    }
}