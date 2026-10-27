using UnityEngine;
using System.Collections;
public class DesactiveObjectsWithDelay : MonoBehaviour
{
    [SerializeField] GameObject[] _objectsToActive;
    [SerializeField]  float       _delayToStart;
    [SerializeField] float        _delayForEachObject;
    [SerializeField] bool         _state;
    [SerializeField] bool         _activeOnStart;
    
    void Start()
    {
        if(!_activeOnStart) return;
        StartCoroutine(ActiveObjects(_state));
    }
    IEnumerator ActiveObjects(bool state)
    {
        yield return new WaitForSeconds(_delayToStart);
        foreach(GameObject obj in _objectsToActive)
        {
            obj.SetActive(_state);
            yield return new WaitForSeconds(_delayForEachObject);
        }


    }
    public void ActiveObjectsByState()
    {
        
        StartCoroutine(ActiveObjects(_state));
    }
    
    public void ActiveObjects()
    {
        StartCoroutine(ActiveObjects(true));

    }
    
    public void DesactiveObjects()
    {
        StartCoroutine(ActiveObjects(false));

    }
}
