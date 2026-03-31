using UnityEngine;

public abstract class CardData : MonoBehaviour
{
    public CardDataSO DataSO;

    public virtual void Execute() { }
}