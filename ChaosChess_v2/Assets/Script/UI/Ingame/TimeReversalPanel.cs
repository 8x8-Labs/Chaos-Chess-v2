using System;

public class TimeReversalPanel : ButtonPanel
{
    private Action onYes;
    private Action onNo;

    public void Show(Action yes, Action no)
    {
        onYes = yes;
        onNo = no;
        EnablePanel();
    }

    public void OnClickYes()
    {
        DisablePanel();
        onYes?.Invoke();
    }

    public void OnClickNo()
    {
        DisablePanel();
        onNo?.Invoke();
    }
}