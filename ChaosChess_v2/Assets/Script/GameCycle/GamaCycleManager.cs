using UnityEngine;
using System.Collections.Generic;

public class GamaCycleManager : MonoBehaviour
{
    public static GamaCycleManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartGame()
    {
        PlayerState.Instance?.InitializeRun();
        MapManager.Instance?.Init();
    }

    public void StartPractice(PracticeDifficulty difficulty)
    {
        PlayerState.Instance.InitializeRun();
        GiveAllCards();
        MapManager.Instance.StartPractice(difficulty);
    }

    private void GiveAllCards()
    {
        foreach (GameObject card in CardRandomizerManager.Instance.AllCards)
        {
            PlayerState.Instance.AddCard(card);
        }
    }
}
