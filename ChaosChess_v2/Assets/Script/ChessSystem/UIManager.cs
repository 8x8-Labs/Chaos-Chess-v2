using System.Data.Common;
using UnityEngine;
using UnityEngine.UIElements;

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
}
