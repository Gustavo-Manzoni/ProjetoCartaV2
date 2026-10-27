using UnityEngine;
using TMPro;
using DG.Tweening;
    [System.Serializable]
    public struct TooltipStats
    {
        public string title;
        public bool isRevealed;
        public string description;
    }
public class TooltipManager : MonoBehaviour
{
    [SerializeField] Transform tooltip;
    [SerializeField] TMP_Text titleText;
    [SerializeField] GameObject notRevealedObj;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] float scaleTime;
[SerializeField] float reScaleTime;
    [SerializeField] Vector3 tooltipOffset;
    [SerializeField] Ease scaleEase;
    Tweener scaleTween;
    Vector3 tooltipBaseScale;
    private void Awake()
    {
        ServiceLocator.Register(this);
        tooltipBaseScale = tooltip.localScale;
    }
    public void ShowTooltip(Vector3 pos, TooltipStats attibutes) 
    {

        titleText.text = attibutes.title;
        descriptionText.text = attibutes.description;
        
        descriptionText.gameObject.SetActive(attibutes.isRevealed);
        notRevealedObj.SetActive(!attibutes.isRevealed);

        tooltip.position = pos + tooltipOffset;
        if(scaleTween != null && scaleTween.IsPlaying())
        {
            scaleTween?.SmoothRewind();
            return;
        }
        tooltip.gameObject.SetActive(true);
        scaleTween = tooltip.transform.DOScale(tooltipBaseScale, scaleTime).SetEase(scaleEase);
    }
    public void HideTooltip()
    {
        //scaleTween?.Kill();
        if(scaleTween != null && scaleTween.IsPlaying())
        {
            scaleTween?.SmoothRewind();
            return;
        }
        scaleTween =tooltip.transform.DOScale(Vector3.zero, reScaleTime).SetEase(scaleEase);
        
    }
}
