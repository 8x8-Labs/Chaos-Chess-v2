using TMPro;
using UnityEngine;

public class BuffPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    private BuffSO buff;
    private BuffSide side;

    public void Init(BuffSO randomBuff, BuffSide randomSide)
    {
        buff = randomBuff;
        side = randomSide;

        text.text = buff.Description;
    }

    public void OnClick()
    {
        if (PlayerState.Instance != null && buff != null)
            PlayerState.Instance.AddBuff(buff, side);

        gameObject.SetActive(false);
    }
}