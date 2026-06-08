using TMPro;
using UnityEngine;

public class CurrentStage : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    private void Start()
    {
        if (titleText == null) return;
        titleText.text = MapManager.Instance?.curMap?.MapName ?? "Unknown Stage";
    }
}
