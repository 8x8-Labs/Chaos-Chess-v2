using UnityEngine;

public class ButtonParent : MonoBehaviour
{
    [SerializeField] protected bool isMainParent = false;

    protected virtual void Start()
    {
        if(isMainParent) EnableParent();
        else DisableParent();
    }

    public virtual void EnableParent() { }
    public virtual void DisableParent() { }
}
