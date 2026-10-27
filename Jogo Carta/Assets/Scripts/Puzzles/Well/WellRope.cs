using UnityEngine;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Corda do poco: um cilindro esticado entre o eixo da manivela e o gancho do balde.
    /// Atualiza todo frame, entao a corda desenrola e recolhe junto com o balde.
    /// </summary>
    [ExecuteAlways]
    public sealed class WellRope : MonoBehaviour
    {
        [SerializeField] private Transform top;
        [SerializeField] private Transform bottom;
        [Tooltip("Cilindro padrao do Unity (altura 2, pivo no centro).")]
        [SerializeField] private Transform ropeVisual;
        [SerializeField, Min(0.001f)] private float thickness = 0.022f;

        private void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (top == null || bottom == null || ropeVisual == null)
                return;

            Vector3 a = top.position;
            Vector3 b = bottom.position;
            Vector3 delta = a - b;
            float length = delta.magnitude;
            if (length < 0.0001f)
                return;

            ropeVisual.position = (a + b) * 0.5f;
            ropeVisual.rotation = Quaternion.FromToRotation(Vector3.up, delta / length);

            // Compensa a escala do pai para a espessura ficar em metros.
            Vector3 parentScale = ropeVisual.parent != null ? ropeVisual.parent.lossyScale : Vector3.one;
            float sx = parentScale.x != 0f ? thickness / Mathf.Abs(parentScale.x) : thickness;
            float sy = parentScale.y != 0f ? length * 0.5f / Mathf.Abs(parentScale.y) : length * 0.5f;
            float sz = parentScale.z != 0f ? thickness / Mathf.Abs(parentScale.z) : thickness;
            ropeVisual.localScale = new Vector3(sx, sy, sz);
        }
    }
}
