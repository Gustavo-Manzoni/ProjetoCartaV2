using System.Collections.Generic;
using UnityEngine;

namespace ProjetoCarta.NoirEnv
{
    /// <summary>
    /// Camada sinistra por cima do cenario de cada sala: luz baixa em pocos com cones de luz na
    /// neblina, neblina rasteira, lampada pendurada balancando, infiltracao, mofo, dias riscados na parede,
    /// maos arrastadas, chao queimado sob a cadeira, vultos que se sentam quando ninguem esta olhando e a
    /// grade de cor (pretos esmagados, sombras doentias). Nada aqui tem collider nem cobre os puzzles.
    /// </summary>
    public static partial class NoirEnvironment
    {
        private enum Side { Back, Front, Left, Right }

        private static void Sinister(Transform cena, GameObject envGo, Frame f, int room)
        {
            Transform env = envGo.transform;
            NoirRoomMood mood = envGo.GetComponent<NoirRoomMood>();
            switch (room)
            {
                case 0: SinisterOffice(cena, env, f, mood); break;
                case 1: SinisterChamber(cena, env, f, mood); break;
                default: SinisterArchive(cena, env, f, mood); break;
            }

            // Toda chegada no momento: as luzes acordam engasgando. Toda volta: algo na sala mudou.
            envGo.AddComponent<NoirLightsWake>();
            NoirRevisit revisit = envGo.AddComponent<NoirRevisit>();
            foreach (Transform t in env.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == null || t.parent.name != "Mobilia") continue;
                if (t.name.StartsWith("Quadro_") || t.name.StartsWith("Diploma_") || t.name.StartsWith("Retrato") || t.name.StartsWith("Aviso_"))
                    revisit.pictures.Add(t);
            }

            Transform tally = env.Find("Dias_Riscados");
            if (tally != null) revisit.tally = tally.GetComponent<Renderer>();
            revisit.floorY = f.O.y;
            revisit.roomCenter = f.O;
            revisit.halfSize = new Vector2(f.hx - 0.3f, f.hz - 0.3f);
        }

        // ------------------------------------------------------------------ P1: escritorio

        private static void SinisterOffice(Transform cena, Transform env, Frame f, NoirRoomMood mood)
        {
            if (mood != null)
            {
                Darken(mood, 0.98f, 0.6f, 1.2f);
                mood.saturation -= 10f; mood.contrast += 14f; mood.vignette = 0.36f;
                mood.splitShadows = new Color(0.33f, 0.45f, 0.5f); mood.splitHighlights = new Color(0.68f, 0.52f, 0.36f); mood.splitBalance = -25f;
                mood.lift = new Vector4(1f, 1f, 1.04f, -0.005f);
                mood.chromatic = 0.12f;
            }

            Scale(env, "Luz_Preenchimento", 0.25f);

            // Lampada pendurada: tudo dela passa a balancar num pivo no teto (as sombras correm pela sala).
            Vector3 top = f.P(0, f.ceil, 0);
            Transform pivot = new GameObject("Pendulo_Lampada").transform;
            pivot.SetParent(env, false);
            pivot.position = top;
            foreach (string n in new[] { "Cabo", "Cupula", "Lampada", "Luz_Pendente" })
            {
                Transform t = env.Find(n);
                if (t != null) t.SetParent(pivot, true);
            }

            Transform pend = pivot.Find("Luz_Pendente");
            if (pend != null)
            {
                Cone(pivot, pend.position + Vector3.up * 0.05f, 0.32f, 2.4f, f.ceil - 1.25f, new Color(1f, 0.72f, 0.42f, 0.11f));
                Light l = pend.GetComponent<Light>();
                if (l != null) l.shadowStrength = 1f;
            }

            NoirSwing swing = pivot.gameObject.AddComponent<NoirSwing>();
            swing.amplitude = 4.5f; swing.period = 5.8f;

            // Infiltracao no alto das paredes e mofo nos cantos.
            Leaks(env, f, 1, 0.75f);
            Decal(env, f, Side.Left, -4.2f, 0.7f, new Vector2(1.4f, 1.4f), SinisterTex.Mold(2), "Mofo_Canto");
            Decal(env, f, Side.Back, 5.3f, f.ceil - 0.6f, new Vector2(1.4f, 1.3f), SinisterTex.Mold(3), "Mofo_Teto");

            // Vultos nas poltronas do canto de leitura.
            SeatGhosts(env, f, new[] { "Poltrona_A", "Poltrona_B" }, 0.56f, 1.3f, 0.06f, 2);

            GroundFog(env, f, new Color(0.55f, 0.42f, 0.3f, 0.07f), 26);
        }

        // ------------------------------------------------------------------ P2: camara de execucao

        private static void SinisterChamber(Transform cena, Transform env, Frame f, NoirRoomMood mood)
        {
            if (mood != null)
            {
                Darken(mood, 0.92f, 0.45f, 0.7f);
                mood.saturation -= 5f; mood.contrast += 18f; mood.vignette = 0.4f;
                mood.splitShadows = new Color(0.4f, 0.52f, 0.38f); mood.splitHighlights = new Color(0.45f, 0.6f, 0.62f); mood.splitBalance = -15f;
                mood.lift = new Vector4(1f, 1.02f, 1f, -0.008f);
                mood.chromatic = 0.15f;
            }

            // Duas das tres lampadas de grade mais fracas: a luz fica concentrada na cadeira.
            Scale(env, "Luz_Teto_0", 0.45f);
            Scale(env, "Luz_Teto_2", 0.3f);
            foreach (Transform t in Children(env, "Lampada"))
                Cone(env, t.position, 0.18f, 1.9f, 3.2f, new Color(0.6f, 1f, 0.95f, 0.035f));

            // O holofote da cadeira tinha dois quads aditivos cruzados que clareavam tudo atras deles: vira cone.
            foreach (Transform t in Children(env, "Feixe"))
                t.gameObject.SetActive(false);
            Transform spot = env.Find("Holofote_Cadeira");
            if (spot != null && spot.TryGetComponent(out Light spotLight))
            {
                float h = spot.position.y - f.O.y;
                float r = Mathf.Tan(spotLight.spotAngle * 0.5f * Mathf.Deg2Rad) * h * 0.8f;
                Cone(env, spot.position, 0.12f, r, h, new Color(0.65f, 0.92f, 1f, 0.08f));
            }

            Transform red = env.Find("Emergencia");
            if (red != null && red.TryGetComponent(out Light emergency)) emergency.range = 14f;

            Transform chair = Deep(cena, "P8_Cadeira_Eletrica") ?? Deep(cena, "P5_Cadeira_Eletrica");
            Vector3 cp = chair != null ? chair.position : f.P(0, 0, -4f);
            Vector3 floorAtChair = new(cp.x, f.O.y + 0.012f, cp.z);
            Prims.Quad(env, "Chao_Queimado", floorAtChair, new Vector2(3.4f, 3.4f),
                SinisterTex.Mat("scorch", SinisterTex.Scorch(), Color.white, SinisterTex.Blend.Multiply), Vector3.up, f.D(0, 0, 1));

            // Dias riscados atras da cadeira, maos arrastadas na parede da frente, arranhoes perto da porta.
            Decal(env, f, Side.Back, 1.15f, 1.55f, new Vector2(2.2f, 1.1f), SinisterTex.Tally(7, 97), "Dias_Riscados");
            Decal(env, f, Side.Right, -1.4f, 1.25f, new Vector2(0.8f, 0.8f), SinisterTex.Claws(9), "Arranhoes");
            Decal(env, f, Side.Back, -4.6f, 0.45f, new Vector2(1.3f, 1.3f), SinisterTex.Mold(5), "Mofo_Rodape");
            Leaks(env, f, 2, 0.9f);

            // Testemunhas: aparecem nas cadeiras atras do vidro quando ninguem esta olhando.
            SeatGhosts(env, f, new[] { "Cadeira_Testemunha" }, 0.49f, 1.3f, 0f, 4);

            // O condenado na cadeira: so aparece no escuro de uma queda de luz (NoirDread).
            if (chair != null)
            {
                Transform holder = new GameObject("Condenado").transform;
                holder.SetParent(env, false);
                GameObject ghost = SeatedFigure(holder, chair.position, Quaternion.LookRotation(f.D(0, 0, 1)), 0.62f, 1.05f, true);
                NoirChairGhost cg = holder.gameObject.AddComponent<NoirChairGhost>();
                cg.figure = ghost;
                ghost.SetActive(false);
            }

            GroundFog(env, f, new Color(0.35f, 0.55f, 0.55f, 0.06f), 22);
        }

        // ------------------------------------------------------------------ P3: reparticao

        private static void SinisterArchive(Transform cena, Transform env, Frame f, NoirRoomMood mood)
        {
            if (mood != null)
            {
                Darken(mood, 0.88f, 0.55f, 1.2f);
                mood.saturation -= 8f; mood.contrast += 16f; mood.vignette = 0.4f;
                mood.splitShadows = new Color(0.38f, 0.48f, 0.42f); mood.splitHighlights = new Color(0.6f, 0.6f, 0.48f); mood.splitBalance = -20f;
                mood.lift = new Vector4(1f, 1.02f, 1f, -0.008f);
                mood.chromatic = 0.12f;
            }

            // Uma fluorescente queimada, uma falhando muito e uma boa: a sala vira manchas de luz e breu.
            Transform dead = env.Find("Fluorescente_0");
            if (dead != null)
            {
                dead.gameObject.SetActive(false);
                Transform tube = FindNear(env, "Tubo", dead.position, 0.6f);
                if (tube != null) tube.GetComponent<Renderer>().sharedMaterial = EnvMat.Glow(null, new Color(0.82f, 1f, 0.94f), 0.05f);
            }

            Transform bad = env.Find("Fluorescente_2");
            if (bad != null && bad.TryGetComponent(out NoirLightFlicker fl)) { fl.dropoutChance = 1.4f; fl.amount = 0.12f; }
            foreach (Transform t in Children(env, "Tubo"))
            {
                if (dead != null && Vector3.Distance(t.position, dead.position) < 0.6f) continue;
                Cone(env, t.position, 0.4f, 2.6f, f.ceil - 0.2f, new Color(0.8f, 1f, 0.95f, 0.07f));
            }

            Leaks(env, f, 3, 1f);
            Decal(env, f, Side.Front, 4.6f, f.ceil - 0.7f, new Vector2(1.6f, 1.5f), SinisterTex.Mold(6), "Mofo_Teto");
            Decal(env, f, Side.Back, -5.0f, 0.7f, new Vector2(1.5f, 1.5f), SinisterTex.Mold(8), "Mofo_Rodape");

            // Gente esperando no banco que nao esta ali.
            Transform bench = Deep(env, "Banco_Espera");
            if (bench != null)
            {
                List<Transform> ghosts = new();
                foreach (float x in new[] { -0.8f, 0.05f, 0.85f })
                    ghosts.Add(SeatedFigure(env, bench.TransformPoint(new Vector3(x, 0f, 0.02f)), bench.rotation, 0.64f, 1.05f, false).transform);
                AddGhosts(env, ghosts, 2);
            }

            MaintenanceBoxLight(cena, f);

            GroundFog(env, f, new Color(0.45f, 0.5f, 0.48f, 0.06f), 24);
        }

        /// <summary>Lampada dentro do compartimento de manutencao: sem ela a manivela some no breu da sala.</summary>
        private static void MaintenanceBoxLight(Transform cena, Frame f)
        {
            Transform box = Deep(cena, "Compartimento_Manutencao");
            if (box == null) return;

            Vector3 pos = box.position;
            Renderer[] parts = box.GetComponentsInChildren<Renderer>(true);
            if (parts.Length > 0)
            {
                Bounds b = parts[0].bounds;
                for (int i = 1; i < parts.Length; i++) b.Encapsulate(parts[i].bounds);
                pos = b.center;
            }

            // Afasta a luz da parede, em direcao ao centro da sala.
            Vector3 toRoom = f.O - pos;
            toRoom.y = 0f;
            if (toRoom.sqrMagnitude > 0.0001f) pos += toRoom.normalized * 0.25f;

            Light lamp = Prims.MakeLight(box, "Luz_Compartimento", pos, LightType.Point, new Color(1f, 0.86f, 0.62f), 0.25f, 1.2f, false);
            lamp.enabled = false;
            // So acende quando a trava de discos e resolvida (o compartimento abre e a manivela precisa aparecer).
            lamp.gameObject.AddComponent<NoirLightWhenSolved>().Init(lamp, UnityEngine.Object.FindFirstObjectByType<ProjetoCarta.Puzzles.DialLockMechanism>(FindObjectsInactive.Include));
        }

        // ------------------------------------------------------------------ pecas

        private static void Darken(NoirRoomMood m, float ambient, float fog, float fogDensity)
        {
            m.ambientSky *= ambient; m.ambientEquator *= ambient; m.ambientGround *= ambient;
            m.fogColor *= fog; m.fogDensity *= fogDensity;
        }

        private static void Scale(Transform env, string light, float k)
        {
            Transform t = env.Find(light);
            if (t != null && t.TryGetComponent(out Light l)) l.intensity *= k;
        }

        private static List<Transform> Children(Transform env, string name)
        {
            List<Transform> found = new();
            foreach (Transform t in env)
                if (t.name == name) found.Add(t);
            return found;
        }

        private static Transform FindNear(Transform env, string name, Vector3 pos, float radius)
        {
            foreach (Transform t in env)
                if (t.name == name && Vector3.Distance(t.position, pos) < radius) return t;
            return null;
        }

        /// <summary>Cone de luz aditivo na neblina, da lampada para baixo.</summary>
        private static void Cone(Transform parent, Vector3 top, float r0, float r1, float height, Color c)
        {
            // Shader volumetrico (borda, superficies e camera atenuadas); sem ele, o aditivo simples.
            Shader volume = Resources.Load<Shader>("NoirEnv/NoirVolumeCone");
            Material m;
            if (volume != null)
            {
                m = new Material(volume) { name = "NoirCone" };
                m.SetColor("_Color", c);
            }
            else
            {
                m = SinisterTex.Mat("beam", SinisterTex.Beam(), new Color(c.r, c.g, c.b, c.a * 0.3f), SinisterTex.Blend.Additive);
            }

            GameObject go = Prims.LatheObj(parent, "Cone_Luz", top,
                new[] { new Vector2(r0, 0f), new Vector2(Mathf.Lerp(r0, r1, 0.5f), -height * 0.5f), new Vector2(r1, -height) }, m);
            Renderer r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>Quad colado na parede (por dentro da sala), multiplicando a cor dela.</summary>
        private static GameObject Decal(Transform env, Frame f, Side wall, float along, float y, Vector2 size, Texture2D tex,
            string name)
        {
            const float gap = 0.035f;
            Vector3 pos, inward;
            switch (wall)
            {
                case Side.Back: pos = f.P(along, y, f.hz - gap); inward = f.D(0, 0, -1); break;
                case Side.Front: pos = f.P(along, y, -f.hz + gap); inward = f.D(0, 0, 1); break;
                case Side.Left: pos = f.P(-f.hx + gap, y, along); inward = f.D(1, 0, 0); break;
                default: pos = f.P(f.hx - gap, y, along); inward = f.D(-1, 0, 0); break;
            }

            return Prims.Quad(env, name, pos, size, SinisterTex.Mat(tex.name, tex, Color.white, SinisterTex.Blend.Multiply), inward);
        }

        /// <summary>Manchas de infiltracao descendo do teto em pontos das quatro paredes.</summary>
        private static void Leaks(Transform env, Frame f, int seed, float amount)
        {
            System.Random rng = new(seed * 31);
            Side[] walls = { Side.Back, Side.Left, Side.Right, Side.Front, Side.Back, Side.Left, Side.Right };
            int count = Mathf.RoundToInt(walls.Length * amount);
            for (int i = 0; i < count; i++)
            {
                Side w = walls[i];
                float span = w == Side.Back || w == Side.Front ? f.hx : f.hz;
                float along = ((float)rng.NextDouble() * 2f - 1f) * (span - 1.2f);
                float width = 1.4f + (float)rng.NextDouble() * 1.4f;
                float height = 1.8f + (float)rng.NextDouble() * 1.4f;
                Decal(env, f, w, along, f.ceil - height * 0.5f - 0.02f, new Vector2(width, height), SinisterTex.Leak(seed * 10 + i % 3), "Infiltracao");
            }
        }

        /// <summary>Neblina rasteira: manchas horizontais grandes e quase transparentes, deslizando devagar no chao.</summary>
        private static void GroundFog(Transform env, Frame f, Color c, int count)
        {
            GameObject go = new("Neblina_Rasteira");
            go.transform.SetParent(env, true);
            go.transform.position = f.P(0, 0.35f, 0);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 28f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.6f, 4.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = c;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = count / 22f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(f.hx * 1.8f, 0.5f, f.hz * 1.8f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f); vel.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            var col = ps.colorOverLifetime; col.enabled = true;
            Gradient g = new();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = SinisterTex.Mat("puff", SinisterTex.Puff(), Color.white, SinisterTex.Blend.Alpha);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 5f;
            r.sortingFudge = 10f;
            ps.Play();
        }

        // ------------------------------------------------------------------ vultos sentados

        private static void SeatGhosts(Transform env, Frame f, string[] seatNames, float seatHeight, float seatScale, float back, int maxVisible)
        {
            List<Transform> ghosts = new();
            foreach (Transform t in env.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(seatNames, t.name) < 0) continue;
                Vector3 seat = t.position + t.rotation * new Vector3(0f, seatHeight * seatScale, -back);
                ghosts.Add(SeatedFigure(env, seat - Vector3.up * seatHeight * seatScale, t.rotation, seatHeight * seatScale, 1.05f, false).transform);
            }

            AddGhosts(env, ghosts, maxVisible);
        }

        private static void AddGhosts(Transform env, List<Transform> ghosts, int maxVisible)
        {
            if (ghosts.Count == 0) return;
            NoirSeatedGhosts sg = new GameObject("Vultos_Sentados").AddComponent<NoirSeatedGhosts>();
            sg.transform.SetParent(env, false);
            sg.ghosts = ghosts;
            sg.maxVisible = maxVisible;

            // Contraluz: atras do centro do grupo, na altura dos ombros.
            Vector3 center = Vector3.zero, back = Vector3.zero;
            foreach (Transform g in ghosts) { center += g.position; back -= g.forward; }
            center /= ghosts.Count;
            back = back.sqrMagnitude > 0.001f ? back.normalized : Vector3.back;
            float spread = 0f;
            foreach (Transform g in ghosts) spread = Mathf.Max(spread, Vector3.Distance(g.position, center));
            sg.backlight = Prims.MakeLight(env, "Contraluz_Vultos", center + back * 0.35f + Vector3.up * 1.6f, LightType.Point,
                new Color(1f, 0.62f, 0.38f), 0.6f, 3.2f + spread, false);
        }

        /// <summary>
        /// Figura sentada, preta e um pouco grande demais: tronco curvado, cabeca pendendo, bracos compridos.
        /// <paramref name="floor"/> e o chao abaixo do assento; frente = rot * +z.
        /// </summary>
        private static GameObject SeatedFigure(Transform parent, Vector3 floor, Quaternion rot, float seatH, float k, bool strapped)
        {
            Material black = EnvMat.Lit(null, Vector2.one, new Color(0.012f, 0.012f, 0.014f), 0.05f);
            GameObject root = new("Vulto_Sentado");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(floor, rot);

            void Part(PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Transform p = null)
            {
                GameObject g = GameObject.CreatePrimitive(type);
                Object.Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(p ?? root.transform, false);
                g.transform.localPosition = pos;
                g.transform.localRotation = Quaternion.Euler(euler);
                g.transform.localScale = scale;
                Renderer r = g.GetComponent<Renderer>();
                r.sharedMaterial = black;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            float s = seatH;
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Capsule, new Vector3(side * 0.12f * k, s + 0.08f, 0.18f * k), new Vector3(0.16f, 0.26f, 0.16f) * k, new Vector3(90f, 0f, 0f));
                Part(PrimitiveType.Capsule, new Vector3(side * 0.13f * k, s * 0.5f, 0.42f * k), new Vector3(0.12f * k, s * 0.5f, 0.12f * k), new Vector3(-6f, 0f, 0f));
                // Bracos: no apoio da cadeira (amarrado) ou caidos sobre as coxas.
                if (strapped)
                    Part(PrimitiveType.Capsule, new Vector3(side * 0.3f * k, s + 0.2f, 0.1f * k), new Vector3(0.09f, 0.24f, 0.09f) * k, new Vector3(80f, 0f, 0f));
                // Os vultos soltos (nao amarrados) ficam sem bracos.
            }

            Part(PrimitiveType.Capsule, new Vector3(0f, s + 0.42f * k, -0.05f * k), new Vector3(0.44f, 0.36f, 0.26f) * k, new Vector3(strapped ? -4f : 10f, 0f, 0f));
            Transform head = new GameObject("Cabeca").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, s + 0.86f * k, 0.02f * k);
            head.localRotation = Quaternion.Euler(strapped ? 8f : 22f, 0f, strapped ? 0f : -9f);
            Part(PrimitiveType.Sphere, new Vector3(0f, 0.11f * k, 0.03f * k), new Vector3(0.24f, 0.3f, 0.26f) * k, Vector3.zero, head);
            if (strapped)
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.27f * k, 0.02f * k), new Vector3(0.3f, 0.05f, 0.3f) * k, Vector3.zero, head);
            return root;
        }
    }

    /// <summary>Luz que fica apagada ate a trava de discos ser resolvida (sem trava na cena, fica apagada).</summary>
    public sealed class NoirLightWhenSolved : MonoBehaviour
    {
        private Light lamp;
        private ProjetoCarta.Puzzles.DialLockMechanism lockMechanism;

        public void Init(Light light, ProjetoCarta.Puzzles.DialLockMechanism mechanism)
        {
            lamp = light;
            lockMechanism = mechanism;
        }

        private void Update()
        {
            if (lamp != null)
                lamp.enabled = lockMechanism != null && lockMechanism.IsSolved;
        }
    }

    /// <summary>Guarda o vulto do condenado na cadeira de P2 para o NoirDread revelar numa queda de luz.</summary>
    public sealed class NoirChairGhost : MonoBehaviour
    {
        public static NoirChairGhost Current { get; private set; }
        public GameObject figure;

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
            if (figure != null) figure.SetActive(false);
        }
    }
}
