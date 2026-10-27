using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class EvidenceCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text cardText;

    [Header("Visual feedback")]
    [SerializeField] private Transform visual;
    [SerializeField] private Behaviour[] outlineComponents;
    [SerializeField] private float hoverScale = 1.035f;
    [SerializeField] private float dragScale = 1.08f;
    [SerializeField] private Vector3 hoverRotation = new(-1.5f, 0f, 0f);
    [SerializeField] private float feedbackDuration = 0.08f;

    [Header("Drag smoothing")]
    [SerializeField, Min(0f)] private float positionSharpness = 28f;
    [SerializeField, Min(0f)] private float rotationSharpness = 32f;
    [SerializeField] private float movementTilt = 120f;
    [SerializeField] private float tiltSharpness = 24f;

    [Header("Snap feedback")]
    [SerializeField] private float moveDuration = 0.12f;
    [SerializeField] private Ease moveEase = Ease.OutCubic;
    [SerializeField] private Vector3 snapPunchScale = new(0.06f, -0.025f, 0.06f);
    [SerializeField] private float snapPunchDuration = 0.14f;

    private Tween moveTween;
    private Tween feedbackTween;
    private Vector3 restingVisualScale;
    private Quaternion restingVisualRotation;
    private bool isHovered;
    private bool isDragging;

    public EvidenceCardDefinition Definition { get; private set; }
    public EvidenceCardSlot CurrentSlot { get; private set; }

    private void Awake()
    {
        if (visual == null)
            visual = transform;

        restingVisualScale = visual.localScale;
        restingVisualRotation = visual.localRotation;
        SetOutline(false);
    }

    public void Initialize(EvidenceCardDefinition definition, Vector3 position, Quaternion rotation, float revealDelay)
    {
        Definition = definition;
        transform.SetPositionAndRotation(position, rotation);
        gameObject.name = $"Card_{definition.Id}";

        if (cardText != null)
            cardText.text = definition.Text;

        feedbackTween?.Kill();
        visual.localScale = restingVisualScale * 0.72f;
        visual.localRotation = restingVisualRotation * Quaternion.Euler(0f, 0f, -4f);
        feedbackTween = DOTween.Sequence()
            .AppendInterval(revealDelay)
            .Append(visual.DOScale(restingVisualScale, 0.18f).SetEase(Ease.OutBack))
            .Join(visual.DOLocalRotateQuaternion(restingVisualRotation, 0.14f).SetEase(Ease.OutQuad))
            .SetUpdate(true);
    }

    public void SetHovered(bool hovered)
    {
        if (isDragging || isHovered == hovered)
            return;

        isHovered = hovered;
        SetOutline(hovered);
        feedbackTween?.Kill();

        Vector3 targetScale = hovered ? restingVisualScale * hoverScale : restingVisualScale;
        Quaternion targetRotation = hovered
            ? restingVisualRotation * Quaternion.Euler(hoverRotation)
            : restingVisualRotation;

        feedbackTween = DOTween.Sequence()
            .Join(visual.DOScale(targetScale, feedbackDuration))
            .Join(visual.DOLocalRotateQuaternion(targetRotation, feedbackDuration))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    public void BeginDrag()
    {
        moveTween?.Kill();
        feedbackTween?.Kill();
        CurrentSlot?.Remove(this);
        CurrentSlot = null;
        isDragging = true;
        isHovered = false;
        SetOutline(true);

        feedbackTween = DOTween.Sequence()
            .Join(visual.DOScale(restingVisualScale * dragScale, feedbackDuration))
            .Join(visual.DOLocalRotateQuaternion(restingVisualRotation, feedbackDuration))
            .SetEase(Ease.OutBack)
            .SetUpdate(true);

        SoundManager.Instance?.Play(SFX.CardPickup);
    }

    public void SetDragPosition(Vector3 position, Quaternion rotation)
    {
        float deltaTime = Time.unscaledDeltaTime;
        float positionBlend = DampFactor(positionSharpness, deltaTime);
        float rotationBlend = DampFactor(rotationSharpness, deltaTime);
        Vector3 previousPosition = transform.position;

        transform.position = Vector3.Lerp(transform.position, position, positionBlend);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationBlend);

        Vector3 frameMovement = transform.position - previousPosition;
        Vector3 localMovement = transform.InverseTransformDirection(frameMovement);
        Quaternion targetTilt = restingVisualRotation * Quaternion.Euler(
            Mathf.Clamp(localMovement.z * movementTilt, -7f, 7f),
            0f,
            Mathf.Clamp(-localMovement.x * movementTilt, -7f, 7f));
        float blend = DampFactor(tiltSharpness, deltaTime);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, targetTilt, blend);
    }

    private static float DampFactor(float sharpness, float deltaTime)
    {
        return sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * deltaTime);
    }

    public void SnapTo(EvidenceCardSlot slot, Transform anchor)
    {
        CurrentSlot = slot;
        isDragging = false;
        SetOutline(false);
        AnimateTo(anchor.position, anchor.rotation, true);
        slot.PlaySnapFeedback();
        SoundManager.Instance?.Play(SFX.CardPlace);
    }

    public void ReleaseOnTable()
    {
        isDragging = false;
        SetOutline(false);
        SettleVisual();
        SoundManager.Instance?.Play(SFX.CardPlace);
    }

    private void AnimateTo(Vector3 position, Quaternion rotation, bool punchOnComplete)
    {
        moveTween?.Kill();
        moveTween = DOTween.Sequence()
            .Join(transform.DOMove(position, moveDuration))
            .Join(transform.DORotateQuaternion(rotation, moveDuration))
            .SetEase(moveEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                feedbackTween?.Kill();
                Sequence settleSequence = DOTween.Sequence()
                    .Join(visual.DOScale(restingVisualScale, feedbackDuration))
                    .Join(visual.DOLocalRotateQuaternion(restingVisualRotation, feedbackDuration))
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);

                if (punchOnComplete)
                    settleSequence.Append(visual.DOPunchScale(snapPunchScale, snapPunchDuration, 5, 0.45f));

                feedbackTween = settleSequence;
            });
    }

    private void SettleVisual()
    {
        feedbackTween?.Kill();
        feedbackTween = DOTween.Sequence()
            .Join(visual.DOScale(restingVisualScale, feedbackDuration))
            .Join(visual.DOLocalRotateQuaternion(restingVisualRotation, feedbackDuration))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void SetOutline(bool visible)
    {
        foreach (Behaviour outline in outlineComponents)
        {
            if (outline != null)
                outline.enabled = visible;
        }
    }

    private void OnDisable()
    {
        moveTween?.Kill();
        feedbackTween?.Kill();
        isDragging = false;
        isHovered = false;
        SetOutline(false);

        if (visual != null)
        {
            visual.localScale = restingVisualScale;
            visual.localRotation = restingVisualRotation;
        }
    }
}
