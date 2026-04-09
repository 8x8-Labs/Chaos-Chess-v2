using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject PromotionPanel;

    private System.Action<char> onSelected;

    void Awake()
    {
        PromotionPanel.SetActive(false);
    }

    public void Show(System.Action<char> callback)
    {
        onSelected = callback;
        PromotionPanel.SetActive(true);
    }

    public void OnClickQueen() => Select('q');
    public void OnClickRook() => Select('r');
    public void OnClickBishop() => Select('b');
    public void OnClickKnight() => Select('n');

    private void Select(char type)
    {
        PromotionPanel.SetActive(false);
        onSelected?.Invoke(type);
    }


    [SerializeField] private GameObject TimeReversalPanel;

    private System.Action onYes;
    private System.Action onNo;

    public void ShowTimeReversal(System.Action yes, System.Action no)
    {
        onYes = yes;
        onNo = no;

        TimeReversalPanel.SetActive(true);
    }

    public void OnClickTimeReversalYes()
    {
        TimeReversalPanel.SetActive(false);
        onYes?.Invoke();
    }

    public void OnClickTimeReversalNo()
    {
        TimeReversalPanel.SetActive(false);
        onNo?.Invoke();
    }
}
