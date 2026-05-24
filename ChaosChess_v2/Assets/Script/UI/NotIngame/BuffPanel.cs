using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuffPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    [Header("Text Layout")]
    [SerializeField] private float baseFontSize = 120f;
    [SerializeField] private float minFontSize = 12f;
    [SerializeField] private float fontStep = 12f;
    [SerializeField] private bool enableWordWrapping = true;

    private BuffSO buff;
    private BuffSide side;
    private int rolledMagnitude;

    public void Init(BuffSO randomBuff, BuffSide randomSide)
    {
        buff = randomBuff;
        side = randomSide;
        ApplyBaseTextLayout();

        if (buff == null)
        {
            text.text = "선택 가능한 버프가 없습니다.";
            FitTextToRect(text.text);
            StartCoroutine(RefitAfterLayout(text.text));
            return;
        }

        rolledMagnitude = buff.RollMagnitude(side);
        string description = buff.GetDescription(side, rolledMagnitude);
        text.text = description;
        FitTextToRect(description);
        StartCoroutine(RefitAfterLayout(description));
    }

    private void ApplyBaseTextLayout()
    {
        if (text == null) return;

        text.enableAutoSizing = false;
        text.fontSize = baseFontSize;
        text.textWrappingMode = enableWordWrapping
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private void FitTextToRect(string content)
    {
        if (text == null) return;

        float start = Mathf.Max(minFontSize, baseFontSize);
        float step = Mathf.Max(1f, fontStep);

        for (float size = start; size >= minFontSize; size -= step)
        {
            text.fontSize = size;
            text.text = content;
            text.ForceMeshUpdate();
            if (!text.isTextOverflowing)
            {
                return;
            }
        }

        text.fontSize = minFontSize;
    }

    private IEnumerator RefitAfterLayout(string content)
    {
        // 레이아웃 그룹/애니메이션에 의해 Rect가 늦게 확정되는 케이스 대응
        yield return null;
        yield return null;

        if (text == null) yield break;

        LayoutRebuilder.ForceRebuildLayoutImmediate(text.rectTransform);
        FitTextToRect(content);
    }

    public void OnClick()
    {
        if (PlayerState.Instance != null && buff != null)
            PlayerState.Instance.AddBuff(buff, side, rolledMagnitude);

        gameObject.SetActive(false);
    }
}
