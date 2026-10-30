using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjetoCarta.NoirEnv
{
    /// <summary>
    /// Refaz o cenario das tres salas em tempo de execucao: materiais, teto, parede frontal,
    /// rodapes, vigas, luzes, sombras, poeira e pos-processamento. Nada na cena precisa ser editado.
    /// </summary>
    public static partial class NoirEnvironment
    {
        // Coloque false para desligar os lambris/azulejos se algum objeto colado na parede sumir.
        public static bool UseWainscot = true;

        private sealed class Frame
        {
            public Vector3 O;           // centro do piso, na altura do chao
            public float sx = 1f, sz = 1f, hx, hz, ceil;
            public Vector3 P(float x, float y, float z) => new(O.x + sx * x, O.y + y, O.z + sz * z);
            public Vector3 D(float x, float y, float z) => new(sx * x, y, sz * z);
        }

        private static bool built;

        public static void Build()
        {
            GameObject root = GameObject.Find("PiscadaFatal_Cenarios");
            if (root == null) return;

            foreach (Transform cena in root.transform)
            {
                if (!cena.name.StartsWith("CENA_")) continue;
                if (cena.Find("NoirEnv") != null) continue;
                if (!TryFrame(cena, out Frame f, out Renderer[] walls)) continue;

                GameObject env = new("NoirEnv");
                env.SetActive(false);
                env.transform.SetParent(cena, true);

                if (cena.name.Contains("1")) Office(cena, env, f, walls);
                else if (cena.name.Contains("2")) Chamber(cena, env, f, walls);
                else Archive(cena, env, f, walls);
                Sinister(cena, env, f, cena.name.Contains("1") ? 0 : cena.name.Contains("2") ? 1 : 2);

                env.SetActive(true);
                TameLooseColliders(cena);
            }

            if (built) return;
            built = true;

            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                UniversalAdditionalCameraData d = cam.GetUniversalAdditionalCameraData();
                if (d != null) { d.renderPostProcessing = true; d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; }
            }

            if (Object.FindFirstObjectByType<NoirFallGuard>() == null)
                new GameObject("NoirFallGuard").AddComponent<NoirFallGuard>();
            SceneManagerHook();
        }

        // Colliders solidos escondidos (ex.: Olho_de_Vidro sob a tabua solta) empurram o CharacterController
        // quando ativados; viram gatilhos para continuarem clicaveis sem mover o jogador.
        private static void TameLooseColliders(Transform cena)
        {
            Transform eye = Deep(cena, "Olho_de_Vidro");
            if (eye == null) return;
            foreach (Collider c in eye.GetComponentsInChildren<Collider>(true))
                c.isTrigger = true;
        }

        private static void SceneManagerHook()
        {
            // reseta a flag quando outra cena for carregada
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += _ => built = false;
        }

        // ------------------------------------------------------------------ geometria base

        private static bool TryFrame(Transform cena, out Frame f, out Renderer[] walls)
        {
            f = null; walls = null;
            Transform sala = Deep(cena, "Sala_12x12");
            if (sala == null) return false;
            Renderer piso = R(sala, "Piso"), fundo = R(sala, "Parede_Fundo"),
                     esq = R(sala, "Parede_Esquerda"), dir = R(sala, "Parede_Direita");
            if (piso == null || fundo == null || esq == null || dir == null) return false;

            Bounds pb = piso.bounds;
            float sz = fundo.bounds.center.z >= pb.center.z ? 1f : -1f;
            float backInner = sz > 0 ? fundo.bounds.min.z : fundo.bounds.max.z;
            float leftInner = esq.bounds.center.x < dir.bounds.center.x ? esq.bounds.max.x : esq.bounds.min.x;
            float rightInner = esq.bounds.center.x < dir.bounds.center.x ? dir.bounds.min.x : dir.bounds.max.x;
            float depth = pb.size.z;

            f = new Frame
            {
                sz = sz,
                sx = rightInner >= leftInner ? 1f : -1f,
                hx = Mathf.Abs(rightInner - leftInner) * 0.5f,
                hz = depth * 0.5f,
                ceil = fundo.bounds.max.y,
                O = new Vector3((leftInner + rightInner) * 0.5f, pb.max.y, backInner - sz * depth * 0.5f)
            };
            f.ceil -= f.O.y;
            walls = new[] { piso, fundo, esq, dir };
            return true;
        }

        private static Renderer R(Transform p, string n) => p.Find(n) != null ? p.Find(n).GetComponent<Renderer>() : null;

        private static Transform Deep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { Transform r = Deep(c, n); if (r != null) return r; }
            return null;
        }

        private struct Spec
        {
            public string wallTex, panelTex, floorTex, ceilTex, trimTex;
            public float wallTile, panelTile, floorTile, panelH;
            public Color wall, panel, floor, ceil, trim;
        }

        private static Vector2 Tl(float w, float h, float tile) => new(Mathf.Max(0.1f, w / tile), Mathf.Max(0.1f, h / tile));

        private static void Shell(Transform cena, Transform env, Frame f, Renderer[] w, Spec s)
        {
            float W = f.hx * 2f, D = f.hz * 2f, H = f.ceil;

            // materiais nas paredes e piso existentes
            w[0].sharedMaterial = EnvMat.Lit(s.floorTex, Tl(W, D, s.floorTile), s.floor, 0.35f);
            for (int i = 1; i < 4; i++)
            {
                Vector3 sc = w[i].transform.lossyScale;
                float len = Mathf.Max(sc.x, sc.z);
                w[i].sharedMaterial = EnvMat.Lit(s.wallTex, Tl(len, sc.y, s.wallTile), s.wall, 0.12f);
            }

            Material wallM = EnvMat.Lit(s.wallTex, Tl(W, H, s.wallTile), s.wall, 0.12f);
            // parede frontal que fecha a sala
            Prims.Box(env, "Parede_Frontal", f.P(0, H * 0.5f, -f.hz - 0.1f), new Vector3(W + 0.4f, H, 0.2f), wallM, true);
            // piso de seguranca invisivel: impede cair no vazio se o chao visivel sumir ou for desativado
            GameObject safe = new("Chao_Seguranca");
            safe.transform.SetParent(env, true);
            safe.transform.position = f.P(0, -0.52f, 0);
            BoxCollider safeCol = safe.AddComponent<BoxCollider>();
            safeCol.size = new Vector3(W + 1f, 1f, D + 1f);
            // teto
            Material ceilM = EnvMat.Lit(s.ceilTex, Tl(W, D, 3f), s.ceil, 0.1f);
            Prims.Box(env, "Teto", f.P(0, H + 0.1f, 0), new Vector3(W + 0.6f, 0.2f, D + 0.6f), ceilM, true);

            Material trimM = EnvMat.Lit(s.trimTex, Vector2.one, s.trim, 0.3f);
            const float t = 0.05f;
            // rodape e moldura de teto em volta
            Ring(env, f, trimM, 0.04f, 0.16f, t);
            Ring(env, f, trimM, H - 0.12f, 0.12f, t);

            // lambris
            if (UseWainscot && s.panelH > 0f)
            {
                const float th = 0.014f;
                Material pb = EnvMat.Lit(s.panelTex, Tl(W, s.panelH, s.panelTile), s.panel, 0.3f);
                Material pl = EnvMat.Lit(s.panelTex, Tl(D, s.panelH, s.panelTile), s.panel, 0.3f);
                float y = s.panelH * 0.5f;
                Prims.Box(env, "Lambril_Fundo", f.P(0, y, f.hz - th * 0.5f - 0.002f), new Vector3(W, s.panelH, th), pb, false, null, false);
                Prims.Box(env, "Lambril_Frente", f.P(0, y, -f.hz + th * 0.5f + 0.002f), new Vector3(W, s.panelH, th), pb, false, null, false);
                Prims.Box(env, "Lambril_Esq", f.P(-f.hx + th * 0.5f + 0.002f, y, 0), new Vector3(th, s.panelH, D), pl, false, null, false);
                Prims.Box(env, "Lambril_Dir", f.P(f.hx - th * 0.5f - 0.002f, y, 0), new Vector3(th, s.panelH, D), pl, false, null, false);
                // friso superior do lambril
                Ring(env, f, trimM, s.panelH, 0.06f, 0.035f);
            }
        }

        private static void Ring(Transform env, Frame f, Material m, float y, float h, float t)
        {
            float W = f.hx * 2f, D = f.hz * 2f;
            Prims.Box(env, "Faixa_F", f.P(0, y + h * 0.5f, f.hz - t * 0.5f), new Vector3(W, h, t), m, false, null, false);
            Prims.Box(env, "Faixa_T", f.P(0, y + h * 0.5f, -f.hz + t * 0.5f), new Vector3(W, h, t), m, false, null, false);
            Prims.Box(env, "Faixa_E", f.P(-f.hx + t * 0.5f, y + h * 0.5f, 0), new Vector3(t, h, D), m, false, null, false);
            Prims.Box(env, "Faixa_D", f.P(f.hx - t * 0.5f, y + h * 0.5f, 0), new Vector3(t, h, D), m, false, null, false);
        }

        private static void Beams(Transform env, Frame f, Material m, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(-f.hz + 1.5f, f.hz - 1.5f, count == 1 ? 0.5f : i / (count - 1f));
                Prims.Box(env, "Viga", f.P(0, f.ceil - 0.17f, z), new Vector3(f.hx * 2f, 0.34f, 0.36f), m, false);
            }
        }

        private static void Mood(GameObject env, Color sky, Color eq, Color gr, Color fog, float dens,
            float sat, float con, Color filter, float vig, float bloom, float thr, Color bloomTint, float grain = 0.15f)
        {
            NoirRoomMood m = env.AddComponent<NoirRoomMood>();
            m.ambientSky = sky * 5f; m.ambientEquator = eq * 5f; m.ambientGround = gr * 5f; m.fogColor = fog; m.fogDensity = dens * 0.45f; m.postExposure = 1.8f; m.grain = 0f;
            m.saturation = sat; m.contrast = con; m.colorFilter = filter; m.vignette = vig * 0.35f;
            m.bloom = bloom; m.bloomThreshold = thr; m.bloomTint = bloomTint;
        }

        private static void WindowFrame(Transform env, Frame f, Vector3 c, Vector2 size, Material frame)
        {
            // Moldura do vidro de observacao da camara. c: centro do vidro (encostado na parede esquerda); abre para +x local
            float x = c.x;
            const float b = 0.12f;
            Vector3 centerWorld = c;
            Prims.Box(env, "Jan_Topo", centerWorld + f.D(0, size.y * 0.5f + b * 0.5f, 0), new Vector3(0.1f, b, size.x + b * 2f), frame);
            Prims.Box(env, "Jan_Base", centerWorld + f.D(0, -size.y * 0.5f - b * 0.5f, 0), new Vector3(0.18f, b, size.x + b * 2f), frame);
            Prims.Box(env, "Jan_E", centerWorld + f.D(0, 0, -size.x * 0.5f - b * 0.5f), new Vector3(0.1f, size.y, b), frame);
            Prims.Box(env, "Jan_D", centerWorld + f.D(0, 0, size.x * 0.5f + b * 0.5f), new Vector3(0.1f, size.y, b), frame);
            Prims.Box(env, "Jan_V", centerWorld, new Vector3(0.08f, size.y, 0.07f), frame);
            Prims.Box(env, "Jan_H", centerWorld + f.D(0, size.y * 0.15f, 0), new Vector3(0.08f, 0.07f, size.x), frame);
        }

        // ------------------------------------------------------------------ SALA 1: escritorio

        private static void Office(Transform cena, GameObject envGo, Frame f, Renderer[] w)
        {
            Transform env = envGo.transform;
            Shell(cena, env, f, w, new Spec
            {
                wallTex = "env_wallpaper", wallTile = 1.5f, wall = new Color(0.82f, 0.72f, 0.62f),
                panelTex = "env_wood_panel", panelTile = 1.5f, panel = new Color(0.7f, 0.55f, 0.45f), panelH = 1.15f,
                floorTex = "env_wood_floor", floorTile = 3f, floor = new Color(0.85f, 0.75f, 0.68f),
                ceilTex = "env_plaster", ceil = new Color(0.4f, 0.35f, 0.3f),
                trimTex = "env_wood_panel", trim = new Color(0.35f, 0.24f, 0.17f)
            });

            Beams(env, f, EnvMat.Lit("env_beam", new Vector2(4f, 1f), new Color(0.5f, 0.38f, 0.3f), 0.25f), 3);

            // tapete
            Prims.Box(env, "Tapete", f.P(0, 0.009f, 0), new Vector3(4.6f, 0.018f, 3.4f),
                EnvMat.Lit("env_rug", Vector2.one, Color.white, 0.1f), false, null, false);

            // luminaria central
            Vector3 lampPos = f.P(0, f.ceil - 1.25f, 0);
            Material metal = EnvMat.Lit("env_metal", Vector2.one, new Color(0.25f, 0.22f, 0.18f), 0.5f, 0.7f);
            Prims.Cyl(env, "Cabo", f.P(0, f.ceil - 0.5f, 0), new Vector3(0.025f, 0.5f, 0.025f), metal);
            Prims.LatheObj(env, "Cupula", lampPos + Vector3.up * 0.1f,
                new[] { new Vector2(0.05f, 0.25f), new Vector2(0.12f, 0.22f), new Vector2(0.3f, 0.0f), new Vector2(0.42f, -0.1f) }, metal);
            Material bulbM = EnvMat.Glow(null, new Color(1f, 0.75f, 0.45f), 3.5f);
            GameObject bulb = Prims.Ball(env, "Lampada", lampPos - Vector3.up * 0.02f, 0.15f, bulbM);
            Light pend = Prims.MakeLight(env, "Luz_Pendente", lampPos - Vector3.up * 0.1f, LightType.Point,
                new Color(1f, 0.72f, 0.42f), 6f, 14f, true);
            NoirLightFlicker fl = pend.gameObject.AddComponent<NoirLightFlicker>();
            fl.target = pend; fl.emissive = bulb.GetComponent<Renderer>(); fl.emissiveColor = new Color(1f, 0.75f, 0.45f) * 3.5f;
            fl.amount = 0.05f; fl.speed = 1.6f; fl.dropoutChance = 0.03f;
            Prims.MakeLight(env, "Luz_Preenchimento", f.P(0, 2f, -4f), LightType.Point, new Color(1f, 0.6f, 0.35f), 0.9f, 9f, false);

            Prims.Dust(env, "Poeira", f.P(0, 2f, 0), new Vector3(f.hx * 1.6f, 3f, f.hz * 1.6f), new Color(1f, 0.85f, 0.6f, 0.5f), 90);
            DecorOffice(env, f);

            Mood(envGo, new Color(0.16f, 0.12f, 0.10f), new Color(0.12f, 0.09f, 0.07f), new Color(0.05f, 0.04f, 0.03f),
                new Color(0.13f, 0.09f, 0.06f), 0.035f, -15f, 15f, new Color(1f, 0.93f, 0.82f), 0.38f, 0.7f, 0.9f,
                new Color(1f, 0.8f, 0.55f));
        }

        // ------------------------------------------------------------------ SALA 2: camara de execucao

        private static void Chamber(Transform cena, GameObject envGo, Frame f, Renderer[] w)
        {
            Transform env = envGo.transform;
            Shell(cena, env, f, w, new Spec
            {
                wallTex = "env_plaster", wallTile = 2f, wall = new Color(0.5f, 0.66f, 0.6f),
                panelTex = "env_tiles", panelTile = 2f, panel = new Color(0.75f, 0.92f, 0.9f), panelH = 2.2f,
                floorTex = "env_concrete", floorTile = 3f, floor = new Color(0.5f, 0.55f, 0.55f),
                ceilTex = "env_concrete", ceil = new Color(0.35f, 0.4f, 0.4f),
                trimTex = "env_metal", trim = new Color(0.3f, 0.34f, 0.34f)
            });

            Material steel = EnvMat.Lit("env_metal", new Vector2(4f, 1f), new Color(0.4f, 0.45f, 0.45f), 0.55f, 0.8f);
            Beams(env, f, steel, 3);
            for (int i = 0; i < 2; i++)
                Prims.Cyl(env, "Cano", f.P(0, f.ceil - 0.5f, -3.4f + i * 6.8f), new Vector3(0.18f, f.hx, 0.18f), steel,
                    Quaternion.Euler(0, 0, 90));

            // lampadas com grade
            Material cage = EnvMat.Lit("env_metal", Vector2.one, new Color(0.15f, 0.17f, 0.17f), 0.5f, 0.8f);
            Color cold = new(0.6f, 1f, 0.95f);
            float[] xs = { -3.6f, 0f, 3.6f };
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = f.P(xs[i], f.ceil - 0.28f, 0f);
                Prims.Box(env, "Gaiola", p + Vector3.up * 0.1f, new Vector3(0.36f, 0.08f, 0.36f), cage);
                GameObject bulb = Prims.Ball(env, "Lampada", p, 0.2f, EnvMat.Glow(null, cold, 3f));
                Light l = Prims.MakeLight(env, "Luz_Teto_" + i, p - Vector3.up * 0.15f, LightType.Point, cold, i == 1 ? 3f : 2.2f, 10f, false);
                NoirLightFlicker fl = l.gameObject.AddComponent<NoirLightFlicker>();
                fl.target = l; fl.emissive = bulb.GetComponent<Renderer>(); fl.emissiveColor = cold * 3f;
                fl.amount = i == 1 ? 0.25f : 0.04f; fl.speed = i == 1 ? 9f : 2f; fl.dropoutChance = i == 1 ? 0.6f : 0.02f;
            }

            // cadeira: holofote, halo e faixas de aviso
            Transform chair = Deep(cena, "P5_Cadeira_Eletrica");
            Vector3 cp = chair != null ? chair.position : f.P(0, 0, -4f);
            Vector3 cf = new(cp.x, f.O.y, cp.z);
            Light spot = Prims.MakeLight(env, "Holofote_Cadeira", cf + Vector3.up * (f.ceil - 0.4f), LightType.Spot,
                new Color(0.75f, 0.95f, 1f), 22f, 12f, true, cf + Vector3.up * 0.5f, 52f);
            NoirLightFlicker sf = spot.gameObject.AddComponent<NoirLightFlicker>();
            sf.target = spot; sf.amount = 0.07f; sf.speed = 4f; sf.dropoutChance = 0.05f;
            Material shaft = EnvMat.Additive("env_shaft", new Color(0.6f, 0.9f, 1f, 0.22f));
            for (int i = 0; i < 2; i++)
                Prims.Make(PrimitiveType.Quad, env, "Feixe", cf + Vector3.up * (f.ceil * 0.5f - 0.2f), new Vector3(2.8f, f.ceil - 0.4f, 1f),
                    shaft, false, Quaternion.Euler(0, 90f * i, 0), false);
            Prims.Box(env, "Halo_Chao", cf + Vector3.up * 0.014f, new Vector3(3.2f, 0.01f, 3.2f), EnvMat.Additive("env_halo", new Color(0.7f, 0.95f, 1f, 0.35f)), false, null, false);
            Material haz = EnvMat.Lit("env_hazard", new Vector2(3f, 0.2f), Color.white, 0.2f);
            float hs = 1.9f;
            Prims.Box(env, "Faixa_A", cf + new Vector3(0, 0.012f, hs), new Vector3(2 * hs + 0.2f, 0.012f, 0.18f), haz, false, null, false);
            Prims.Box(env, "Faixa_B", cf + new Vector3(0, 0.012f, -hs), new Vector3(2 * hs + 0.2f, 0.012f, 0.18f), haz, false, null, false);
            Prims.Box(env, "Faixa_C", cf + new Vector3(hs, 0.012f, 0), new Vector3(0.18f, 0.012f, 2 * hs), haz, false, null, false);
            Prims.Box(env, "Faixa_D", cf + new Vector3(-hs, 0.012f, 0), new Vector3(0.18f, 0.012f, 2 * hs), haz, false, null, false);

            // vidro de observacao na parede esquerda
            Vector3 gc = f.P(-f.hx + 0.04f, 2.0f, -1.0f);
            Prims.Quad(env, "Vidro_Observacao", gc, new Vector2(4f, 1.5f), EnvMat.Glass(new Color(0.02f, 0.05f, 0.06f), 0.12f), f.D(1, 0, 0));
            WindowFrame(env, f, gc, new Vector2(4f, 1.5f), cage);

            Prims.Dust(env, "Poeira", f.P(0, 2f, 0), new Vector3(f.hx * 1.6f, 3.5f, f.hz * 1.6f), new Color(0.7f, 0.95f, 1f, 0.45f), 70);
            DecorChamber(env, f);

            Mood(envGo, new Color(0.05f, 0.09f, 0.09f), new Color(0.04f, 0.07f, 0.07f), new Color(0.02f, 0.03f, 0.03f),
                new Color(0.04f, 0.09f, 0.09f), 0.045f, -35f, 20f, new Color(0.82f, 0.95f, 1f), 0.45f, 0.8f, 0.85f,
                new Color(0.6f, 0.9f, 1f), 0.25f);
        }

        // ------------------------------------------------------------------ SALA 3: reparticao administrativa (fria)

        private static void Archive(Transform cena, GameObject envGo, Frame f, Renderer[] w)
        {
            // O Perdao acontece numa reparticao: luz fluorescente fria e chapada, paredes bege-acinzentadas,
            // lambri pintado de azul-cinza e piso gasto. Contrasta com o ambar do escritorio (P1) e com a
            // camara azulejada em ciano e vermelho (P2).
            Transform env = envGo.transform;
            Shell(cena, env, f, w, new Spec
            {
                wallTex = "env_plaster", wallTile = 2f, wall = new Color(0.8f, 0.79f, 0.74f),
                panelTex = "env_wood_panel", panelTile = 1.2f, panel = new Color(0.42f, 0.48f, 0.55f), panelH = 1.05f,
                floorTex = "env_concrete", floorTile = 1.2f, floor = new Color(0.62f, 0.6f, 0.55f),
                ceilTex = "env_plaster", ceil = new Color(0.72f, 0.72f, 0.7f),
                trimTex = "env_metal", trim = new Color(0.28f, 0.32f, 0.31f)
            });

            // Calhas de lampada fluorescente: uma delas falha de tempos em tempos.
            Material housing = EnvMat.Lit("env_metal", Vector2.one, new Color(0.55f, 0.58f, 0.58f), 0.5f, 0.6f);
            Color tube = new(0.82f, 1f, 0.94f);
            float[] zs = { -2.8f, 0.4f, 3.4f };
            for (int i = 0; i < zs.Length; i++)
            {
                Vector3 p = f.P(-0.4f, f.ceil - 0.08f, zs[i]);
                Prims.Box(env, "Calha", p + Vector3.up * 0.03f, new Vector3(1.5f, 0.06f, 0.32f), housing, false);
                GameObject glow = Prims.Box(env, "Tubo", p - Vector3.up * 0.015f, new Vector3(1.4f, 0.025f, 0.22f),
                    EnvMat.Glow(null, tube, 2.4f), false, null, false);
                Light l = Prims.MakeLight(env, "Fluorescente_" + i, p - Vector3.up * 0.3f, LightType.Point, tube,
                    i == 1 ? 2.6f : 2.1f, 9f, i == 1);
                NoirLightFlicker fl = l.gameObject.AddComponent<NoirLightFlicker>();
                fl.target = l; fl.emissive = glow.GetComponent<Renderer>(); fl.emissiveColor = tube * 2.4f;
                fl.amount = 0.03f; fl.speed = 14f; fl.dropoutChance = i == 2 ? 0.35f : 0.015f;
            }

            Prims.Dust(env, "Poeira", f.P(-1f, 2f, 0), new Vector3(f.hx * 1.4f, 3.2f, f.hz * 1.4f), new Color(0.8f, 0.92f, 0.9f, 0.4f), 60, 0.045f);
            DecorArchive(env, f);

            Mood(envGo, new Color(0.15f, 0.16f, 0.16f), new Color(0.12f, 0.12f, 0.12f), new Color(0.05f, 0.05f, 0.05f),
                new Color(0.12f, 0.13f, 0.13f), 0.028f, -42f, 10f, new Color(0.92f, 0.98f, 1f), 0.46f, 0.45f, 0.95f,
                new Color(0.9f, 1f, 1f), 0.1f);
        }

        // ------------------------------------------------------------------ jogador

        public static void FixPlayerSpawn()
        {
            FirstPersonController pc = Object.FindFirstObjectByType<FirstPersonController>();
            if (pc == null) return;
            GameObject root = GameObject.Find("PiscadaFatal_Cenarios");
            if (root == null) return;

            foreach (Transform cena in root.transform)
            {
                if (!cena.name.StartsWith("CENA_1") || !TryFrame(cena, out Frame f, out _)) continue;
                Vector3 p = pc.transform.position;
                bool inside = Mathf.Abs(p.x - f.O.x) < f.hx && Mathf.Abs(p.z - f.O.z) < f.hz;
                if (inside) return;

                CharacterController cc = pc.GetComponent<CharacterController>();
                float feetOffset = cc != null ? p.y - cc.bounds.min.y : 1f;
                Vector3 target = f.P(0f, 0f, -f.hz + 1.6f);
                target.y = f.O.y + feetOffset + 0.03f;
                if (cc != null) cc.enabled = false;
                pc.transform.position = target;
                pc.transform.rotation = Quaternion.LookRotation(f.D(0, 0, 1), Vector3.up);
                if (cc != null) cc.enabled = true;
                return;
            }
        }
    }
}

namespace ProjetoCarta.NoirEnv
{
    /// <summary>Se o jogador cair para fora do mundo, volta ao ultimo ponto onde estava no chao.</summary>
    public sealed class NoirFallGuard : MonoBehaviour
    {
        private CharacterController cc;
        private Vector3 lastSafe;
        private bool hasSafe;
        private float floorY = float.NaN;

        private void LateUpdate()
        {
            if (cc == null)
            {
                FirstPersonController pc = FindFirstObjectByType<FirstPersonController>();
                if (pc == null) return;
                cc = pc.GetComponent<CharacterController>();
                if (cc == null) return;
            }

            if (float.IsNaN(floorY)) floorY = cc.bounds.min.y - 0.1f;

            if (cc.isGrounded && cc.transform.position.y > floorY - 1f)
            {
                lastSafe = cc.transform.position;
                hasSafe = true;
            }

            if (hasSafe && cc.transform.position.y < floorY - 3f)
            {
                cc.enabled = false;
                cc.transform.position = lastSafe + Vector3.up * 0.1f;
                cc.enabled = true;
                if (cc.TryGetComponent(out FirstPersonController player))
                    player.ResetMotion();
            }
        }
    }
}
