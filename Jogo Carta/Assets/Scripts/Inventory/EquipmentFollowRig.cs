using UnityEngine;

namespace ProjetoCarta.Inventory
{
    public sealed class EquipmentFollowRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform itemMount;

        [Header("Follow")]
        [SerializeField, Min(0.01f)] private float positionSharpness = 18f;
        [SerializeField, Min(0.01f)] private float rotationSharpness = 22f;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool snapOnEnable = true;

        public Transform ItemMount => itemMount != null ? itemMount : transform;

        private void OnEnable()
        {
            if (snapOnEnable)
                SnapToTarget();
        }

        private void LateUpdate()
        {
            if (followTarget == null)
                return;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float positionT = 1f - Mathf.Exp(-positionSharpness * deltaTime);
            float rotationT = 1f - Mathf.Exp(-rotationSharpness * deltaTime);

            transform.position = Vector3.Lerp(transform.position, followTarget.position, positionT);
            transform.rotation = Quaternion.Slerp(transform.rotation, followTarget.rotation, rotationT);
        }

        [ContextMenu("Snap To Target")]
        public void SnapToTarget()
        {
            if (followTarget == null)
                return;

            transform.SetPositionAndRotation(followTarget.position, followTarget.rotation);
        }
    }
}
