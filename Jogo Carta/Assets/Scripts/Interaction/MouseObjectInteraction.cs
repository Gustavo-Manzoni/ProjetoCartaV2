using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class MouseObjectInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera renderingCamera;
    [SerializeField] private TableCameraMouseSwitcher cameraSwitcher;
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("Raycast")]
    [SerializeField] private LayerMask interactableLayers = ~0;
    [SerializeField] private float maxDistance = 100f;

    [Header("Click fade")]
    [SerializeField] private float pressLeadIn = 0.04f;
    [SerializeField] private float fadeInDuration = 0.16f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private Ease fadeEase = Ease.InOutQuad;

    private HoverableObject hoveredObject;
    private Tween fadeTween;
    private bool isFading;

    private void Awake()
    {
        if (renderingCamera == null)
            renderingCamera = Camera.main;

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }
    }

    private void Update()
    {
        if (Mouse.current == null || renderingCamera == null)
            return;

        if (!Application.isFocused)
        {
            ClearHover();
            return;
        }

        if (cameraSwitcher != null && !cameraSwitcher.IsLookingForward)
        {
            ClearHover();
            return;
        }

        UpdateHoveredObject();

        if (!isFading && hoveredObject != null && Mouse.current.leftButton.wasPressedThisFrame)
            ClickHoveredObject();
    }

    private void UpdateHoveredObject()
    {
        Ray ray = renderingCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        HoverableObject newHoveredObject = null;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableLayers, QueryTriggerInteraction.Collide))
            newHoveredObject = hit.collider.GetComponentInParent<HoverableObject>();

        if (newHoveredObject == hoveredObject)
            return;

        ClearHover();
        hoveredObject = newHoveredObject;
        hoveredObject?.SetHovered(true);
    }

    private void ClickHoveredObject()
    {
        HoverableObject clickedObject = hoveredObject;
        clickedObject.PlayPressedFeedback();
        ClearHover();

        if (fadeCanvasGroup == null)
        {
            clickedObject.InvokeClick();
            return;
        }

        isFading = true;
        cameraSwitcher?.SetSwitchingBlocked(true);
        fadeCanvasGroup.blocksRaycasts = true;
        fadeTween?.Kill();

        fadeTween = DOTween.Sequence()
            .AppendInterval(pressLeadIn)
            .Append(fadeCanvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeEase))
            .AppendCallback(() =>
            {
                if (clickedObject != null)
                    clickedObject.InvokeClick();
            })
            .Append(fadeCanvasGroup.DOFade(0f, fadeOutDuration).SetEase(fadeEase))
            .SetUpdate(true)
            .OnKill(ResetFadeState);
    }

    private void ClearHover()
    {
        if (hoveredObject == null)
            return;

        hoveredObject.SetHovered(false);
        hoveredObject = null;
    }

    private void OnDisable()
    {
        ClearHover();
        fadeTween?.Kill();
        isFading = false;

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private void ResetFadeState()
    {
        isFading = false;
        fadeTween = null;
        cameraSwitcher?.SetSwitchingBlocked(false);

        if (fadeCanvasGroup != null)
            fadeCanvasGroup.blocksRaycasts = false;
    }
}
