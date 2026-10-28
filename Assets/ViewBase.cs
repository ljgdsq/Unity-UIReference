using UnityEngine;

public interface IView
{
}


[RequireComponent(typeof(ReferenceHolder))]
public abstract class ViewBase : MonoBehaviour, IView
{
    protected ReferenceHolder referenceHolder;
    protected virtual void Awake()
    {
        referenceHolder = GetComponent<ReferenceHolder>();
        BindReferences();
        OnViewAwake();
    }

    protected virtual void BindReferences()
    {
        
    }

    protected virtual void OnViewAwake()
    {
    }
}
