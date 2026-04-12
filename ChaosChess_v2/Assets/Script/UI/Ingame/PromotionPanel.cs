using System;

public class PromotionPanel : ButtonPanel
{
    private GameManager gameManager = GameManager.Instance;
    private Action<char> onSelected;

    public void Show(Action<char> callback)
    {
        onSelected = callback;
        EnablePanel();
    }

    public override void EnablePanel()
    {
        base.EnablePanel();
        if (gameManager == null) gameManager = GameManager.Instance;
        gameManager.IsGameInput = false;
    }

    public override void DisablePanel()
    {
        base.DisablePanel();
        if (gameManager == null) gameManager = GameManager.Instance;
        gameManager.IsGameInput = true;
    }

    public void OnClickQueen() => Select('q');
    public void OnClickRook() => Select('r');
    public void OnClickBishop() => Select('b');
    public void OnClickKnight() => Select('n');

    private void Select(char type)
    {
        DisablePanel();
        onSelected?.Invoke(type);
    }
}