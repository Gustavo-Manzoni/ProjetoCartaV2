using System.Collections.Generic;
using UnityEngine;

namespace ProjetoCarta.NoirEnv
{
    /// <summary>Materiais URP criados em tempo de execucao a partir de Resources/NoirEnv.</summary>
    public static class EnvMat
    {
        private static readonly Dictionary<string, Material> Cache = new();

        private static Shader LitShader =>
            Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        public static Texture2D Tex(string n) => string.IsNullOrEmpty(n) ? null : Resources.Load<Texture2D>("NoirEnv/" + n);

        public static Material Lit(string tex, Vector2 tiling, Color tint, float smooth = 0.2f,
            float metal = 0f, float bump = 1f)
        {
            string key = $"L|{tex}|{tiling}|{tint}|{smooth}|{metal}|{bump}";
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;

            m = new Material(LitShader) { name = "NoirEnv_" + tex };
            Texture2D albedo = Tex(tex);
            Texture2D normal = Tex(tex + "_n");
            SetTex(m, "_BaseMap", albedo, tiling);
            SetTex(m, "_MainTex", albedo, tiling);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            if (normal != null)
            {
                SetTex(m, "_BumpMap", normal, tiling);
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Material Lit com uma textura gerada por codigo (cortica, papeis do quadro).</summary>
        public static Material LitTexture(string key, Texture tex, Vector2 tiling, Color tint, float smooth = 0.2f)
        {
            string cacheKey = $"T|{key}|{tiling}|{tint}|{smooth}";
            if (Cache.TryGetValue(cacheKey, out Material m) && m != null) return m;

            m = new Material(LitShader) { name = "NoirEnv_" + key };
            SetTex(m, "_BaseMap", tex, tiling);
            SetTex(m, "_MainTex", tex, tiling);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", 0f);
            Cache[cacheKey] = m;
            return m;
        }

        /// <summary>Superficie que so emite luz (lampadas, janelas).</summary>
        public static Material Glow(string tex, Color color, float intensity)
        {
            string key = $"G|{tex}|{color}|{intensity}";
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;

            m = new Material(LitShader) { name = "NoirEnvGlow" };
            m.SetColor("_BaseColor", Color.black);
            m.SetColor("_Color", Color.black);
            m.SetFloat("_Smoothness", 0.1f);
            m.SetColor("_EmissionColor", color * intensity);
            Texture2D t = Tex(tex);
            if (t != null) m.SetTexture("_EmissionMap", t);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Cache[key] = m;
            return m;
        }

        /// <summary>Vidro escuro e polido.</summary>
        public static Material Glass(Color tint, float glow)
        {
            string key = $"V|{tint}|{glow}";
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(LitShader) { name = "NoirEnvGlass" };
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            m.SetFloat("_Smoothness", 0.97f);
            m.SetFloat("_Metallic", 0.3f);
            if (glow > 0f)
            {
                m.SetColor("_EmissionColor", tint * glow);
                m.EnableKeyword("_EMISSION");
            }
            Cache[key] = m;
            return m;
        }

        /// <summary>Material aditivo (feixes de luz, halos, poeira).</summary>
        public static Material Additive(string tex, Color tint)
        {
            string key = $"A|{tex}|{tint}";
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;

            // O template em Resources garante que o shader (e a variante transparente) entre no build;
            // Shader.Find sozinho voltava null no player e caia no Sprites/Default, que desenhava quadrados brancos.
            Material template = Resources.Load<Material>("NoirEnv/NoirAdditiveTemplate");
            if (template != null)
                m = new Material(template);
            else
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                 ?? Shader.Find("Legacy Shaders/Particles/Additive")
                                 ?? Shader.Find("Sprites/Default"));
            m.name = "NoirEnvAdd_" + tex;
            Texture2D t = Tex(tex);
            if (t != null) { m.SetTexture("_BaseMap", t); m.SetTexture("_MainTex", t); }
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            m.SetColor("_TintColor", tint);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            Cache[key] = m;
            return m;
        }

        private static void SetTex(Material m, string prop, Texture t, Vector2 tiling)
        {
            if (!m.HasProperty(prop)) return;
            m.SetTexture(prop, t);
            m.SetTextureScale(prop, tiling);
        }
    }

    /// <summary>Criacao de geometria decorativa simples.</summary>
    public static class Prims
    {
        public static float LightGain = 2.6f;
        public static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 pos,
            Vector3 scale, Material mat, bool collider = false, Quaternion? rot = null, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            if (!collider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.Destroy(c);
            }
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Box(Transform p, string n, Vector3 pos, Vector3 scale, Material m,
            bool collider = false, Quaternion? rot = null, bool shadows = true) =>
            Make(PrimitiveType.Cube, p, n, pos, scale, m, collider, rot, shadows);

        public static GameObject Cyl(Transform p, string n, Vector3 pos, Vector3 scale, Material m,
            Quaternion? rot = null) => Make(PrimitiveType.Cylinder, p, n, pos, scale, m, false, rot);

        public static GameObject Ball(Transform p, string n, Vector3 pos, float d, Material m,
            bool shadows = false) => Make(PrimitiveType.Sphere, p, n, pos, Vector3.one * d, m, false, null, shadows);

        /// <summary>Quad visivel por quem esta na direcao toward (aponta para o observador).</summary>
        public static GameObject Quad(Transform p, string n, Vector3 pos, Vector2 size, Material m,
            Vector3 toward, Vector3? up = null, bool shadows = false)
        {
            Quaternion q = Quaternion.LookRotation(-toward, up ?? Vector3.up);
            return Make(PrimitiveType.Quad, p, n, pos, new Vector3(size.x, size.y, 1f), m, false, q, shadows);
        }

        public static Mesh Lathe(Vector2[] profile, int seg)
        {
            List<Vector3> v = new(); List<Vector2> uv = new(); List<int> t = new();
            int n = profile.Length;
            for (int pass = 0; pass < 2; pass++)
            {
                int b = v.Count;
                for (int i = 0; i < n; i++)
                    for (int s = 0; s <= seg; s++)
                    {
                        float a = s * Mathf.PI * 2f / seg;
                        v.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                        uv.Add(new Vector2((float)s / seg, (float)i / (n - 1)));
                    }
                for (int i = 0; i < n - 1; i++)
                    for (int s = 0; s < seg; s++)
                    {
                        int a = b + i * (seg + 1) + s;
                        int c = a + seg + 1;
                        if (pass == 0) { t.Add(a); t.Add(c); t.Add(a + 1); t.Add(a + 1); t.Add(c); t.Add(c + 1); }
                        else { t.Add(a); t.Add(a + 1); t.Add(c); t.Add(a + 1); t.Add(c + 1); t.Add(c); }
                    }
            }
            Mesh m = new() { name = "NoirLathe" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public static GameObject LatheObj(Transform p, string n, Vector3 pos, Vector2[] profile, Material m)
        {
            GameObject go = new(n);
            go.transform.SetParent(p, true);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = Lathe(profile, 24);
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        public static Light MakeLight(Transform p, string n, Vector3 pos, LightType type, Color c, float intensity,
            float range, bool shadows, Vector3? aimAt = null, float spot = 60f, Texture cookie = null)
        {
            GameObject go = new(n);
            go.transform.SetParent(p, true);
            go.transform.position = pos;
            if (aimAt.HasValue) go.transform.rotation = Quaternion.LookRotation(aimAt.Value - pos);
            Light l = go.AddComponent<Light>();
            l.type = type; l.color = c; l.intensity = intensity * LightGain; l.range = range;
            if (type == LightType.Spot) l.spotAngle = spot;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (shadows) { l.shadowStrength = 0.92f; l.shadowBias = 0.03f; l.shadowNormalBias = 0.4f; }
            if (cookie != null) l.cookie = cookie;
            return l;
        }

        public static ParticleSystem Dust(Transform p, string n, Vector3 center, Vector3 size, Color c, int count, float particle = 0.05f)
        {
            GameObject go = new(n);
            go.transform.SetParent(p, true);
            go.transform.position = center;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(particle * 0.5f, particle);
            main.startColor = c;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = count / 10f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.15f; noise.scrollSpeed = 0.1f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(-0.015f, 0.04f);
            vel.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f); vel.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            var col = ps.colorOverLifetime; col.enabled = true;
            Gradient g = new();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = EnvMat.Additive("env_halo", new Color(1f, 1f, 1f, 0.5f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Poeira colada na camera (keypad, leitura de perto) nao vira manchas enormes na tela.
            r.maxParticleSize = 0.012f;
            ps.Play();
            return ps;
        }
    }
}
