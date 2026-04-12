using System;

public class AwakenPanel : ButtonPanel
{
    private Action onClick;

    public void Show(Action callback)
    {
        onClick = callback;
        EnablePanel();
    }

    public void Hide()
    {
        DisablePanel();
        onClick = null;
    }

    public void OnClickAwaken()
    {
        DisablePanel();
        onClick?.Invoke();
    }
}