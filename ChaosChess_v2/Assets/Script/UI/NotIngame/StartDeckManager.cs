using TMPro;
using UnityEngine;

public class StartDeckManager : MonoBehaviour
{
    [SerializeField] private UICardPanel uiCardPanel;
    [SerializeField] private int startCardCount = 4;

    private void Start()
    {
        if (uiCardPanel == null)
            return;

        uiCardPanel.SetRequestedCardCount(startCardCount);
    }
}
