using UnityEngine;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// Volume de mira amplo (collider trigger) que cobre um objeto inteiro, para o jogador nao precisar acertar um
    /// ponto minusculo: o clique vai para o <see cref="RaycastInteractable"/> do pai. Pecas interagiveis mais
    /// especificas dentro do volume (uma placa, um gancho, o item que subiu no balde) continuam tendo prioridade:
    /// o <see cref="PlayerRaycastInteractor"/> olha atras do volume antes de entregar o clique a ele.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class InteractionVolume : MonoBehaviour
    {
        private Collider volume;

        public Collider Volume => volume != null ? volume : volume = GetComponent<Collider>();

        private void Reset()
        {
            Collider own = GetComponent<Collider>();
            if (own != null)
                own.isTrigger = true;
        }

        private void OnValidate()
        {
            Collider own = GetComponent<Collider>();
            if (own != null && !own.isTrigger)
                own.isTrigger = true;
        }

        /// <summary>O ponto esta dentro do volume (com uma folga pequena para superficies na borda).</summary>
        public bool Contains(Vector3 point)
        {
            Bounds bounds = Volume.bounds;
            bounds.Expand(0.02f);
            return bounds.Contains(point);
        }
    }
}
