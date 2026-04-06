using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    protected virtual void Awake()
    {
        selectorCanvas = GetComponent<Canvas>();
        selectorCanvas.enabled = false;

        mainCamera = Camera.main;
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