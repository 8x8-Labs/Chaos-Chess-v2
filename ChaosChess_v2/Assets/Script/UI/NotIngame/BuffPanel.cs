using TMPro;
using UnityEngine;

public class BuffPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    private BuffSO buff;

    public void Init(BuffSO randomBuff)
    {
        buff = randomBuff;

        text.text = buff.description;
    }

    public void OnClick()
    {
        if (PlayerState.Instance != null)
            PlayerState.Instance.AddBuff(buff);

        gameObject.SetActive(false);
    }
}