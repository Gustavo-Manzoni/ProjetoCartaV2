using UnityEngine;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Fecha as laterais da mesa do arquivo com colisores. A malha animada da mesa nao traz colisor, entao os
    /// paineis das duas pontas (no eixo mais comprido) sao calculados a partir dos renderers da mesa, na pose de repouso.
    /// </summary>
    public sealed class DeskSolidSides : MonoBehaviour
    {
        [SerializeField] private Transform desk;
        [SerializeField, Min(0.02f)] private float thickness = 0.12f;

        private bool built;

        private void OnEnable()
        {
            if (built || desk == null)
                return;

            Renderer[] renderers = desk.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            built = true;
            bool longX = b.size.x >= b.size.z;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject panel = new(side < 0 ? "Lateral_A" : "Lateral_B");
                panel.transform.SetParent(transform.parent, false);
                panel.transform.rotation = Quaternion.identity;
                BoxCollider box = panel.AddComponent<BoxCollider>();
                Vector3 center = b.center;
                Vector3 size = b.size;
                if (longX)
                {
                    center.x += side * (b.extents.x - thickness * 0.5f);
                    size.x = thickness;
                }
                else
                {
                    center.z += side * (b.extents.z - thickness * 0.5f);
                    size.z = thickness;
                }

                panel.transform.position = center;
                box.size = Vector3.Scale(size, new Vector3(1f / panel.transform.lossyScale.x, 1f / panel.transform.lossyScale.y, 1f / panel.transform.lossyScale.z));
            }
        }
    }
}
