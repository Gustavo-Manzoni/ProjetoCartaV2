using UnityEngine;
using UnityEngine.Events;
public class Conditions : MonoBehaviour
{
    [SerializeField] Condition[] conditions;
    
    [SerializeField] UnityEvent onCondicionTrue;
    [SerializeField] UnityEvent onCondicionFalse;

    [ContextMenu("Check Conditions")]
    public bool CheckConditions()
    {
        foreach (var condition in conditions)
        {
            if (!condition.CheckCondicion())
            {
                onCondicionFalse?.Invoke();
                return false;
            }
        }
        onCondicionTrue?.Invoke();
        return true;
    }
}
