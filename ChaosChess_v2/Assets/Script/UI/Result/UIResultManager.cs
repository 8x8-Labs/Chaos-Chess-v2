using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIResultManager : MonoBehaviour
{
    void Start()
    {
        UpdateRecordText();
        SpawnDeckCardUI();
    }


    [SerializeField] private TextMeshProUGUI winValue;
    [SerializeField] private TextMeshProUGUI drawValue;
    [SerializeField] private TextMeshProUGUI loseValue;

    private void UpdateRecordText()
    {
        if (PlayerState.Instance == null) return;

        winValue.text = PlayerState.Instance.WinCount.ToString();
        drawValue.text = PlayerState.Instance.DrawCount.ToString();
        loseValue.text = PlayerState.Instance.LoseCount.ToString();
    }

    [SerializeField] private Transform content;
    [SerializeField] private GameObject cardPrefab;

    private void SpawnDeckCardUI()
    {
        if (PlayerState.Instance == null) return;

        List<GameObject> cards = PlayerState.Instance.CardPool.ToList();

        foreach (GameObject card in cards)
        {
            CardData cardData = card.GetComponent<CardData>();

            GameObject cardObject = Instantiate(cardPrefab, content);

            Image img = cardObject.GetComponentInChildren<Image>();
            TextMeshProUGUI text = cardObject.GetComponentInChildren<TextMeshProUGUI>();

            if (cardData.DataSO.CardImage != null)
                img.sprite = cardData.DataSO.CardImage;

            text.text = cardData.DataSO.CardName;
        }
    }
}
