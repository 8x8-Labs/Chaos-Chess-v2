using TMPro;
using UnityEngine;

public class CurrentStage : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    private void Start()
    {
        titleText.text = MapManager.Instance?.curMap?.MapName;
    }
}
