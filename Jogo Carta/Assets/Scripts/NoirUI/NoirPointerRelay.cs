using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Repassa os eventos de ponteiro de um Graphic para quem construiu a UI por codigo (linhas de opcao, setas,
    /// itens do dropdown). Precisa de um Graphic com raycastTarget no mesmo objeto.
    /// </summary>
    public sealed class NoirPointerRelay : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        public event Action Entered;
        public event Action Exited;
        public event Action Clicked;
        public event Action<PointerEventData> Pressed;
        public event Action<PointerEventData> Dragged;
        public event Action<float> Scrolled;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Pressed?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Dragged?.Invoke(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Scrolled != null)
                Scrolled.Invoke(eventData.scrollDelta.y);
            else
                ExecuteEvents.ExecuteHierarchy(transform.parent != null ? transform.parent.gameObject : null, eventData,
                    ExecuteEvents.scrollHandler);
        }
    }
}
