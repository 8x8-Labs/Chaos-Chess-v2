using UnityEngine;

[CreateAssetMenu(fileName = "TierStyleSet", menuName = "UI/Tier Style Set")]
public class TierStyleSetSO : ScriptableObject
{
    public TierStyleData[] tierStyles =
    {
        new TierStyleData { tier = Tier.Common, label = "일반 등급" },
        new TierStyleData { tier = Tier.Uncommon, label = "희귀" },
        new TierStyleData { tier = Tier.Unique, label = "고급" },
        new TierStyleData { tier = Tier.Rare, label = "레어" },
        new TierStyleData { tier = Tier.Legendary, label = "전설" }
    };

    public TierStyleData GetStyle(Tier tier)
    {
        if (tierStyles == null)
        {
            return null;
        }

        foreach (TierStyleData style in tierStyles)
        {
            if (style != null && style.tier == tier)
            {
                return style;
            }
        }

        return null;
    }
}
