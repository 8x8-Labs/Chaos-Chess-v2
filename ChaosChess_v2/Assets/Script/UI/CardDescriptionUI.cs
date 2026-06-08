// CardDescPanel을 메인 화면 UI 전용으로 분리한 클래스.
// 제거: GameManager 의존성(IsGameInput 토글), CardAnim 참조, executeButton 및 카드 실행 로직
// 변경: SetCardData 인자를 CardAnim → CardDataSO로 교체
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDescriptionUI : ButtonPanel
{
    private bool tierUIBindingAttempted;

    [Header("UI Elements")]
    [SerializeField] private Image cardImage;
    [SerializeField] private TMP_Text cardTitle;
    [SerializeField] private TMP_Text cardDesc;
    [Header("Tier UI")]
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private Image[] tierColorTargets;
    [SerializeField] private TierStyleSetSO tierStyleSet;
    // [수정] executeButton 제거 — 메인 화면에서는 카드를 실행하지 않음
    [SerializeField] private GameObject subDesc;
    [SerializeField] private TMP_Text subDescTitle;
    // 기물 타입
    [SerializeField] private Image subDescPieceImage;
    [SerializeField] private Image subDescMovementImage;
    [SerializeField] private TMP_Text subDescPieceContent;
    // 규칙 타입
    [SerializeField] private TMP_Text subDescContent;
    [SerializeField] private SubDescFoldController subDescFoldController;
    [SerializeField] private CardTargetUI targetUI;

    private Sprite subDescDefaultIconSprite;

    protected override void Awake()
    {
        base.Awake();
        TryAutoBindTierUI();
        CacheSubDescDefaultIcon();
    }

    // [변경] CardAnim 대신 CardDataSO를 직접 받아 표시만 수행
    public void SetCardData(CardDataSO data)
    {
        UpdateUI(data);
    }

    private void UpdateUI(CardDataSO data)
    {
        if (data == null) return;

        if (!tierUIBindingAttempted)
        {
            TryAutoBindTierUI();
        }

        if (cardImage != null) cardImage.sprite = data.CardImage;
        if (cardTitle != null) cardTitle.text = data.CardName;
        if (cardDesc != null) cardDesc.text = data.CardDescription;
        ApplyTierStyle(data.CardTier);
        targetUI?.SetData(data);

        if (data.NeedAdditionalDescription)
        {
            if (subDesc != null) subDesc.SetActive(true);
            subDescFoldController?.Hide();

            if (subDescTitle != null)
            {
                subDescTitle.enabled = true;
                subDescTitle.text = data.AdditionalDescriptionTitle;
            }

            bool isPiece = data.DescriptionType == AdditionalDescription.Piece;
            ApplySubDescIcon(data, isPiece);
            TMP_Text activeSubDescText = isPiece ? subDescPieceContent : subDescContent;

            if (subDescMovementImage != null) subDescMovementImage.gameObject.SetActive(isPiece);
            if (subDescPieceContent != null) subDescPieceContent.gameObject.SetActive(isPiece);
            if (subDescContent != null) subDescContent.gameObject.SetActive(!isPiece);

            if (isPiece)
            {
                if (subDescPieceImage != null)
                {
                    subDescPieceImage.enabled = true;
                }
                if (subDescMovementImage != null)
                {
                    subDescMovementImage.enabled = true;
                    subDescMovementImage.sprite = data.MovementImage;
                }
                if (subDescPieceContent != null)
                {
                    subDescPieceContent.enabled = true;
                    subDescPieceContent.text = data.PieceDescContent;
                }
            }
            else
            {
                if (subDescContent != null)
                {
                    subDescContent.enabled = true;
                    subDescContent.text = data.AdditionalDescriptionContent;
                }
            }

            subDescFoldController?.Refresh(activeSubDescText);
        }
        else
        {
            subDescFoldController?.Hide();
            if (subDesc != null) subDesc.SetActive(false);
        }
    }

    private void TryAutoBindTierUI()
    {
        if (tierUIBindingAttempted)
        {
            return;
        }

        tierUIBindingAttempted = true;

        Transform mainSlot = transform.Find("MainSlot");
        if (mainSlot == null)
        {
            return;
        }

        Transform tierRoot = mainSlot.Find("Tier");
        Transform lineRoot = mainSlot.Find("Line");

        if (tierText == null && tierRoot != null)
        {
            tierText = tierRoot.GetComponentInChildren<TMP_Text>(true);
        }

        TrySetDefaultColorTargets(tierRoot, lineRoot);
    }

    private void ApplyTierStyle(Tier tier)
    {
        TierStyleData style = tierStyleSet != null ? tierStyleSet.GetStyle(tier) : null;
        Color targetColor = style != null ? style.color : Color.white;

        ApplyColorToTargets(targetColor);

        if (tierText != null)
        {
            tierText.text = style != null ? style.label : "미확인 등급";
        }
    }

    private void TrySetDefaultColorTargets(Transform tierRoot, Transform lineRoot)
    {
        if (tierColorTargets != null && tierColorTargets.Length > 0)
        {
            return;
        }

        Image tierImage = tierRoot != null ? tierRoot.GetComponent<Image>() : null;
        Image lineImage = lineRoot != null ? lineRoot.GetComponent<Image>() : null;

        int targetCount = 0;
        if (tierImage != null) targetCount++;
        if (lineImage != null) targetCount++;

        if (targetCount == 0)
        {
            return;
        }

        tierColorTargets = new Image[targetCount];
        int index = 0;

        if (tierImage != null) tierColorTargets[index++] = tierImage;
        if (lineImage != null) tierColorTargets[index] = lineImage;
    }

    private void ApplyColorToTargets(Color color)
    {
        if (tierColorTargets == null)
        {
            return;
        }

        foreach (Image target in tierColorTargets)
        {
            if (target != null)
            {
                target.color = color;
            }
        }
    }

    private void ApplySubDescIcon(CardDataSO data, bool isPiece)
    {
        if (subDescPieceImage == null)
        {
            return;
        }

        if (!isPiece)
        {
            subDescPieceImage.gameObject.SetActive(false);
            return;
        }

        subDescPieceImage.gameObject.SetActive(true);
        subDescPieceImage.enabled = true;

        Sprite iconSprite = data.PieceDescImage;
        if (iconSprite != null)
        {
            subDescPieceImage.sprite = iconSprite;
            return;
        }

        if (subDescDefaultIconSprite != null)
        {
            subDescPieceImage.sprite = subDescDefaultIconSprite;
        }
    }

    private void CacheSubDescDefaultIcon()
    {
        if (subDescPieceImage != null && subDescDefaultIconSprite == null)
        {
            subDescDefaultIconSprite = subDescPieceImage.sprite;
        }
    }
}
