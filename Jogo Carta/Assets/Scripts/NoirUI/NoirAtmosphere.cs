using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Camada de atmosfera noir por cima de tudo: vinheta que respira, grao de filme,
    /// poeira flutuando, tom ambar sutil e fade de entrada/saida de cena.
    /// </summary>
    public sealed class NoirAtmosphere : MonoBehaviour
    {
        private static NoirAtmosphere instance;

        private Canvas canvas;
        private RawImage vignette;
        private RawImage grain;
        private Image tint;
        private Image fade;
        private RectTransform dustRoot;
        private float vignetteBase = 0.78f;
        private float grainBase = 0.3f;
        private float pulse;
        private float grainTimer;
        private bool fading;

        private struct Mote { public RectTransform rt; public Image img; public float speed, sway, phase, baseAlpha; }
        private Mote[] motes;

        public static NoirAtmosphere Get()
        {
            if (instance == null)
            {
                GameObject go = new("NoirAtmosphere");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<NoirAtmosphere>();
                instance.Build();
            }

            return instance;
        }

        public static bool Exists => instance != null;

        public void Configure(bool gameplay)
        {
            // Menu e gameplay compartilham o mesmo visual: grao so de leve por cima da tela (com cenas escuras ele
            // virava um veu marrom sobre o breu) e a vinheta fica por conta do pos-processamento da sala.
            vignetteBase = 0.62f;
            grainBase = 0.05f;
            ApplyGrain(null);
            tint.color = NoirTheme.WithAlpha(new Color(1f, 0.62f, 0.25f), 0.012f);        }

        /// <summary>Grao = intensidade do momento (menu ou gameplay) x opcao "Grao de filme".</summary>
        private void ApplyGrain(string key)
        {
            if (key != null && key != ProjetoCarta.Core.GameSettings.FilmGrainKey)
                return;
            if (grain != null)
                grain.color = new Color(1f, 1f, 1f, grainBase * ProjetoCarta.Core.GameSettings.FilmGrain);
        }

        private void OnDestroy()
        {
            ProjetoCarta.Core.GameSettings.Changed -= ApplyGrain;
        }

        /// <summary>Pulso de vinheta (piscada, dano, susto).</summary>
        public void Pulse(float strength = 0.3f)
        {
            pulse = Mathf.Max(pulse, strength);
        }

        /// <summary>Cobre a tela de preto na hora, para a cena nova nao aparecer crua antes do que e montado em runtime.</summary>
        public void Cover()
        {
            fade.DOKill();
            fade.color = Color.black;
        }

        public void FadeIn(float duration = 1.1f, float delay = 0f)
        {
            fade.DOKill();
            fade.color = Color.black;
            fade.DOFade(0f, duration).SetDelay(delay).SetEase(Ease.InOutSine).SetUpdate(true);
        }

        public void FadeOut(float duration, Action done)
        {
            if (fading)
                return;

            fading = true;
            fade.DOKill();
            fade.DOFade(1f, duration).SetEase(Ease.InSine).SetUpdate(true).OnComplete(() =>
            {
                fading = false;
                done?.Invoke();
            });
        }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            tint = Full("Tint", null);
            tint.color = NoirTheme.WithAlpha(new Color(1f, 0.62f, 0.25f), 0.05f);

            vignette = FullRaw("Vignette", NoirTheme.Tex("noir_vignette"));
            vignette.color = new Color(0, 0, 0, vignetteBase);

            grain = FullRaw("Grain", NoirTheme.Tex("noir_grain"));
            grain.texture.wrapMode = TextureWrapMode.Repeat;
            grain.uvRect = new Rect(0, 0, 8f, 4.5f);
            grain.color = new Color(1, 1, 1, 0.7f);
            ProjetoCarta.Core.GameSettings.Changed += ApplyGrain;

            fade = Full("Fade", null);
            fade.color = Color.black;
            // Canvas proprio acima de qualquer UI (menu, pausa, final): o fade de troca de cena cobre tudo.
            Canvas fadeCanvas = fade.gameObject.AddComponent<Canvas>();
            fadeCanvas.overrideSorting = true;
            fadeCanvas.sortingOrder = 32000;
        }

        private Image Full(string name, Sprite sprite)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            Stretch(go.GetComponent<RectTransform>());
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        private RawImage FullRaw(string name, Texture tex)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(transform, false);
            Stretch(go.GetComponent<RectTransform>());
            RawImage img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private void BuildDust(int count)
        {
            motes = new Mote[count];
            for (int i = 0; i < count; i++)
            {
                GameObject go = new("Mote", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(dustRoot, false);
                Image img = go.GetComponent<Image>();
                img.sprite = NoirTheme.GetSprite("noir_dust");
                img.raycastTarget = false;
                float size = UnityEngine.Random.Range(3f, 9f);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.sizeDelta = new Vector2(size, size);
                float a = UnityEngine.Random.Range(0.08f, 0.32f);
                img.color = new Color(0.95f, 0.88f, 0.7f, a);
                motes[i] = new Mote
                {
                    rt = rt, img = img, baseAlpha = a,
                    speed = UnityEngine.Random.Range(6f, 22f),
                    sway = UnityEngine.Random.Range(10f, 40f),
                    phase = UnityEngine.Random.value * 10f
                };
                rt.anchoredPosition = new Vector2(UnityEngine.Random.value * 1920f, UnityEngine.Random.value * 1080f);
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            pulse = Mathf.MoveTowards(pulse, 0f, dt * 0.9f);
            float breathe = Mathf.Sin(t * 0.9f) * 0.035f;
            float flicker = Mathf.PerlinNoise(t * 3.1f, 0.5f) * 0.04f;
            vignette.color = new Color(0, 0, 0, Mathf.Clamp01(vignetteBase + breathe + flicker + pulse));

            grainTimer += dt;
            if (grainTimer > 0.07f)
            {
                grainTimer = 0f;
                grain.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, 8f, 4.5f);
            }

            if (motes == null)
                return;

            for (int i = 0; i < motes.Length; i++)
            {
                Mote m = motes[i];
                Vector2 p = m.rt.anchoredPosition;
                p.y += m.speed * dt;
                p.x += Mathf.Sin(t * 0.6f + m.phase) * m.sway * dt;
                if (p.y > 1100f) { p.y = -20f; p.x = UnityEngine.Random.value * 1920f; }
                m.rt.anchoredPosition = p;
                float tw = 0.6f + 0.4f * Mathf.Sin(t * 1.3f + m.phase * 3f);
                Color c = m.img.color; c.a = m.baseAlpha * tw; m.img.color = c;
            }
        }
    }
}
