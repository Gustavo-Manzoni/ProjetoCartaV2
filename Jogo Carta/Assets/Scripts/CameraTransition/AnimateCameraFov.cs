using UnityEngine;
using DG.Tweening;
using System.Collections;
public class AnimateCameraFov : MonoBehaviour
{
    Camera cam;
    [SerializeField] float targetFov = 30f;
    [SerializeField] float duration = 1f;
    [SerializeField] Ease easeType = Ease.InOutQuad;
    void Awake()
    {
        cam = GetComponent<Camera>();
    }
    
    public void AnimateFov(float delay = 0)
    {
        StartCoroutine(AnimateFovDelayed(delay));
    }
    IEnumerator AnimateFovDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        cam.DOFieldOfView(targetFov, duration).SetEase(easeType);
    }
}
