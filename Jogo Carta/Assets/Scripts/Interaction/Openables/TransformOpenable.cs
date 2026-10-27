using UnityEngine;
using UnityEngine.Serialization;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// Abre movendo/girando um transform a partir da pose fechada (gavetas, tampas, portinholas).
    /// </summary>
    public class TransformOpenable : OpenableInteractable
    {
        [Header("Movimento")]
        [Tooltip("Transform que se move. Usa este objeto quando vazio.")]
        [FormerlySerializedAs("drawerTransform")]
        [SerializeField] private Transform movingTransform;
        [Tooltip("Deslocamento local aplicado a pose fechada quando abre.")]
        [SerializeField] private Vector3 openLocalOffset = new(0f, 0f, -0.45f);
        [Tooltip("Rotacao local (graus) somada a pose fechada quando abre. Use para dobradicas.")]
        [SerializeField] private Vector3 openLocalEulerOffset;
        [SerializeField, Min(0f)] private float movementDuration = 0.35f;
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Vector3 closedLocalPosition;
        private Quaternion closedLocalRotation;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private bool poseCached;

        protected Transform MovingTransform => movingTransform != null ? movingTransform : transform;

        protected override void Awake()
        {
            CachePose();
            base.Awake();
        }

        private void CachePose()
        {
            if (poseCached)
                return;

            closedLocalPosition = MovingTransform.localPosition;
            closedLocalRotation = MovingTransform.localRotation;
            poseCached = true;
        }

        protected override void ApplyInitialState(bool open)
        {
            CachePose();
            MovingTransform.localPosition = TargetPosition(open);
            MovingTransform.localRotation = TargetRotation(open);
        }

        protected override float BeginMotion(bool open)
        {
            startPosition = MovingTransform.localPosition;
            startRotation = MovingTransform.localRotation;
            return movementDuration;
        }

        protected override void UpdateMotion(bool open, float normalizedTime)
        {
            float amount = movementCurve != null && movementCurve.length > 0
                ? movementCurve.Evaluate(normalizedTime)
                : normalizedTime;

            MovingTransform.localPosition = Vector3.LerpUnclamped(startPosition, TargetPosition(open), amount);
            MovingTransform.localRotation = Quaternion.SlerpUnclamped(startRotation, TargetRotation(open), amount);
        }

        protected override void OnLockedRattle()
        {
            if (isActiveAndEnabled && !IsBusy)
                StartCoroutine(Rattle());
        }

        private System.Collections.IEnumerator Rattle()
        {
            Transform target = MovingTransform;
            Vector3 basePosition = target.localPosition;
            float elapsed = 0f;
            while (elapsed < 0.22f && !IsBusy)
            {
                elapsed += Time.deltaTime;
                float strength = (1f - elapsed / 0.22f) * 0.004f;
                Vector3 jitter = new(Mathf.Sin(elapsed * 90f), 0f, Mathf.Cos(elapsed * 77f));
                target.localPosition = basePosition + target.parent.InverseTransformVector(jitter * strength);
                yield return null;
            }

            if (!IsBusy)
                target.localPosition = basePosition;
        }

        private Vector3 TargetPosition(bool open)
        {
            return open ? closedLocalPosition + openLocalOffset : closedLocalPosition;
        }

        private Quaternion TargetRotation(bool open)
        {
            return open ? closedLocalRotation * Quaternion.Euler(openLocalEulerOffset) : closedLocalRotation;
        }
    }
}
