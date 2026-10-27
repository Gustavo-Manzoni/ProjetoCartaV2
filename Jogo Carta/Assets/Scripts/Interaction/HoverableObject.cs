using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public sealed class HoverableObject : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visual;
    [SerializeField] private Behaviour[] outlineComponents;

    [Header("Hover animation")]
    [SerializeField] private float scaleMultiplier = 1.05f;
    [SerializeField] private float yOffset = 0.05f;
    [SerializeField] private float animationDuration = 0.18f;
    [SerializeField] private Ease hoverEase = Ease.OutQuad;
    [SerializeField] private Ease exitEase = Ease.OutQuad;
    [SerializeField] private Vector3 hoverRotation = new(-1.5f, 0f, 0f);

    [Header("Witness name")]
    [SerializeField] private Transform witnessNameVisual;
    [SerializeField] private Vector3 witnessNameVisibleScale = Vector3.one;
    [SerializeField] private float witnessNameYOffset = 0.08f;
    [SerializeField] private float witnessNameShowDuration = 0.16f;
    [SerializeField] private float witnessNameHideDuration = 0.1f;
    [SerializeField] private Ease witnessNameShowEase = Ease.OutBack;
    [SerializeField] private Ease witnessNameHideEase = Ease.InQuad;

    [Header("Click feedback")]
    [SerializeField] private Vector3 clickPunchScale = new(0.05f, -0.03f, 0.05f);
    [SerializeField] private float clickPunchDuration = 0.12f;

    [Header("Interaction")]
    [SerializeField] private UnityEvent onClick;

    private Vector3 initialLocalPosition;
    private Vector3 initialLocalScale;
    private Quaternion initialLocalRotation;
    private Vector3 witnessNameInitialPosition;
    private Tween hoverTween;
    private Tween clickTween;
    private Tween witnessNameTween;
    private bool isHovered;

    private void Awake()
    {
        if (visual == null)
            visual = transform;

        initialLocalPosition = visual.localPosition;
        initialLocalScale = visual.localScale;
        initialLocalRotation = visual.localRotation;

        if (witnessNameVisual != null)
        {
            witnessNameInitialPosition = witnessNameVisual.localPosition;
            witnessNameVisual.localScale = Vector3.zero;
        }

        SetOutline(false);
    }

    public void SetHovered(bool hovered)
    {
        if (isHovered == hovered)
            return;

        isHovered = hovered;
        SetOutline(hovered);

        hoverTween?.Kill();

        Vector3 targetPosition = hovered
            ? initialLocalPosition + Vector3.up * yOffset
            : initialLocalPosition;
        Vector3 targetScale = hovered
            ? initialLocalScale * scaleMultiplier
            : initialLocalScale;
        Ease ease = hovered ? hoverEase : exitEase;
        Quaternion targetRotation = hovered
            ? initialLocalRotation * Quaternion.Euler(hoverRotation)
            : initialLocalRotation;

        hoverTween = DOTween.Sequence()
            .Join(visual.DOLocalMove(targetPosition, animationDuration))
            .Join(visual.DOScale(targetScale, animationDuration))
            .Join(visual.DOLocalRotateQuaternion(targetRotation, animationDuration))
            .SetEase(ease)
            .SetUpdate(true);

        AnimateWitnessName(hovered);
    }

    public void PlayPressedFeedback()
    {
        if (visual == null)
            return;

        isHovered = false;
        SetOutline(false);
        hoverTween?.Kill();
        clickTween?.Kill();
        AnimateWitnessName(false);
        clickTween = DOTween.Sequence()
            .Append(visual.DOPunchScale(clickPunchScale, clickPunchDuration, 4, 0.35f))
            .Join(visual.DOLocalMove(initialLocalPosition, clickPunchDuration))
            .Join(visual.DOLocalRotateQuaternion(initialLocalRotation, clickPunchDuration))
            .Append(visual.DOScale(initialLocalScale, 0.06f))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    public void InvokeClick()
    {
        onClick?.Invoke();
    }

    private void SetOutline(bool visible)
    {
        foreach (Behaviour outline in outlineComponents)
        {
            if (outline != null)
                outline.enabled = visible;
        }
    }

    private void AnimateWitnessName(bool visible)
    {
        if (witnessNameVisual == null)
            return;

        witnessNameTween?.Kill();

        Vector3 targetPosition = visible
            ? witnessNameInitialPosition + Vector3.up * witnessNameYOffset
            : witnessNameInitialPosition;
        Vector3 targetScale = visible ? witnessNameVisibleScale : Vector3.zero;
        float duration = visible ? witnessNameShowDuration : witnessNameHideDuration;
        Ease ease = visible ? witnessNameShowEase : witnessNameHideEase;

        witnessNameTween = DOTween.Sequence()
            .Join(witnessNameVisual.DOLocalMove(targetPosition, duration))
            .Join(witnessNameVisual.DOScale(targetScale, duration))
            .SetEase(ease)
            .SetUpdate(true);
    }

    private void OnDisable()
    {
        hoverTween?.Kill();
        clickTween?.Kill();
        witnessNameTween?.Kill();

        if (visual != null)
        {
            visual.localPosition = initialLocalPosition;
            visual.localScale = initialLocalScale;
            visual.localRotation = initialLocalRotation;
        }

        if (witnessNameVisual != null)
        {
            witnessNameVisual.localPosition = witnessNameInitialPosition;
            witnessNameVisual.localScale = Vector3.zero;
        }

        isHovered = false;
        SetOutline(false);
    }
}
