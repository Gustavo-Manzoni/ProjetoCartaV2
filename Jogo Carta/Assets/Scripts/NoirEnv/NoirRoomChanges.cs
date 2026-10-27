using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjetoCarta.NoirEnv
{
    /// <summary>
    /// Ao chegar no momento (inicio do jogo ou depois da piscada), as lampadas estao apagadas e acordam
    /// engasgando, como se a energia voltasse aos poucos.
    /// </summary>
    public sealed class NoirLightsWake : MonoBehaviour
    {
        private void OnEnable() => StartCoroutine(Wake());

        private void OnDisable()
        {
            StopAllCoroutines();
            NoirLightFlicker.WakeScale = 1f;
        }

        private IEnumerator Wake()
        {
            // As palpebras da piscada ainda estao abrindo: segura o escuro um instante.
            NoirLightFlicker.WakeScale = 0.03f;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) yield return null;

            float elapsed = 0f;
            const float duration = 1.1f;
            while (elapsed < duration)
            {
                float k = elapsed / duration;
                // Cada vez mais tempo aceso entre as falhas.
                NoirLightFlicker.WakeScale = Random.value < 0.35f + k * 0.6f ? Mathf.Lerp(0.5f, 1f, k) : Random.Range(0.02f, 0.15f);
                float step = Random.Range(0.03f, 0.08f);
                elapsed += step;
                for (float t = 0f; t < step; t += Time.deltaTime) yield return null;
            }

            NoirLightFlicker.WakeScale = 1f;
        }
    }

    /// <summary>
    /// A sala nunca esta exatamente como o jogador deixou. A cada volta ao momento algo mudou: o relogio um minuto
    /// mais perto da meia-noite, mais um dia riscado na parede, um quadro torto, pegadas molhadas vindo de uma
    /// parede ate onde o jogador apareceu, um retrato virado para a parede.
    /// </summary>
    public sealed class NoirRevisit : MonoBehaviour
    {
        public List<Transform> pictures = new();
        public List<Transform> clocks = new();
        public Renderer tally;
        public int tallyDays = 97;
        public float floorY;
        public Vector3 roomCenter;
        public Vector2 halfSize = new(5.5f, 5.5f);

        private const float Stride = 0.42f;
        private int visits;
        private bool footprintsDone, portraitDone;

        private void Awake()
        {
            // O relogio montado pelo cenario aparece espelhado (1h05 em vez de 11h55): espelha os ponteiros.
            foreach (Transform clock in clocks)
                foreach (string hand in new[] { "Ponteiro_Min", "Ponteiro_Hora" })
                {
                    Transform h = clock.Find(hand);
                    if (h == null) continue;
                    Vector3 p = h.localPosition;
                    h.localPosition = new Vector3(-p.x, p.y, p.z);
                    Vector3 e = h.localEulerAngles;
                    h.localEulerAngles = new Vector3(e.x, e.y, -e.z);
                }
        }

        private void OnEnable()
        {
            visits++;
            if (visits < 2) return;

            // O relogio sempre anda um minuto; o resto e sorteado.
            foreach (Transform clock in clocks) AdvanceClock(clock);

            if (tally != null)
            {
                tallyDays++;
                Texture2D tex = SinisterTex.Tally(7, tallyDays);
                tally.sharedMaterial = SinisterTex.Mat(tex.name, tex, Color.white, SinisterTex.Blend.Multiply);
            }

            if (pictures.Count > 0 && Random.value < 0.65f)
            {
                Transform p = pictures[Random.Range(0, pictures.Count)];
                p.Rotate(Vector3.forward, Random.Range(5f, 13f) * (Random.value < 0.5f ? -1f : 1f), Space.Self);
            }

            if (!footprintsDone && visits >= 3 && Random.value < 0.6f)
                StartCoroutine(FootprintsAfterBlink());
            else if (!portraitDone && visits >= 4 && Random.value < 0.5f)
                TurnPortrait();
        }

        private static void AdvanceClock(Transform clock)
        {
            Vector3 center = clock.TransformPoint(new Vector3(0f, 0f, 0.1f));
            Vector3 axis = clock.forward;
            Transform min = clock.Find("Ponteiro_Min"), hour = clock.Find("Ponteiro_Hora");
            if (min != null) min.RotateAround(center, axis, 6f);
            if (hour != null) hour.RotateAround(center, axis, 0.5f);
        }

        private void TurnPortrait()
        {
            Transform p = null;
            foreach (Transform t in pictures)
                if (t.name.Contains("Retrato")) p = t;
            if (p == null) return;
            portraitDone = true;
            // De costas: a moldura fica para a sala e a pintura contra a parede.
            p.position += p.forward * 0.07f;
            p.Rotate(Vector3.up, 180f, Space.Self);
        }

        private IEnumerator FootprintsAfterBlink()
        {
            // Espera a piscada reposicionar o jogador, para a trilha terminar onde ele esta.
            yield return null;
            yield return null;
            Camera cam = Camera.main;
            if (cam == null) yield break;

            Vector3 end = cam.transform.position;
            end.y = floorY;
            Vector3 back = -cam.transform.forward;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back;
            // A trilha vem de tras do jogador: comeca encostada numa parede e para a um passo dele.
            float reach = WallDistance(end, back) - 0.35f;
            if (reach < 2.5f) yield break;
            footprintsDone = true;

            Vector3 start = end + back * reach;
            end += back * 0.8f;
            Vector3 walk = (end - start).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, walk);
            Material mat = SinisterTex.Mat("footprint", SinisterTex.Footprint(), Color.white, SinisterTex.Blend.Multiply);
            // Por cima da marca escura, o brilho de agua: e o que aparece no assoalho escuro.
            Material wet = SinisterTex.Mat("footprint_wet", SinisterTex.FootprintWet(), new Color(0.5f, 0.58f, 0.7f, 0.16f), SinisterTex.Blend.Additive);
            Transform root = new GameObject("Pegadas").transform;
            root.SetParent(transform, false);
            int steps = Mathf.FloorToInt(Vector3.Distance(start, end) / Stride) + 1;
            for (int i = 0; i < steps; i++)
            {
                Vector3 p = start + walk * (i * Stride) + side * (i % 2 == 0 ? 0.12f : -0.12f);
                // Acima do tapete do escritorio (0,018 m).
                p.y = floorY + 0.026f + i * 0.0002f;
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "Pegada";
                q.transform.SetParent(root, true);
                q.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.down, walk));
                q.transform.localScale = new Vector3(0.13f, 0.3f, 1f);
                Renderer r = q.GetComponent<Renderer>();
                r.sharedMaterials = new[] { mat, wet };
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private float WallDistance(Vector3 from, Vector3 dir)
        {
            float best = 50f;
            Vector3 local = from - roomCenter;
            if (Mathf.Abs(dir.x) > 0.001f) best = Mathf.Min(best, ((dir.x > 0f ? halfSize.x : -halfSize.x) - local.x) / dir.x);
            if (Mathf.Abs(dir.z) > 0.001f) best = Mathf.Min(best, ((dir.z > 0f ? halfSize.y : -halfSize.y) - local.z) / dir.z);
            return Mathf.Max(0f, best);
        }
    }
}
