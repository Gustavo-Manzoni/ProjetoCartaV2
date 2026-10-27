using System.Collections;
using System.Collections.Generic;
using ProjetoCarta.Blink;
using ProjetoCarta.Core;
using ProjetoCarta.NoirEnv;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Diretor de suspense visual da cena de gameplay (nao toca som). A tensao sobe devagar enquanto nada acontece
    /// e despenca depois de cada susto (ciclo de tensao e alivio). Com ela crescem a vinheta avermelhada, a cor
    /// drenada, o grao e a vinheta pulsando no ritmo do coracao de Tomas. De tempos em tempos dispara um evento:
    /// lampadas engasgando, queda de luz, a tela se fechando, surto da cadeira nas luzes (P2), uma silhueta que
    /// passa no canto do olho e outra que espera pelas costas e some quando e vista.
    /// Nada tem collider: nao interfere em puzzle, raycast nem no teste do fluxo.
    /// Fica quieto enquanto o jogador le documento, usa keypad, pisca, pausa ou no final.
    /// </summary>
    public sealed class NoirDread : MonoBehaviour
    {
        private enum Scare { Flicker, Blackout, Pressure, Surge, Passing, Figure }

        private static NoirDread instance;

        private const float DreadRise = 1f / 80f;   // tensao por segundo sem susto
        private const float DreadMax = 0.62f;
        private const float MinEventGap = 22f, MaxEventGap = 45f;
        private const float FigureCooldown = 120f;

        private BlinkSceneController blink;
        private CharacterController player;
        private Volume tensionVolume, scareVolume;
        private FilmGrain grain;

        private float dread = 0.12f, scare, master = 1f, masterTarget = 1f, masterSpeed = 1f;
        private float nextEvent, freeFor, heartTimer, lastFigureTime = -999f;
        private int lastMoment = -1;
        private Scare lastScare = Scare.Figure;
        private bool chairPowered = true, busy;

        // Luzes: as com NoirLightFlicker leem DreadScale; as outras sao escaladas aqui e restauradas no fim.
        private readonly List<Light> heldLights = new();
        private readonly List<float> heldBase = new();
        private readonly List<float> heldWritten = new();
        private float lightScale = 1f;
        private bool lightsHeld;

        // Silhueta.
        private GameObject figure;
        private float figureLife, figureSeen;
        private bool figureVanishing, figureScripted;

        public static void SetChairPowered(bool powered)
        {
            if (instance != null)
                instance.chairPowered = powered;
        }

        /// <summary>Desliga (0) ou religa (1) a camada de suspense, ex.: no final.</summary>
        public static void FadeAll(float target, float seconds)
        {
            if (instance == null)
                return;

            instance.masterTarget = Mathf.Clamp01(target);
            instance.masterSpeed = 1f / Mathf.Max(0.05f, seconds);
        }

        // ------------------------------------------------------------ montagem

        private void Awake()
        {
            instance = this;
            NoirLightFlicker.DreadScale = 1f;
            blink = FindFirstObjectByType<BlinkSceneController>(FindObjectsInactive.Include);
            FirstPersonController fpc = FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (fpc != null) player = fpc.GetComponent<CharacterController>();

            BuildVolumes();
            GameSettings.Changed += OnSettingsChanged;
            nextEvent = Random.Range(15f, 25f);
        }

        private void OnDestroy()
        {
            ReleaseLights();
            GameSettings.Changed -= OnSettingsChanged;
            if (tensionVolume != null) Destroy(tensionVolume.profile);
            if (scareVolume != null) Destroy(scareVolume.profile);
            if (instance == this)
                instance = null;
        }

        private void BuildVolumes()
        {
            // Tensao: vinheta avermelhada, cor drenada, grao e leve aberracao. Nao mexe na exposicao (opcao de brilho).
            VolumeProfile tp = ScriptableObject.CreateInstance<VolumeProfile>();
            Vignette tv = tp.Add<Vignette>(true);
            tv.intensity.Override(0.5f);
            tv.smoothness.Override(0.5f);
            tv.color.Override(new Color(0.07f, 0f, 0f));
            ColorAdjustments tc = tp.Add<ColorAdjustments>();
            tc.saturation.Override(-45f);
            tc.contrast.Override(18f);
            tp.Add<ChromaticAberration>().intensity.Override(0.2f);
            grain = tp.Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.response.Override(0.8f);
            ApplyGrain();
            tensionVolume = MakeVolume("Dread_Tensao", tp, 50f);

            // Susto: lente puxando, cor quase sumindo e aberracao forte por um instante.
            VolumeProfile sp = ScriptableObject.CreateInstance<VolumeProfile>();
            sp.Add<LensDistortion>().intensity.Override(-0.3f);
            sp.Add<ChromaticAberration>().intensity.Override(1f);
            ColorAdjustments sc = sp.Add<ColorAdjustments>();
            sc.saturation.Override(-85f);
            sc.contrast.Override(35f);
            Vignette sv = sp.Add<Vignette>();
            sv.intensity.Override(0.62f);
            sv.color.Override(new Color(0.1f, 0f, 0f));
            scareVolume = MakeVolume("Dread_Susto", sp, 60f);
        }

        private Volume MakeVolume(string name, VolumeProfile profile, float priority)
        {
            GameObject go = new(name);
            go.transform.SetParent(transform, false);
            Volume v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = priority;
            v.weight = 0f;
            v.profile = profile;
            return v;
        }

        private void OnSettingsChanged(string key)
        {
            if (key == GameSettings.FilmGrainKey)
                ApplyGrain();
        }

        private void ApplyGrain()
        {
            if (grain != null)
                grain.intensity.Override(0.45f * GameSettings.FilmGrain);
        }

        // ------------------------------------------------------------ ciclo

        private bool Calm => GameplayLocks.MovementLocked || (blink != null && blink.IsBlinking) || masterTarget < 1f;

        private void Update()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;
            master = Mathf.MoveTowards(master, masterTarget, masterSpeed * udt);

            int moment = blink != null ? blink.CurrentMoment : 0;
            if (moment != lastMoment)
            {
                // Piscar nao alivia: o momento novo ja chega um pouco mais pesado.
                if (lastMoment >= 0) dread = Mathf.Min(DreadMax, dread + 0.06f);
                lastMoment = moment;
                HideFigure();
            }

            bool calm = Calm;
            if (!PauseState.IsPaused)
            {
                dread = Mathf.Min(DreadMax, dread + DreadRise * dt);
                scare = Mathf.MoveTowards(scare, 0f, dt * 0.35f);
                freeFor = calm ? 0f : freeFor + dt;
                if (!calm && !busy && freeFor > 2.5f)
                {
                    nextEvent -= dt;
                    if (nextEvent <= 0f)
                        Trigger(moment);
                }

                UpdateFigure(dt, calm);
                UpdateHeart(dt, calm);
            }

            if (calm && lightsHeld && !figureVanishing)
                ReleaseLights();

            float tension = Mathf.Clamp01(Mathf.Max(dread, scare));
            tensionVolume.weight = Mathf.Lerp(tensionVolume.weight, (0.25f + 0.75f * tension) * master, 1f - Mathf.Exp(-3f * udt));
            scareVolume.weight = Mathf.Lerp(scareVolume.weight, Mathf.SmoothStep(0f, 1f, scare) * master, 1f - Mathf.Exp(-12f * udt));
        }

        private void LateUpdate()
        {
            if (!lightsHeld)
                return;

            NoirLightFlicker.DreadScale = lightScale;
            for (int i = heldLights.Count - 1; i >= 0; i--)
            {
                Light l = heldLights[i];
                // Outro sistema mexeu nessa luz (puzzle, final): ela deixa de ser nossa e fica como ele deixou.
                if (l == null || Mathf.Abs(l.intensity - heldWritten[i]) > 0.001f)
                {
                    heldLights.RemoveAt(i); heldBase.RemoveAt(i); heldWritten.RemoveAt(i);
                    continue;
                }

                l.intensity = heldBase[i] * lightScale;
                heldWritten[i] = l.intensity;
            }
        }

        /// <summary>Com a tensao alta, a vinheta bate no ritmo do coracao (dupla: tum-tum), cada vez mais rapido.</summary>
        private void UpdateHeart(float dt, bool calm)
        {
            float tension = Mathf.Max(dread, scare);
            if (calm || tension < 0.42f || master < 0.5f || !NoirAtmosphere.Exists)
                return;

            heartTimer -= dt;
            if (heartTimer > 0f)
                return;

            float f = Mathf.InverseLerp(0.42f, 1f, tension);
            StartCoroutine(HeartPulse(Mathf.Lerp(0.05f, 0.16f, f)));
            heartTimer = Mathf.Lerp(1.35f, 0.5f, f);
        }

        private IEnumerator HeartPulse(float strength)
        {
            NoirAtmosphere.Get().Pulse(strength);
            yield return Wait(0.26f);
            NoirAtmosphere.Get().Pulse(strength * 0.7f);
        }

        // ------------------------------------------------------------ eventos

        private void Trigger(int moment)
        {
            Scare pick = Pick(moment);
            lastScare = pick;
            nextEvent = Random.Range(MinEventGap, MaxEventGap);

            switch (pick)
            {
                case Scare.Flicker:
                    StartCoroutine(FlickerRoutine());
                    break;
                case Scare.Blackout:
                    StartCoroutine(BlackoutRoutine(FigureAllowed && Random.value < 0.5f));
                    break;
                case Scare.Pressure:
                    StartCoroutine(PressureRoutine());
                    break;
                case Scare.Surge:
                    StartCoroutine(SurgeRoutine());
                    break;
                case Scare.Passing:
                    if (!PlaceFigure(FigurePlacement.Side))
                        goto case Scare.Flicker;
                    lastFigureTime = Time.time;
                    StartCoroutine(PassingRoutine());
                    break;
                case Scare.Figure:
                    if (!PlaceFigure(FigurePlacement.Behind))
                        goto case Scare.Flicker;
                    lastFigureTime = Time.time;
                    break;
            }
        }

        private bool FigureAllowed => Time.timeSinceLevelLoad > 60f && Time.time - lastFigureTime > FigureCooldown;

        private Scare Pick(int moment)
        {
            // Pesos por momento: P1 escritorio (presenca), P2 camara (eletricidade da cadeira), P3 reparticao (luz que falha).
            float[] w = new float[6];
            switch (moment)
            {
                case 0: w[0] = 3f; w[1] = 2f; w[2] = 2f; break;
                case 1: w[0] = 2f; w[1] = 2f; w[2] = 1.5f; w[3] = chairPowered ? 3f : 0f; break;
                default: w[0] = 4f; w[1] = 3f; w[2] = 1.5f; break;
            }

            bool figures = FigureAllowed;
            w[4] = figures ? 2f : 0f;
            w[5] = figures ? 2.5f : 0f;
            w[(int)lastScare] = 0f;

            float total = 0f;
            foreach (float x in w) total += x;
            float r = Random.value * total;
            for (int i = 0; i < w.Length; i++)
            {
                r -= w[i];
                if (r <= 0f && w[i] > 0f)
                    return (Scare)i;
            }

            return Scare.Flicker;
        }

        /// <summary>Sobe o susto e alivia a tensao acumulada (o jogador respira depois do evento).</summary>
        private void Spike(float amount, float relief)
        {
            scare = Mathf.Max(scare, amount);
            dread *= relief;
            if (NoirAtmosphere.Exists) NoirAtmosphere.Get().Pulse(amount * 0.4f);
        }

        private Transform View => Camera.main != null ? Camera.main.transform : null;

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        private IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds && !Calm)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator FlickerRoutine()
        {
            busy = true;
            HoldLights();
            yield return Stutter(Random.Range(0.3f, 0.7f));
            Spike(0.15f, 0.85f);
            ReleaseLights();
            busy = false;
        }

        /// <summary>A tela se fecha devagar (vinheta, cor sumindo, lente puxando) e depois solta.</summary>
        private IEnumerator PressureRoutine()
        {
            busy = true;
            float t = 0f;
            const float rise = 3.5f;
            while (t < rise && !Calm)
            {
                t += Time.deltaTime;
                scare = Mathf.Max(scare, Mathf.SmoothStep(0f, 0.55f, t / rise));
                yield return null;
            }

            Spike(0.55f, 0.6f);
            busy = false;
        }

        private IEnumerator SurgeRoutine()
        {
            busy = true;
            HoldLights();

            // A rede da cadeira puxa a corrente: as luzes tremem cada vez mais ate a descarga.
            float t = 0f;
            while (t < 2f && !Calm)
            {
                t += Time.deltaTime;
                float f = t / 2f;
                lightScale = 1f + Mathf.Sin(t * 60f) * 0.25f * f + (Random.value < f * 0.15f ? -0.85f : 0f);
                yield return null;
            }

            if (!Calm)
            {
                Spike(0.85f, 0.15f);
                lightScale = 2.2f;
                yield return Wait(0.06f);
                lightScale = 0.02f;
                yield return Wait(Random.Range(0.9f, 1.4f));
                yield return Stutter(0.35f);
            }

            ReleaseLights();
            busy = false;
        }

        private IEnumerator BlackoutRoutine(bool reveal)
        {
            busy = true;
            HoldLights();
            yield return Stutter(0.45f);

            lightScale = 0.02f;
            if (Calm) { ReleaseLights(); busy = false; yield break; }

            // No escuro: a coisa aparece na frente (ou o condenado sentado na cadeira, em P2), para ser vista
            // quando a luz voltar.
            NoirChairGhost chairGhost = reveal ? NoirChairGhost.Current : null;
            bool inChair = chairGhost != null && chairGhost.figure != null && Random.value < 0.6f;
            bool placed = !inChair && reveal && PlaceFigure(FigurePlacement.Front);
            if (inChair) chairGhost.figure.SetActive(true);
            if (placed || inChair) lastFigureTime = Time.time;
            Spike(0.45f, 0.7f);

            yield return Wait(Random.Range(1.2f, 2.4f));
            yield return Stutter(0.3f);
            lightScale = 1f;

            if ((inChair || (placed && figure != null && figure.activeSelf)) && !Calm)
            {
                yield return Wait(Random.Range(0.45f, 0.75f));
                yield return VanishRoutine();
            }

            HideFigure();
            ReleaseLights();
            busy = false;
        }

        private IEnumerator Stutter(float seconds)
        {
            float t = 0f;
            while (t < seconds && !Calm)
            {
                lightScale = Random.value > 0.5f ? 1f : Random.Range(0.02f, 0.2f);
                float step = Random.Range(0.04f, 0.09f);
                t += step;
                yield return Wait(step);
            }
        }

        // ------------------------------------------------------------ luzes

        private void HoldLights()
        {
            if (lightsHeld) return;
            lightsHeld = true;
            lightScale = 1f;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l.isActiveAndEnabled || l.intensity <= 0f || l.GetComponent<NoirLightFlicker>() != null)
                    continue;
                heldLights.Add(l);
                heldBase.Add(l.intensity);
                heldWritten.Add(l.intensity);
            }
        }

        private void ReleaseLights()
        {
            for (int i = 0; i < heldLights.Count; i++)
            {
                if (heldLights[i] != null && Mathf.Abs(heldLights[i].intensity - heldWritten[i]) <= 0.001f)
                    heldLights[i].intensity = heldBase[i];
            }

            heldLights.Clear(); heldBase.Clear(); heldWritten.Clear();
            lightsHeld = false;
            lightScale = 1f;
            NoirLightFlicker.DreadScale = 1f;
        }

        // ------------------------------------------------------------ silhueta

        private enum FigurePlacement { Behind, Front, Side }

        private GameObject BuildFigure()
        {
            // Alta e magra, bracos compridos e a cabeca pendendo: so uma sombra mais escura que o escuro.
            Material black = EnvMat.Lit(null, Vector2.one, Color.black, 0f);
            GameObject root = new("Dread_Silhueta");
            root.transform.SetParent(transform, false);
            Part(root, PrimitiveType.Capsule, new Vector3(0f, 1.25f, 0f), new Vector3(0.46f, 0.95f, 0.3f), Quaternion.identity, black);
            Part(root, PrimitiveType.Sphere, new Vector3(0.04f, 2.3f, 0.05f), new Vector3(0.28f, 0.34f, 0.3f), Quaternion.Euler(0f, 0f, -14f), black);
            Part(root, PrimitiveType.Capsule, new Vector3(-0.3f, 1.25f, 0.02f), new Vector3(0.1f, 0.62f, 0.1f), Quaternion.Euler(0f, 0f, -4f), black);
            Part(root, PrimitiveType.Capsule, new Vector3(0.3f, 1.25f, 0.02f), new Vector3(0.1f, 0.62f, 0.1f), Quaternion.Euler(0f, 0f, 4f), black);
            root.SetActive(false);
            return root;
        }

        private static void Part(GameObject root, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
        {
            GameObject p = GameObject.CreatePrimitive(type);
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(root.transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = rot;
            p.transform.localScale = scale;
            Renderer r = p.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>
        /// Coloca a silhueta no chao do comodo, com linha de visao livre: pelas costas (para o jogador encontrar ao
        /// se virar), na frente (revelada quando a luz volta) ou no canto do olho (atravessando).
        /// </summary>
        private bool PlaceFigure(FigurePlacement placement)
        {
            Transform view = View;
            if (view == null || player == null) return false;
            if (figure == null) figure = BuildFigure();

            float feetY = player.bounds.min.y;
            Vector3 fwd = Flat(view.forward);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float angle, distance;
                switch (placement)
                {
                    case FigurePlacement.Front: angle = Random.Range(-15f, 15f); distance = Random.Range(4f, 6.5f); break;
                    case FigurePlacement.Side: angle = Random.Range(50f, 70f) * side; distance = Random.Range(5f, 8f); break;
                    default: angle = Random.Range(105f, 165f) * side; distance = Random.Range(3.5f, 7f); break;
                }

                Vector3 probe = view.position + Quaternion.Euler(0f, angle, 0f) * fwd * distance;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                Vector3 feet = hit.point;
                if (Mathf.Abs(feet.y - feetY) > 0.3f)
                    continue;
                if (Physics.CheckCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 2.2f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (Physics.Linecast(view.position, feet + Vector3.up * 2.2f, ~0, QueryTriggerInteraction.Ignore) ||
                    Physics.Linecast(view.position, feet + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                figure.transform.position = feet;
                figure.transform.rotation = Quaternion.LookRotation(Flat(view.position - feet));
                figure.SetActive(true);
                figureLife = figureSeen = 0f;
                figureVanishing = false;
                figureScripted = placement != FigurePlacement.Behind;
                return true;
            }

            return false;
        }

        /// <summary>A silhueta atravessa de lado, no canto do olho, e some antes de ser encarada.</summary>
        private IEnumerator PassingRoutine()
        {
            busy = true;
            Transform view = View;
            Vector3 start = figure.transform.position;
            Vector3 across = Vector3.Cross(Vector3.up, Flat(start - view.position));
            if (Random.value < 0.5f) across = -across;
            float t = 0f;
            const float duration = 1.3f;
            while (t < duration && !Calm && figure.activeSelf)
            {
                t += Time.deltaTime;
                Vector3 next = start + across * (2.6f * t / duration);
                // Para antes de entrar num movel ou parede.
                if (Physics.CheckCapsule(next + Vector3.up * 0.5f, next + Vector3.up * 2.2f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                    break;
                figure.transform.position = next;
                figure.transform.rotation = Quaternion.LookRotation(across);

                // Encarou: a luz engasga e ela some.
                Vector3 head = next + Vector3.up * 2.1f;
                if (Vector3.Angle(view.forward, head - view.position) < 20f)
                {
                    HoldLights();
                    yield return VanishRoutine();
                    ReleaseLights();
                    busy = false;
                    yield break;
                }

                yield return null;
            }

            HideFigure();
            Spike(0.3f, 0.7f);
            busy = false;
        }

        private void UpdateFigure(float dt, bool calm)
        {
            if (figure == null || !figure.activeSelf || figureVanishing)
                return;

            Transform view = View;
            if (calm || view == null) { HideFigure(); return; }
            if (figureScripted)
                return;

            figureLife += dt;
            Vector3 feet = figure.transform.position;
            Quaternion face = Quaternion.LookRotation(Flat(view.position - feet));
            figure.transform.rotation = Quaternion.Slerp(figure.transform.rotation, face, 1f - Mathf.Exp(-2f * dt));

            Vector3 head = feet + Vector3.up * 2.1f;
            float angle = Vector3.Angle(view.forward, head - view.position);
            bool inView = angle < 28f && !Physics.Linecast(view.position, head, ~0, QueryTriggerInteraction.Ignore);
            Vector3 gap = view.position - feet;
            gap.y = 0f;

            if (inView) figureSeen += dt;
            if (figureSeen > 0.22f || gap.magnitude < 2.3f)
            {
                busy = true;
                StartCoroutine(VanishFromView());
            }
            else if (figureLife > 14f && angle > 80f)
            {
                HideFigure();
            }
        }

        private IEnumerator VanishFromView()
        {
            HoldLights();
            yield return VanishRoutine();
            ReleaseLights();
            busy = false;
        }

        /// <summary>A luz engasga, a silhueta some no escuro e o susto explode na tela.</summary>
        private IEnumerator VanishRoutine()
        {
            figureVanishing = true;
            lightScale = 0.02f;
            yield return null;
            yield return null;
            Spike(1f, 0.08f);
            yield return Stutter(0.12f);
            HideFigure();
            yield return Stutter(0.35f);
            lightScale = 1f;
        }

        private void HideFigure()
        {
            if (figure != null)
                figure.SetActive(false);
            if (NoirChairGhost.Current != null && NoirChairGhost.Current.figure != null)
                NoirChairGhost.Current.figure.SetActive(false);
            figureVanishing = false;
        }
    }
}
