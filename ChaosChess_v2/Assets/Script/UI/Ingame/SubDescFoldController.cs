using TMPro;
using UnityEngine;

public class SubDescFoldController : MonoBehaviour
{
    [SerializeField] private RectTransform subDescRoot;
    [SerializeField] private RectTransform pushedContentRoot;
    [SerializeField] private GameObject moreToggle;
    [SerializeField] private TMP_Text moreToggleIcon;
    [SerializeField] private TMP_Text pieceDescriptionText;
    [SerializeField] private TMP_Text ruleDescriptionText;
    [SerializeField] private float minExpandHeight = 0f;
    [SerializeField] private float maxExpandHeight = 640f;
    [SerializeField] private float overflowPadding = 32f;

    private TMP_Text activeText;
    private Vector2 rootDefaultSize;
    private Vector2 rootDefaultPosition;
    private Vector2 pushedContentDefaultPosition;
    private Vector2 pieceTextDefaultSize;
    private Vector2 ruleTextDefaultSize;
    private float currentExpandHeight;
    private bool isExpanded;
    private bool hasDefaults;

    private void Awake()
    {
        CacheDefaults();
        ConfigureTextOverflow();
        HideToggle();
    }

    public void Refresh(TMP_Text text)
    {
        CacheDefaults();
        ConfigureTextOverflow();

        activeText = text;
        SetExpanded(false);

        bool shouldShow = IsTextOverflowing(activeText);
        if (moreToggle != null)
        {
            moreToggle.SetActive(shouldShow);
        }

        currentExpandHeight = shouldShow ? CalculateExpandHeight(activeText) : 0f;
    }

    public void Hide()
    {
        SetExpanded(false);
        activeText = null;
        HideToggle();
    }

    public void Toggle()
    {
        SetExpanded(!isExpanded);
    }

    private void HideToggle()
    {
        if (moreToggle != null)
        {
            moreToggle.SetActive(false);
        }
    }

    private void CacheDefaults()
    {
        if (hasDefaults || subDescRoot == null)
        {
            return;
        }

        rootDefaultSize = subDescRoot.sizeDelta;
        rootDefaultPosition = subDescRoot.anchoredPosition;

        if (pushedContentRoot != null)
        {
            pushedContentDefaultPosition = pushedContentRoot.anchoredPosition;
        }

        if (pieceDescriptionText != null)
        {
            pieceTextDefaultSize = pieceDescriptionText.rectTransform.sizeDelta;
        }

        if (ruleDescriptionText != null)
        {
            ruleTextDefaultSize = ruleDescriptionText.rectTransform.sizeDelta;
        }

        hasDefaults = true;
    }

    private void ConfigureTextOverflow()
    {
        if (pieceDescriptionText != null)
        {
            pieceDescriptionText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (ruleDescriptionText != null)
        {
            ruleDescriptionText.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void SetExpanded(bool expanded)
    {
        CacheDefaults();

        if (!hasDefaults || subDescRoot == null)
        {
            isExpanded = false;
            return;
        }

        isExpanded = expanded;
        float heightDelta = expanded ? currentExpandHeight : 0f;

        subDescRoot.sizeDelta = new Vector2(rootDefaultSize.x, rootDefaultSize.y + heightDelta);
        subDescRoot.anchoredPosition = rootDefaultPosition;
        ApplyPushedContentPosition(heightDelta);
        ApplyTextHeight(heightDelta);

        if (moreToggleIcon != null)
        {
            moreToggleIcon.text = "v";
            moreToggleIcon.rectTransform.localScale = new Vector3(1f, expanded ? -1f : 1f, 1f);
        }
    }

    private void ApplyPushedContentPosition(float heightDelta)
    {
        if (pushedContentRoot == null)
        {
            return;
        }

        pushedContentRoot.anchoredPosition = pushedContentDefaultPosition + Vector2.down * heightDelta;
    }

    private void ApplyTextHeight(float heightDelta)
    {
        if (pieceDescriptionText != null)
        {
            RectTransform rectTransform = pieceDescriptionText.rectTransform;
            rectTransform.sizeDelta = new Vector2(pieceTextDefaultSize.x, pieceTextDefaultSize.y + heightDelta);
        }

        if (ruleDescriptionText != null)
        {
            RectTransform rectTransform = ruleDescriptionText.rectTransform;
            rectTransform.sizeDelta = new Vector2(ruleTextDefaultSize.x, ruleTextDefaultSize.y + heightDelta);
        }
    }

    private bool IsTextOverflowing(TMP_Text text)
    {
        if (text == null || !text.gameObject.activeInHierarchy)
        {
            return false;
        }

        Canvas.ForceUpdateCanvases();
        text.ForceMeshUpdate();

        float visibleHeight = text.rectTransform.rect.height;
        return text.isTextOverflowing || text.preferredHeight > visibleHeight + 1f;
    }

    private float CalculateExpandHeight(TMP_Text text)
    {
        if (text == null)
        {
            return minExpandHeight;
        }

        float overflowHeight = Mathf.Max(0f, text.preferredHeight - text.rectTransform.rect.height);
        return Mathf.Clamp(overflowHeight + overflowPadding, minExpandHeight, maxExpandHeight);
    }
}
