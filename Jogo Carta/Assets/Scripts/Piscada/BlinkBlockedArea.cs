using UnityEngine;

namespace ProjetoCarta.Blink
{
    /// <summary>
    /// Parte da sala de um momento onde o jogador nunca pode surgir depois da piscada, mesmo que o espaco esteja
    /// livre (ex.: a sala das testemunhas atras do vidro de observacao em P2, que tem chao mas nao e acessivel).
    /// Fica dentro do momento; o <see cref="BlinkSceneController"/> passa as areas do momento de destino para o
    /// <see cref="PlayerSafePlacement"/>, que leva o jogador ao ponto livre mais proximo fora delas.
    /// A caixa e local a este transform e so o plano XZ conta.
    /// </summary>
    public sealed class BlinkBlockedArea : MonoBehaviour
    {
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size = new(2f, 3f, 12f);

        /// <summary>Caixa em coordenadas de mundo (alinhada aos eixos).</summary>
        public Bounds WorldBounds
        {
            get
            {
                Bounds local = new(center, size);
                Vector3 min = local.min, max = local.max;
                Bounds world = new(transform.TransformPoint(local.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new(i % 2 == 0 ? min.x : max.x, (i / 2) % 2 == 0 ? min.y : max.y, i / 4 == 0 ? min.z : max.z);
                    world.Encapsulate(transform.TransformPoint(corner));
                }

                return world;
            }
        }

        private void OnValidate()
        {
            size = Vector3.Max(size, Vector3.one * 0.1f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
            Bounds bounds = WorldBounds;
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
