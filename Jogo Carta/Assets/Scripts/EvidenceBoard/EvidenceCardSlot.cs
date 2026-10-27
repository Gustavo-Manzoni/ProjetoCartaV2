using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class EvidenceCardSlot : MonoBehaviour
{
    [SerializeField] private Transform cardAnchor;

    [Header("Feedback")]
    [SerializeField] private Transform visual;
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float hoverDuration = 0.08f;
    [SerializeField] private float snapPunch = 0.08f;
    [SerializeField] private float snapPunchDuration = 0.14f;

    private Vector3 restingScale;
    private Tween feedbackTween;

    public EvidenceCardView CurrentCard { get; private set; }
    public bool IsEmpty => CurrentCard == null;

    private void Awake()
    {
        if (cardAnchor == null)
            cardAnchor = transform;
        if (visual == null)
            visual = transform;

        restingScale = visual.localScale;
    }

    public bool TryPlace(EvidenceCardView card)
    {
        if (card == null || (!IsEmpty && CurrentCard != card))
            return false;

        CurrentCard = card;
        SetHovered(false);
        card.SnapTo(this, cardAnchor);
        return true;
    }

    public void Remove(EvidenceCardView card)
    {
        if (CurrentCard == card)
            CurrentCard = null;
    }

    public void SetHovered(bool hovered)
    {
        feedbackTween?.Kill();
        Vector3 target = hovered && IsEmpty ? restingScale * hoverScale : restingScale;
        feedbackTween = visual.DOScale(target, hoverDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    public void PlaySnapFeedback()
    {
        feedbackTween?.Kill(true);
        feedbackTween = visual.DOPunchScale(Vector3.one * snapPunch, snapPunchDuration, 5, 0.45f)
            .SetUpdate(true);
    }

    private void OnDisable()
    {
        feedbackTween?.Kill();
        if (visual != null)
            visual.localScale = restingScale;
    }
}
