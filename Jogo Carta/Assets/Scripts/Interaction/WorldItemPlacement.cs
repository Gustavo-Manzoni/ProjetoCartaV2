using System;
using System.Collections;
using ProjetoCarta.Core;
using UnityEngine;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// Leva um objeto de item (vindo da mao do jogador) ate um ponto do mundo com uma interpolacao suave.
    /// Usado por encaixes, poco, envelope etc. para que nada apareca ou suma sem explicacao visual.
    /// </summary>
    public static class WorldItemPlacement
    {
        /// <summary>Desliga colisao, fisica e interacoes do objeto e o pendura em <paramref name="parent"/>.</summary>
        public static void PrepareForWorld(GameObject itemObject, Transform parent)
        {
            foreach (RaycastInteractable interactable in itemObject.GetComponentsInChildren<RaycastInteractable>(true))
                interactable.SetAvailable(false);

            foreach (Collider itemCollider in itemObject.GetComponentsInChildren<Collider>(true))
                itemCollider.enabled = false;

            foreach (Rigidbody itemRigidbody in itemObject.GetComponentsInChildren<Rigidbody>(true))
            {
                itemRigidbody.linearVelocity = Vector3.zero;
                itemRigidbody.angularVelocity = Vector3.zero;
                itemRigidbody.isKinematic = true;
                itemRigidbody.detectCollisions = false;
            }

            itemObject.transform.SetParent(parent, true);
            itemObject.SetActive(true);
        }

        /// <summary>
        /// Escala local (uniforme) para que o maior lado do objeto meca <paramref name="worldSize"/> metros
        /// depois de pendurado em <paramref name="parent"/>. Usa as dimensoes proprias do modelo, entao nao
        /// depende da rotacao ou escala em que o objeto esta agora (por exemplo, na mao).
        /// </summary>
        public static Vector3 ScaleToFit(GameObject item, float worldSize, Quaternion targetLocalRotation, Transform parent)
        {
            Transform root = item.transform;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            bool hasBounds = false;
            Bounds local = default;

            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                Bounds meshBounds = filter.sharedMesh.bounds;
                Vector3 c = meshBounds.center, e = meshBounds.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
                    if (!hasBounds)
                    {
                        local = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        local.Encapsulate(point);
                    }
                }
            }

            if (!hasBounds)
                return root.localScale;

            // Mantem as proporcoes atuais do objeto (alguns modelos dependem de escala nao uniforme).
            Vector3 ratio = new(Mathf.Abs(root.localScale.x), Mathf.Abs(root.localScale.y), Mathf.Abs(root.localScale.z));
            Vector3 dims = Vector3.Scale(local.size, ratio);
            float largest = Mathf.Max(dims.x, Mathf.Max(dims.y, dims.z));
            float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
            if (largest <= 0.000001f || parentScale <= 0.000001f)
                return root.localScale;

            return root.localScale * (worldSize / (largest * parentScale));
        }

        /// <summary>
        /// Roda no <see cref="CoroutineHost"/> da cena, e nao no objeto que chamou: se o momento for desativado
        /// por uma piscada no meio do movimento, o item ainda chega ao destino e o <paramref name="onComplete"/>
        /// (ex.: avisar o painel do ritual que o encaixe foi preenchido) nunca se perde.
        /// </summary>
        public static Coroutine MoveTo(
            Transform item,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            float duration,
            AnimationCurve curve,
            Action onComplete = null)
        {
            if (item == null)
                return null;

            return CoroutineHost.Run(MoveRoutine(item, localPosition, localRotation, localScale, duration, curve, onComplete));
        }

        private static IEnumerator MoveRoutine(
            Transform item,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            float duration,
            AnimationCurve curve,
            Action onComplete)
        {
            Vector3 startPosition = item.localPosition;
            Quaternion startRotation = item.localRotation;
            Vector3 startScale = item.localScale;

            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (item == null)
                        yield break;

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float amount = curve != null && curve.length > 0 ? curve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

                    // Arco leve para cima: o objeto "e colocado", nao desliza em linha reta.
                    Vector3 arc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.06f);
                    if (item.parent != null)
                        arc = item.parent.InverseTransformVector(arc);

                    item.localPosition = Vector3.LerpUnclamped(startPosition, localPosition, amount) + arc;
                    item.localRotation = Quaternion.SlerpUnclamped(startRotation, localRotation, amount);
                    item.localScale = Vector3.LerpUnclamped(startScale, localScale, amount);
                    yield return null;
                }
            }

            if (item == null)
                yield break;

            item.localPosition = localPosition;
            item.localRotation = localRotation;
            item.localScale = localScale;
            onComplete?.Invoke();
        }
    }
}
