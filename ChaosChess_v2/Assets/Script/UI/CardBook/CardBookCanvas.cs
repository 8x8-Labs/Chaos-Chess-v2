using UnityEngine;

public class CardBookCanvas : ButtonCanvas
{
    [SerializeField] private CardBookTierGroup[] tierGroups;
    [SerializeField] private CardDescriptionUI descriptionUI;

    protected override void Awake()
    {
        base.Awake();
        foreach (CardBookTierGroup group in tierGroups)
            group?.SetDescriptionUI(descriptionUI);
    }

    public override void EnableParent()
    {
        RefreshAll();
        base.EnableParent();
    }

    private void RefreshAll()
    {
        if (tierGroups == null) return;

        foreach (CardBookTierGroup group in tierGroups)
        {
            group?.RefreshStates();
            group?.ResetScroll();
        }
    }
}
