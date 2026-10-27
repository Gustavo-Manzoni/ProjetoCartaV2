using UnityEngine;

public abstract class InteractableBase : MonoBehaviour
{
    public abstract void OnClicked();
    public virtual void OnLookedAt(){}
    public virtual void OnLookOut(){}

}
