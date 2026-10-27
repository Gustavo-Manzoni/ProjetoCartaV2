using UnityEngine;

namespace ProjetoCarta.Blink
{
    /// <summary>
    /// Garante que o jogador nao fique dentro de moveis/paredes depois de trocar de momento.
    /// Se a posicao atual estiver livre na sala de destino, nada muda; senao procura, em aneis
    /// crescentes, a posicao livre e caminhavel mais proxima dentro dos limites da sala e fora das areas
    /// bloqueadas do momento (<see cref="BlinkBlockedArea"/>: espaco livre, mas do lado errado de uma parede).
    /// </summary>
    public static class PlayerSafePlacement
    {
        private const float RingStep = 0.2f;
        private const float MaxRadius = 6f;
        private const int SamplesPerRing = 24;
        private const float SkinShrink = 0.03f;
        private const float GroundProbe = 1.2f;

        private static readonly Collider[] OverlapBuffer = new Collider[32];

        /// <summary>Move o jogador para um ponto seguro, se necessario. Retorna true se moveu.</summary>
        public static bool Resolve(CharacterController controller, Bounds walkableArea)
        {
            return Resolve(controller, walkableArea, null);
        }

        /// <summary>Igual a <see cref="Resolve(CharacterController, Bounds)"/>, sem deixar o jogador em blockedAreas.</summary>
        public static bool Resolve(CharacterController controller, Bounds walkableArea, Bounds[] blockedAreas)
        {
            if (controller == null)
                return false;

            Physics.SyncTransforms();
            Transform player = controller.transform;
            Vector3 origin = player.position;

            if (IsFree(controller, origin) && Inside(walkableArea, blockedAreas, origin, controller.radius))
                return false;

            for (float radius = RingStep; radius <= MaxRadius; radius += RingStep)
            {
                int samples = Mathf.Max(8, Mathf.RoundToInt(SamplesPerRing * Mathf.Min(1f, radius)));
                float bestDistance = float.MaxValue;
                Vector3 best = origin;
                bool found = false;

                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (!Inside(walkableArea, blockedAreas, candidate, controller.radius))
                        continue;

                    if (!TryGround(controller, candidate, out Vector3 grounded) || !IsFree(controller, grounded))
                        continue;

                    float distance = (grounded - origin).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = grounded;
                        found = true;
                    }
                }

                if (!found)
                    continue;

                Teleport(controller, best);
                return true;
            }

            return false;
        }

        private static void Teleport(CharacterController controller, Vector3 position)
        {
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            if (controller.TryGetComponent(out FirstPersonController player))
                player.ResetMotion();
            Physics.SyncTransforms();
        }

        private static bool Inside(Bounds area, Bounds[] blockedAreas, Vector3 position, float radius)
        {
            float margin = radius + 0.05f;
            if (blockedAreas != null)
            {
                // O corpo inteiro precisa ficar fora da area bloqueada (nada de meio corpo atras da parede).
                foreach (Bounds blocked in blockedAreas)
                {
                    if (position.x > blocked.min.x - margin && position.x < blocked.max.x + margin &&
                        position.z > blocked.min.z - margin && position.z < blocked.max.z + margin)
                        return false;
                }
            }

            if (area.size == Vector3.zero)
                return true;

            return position.x > area.min.x + margin && position.x < area.max.x - margin &&
                   position.z > area.min.z + margin && position.z < area.max.z - margin;
        }

        /// <summary>Ajusta a altura para os pes ficarem no chao (evita surgir no ar ou dentro do piso).</summary>
        private static bool TryGround(CharacterController controller, Vector3 candidate, out Vector3 grounded)
        {
            grounded = candidate;
            float halfHeight = Mathf.Max(controller.height * 0.5f, controller.radius);
            Vector3 center = candidate + controller.center;
            Vector3 rayStart = center + Vector3.up * 0.2f;
            float feetOffset = controller.center.y - halfHeight;

            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, halfHeight + 0.2f + GroundProbe,
                ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity;
            bool hitGround = false;
            foreach (RaycastHit hit in hits)
            {
                if (IsPlayerCollider(controller, hit.collider) || hit.normal.y < 0.6f)
                    continue;

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    hitGround = true;
                }
            }

            if (!hitGround)
                return false;

            // Nao sobe em cima de moveis: o chao encontrado precisa estar perto da altura atual dos pes.
            float currentFeet = candidate.y + feetOffset;
            if (bestY > currentFeet + controller.stepOffset + 0.05f)
                return false;

            grounded.y = bestY - feetOffset + controller.skinWidth + 0.02f;
            return true;
        }

        private static bool IsFree(CharacterController controller, Vector3 position)
        {
            float radius = Mathf.Max(0.05f, controller.radius - SkinShrink);
            float halfHeight = Mathf.Max(controller.height * 0.5f, controller.radius);
            Vector3 center = position + controller.center;
            // Levanta um pouco a base para nao contar o proprio piso como obstaculo.
            Vector3 bottom = center + Vector3.down * (halfHeight - controller.radius - 0.08f);
            Vector3 top = center + Vector3.up * (halfHeight - controller.radius);

            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsPlayerCollider(controller, OverlapBuffer[i]))
                    return false;
            }

            return true;
        }

        private static bool IsPlayerCollider(CharacterController controller, Collider other)
        {
            return other == controller || other.transform.IsChildOf(controller.transform) ||
                   other.attachedRigidbody != null && other.attachedRigidbody.transform.IsChildOf(controller.transform);
        }
    }
}
