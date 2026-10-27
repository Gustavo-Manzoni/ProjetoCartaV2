using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Botao com toque curto e toque longo (ex.: Apagar: curto apaga o ultimo digito, longo limpa tudo).
    /// Fica no mesmo objeto de um <see cref="Button"/> cujo onClick nao deve ser usado.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Min(0.1f)] private float holdDuration = 0.6f;
        [SerializeField] private UnityEvent onShortPress = new();
        [SerializeField] private UnityEvent onLongPress = new();

        private Button button;
        private bool pressed;
        private bool longFired;
        private float pressTime;

        public UnityEvent OnShortPress => onShortPress;
        public UnityEvent OnLongPress => onLongPress;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void Update()
        {
            if (!pressed || longFired)
                return;

            if (Time.unscaledTime - pressTime >= holdDuration)
            {
                longFired = true;
                onLongPress?.Invoke();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !button.IsInteractable())
                return;

            pressed = true;
            longFired = false;
            pressTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed)
                return;

            pressed = false;
            if (!longFired && button.IsInteractable())
                onShortPress?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pressed = false;
        }

        private void OnDisable()
        {
            pressed = false;
        }
    }
}
