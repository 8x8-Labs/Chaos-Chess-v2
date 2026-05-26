using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum CardSelectionOwner
{
    None,
    Piece,
    Tile
}

public static class CardSelectionState
{
    private static CardSelectionOwner currentSelectionOwner = CardSelectionOwner.None;
    public static bool IsLocked => currentSelectionOwner != CardSelectionOwner.None;

    public static void Lock(CardSelectionOwner owner)
    {
        if (owner == CardSelectionOwner.None) return;
        currentSelectionOwner = owner;
    }

    public static void Unlock(CardSelectionOwner owner)
    {
        if (currentSelectionOwner == owner)
            currentSelectionOwner = CardSelectionOwner.None;
    }

    public static void Reset()
    {
        currentSelectionOwner = CardSelectionOwner.None;
    }
}

public abstract class Selector<T> : MonoBehaviour
{
    [SerializeField] protected Tilemap tilemap;
    [SerializeField] protected Tilemap gameSelectTilemap;
    [SerializeField] protected BoardManager boardManager;

    protected CardData cardData;
    protected Canvas selectorCanvas;
    protected Camera mainCamera;
    protected List<T> selectedTargets = new List<T>();
    protected bool selectState = false;
    protected CardRandomizer cardRandomizer;

    protected virtual void Awake()
    {
        selectorCanvas = GetComponent<Canvas>();
        selectorCanvas.enabled = false;

        mainCamera = Camera.main;
        cardRandomizer = FindFirstObjectByType<CardRandomizer>();
    }

    protected void LockCardSelection(CardSelectionOwner owner)
    {
        CardSelectionState.Lock(owner);
    }

    protected void UnlockCardSelection(CardSelectionOwner owner)
    {
        // 현재 잠금 소유자만 해제 가능하게 해서 Piece->Tile 전환 중 잠금이 풀리지 않게 합니다.
        CardSelectionState.Unlock(owner);
    }

    public abstract void EnableSelector(CardData data);
    protected abstract void DisableSelector();

    public abstract void SelectTarget(T target);
    public abstract void DeselectTarget(T target);
    public abstract void DeselectFirstTarget();
    public abstract void DeselectAllTarget();
    public abstract void ExecuteSkill();
    protected virtual bool isExecute() { return false; }
    protected virtual bool isTargetExist() { return false; }
}