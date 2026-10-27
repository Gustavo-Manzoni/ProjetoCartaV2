using System.Collections;
using TMPro;
using UnityEngine;

public class FpsText : MonoBehaviour
{

    TMP_Text text;
    Coroutine fpsCoroutine;
    [SerializeField] float fpsCheckerTime;
    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }
    private void OnEnable()
    {
        fpsCoroutine = StartCoroutine(CheckFps());
    }
    private void OnDisable()
    {
        StopCheckingFps();
    }
    IEnumerator CheckFps() 
    {
        float fps = 1 / Time.deltaTime;
        text.text = fps.ToString("F0");
        yield return new WaitForSeconds(fpsCheckerTime);
        fpsCoroutine = StartCoroutine(CheckFps());
    }
    void StopCheckingFps() 
    {
        if(fpsCoroutine != null) 
        StopCoroutine(fpsCoroutine);
    }
}
