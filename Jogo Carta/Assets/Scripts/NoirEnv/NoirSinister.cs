using System.Collections.Generic;
using UnityEngine;

namespace ProjetoCarta.NoirEnv
{
    /// <summary>
    /// Texturas e materiais da camada sinistra, gerados em codigo: manchas de infiltracao, mofo, marcas de dias
    /// riscadas, maos arrastadas, arranhoes, queimado, silhueta e o degrade dos cones de luz.
    /// As texturas de "multiplicar" sao brancas onde a parede fica intacta: funcionam com ou sem a variante do
    /// shader que usa o alfa.
    /// </summary>
    public static class SinisterTex
    {
        private static readonly Dictionary<string, Texture2D> Cache = new();
        private static readonly Dictionary<string, Material> Mats = new();

        public enum Blend { Alpha, Additive, Multiply }

        public static Material Mat(string key, Texture tex, Color tint, Blend blend)
        {
            string k = $"{key}|{tint}|{blend}";
            if (Mats.TryGetValue(k, out Material m) && m != null) return m;

            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            m = new Material(s) { name = "NoirSinister_" + key };
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            switch (blend)
            {
                case Blend.Alpha:
                    m.SetFloat("_Blend", 0f);
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
                case Blend.Additive:
                    m.SetFloat("_Blend", 2f);
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    break;
                default:
                    m.SetFloat("_Blend", 3f);
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    m.EnableKeyword("_ALPHAMODULATE_ON");
                    break;
            }

            m.renderQueue = blend == Blend.Multiply ? 2990 : 3000;
            Mats[k] = m;
            return m;
        }

        // ------------------------------------------------------------ geracao

        private static Texture2D Make(string key, int w, int h, System.Func<Texture2D, Color[]> fill, bool repeat = false)
        {
            if (Cache.TryGetValue(key, out Texture2D t) && t != null) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Sinister_" + key };
            t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 4;
            t.SetPixels(fill(t));
            t.Apply(true, true);
            Cache[key] = t;
            return t;
        }

        private static Color[] White(int n)
        {
            Color[] c = new Color[n];
            for (int i = 0; i < n; i++) c[i] = Color.white;
            return c;
        }

        /// <summary>Mistura multiplicativa: escurece o pixel na direcao de ink com forca a (0..1).</summary>
        private static void Ink(Color[] px, int w, int h, int x, int y, Color ink, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || a <= 0f) return;
            int i = y * w + x;
            Color c = px[i];
            a = Mathf.Clamp01(a);
            px[i] = new Color(Mathf.Lerp(c.r, c.r * ink.r, a), Mathf.Lerp(c.g, c.g * ink.g, a), Mathf.Lerp(c.b, c.b * ink.b, a),
                Mathf.Max(c.a, a));
        }

        private static float Fbm(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 0.55f + Mathf.PerlinNoise(x * 2.1f + 7f, y * 2.1f + 3f) * 0.3f +
                   Mathf.PerlinNoise(x * 4.3f + 1f, y * 4.3f + 9f) * 0.15f;
        }

        /// <summary>Infiltracao escorrendo do teto: faixa encharcada no alto e escorridos de comprimentos variados.</summary>
        public static Texture2D Leak(int seed) => Make("leak" + seed, 256, 512, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = White(w * h);
            System.Random rng = new(seed);
            Color ink = new(0.42f, 0.33f, 0.22f);
            float o = (float)rng.NextDouble() * 50f;
            // Mancha do teto (v = 1 e o alto).
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float edge = Mathf.Sin(u * Mathf.PI);
                    float band = Mathf.InverseLerp(0.62f + Fbm(u * 6f + o, 1f) * 0.25f, 1f, v) * edge;
                    float n = Fbm(u * 8f + o, v * 8f);
                    Ink(px, w, h, x, y, ink, band * (0.55f + n * 0.6f));
                    // Contorno mais escuro da mancha seca (anel de agua).
                    float ring = Mathf.Abs(v - (0.64f + Fbm(u * 6f + o, 1f) * 0.25f));
                    if (ring < 0.012f) Ink(px, w, h, x, y, new Color(0.3f, 0.22f, 0.14f), (1f - ring / 0.012f) * 0.6f * edge);
                }

            // Escorridos.
            int drips = 18 + rng.Next(10);
            for (int d = 0; d < drips; d++)
            {
                float cx = (0.1f + (float)rng.NextDouble() * 0.8f) * w;
                float len = (0.2f + (float)rng.NextDouble() * 0.65f) * h;
                float width = 1.5f + (float)rng.NextDouble() * 4f;
                float strength = 0.25f + (float)rng.NextDouble() * 0.5f;
                for (int y = h - 1; y > h - len && y >= 0; y--)
                {
                    float k = (h - y) / len;
                    float wob = (Mathf.PerlinNoise(cx * 0.1f, y * 0.02f) - 0.5f) * 6f;
                    float wk = width * (1f - k * 0.6f);
                    for (int dx = -6; dx <= 6; dx++)
                    {
                        float dist = Mathf.Abs(dx - wob) / wk;
                        if (dist > 1f) continue;
                        Ink(px, w, h, (int)cx + dx, y, ink, strength * (1f - dist) * (1f - k * 0.8f));
                    }
                }
            }

            return px;
        });

        /// <summary>Mofo e fuligem num canto: manchas fractais esverdeadas.</summary>
        public static Texture2D Mold(int seed) => Make("mold" + seed, 256, 256, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = White(w * h);
            float o = seed * 13.7f;
            Color ink = new(0.28f, 0.32f, 0.2f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w - 0.5f, v = y / (float)h - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float n = Fbm(x * 0.03f + o, y * 0.03f);
                    float a = Mathf.Clamp01((n - 0.32f - r * 0.45f) * 3.5f);
                    float speck = Mathf.PerlinNoise(x * 0.4f + o, y * 0.4f) > 0.72f ? 0.35f * (1f - r) : 0f;
                    Ink(px, w, h, x, y, ink, a * 0.85f + speck);
                }

            return px;
        });

        /// <summary>Dias riscados na parede: grupos de quatro tracos cortados por um quinto, em fileiras tortas.</summary>
        public static Texture2D Tally(int seed, int days) => Make("tally" + seed + "_" + days, 512, 256, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = White(w * h);
            System.Random rng = new(seed);
            Color ink = new(0.22f, 0.2f, 0.19f);
            int perRow = 7;
            int drawn = 0;
            for (int row = 0; drawn < days && row < 4; row++)
            {
                float baseY = h - 40 - row * 58;
                for (int g = 0; g < perRow && drawn < days; g++)
                {
                    float gx = 22 + g * 68 + (float)(rng.NextDouble() * 8 - 4);
                    float gy = baseY + (float)(rng.NextDouble() * 8 - 4);
                    int strokes = Mathf.Min(5, days - drawn);
                    for (int s = 0; s < strokes; s++)
                    {
                        if (s < 4)
                        {
                            float x0 = gx + s * 11 + (float)(rng.NextDouble() * 3);
                            Scratch(px, w, h, x0, gy + 20, x0 + (float)(rng.NextDouble() * 6 - 3), gy - 22, ink, rng);
                        }
                        else
                        {
                            Scratch(px, w, h, gx - 6, gy - 14, gx + 46, gy + 16, ink, rng);
                        }
                    }

                    drawn += strokes;
                }
            }

            return px;
        });

        private static void Scratch(Color[] px, int w, int h, float x0, float y0, float x1, float y1, Color ink, System.Random rng)
        {
            float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Mathf.CeilToInt(len * 2f);
            float depth = 0.6f + (float)rng.NextDouble() * 0.35f;
            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                float x = Mathf.Lerp(x0, x1, k), y = Mathf.Lerp(y0, y1, k);
                float a = depth * Mathf.Sin(k * Mathf.PI) * (0.7f + 0.3f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f));
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > 2.2f) continue;
                        Ink(px, w, h, (int)x + dx, (int)y + dy, ink, a * (1f - d / 2.4f));
                    }

                // Reboco claro levantado na borda do risco.
                Ink(px, w, h, (int)x + 3, (int)y, new Color(0.9f, 0.88f, 0.85f), a * 0.15f);
            }
        }

        /// <summary>Marcas de mao escorregando para baixo, de sangue seco (quase marrom).</summary>
        public static Texture2D Hands(int seed) => Make("hands" + seed, 256, 512, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = White(w * h);
            System.Random rng = new(seed);
            Color ink = new(0.36f, 0.08f, 0.06f);
            int count = 2 + rng.Next(2);
            for (int n = 0; n < count; n++)
            {
                float cx = w * (0.28f + 0.44f * n / Mathf.Max(1, count - 1)) + (float)(rng.NextDouble() * 20 - 10);
                float cy = h * (0.72f - (float)rng.NextDouble() * 0.12f);
                float tilt = (float)(rng.NextDouble() * 0.5 - 0.25);
                float drag = h * (0.25f + (float)rng.NextDouble() * 0.3f);
                Hand(px, w, h, cx, cy, tilt, ink, 0.8f, rng);
                // Arrasto: os dedos deixam riscos ate embaixo, cada vez mais fracos.
                for (int fgr = 0; fgr < 4; fgr++)
                {
                    float fx = cx + (fgr - 1.5f) * 13f;
                    for (int y = (int)(cy + 50); y > cy + 50 - drag; y--)
                    {
                        float k = (cy + 50 - y) / drag;
                        for (int dx = -4; dx <= 4; dx++)
                            Ink(px, w, h, (int)(fx + dx + tilt * (cy - y) * 0.2f), y - 60, ink, 0.55f * (1f - k) * (1f - Mathf.Abs(dx) / 5f) *
                                (0.6f + 0.4f * Mathf.PerlinNoise(fx, y * 0.08f)));
                    }
                }
            }

            return px;
        });

        private static void Hand(Color[] px, int w, int h, float cx, float cy, float tilt, Color ink, float a, System.Random rng)
        {
            float cs = Mathf.Cos(tilt), sn = Mathf.Sin(tilt);
            for (int y = -90; y <= 90; y++)
                for (int x = -60; x <= 60; x++)
                {
                    float lx = x * cs - y * sn, ly = x * sn + y * cs;
                    float best = 0f;
                    // Palma.
                    float pal = (lx * lx) / (34f * 34f) + ((ly + 18f) * (ly + 18f)) / (38f * 38f);
                    if (pal < 1f) best = 1f - pal * 0.6f;
                    // Dedos (o polegar sai de lado).
                    float[] fxs = { -24f, -8f, 8f, 23f };
                    float[] flen = { 38f, 46f, 44f, 34f };
                    for (int f = 0; f < 4; f++)
                    {
                        float dx = lx - fxs[f];
                        float dy = ly - (16f + flen[f] * 0.5f);
                        float d = (dx * dx) / (7.5f * 7.5f) + (dy * dy) / (flen[f] * 0.5f * flen[f] * 0.5f);
                        if (d < 1f) best = Mathf.Max(best, 1f - d * 0.6f);
                    }

                    float tx = lx + 42f, ty = ly + 4f;
                    float td = (tx * tx) / (24f * 24f) + (ty * ty) / (9f * 9f);
                    if (td < 1f) best = Mathf.Max(best, 1f - td * 0.6f);
                    if (best <= 0f) continue;
                    float n = Mathf.PerlinNoise((cx + x) * 0.15f, (cy + y) * 0.15f);
                    Ink(px, w, h, (int)(cx + x), (int)(cy + y), ink, a * best * (0.45f + n * 0.7f));
                }
        }

        /// <summary>Quatro arranhoes paralelos, fundos, como unhas.</summary>
        public static Texture2D Claws(int seed) => Make("claws" + seed, 256, 256, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = White(w * h);
            System.Random rng = new(seed);
            Color ink = new(0.18f, 0.15f, 0.14f);
            for (int s = 0; s < 4; s++)
            {
                float x0 = 60 + s * 30 + (float)rng.NextDouble() * 6, y0 = 220 - s * 6;
                float x1 = x0 + 40 + (float)rng.NextDouble() * 15, y1 = 30 + s * 10;
                Scratch(px, w, h, x0, y0, x1, y1, ink, rng);
                Scratch(px, w, h, x0 + 1, y0 - 2, x1 + 1, y1 - 2, ink, rng);
            }

            return px;
        });

        /// <summary>Chao queimado e encardido sob a cadeira.</summary>
        public static Texture2D Scorch() => Make("scorch", 256, 256, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = White(w * h);
            Color ink = new(0.12f, 0.1f, 0.09f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w - 0.5f, v = y / (float)h - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float n = Fbm(x * 0.025f, y * 0.025f);
                    Ink(px, w, h, x, y, ink, Mathf.Clamp01((1f - r) * 1.3f - 0.15f + (n - 0.5f) * 0.8f) * 0.8f);
                }

            return px;
        });

        /// <summary>Pegada molhada de sapato (sola e salto), escura sobre branco; a ponta aponta para +v.</summary>
        public static Texture2D Footprint() => Make("footprint", 64, 128, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = White(w * h);
            Color ink = new(0.2f, 0.18f, 0.16f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x - w * 0.5f) / (w * 0.5f), v = y / (float)h;
                    float sole = (u * u) / 0.62f + ((v - 0.66f) * (v - 0.66f)) / 0.075f;
                    float heel = (u * u) / 0.45f + ((v - 0.2f) * (v - 0.2f)) / 0.022f;
                    float best = Mathf.Max(sole < 1f ? 1f - sole * 0.5f : 0f, heel < 1f ? 1f - heel * 0.5f : 0f);
                    if (best <= 0f) continue;
                    float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f);
                    Ink(px, w, h, x, y, ink, best * (0.35f + 0.55f * n));
                }

            return px;
        });

        /// <summary>Mascara (alfa) da pegada molhada, para o brilho aditivo: borda mais viva, como agua acumulada.</summary>
        public static Texture2D FootprintWet() => Make("footprint_wet", 64, 128, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x - w * 0.5f) / (w * 0.5f), v = y / (float)h;
                    float sole = (u * u) / 0.62f + ((v - 0.66f) * (v - 0.66f)) / 0.075f;
                    float heel = (u * u) / 0.45f + ((v - 0.2f) * (v - 0.2f)) / 0.022f;
                    float d = Mathf.Min(sole, heel);
                    float a = d < 1f ? Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0.4f, 1f, d)) * Mathf.Clamp01((1f - d) * 8f) : 0f;
                    a *= 0.55f + 0.45f * Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }

            return px;
        });

        /// <summary>Silhueta de um homem de chapeu, preta sobre branco (para multiplicar).</summary>
        public static Texture2D Silhouette() => Make("silhouette", 128, 256, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = White(w * h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x - w * 0.5f) / w, v = y / (float)h;
                    bool body = v < 0.72f && Mathf.Abs(u) < Mathf.Lerp(0.26f, 0.42f, Mathf.InverseLerp(0.72f, 0.6f, v)) * (v < 0.6f ? 1.05f : 1f);
                    bool neck = v >= 0.7f && v < 0.76f && Mathf.Abs(u) < 0.07f;
                    float hu = u / 0.12f, hv = (v - 0.82f) / 0.08f;
                    bool head = hu * hu + hv * hv < 1f;
                    bool brim = v > 0.86f && v < 0.885f && Mathf.Abs(u) < 0.25f;
                    bool crown = v >= 0.88f && v < 0.96f && Mathf.Abs(u) < 0.14f;
                    if (body || neck || head || brim || crown)
                    {
                        float soft = Mathf.Clamp01((0.5f - Mathf.Abs(u)) * 6f);
                        px[y * w + x] = new Color(0.03f, 0.03f, 0.04f, soft);
                    }
                }

            return px;
        });

        /// <summary>Degrade do cone de luz: forte junto a lampada e sumindo para baixo.</summary>
        public static Texture2D Beam() => Make("beam", 32, 128, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = y / (float)(h - 1);
                    float streak = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.6f, 3f);
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Pow(1f - v, 1.6f) * streak);
                }

            return px;
        }, true);

        /// <summary>Mancha macia e redonda (neblina rasteira).</summary>
        public static Texture2D Puff() => Make("puff", 128, 128, t =>
        {
            int w = t.width, h = t.height;
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w - 0.5f, v = y / (float)h - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float n = Fbm(x * 0.05f, y * 0.05f);
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * (0.5f + n));
                }

            return px;
        });
    }

    /// <summary>Pendulo lento com rajadas: lampada pendurada balancando e as sombras correndo pela sala.</summary>
    public sealed class NoirSwing : MonoBehaviour
    {
        public float amplitude = 5f;
        public float period = 5.5f;
        private float seed;

        private void Awake() => seed = Random.value * 50f;

        private void Update()
        {
            float t = Time.time;
            float gust = 0.55f + 0.9f * Mathf.PerlinNoise(seed, t * 0.12f);
            float w = 2f * Mathf.PI / period;
            transform.localRotation = Quaternion.Euler(Mathf.Sin(t * w) * amplitude * gust,
                0f, Mathf.Sin(t * w * 0.71f + 1.3f) * amplitude * 0.45f * gust);
        }
    }

    /// <summary>Rola a textura do material (chuva escorrendo no vidro).</summary>
    public sealed class NoirScroll : MonoBehaviour
    {
        public Vector2 speed = new(0f, -0.35f);
        private Material material;
        private Vector2 offset;

        private void Start() => material = GetComponent<Renderer>().material;

        private void Update()
        {
            if (material == null) return;
            offset += speed * Time.deltaTime;
            offset.x -= Mathf.Floor(offset.x);
            offset.y -= Mathf.Floor(offset.y);
            material.SetTextureOffset("_BaseMap", offset);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }

    /// <summary>
    /// Vultos sentados: aparecem nos assentos quando o jogador passa um tempo sem olhar para eles, viram a cabeca
    /// devagar para acompanhar quem os encara e somem assim que o jogador desvia o olhar de novo.
    /// </summary>
    public sealed class NoirSeatedGhosts : MonoBehaviour
    {
        public List<Transform> ghosts = new();
        public Vector2 cooldown = new(45f, 90f);
        public int maxVisible = 3;
        /// <summary>Luz fraca atras dos assentos: so acende com os vultos, que viram silhuetas recortadas.</summary>
        public Light backlight;
        private float backlightBase;

        private float unseen, wait, seenTime;
        private bool shown, wasSeen;
        private readonly List<Transform> heads = new();
        private readonly List<Quaternion> headRest = new();

        private void Start()
        {
            foreach (Transform g in ghosts)
            {
                Transform head = g.Find("Cabeca");
                heads.Add(head);
                headRest.Add(head != null ? head.localRotation : Quaternion.identity);
                g.gameObject.SetActive(false);
            }

            wait = Random.Range(15f, 30f);
            if (backlight != null) { backlightBase = backlight.intensity; backlight.enabled = false; }
        }

        private void OnDisable()
        {
            // Ao voltar para este momento, ninguem esta sentado.
            Hide();
            wait = Mathf.Max(wait, Random.Range(12f, 25f));
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam == null || ghosts.Count == 0) return;
            Transform view = cam.transform;
            float dt = Time.deltaTime;

            bool lookingAtAny = false;
            for (int i = 0; i < ghosts.Count; i++)
            {
                Vector3 p = ghosts[i].position + Vector3.up * 1.1f;
                if (Vector3.Angle(view.forward, p - view.position) < 42f) { lookingAtAny = true; break; }
            }

            if (backlight != null && backlight.enabled)
                backlight.intensity = backlightBase * (0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 3f, 0.7f));

            if (!shown)
            {
                wait -= dt;
                unseen = lookingAtAny ? 0f : unseen + dt;
                if (wait <= 0f && unseen > 3.5f && !ProjetoCarta.Core.GameplayLocks.MovementLocked)
                    Show();
                return;
            }

            if (lookingAtAny)
            {
                wasSeen = true;
                seenTime += dt;
                // As cabecas viram para quem olha.
                for (int i = 0; i < ghosts.Count; i++)
                {
                    if (!ghosts[i].gameObject.activeSelf || heads[i] == null) continue;
                    Vector3 to = view.position - heads[i].position;
                    Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                    Quaternion target = Quaternion.RotateTowards(heads[i].parent.rotation * headRest[i], look, 75f);
                    heads[i].rotation = Quaternion.Slerp(heads[i].rotation, target, 1f - Mathf.Exp(-0.8f * dt));
                }
            }
            else if (wasSeen || ProjetoCarta.Core.GameplayLocks.MovementLocked)
            {
                // Desviou o olhar: quando voltar, nao tem mais ninguem.
                Hide();
                wait = Random.Range(cooldown.x, cooldown.y);
            }
        }

        private void Show()
        {
            shown = true;
            if (backlight != null) backlight.enabled = true;
            wasSeen = false;
            seenTime = 0f;
            int n = Mathf.Min(ghosts.Count, Random.Range(1, maxVisible + 1));
            List<int> order = new();
            for (int i = 0; i < ghosts.Count; i++) order.Add(i);
            for (int i = 0; i < n; i++)
            {
                int k = Random.Range(i, order.Count);
                (order[i], order[k]) = (order[k], order[i]);
                ghosts[order[i]].gameObject.SetActive(true);
                if (heads[order[i]] != null) heads[order[i]].localRotation = headRest[order[i]];
            }
        }

        private void Hide()
        {
            shown = false;
            if (backlight != null) backlight.enabled = false;
            foreach (Transform g in ghosts)
                if (g != null) g.gameObject.SetActive(false);
        }
    }
}
