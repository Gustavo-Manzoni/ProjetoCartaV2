using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjetoCarta.NoirEnv
{
    /// <summary>Aplica o clima (ambiente, neblina, pos-processamento) do comodo quando ele fica ativo.</summary>
    public sealed class NoirRoomMood : MonoBehaviour
    {
        public Color ambientSky, ambientEquator, ambientGround, fogColor;
        public float fogDensity = 0.03f;
        public float saturation, contrast, postExposure;
        public Color colorFilter = Color.white;
        public float vignette = 0.35f, bloom = 0.6f, bloomThreshold = 0.9f, grain = 0.15f;
        public Color bloomTint = Color.white;
        // Grade sinistra: sombras e altas luzes tingidas, pretos esmagados e aberracao nas bordas (0 = desligado).
        public Color splitShadows = new(0.5f, 0.5f, 0.5f), splitHighlights = new(0.5f, 0.5f, 0.5f);
        public float splitBalance;
        public Vector4 lift = new(1f, 1f, 1f, 0f), gamma = new(1f, 1f, 1f, 0f);
        public float chromatic;

        private Volume volume;
        private ColorAdjustments adjustments;

        private void OnEnable()
        {
            ProjetoCarta.Core.GameSettings.Changed -= OnSettingsChanged;
            ProjetoCarta.Core.GameSettings.Changed += OnSettingsChanged;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.reflectionIntensity = 0.25f;

            if (volume == null)
            {
                volume = gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.weight = 1f;
                volume.profile = BuildProfile();
            }

            ApplyBrightness();
        }

        private void OnDisable()
        {
            ProjetoCarta.Core.GameSettings.Changed -= OnSettingsChanged;
        }

        private void OnSettingsChanged(string key)
        {
            if (key == ProjetoCarta.Core.GameSettings.BrightnessKey)
                ApplyBrightness();
        }

        /// <summary>O brilho da pausa soma a exposicao do comodo (nao troca o clima de cada momento).</summary>
        private void ApplyBrightness()
        {
            if (adjustments != null)
                adjustments.postExposure.Override(postExposure + ProjetoCarta.Core.GameSettings.BrightnessExposure);
        }

        private void OnDestroy()
        {
            if (volume != null && volume.profile != null) Destroy(volume.profile);
        }

        private VolumeProfile BuildProfile()
        {
            VolumeProfile p = ScriptableObject.CreateInstance<VolumeProfile>();

            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            var ca = p.Add<ColorAdjustments>(true);
            ca.saturation.Override(saturation);
            ca.contrast.Override(contrast);
            ca.postExposure.Override(postExposure);
            ca.colorFilter.Override(colorFilter);
            adjustments = ca;

            var v = p.Add<Vignette>(true);
            v.intensity.Override(vignette);
            v.smoothness.Override(0.55f);
            v.color.Override(Color.black);

            var b = p.Add<Bloom>(true);
            b.threshold.Override(bloomThreshold);
            b.intensity.Override(bloom);
            b.scatter.Override(0.7f);
            b.tint.Override(bloomTint);

            var fg = p.Add<FilmGrain>(true);
            fg.type.Override(FilmGrainLookup.Thin1);
            fg.intensity.Override(0f); fg.active = false;

            var cab = p.Add<ChromaticAberration>(true);
            cab.intensity.Override(chromatic);

            var st = p.Add<SplitToning>(true);
            st.shadows.Override(splitShadows);
            st.highlights.Override(splitHighlights);
            st.balance.Override(splitBalance);

            var lgg = p.Add<LiftGammaGain>(true);
            lgg.lift.Override(lift);
            lgg.gamma.Override(gamma);
            return p;
        }
    }

    /// <summary>Cintilacao de luz: ruido suave, falhas (lampada ruim) ou pulso (emergencia).</summary>
    public sealed class NoirLightFlicker : MonoBehaviour
    {
        public Light target;
        public Renderer emissive;
        public Color emissiveColor = Color.white;
        public float amount = 0.06f;
        public float speed = 2.5f;
        public float dropoutChance;      // chance por segundo de a lampada falhar
        public float pulseSpeed;         // > 0: pulso senoidal

        /// <summary>Multiplicador global usado pelo NoirDread (queda de luz, surto da cadeira). 1 = normal.</summary>
        public static float DreadScale = 1f;

        /// <summary>Multiplicador das luzes acordando quando o jogador chega no momento (NoirLightsWake).</summary>
        public static float WakeScale = 1f;

        private float baseIntensity, seed, dropout;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            if (target == null) target = GetComponent<Light>();
            if (target != null) baseIntensity = target.intensity;
            seed = Random.value * 100f;
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (target == null) return;
            float k;
            if (pulseSpeed > 0f)
                k = 0.2f + 0.8f * (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f);
            else
            {
                k = 1f + (Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f) * 2f * amount;
                if (dropout > 0f) { dropout -= Time.deltaTime; k *= Random.value > 0.5f ? 0.04f : 0.35f; }
                else if (dropoutChance > 0f && Random.value < dropoutChance * Time.deltaTime) dropout = Random.Range(0.05f, 0.35f);
            }
            k *= DreadScale * WakeScale;
            target.intensity = baseIntensity * k;

            if (emissive != null)
            {
                emissive.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", emissiveColor * Mathf.Max(0.02f, k));
                emissive.SetPropertyBlock(block);
            }
        }
    }
}
