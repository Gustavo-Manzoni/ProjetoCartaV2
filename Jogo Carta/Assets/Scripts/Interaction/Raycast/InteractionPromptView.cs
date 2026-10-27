using TMPro;
using UnityEngine;

namespace ProjetoCarta.Interaction
{
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerRaycastInteractor interactor;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text promptText;

        private void Awake()
        {
            SetPrompt(string.Empty, false);
        }

        private void OnEnable()
        {
            if (interactor != null)
                interactor.PromptChanged += SetPrompt;
        }

        private void OnDisable()
        {
            if (interactor != null)
                interactor.PromptChanged -= SetPrompt;
        }

        private void SetPrompt(string message, bool visible)
        {
            if (promptText != null)
                promptText.text = message;

            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
