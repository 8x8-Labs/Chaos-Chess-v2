using System;

public enum ActiveEffectStatusType
{
    Active,
    Installed,
    TurnBased,
    CountBased
}

[Serializable]
public sealed class ActiveEffectStatusTextSettings
{
    public string activeText = "활성";
    public string installedText = "설치";
    public string turnFormat = "{0}턴";
    public string countFormat = "{0}회";

    public string Format(ActiveEffectStatusType statusType, int value = 0)
    {
        return statusType switch
        {
            ActiveEffectStatusType.Active => activeText,
            ActiveEffectStatusType.Installed => installedText,
            ActiveEffectStatusType.TurnBased => string.Format(turnFormat, value),
            ActiveEffectStatusType.CountBased => string.Format(countFormat, value),
            _ => activeText
        };
    }
}
