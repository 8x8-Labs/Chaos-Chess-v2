using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class StartDeckRerollButton : MonoBehaviour
{
    private Button cachedButton;

    public Button Button
    {
        get
        {
            if (cachedButton == null)
                cachedButton = GetComponent<Button>();

            return cachedButton;
        }
    }
}
