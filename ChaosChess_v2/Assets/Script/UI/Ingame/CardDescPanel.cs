using UnityEngine;

public class CardDescPanel : ButtonPanel
{
    private GameManager gameManager = GameManager.Instance;

    public override void DisablePanel()
    {
        base.DisablePanel();
        if( gameManager == null ) gameManager = GameManager.Instance;
        gameManager.IsGameInput = true;
    }

    public override void EnablePanel()
    {
        base.EnablePanel();
        if( gameManager == null ) gameManager = GameManager.Instance;
        gameManager.IsGameInput = false;
    }
}
