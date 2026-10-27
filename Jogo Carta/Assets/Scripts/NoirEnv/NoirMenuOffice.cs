using System.Collections.Generic;
using ProjetoCarta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjetoCarta.NoirEnv
{
    /// <summary>
    /// Escritorio de detetive do menu principal, montado em tempo de execucao sobre a geometria de prototipo
    /// do MainMenu (paredes, quadro do caso e quadros de opcoes): papel de parede, lambri, cortica com moldura,
    /// quadro de investigacao (evidencias pequenas ligadas por fio vermelho em volta da foto do caso), luz de
    /// persiana e de abajur, mesa em primeiro plano e o diorama 3D que aparece na foto do caso, com a lampada que
    /// pisca e queima quando o caso e aberto (<see cref="CaseFileLauncher"/>).
    /// Nada na cena precisa ser editado; segue o mesmo padrao de <see cref="NoirEnvironment"/>.
    /// </summary>
    public static class NoirMenuOffice
    {
        private const string RootName = "NoirMenuOffice";

        private static readonly Color BulbColor = new(1f, 0.74f, 0.45f);
        private static Texture2D corkTexture;
        private static readonly Texture2D[] noteTextures = new Texture2D[3];
        private static readonly Dictionary<PaperKind, Texture2D> paperTextures = new();
        // Textos das evidencias (PT e o componente): traduzidos na criacao e de novo quando o idioma muda.
        private static readonly List<BoardLabel> boardTexts = new();
        private static bool listeningLanguage;

        public static void Build()
        {
            GameObject environment = GameObject.Find("Environment");
            if (environment == null || GameObject.Find(RootName) != null)
                return;

            Transform room = environment.transform.Find("FirstRoom");
            if (room == null)
                return;

            // Luzes de prototipo (spots brancos e direcional) dao lugar a iluminacao do escritorio.
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                l.enabled = false;

            // Montado inativo: o clima (NoirRoomMood) e as cintilacoes leem os valores so ao ativar.
            GameObject rootGo = new(RootName);
            rootGo.SetActive(false);
            Transform env = rootGo.transform;

            Transform walls = room.Find("Paredes Sala");
            Renderer front = R(walls, "Cube");
            Renderer floor = R(walls, "Cube (1)");
            Renderer west = R(walls, "Cube (2)");
            Renderer back = R(walls, "Cube (3)");
            Renderer lampCube = R(walls, "Cube (6)");
            if (lampCube != null)
                lampCube.enabled = false;

            if (front != null && floor != null)
                Shell(env, front, floor, west, back);

            Vector3 roomCenter = floor != null ? floor.bounds.center : new Vector3(-4f, 0f, -25f);
            Renderer caseBoard = R(room, "Board/Visual/Cube");
            if (caseBoard != null)
            {
                CorkBoard(env, caseBoard, roomCenter);
                CaseBoardDecor(env, caseBoard, room.Find("Board/Fotos/Foto 1"), room.Find("Board/Canvas"));
            }

            Transform options = room.Find("Options");
            if (options != null)
            {
                foreach (MeshRenderer r in options.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.name.Contains("Board"))
                        continue;
                    CorkBoard(env, r, roomCenter);
                    BoardLight(env, r, roomCenter);
                }
            }

            Lights(env, caseBoard);
            if (front != null && floor != null)
                ForegroundDesk(env, floor.bounds.max.y);

            Prims.Dust(env, "Poeira", new Vector3(-3f, 1.5f, -8f), new Vector3(14f, 6f, 9f), new Color(1f, 0.85f, 0.6f, 0.45f), 80, 0.05f);

            NoirRoomMood mood = rootGo.AddComponent<NoirRoomMood>();
            mood.ambientSky = new Color(0.16f, 0.13f, 0.11f) * 5.5f;
            mood.ambientEquator = new Color(0.12f, 0.09f, 0.07f) * 5.5f;
            mood.ambientGround = new Color(0.05f, 0.04f, 0.03f) * 5.5f;
            mood.fogColor = new Color(0.08f, 0.06f, 0.05f);
            mood.fogDensity = 0.012f;
            mood.postExposure = 1.4f;
            mood.saturation = -18f;
            mood.contrast = 16f;
            mood.colorFilter = new Color(1f, 0.93f, 0.82f);
            mood.vignette = 0.16f;
            mood.bloom = 0.8f;
            mood.bloomThreshold = 0.85f;
            mood.bloomTint = new Color(1f, 0.8f, 0.55f);

            rootGo.SetActive(true);
            RefreshBoardTexts();
            BuildCaseDiorama();
        }

        // ------------------------------------------------------------------ sala

        private static void Shell(Transform env, Renderer front, Renderer floor, Renderer west, Renderer back)
        {
            Color wallTint = new(0.56f, 0.48f, 0.41f);
            foreach (Renderer wall in new[] { front, west, back })
            {
                if (wall == null) continue;
                Vector3 s = wall.bounds.size;
                float len = Mathf.Max(s.x, s.z);
                wall.sharedMaterial = EnvMat.Lit("env_wallpaper", new Vector2(len / 1.6f, s.y / 1.6f), wallTint, 0.1f);
            }

            Vector3 fs = floor.bounds.size;
            floor.sharedMaterial = EnvMat.Lit("env_wood_floor", new Vector2(fs.x / 2.5f, fs.z / 2.5f), new Color(0.6f, 0.48f, 0.4f), 0.32f);

            float floorY = floor.bounds.max.y;
            const float wainscot = 2.1f;
            Material trim = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.24f, 0.16f, 0.11f), 0.35f);

            Bounds fb = front.bounds;
            float frontZ = fb.min.z;
            Material panelX = EnvMat.Lit("env_wood_panel", new Vector2(fb.size.x / 1.4f, 1.4f), new Color(0.44f, 0.31f, 0.22f), 0.3f);
            Prims.Box(env, "Lambril_Frente", new Vector3(fb.center.x, floorY + wainscot * 0.5f, frontZ - 0.012f), new Vector3(fb.size.x, wainscot, 0.02f), panelX, false, null, false);
            Prims.Box(env, "Friso_Frente", new Vector3(fb.center.x, floorY + wainscot, frontZ - 0.035f), new Vector3(fb.size.x, 0.1f, 0.07f), trim);
            Prims.Box(env, "Rodape_Frente", new Vector3(fb.center.x, floorY + 0.13f, frontZ - 0.035f), new Vector3(fb.size.x, 0.26f, 0.07f), trim);

            if (west != null)
            {
                Bounds wb = west.bounds;
                float westX = wb.max.x;
                Material panelZ = EnvMat.Lit("env_wood_panel", new Vector2(wb.size.z / 1.4f, 1.4f), new Color(0.44f, 0.31f, 0.22f), 0.3f);
                Prims.Box(env, "Lambril_Oeste", new Vector3(westX + 0.012f, floorY + wainscot * 0.5f, wb.center.z), new Vector3(0.02f, wainscot, wb.size.z), panelZ, false, null, false);
                Prims.Box(env, "Friso_Oeste", new Vector3(westX + 0.035f, floorY + wainscot, wb.center.z), new Vector3(0.07f, 0.1f, wb.size.z), trim);
                Prims.Box(env, "Rodape_Oeste", new Vector3(westX + 0.035f, floorY + 0.13f, wb.center.z), new Vector3(0.07f, 0.26f, wb.size.z), trim);
            }
        }

        /// <summary>Direcao para onde a face visivel do quadro aponta (eixo fino, virado para o centro da sala).</summary>
        private static Vector3 FaceNormal(Bounds b, Vector3 roomCenter)
        {
            bool facesZ = b.size.z < b.size.x;
            Vector3 toRoom = roomCenter - b.center;
            return facesZ ? new Vector3(0f, 0f, Mathf.Sign(toRoom.z)) : new Vector3(Mathf.Sign(toRoom.x), 0f, 0f);
        }

        private static void CorkBoard(Transform env, Renderer board, Vector3 roomCenter)
        {
            Bounds b = board.bounds;
            Vector3 normal = FaceNormal(b, roomCenter);
            bool facesZ = normal.z != 0f;
            float width = facesZ ? b.size.x : b.size.z;
            board.sharedMaterial = EnvMat.LitTexture("cork", Cork(), new Vector2(width / 1.2f, b.size.y / 1.2f), new Color(0.95f, 0.88f, 0.8f), 0.08f);

            // Moldura de madeira na face visivel.
            Material frame = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.3f, 0.2f, 0.13f), 0.4f);
            const float t = 0.14f, depth = 0.1f;
            float halfThin = facesZ ? b.extents.z : b.extents.x;
            Vector3 face = b.center + normal * (halfThin + depth * 0.5f - 0.02f);
            Vector3 along = facesZ ? Vector3.right : Vector3.forward;
            Vector3 thin = facesZ ? new Vector3(0f, 0f, depth) : new Vector3(depth, 0f, 0f);
            Vector3 horizontal = along * (width + t * 2f) + Vector3.up * t + thin;
            Vector3 vertical = along * t + Vector3.up * b.size.y + thin;
            Prims.Box(env, "Moldura_Topo", face + Vector3.up * (b.size.y + t) * 0.5f, Abs(horizontal), frame);
            Prims.Box(env, "Moldura_Base", face - Vector3.up * (b.size.y + t) * 0.5f, Abs(horizontal), frame);
            Prims.Box(env, "Moldura_A", face + along * (width + t) * 0.5f, Abs(vertical), frame);
            Prims.Box(env, "Moldura_B", face - along * (width + t) * 0.5f, Abs(vertical), frame);
        }

        private static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private enum PaperKind { Typed, RedBand, Newspaper, IndexCard, Tag, Mugshot, Map, Snapshot, Sticky, Strip }

        private struct BoardLabel
        {
            public TextMeshPro text;
            public string pt;
            public TMP_FontAsset font;
            public Color ink;
            public Vector2 size;
            public float maxSize;
        }

        /// <summary>Uma evidencia do quadro: papel com textura gerada, texto opcional e percevejo.</summary>
        private struct Evidence
        {
            public string name;
            public Vector2 center;      // x, y no mundo (o quadro olha para -z)
            public Vector2 size;
            public float angle;
            public PaperKind kind;
            public string text;         // PT (traduzido pelo Loc); vazio = so o papel
            public bool handwritten;
            public Rect textArea;       // fracoes do papel (0..1), origem embaixo a esquerda
            public Color ink;

            public Evidence(string name, float x, float y, float w, float h, float angle, PaperKind kind,
                string text = null, bool handwritten = false, Rect? textArea = null, Color? ink = null)
            {
                this.name = name;
                center = new Vector2(x, y);
                size = new Vector2(w, h);
                this.angle = angle;
                this.kind = kind;
                this.text = text;
                this.handwritten = handwritten;
                this.textArea = textArea ?? new Rect(0.08f, 0.1f, 0.84f, 0.8f);
                this.ink = ink ?? new Color(0.13f, 0.11f, 0.1f);
            }
        }

        /// <summary>
        /// Quadro de investigacao em volta da foto do caso: ficha do detento, recortes, ficha da testemunha, etiqueta
        /// de evidencia, planta, outra foto, bilhetes e datas, presos com percevejo e ligados por fio vermelho. Tudo
        /// pequeno e sobreposto como num quadro real; o texto e parcial (insinua o caso, nao conta a historia).
        /// A foto do caso e a unica coisa clicavel: abre o caso (<see cref="CaseFileLauncher"/>).
        /// </summary>
        private static void CaseBoardDecor(Transform env, Renderer board, Transform photo, Transform canvas)
        {
            Bounds b = board.bounds;
            float z = b.min.z - 0.012f;
            boardTexts.Clear();
            Material pin = EnvMat.Lit(null, Vector2.one, new Color(0.8f, 0.1f, 0.08f), 0.75f);
            Material pinBrass = EnvMat.Lit(null, Vector2.one, new Color(0.78f, 0.62f, 0.3f), 0.8f, 0.9f);
            Material stringM = EnvMat.Lit(null, Vector2.one, new Color(0.78f, 0.08f, 0.06f), 0.25f);

            // Fontes que a cena ja usa: datilografada (ficha do caso) e manuscrita (legenda da foto).
            TMP_FontAsset typed = FontOf(canvas, "Text (TMP) (1)");
            TMP_FontAsset hand = FontOf(photo, "Canvas/Legenda") ?? typed;

            // Foto do caso: percevejo no alto, que acompanha a foto quando ela levanta no hover.
            Renderer photoVisual = photo != null && photo.Find("Visual") != null ? photo.Find("Visual").GetComponent<Renderer>() : null;
            Bounds pb = photoVisual != null ? photoVisual.bounds : new Bounds(new Vector3(0.3f, 0.6f, z), new Vector3(0.97f, 1.13f, 0.05f));
            Vector3 photoTop = new(pb.center.x, pb.max.y - 0.07f, pb.min.z - 0.03f);
            Vector3 photoBottom = new(pb.max.x - 0.07f, pb.min.y + 0.06f, pb.min.z - 0.03f); // canto, longe da legenda
            GameObject photoPin = Prims.Ball(env, "Percevejo_Foto", photoTop, 0.07f, pin, true);
            if (photo != null)
                photoPin.transform.SetParent(photo, true);

            Color red = new(0.55f, 0.1f, 0.08f);
            Evidence[] items =
            {
                new("Ficha_Detento", -0.95f, 1.6f, 0.52f, 0.68f, 4f, PaperKind.Mugshot, "Nº 4471 · T. FERRAZ", false, new Rect(0.06f, 0.02f, 0.88f, 0.16f)),
                new("Recorte_Jornal", -1.7f, 0.4f, 1.02f, 0.64f, -5f, PaperKind.Newspaper, "CADEIRA ELÉTRICA: EXECUÇÃO À MEIA-NOITE", false,
                    new Rect(0.07f, 0.8f, 0.86f, 0.13f), new Color(0.9f, 0.87f, 0.78f)),
                new("Recorte_Pequeno", -0.6f, -0.3f, 0.55f, 0.42f, 7f, PaperKind.Newspaper),
                new("Bilhete_Datilografado", 1.55f, 1.05f, 0.62f, 0.78f, 5f, PaperKind.Typed),
                new("Data", 1.45f, 1.97f, 0.95f, 0.17f, -2f, PaperKind.Strip, "20 · NOV · 1958", false, new Rect(0.05f, 0.08f, 0.9f, 0.84f)),
                new("Etiqueta_Evidencia", 0.2f, -0.78f, 0.52f, 0.32f, -8f, PaperKind.Tag, "EVIDÊNCIA 12\nrelógio da câmara\nparado às 23:58", false,
                    new Rect(0.22f, 0.1f, 0.72f, 0.8f)),
                new("Ficha_Testemunha", 1.6f, -0.55f, 0.78f, 0.5f, 3f, PaperKind.IndexCard, "TESTEMUNHA ÚNICA\ndepoimento contestado", false,
                    new Rect(0.07f, 0.12f, 0.86f, 0.62f)),
                new("Bilhete_Amarelo", 2.5f, -0.78f, 0.45f, 0.45f, 6f, PaperKind.Sticky, "Quem ligou\nàs 23:52?", true, new Rect(0.08f, 0.1f, 0.84f, 0.8f), red),
                new("Ficha_Vermelha", 3.42f, -0.85f, 0.7f, 0.5f, -3f, PaperKind.RedBand),
                new("Foto_Camara", 4.45f, -0.45f, 0.62f, 0.58f, -4f, PaperKind.Snapshot, "câmara, 21/11", true, new Rect(0.06f, 0.02f, 0.88f, 0.17f)),
                new("Planta", -1.75f, -0.85f, 0.9f, 0.62f, 2f, PaperKind.Map, "câmara · pátio · guarita", true, new Rect(0.06f, 0.03f, 0.88f, 0.16f)),
                // Papeis sem texto nas bordas: o quadro fica cheio sem virar uma parede de leitura.
                new("Folha_Canto", -2.15f, 2.05f, 0.6f, 0.8f, -6f, PaperKind.Typed),
                new("Ficha_Topo", 0.2f, 2.2f, 0.62f, 0.42f, 3f, PaperKind.IndexCard),
                new("Recorte_Baixo", 0.95f, -1.15f, 0.62f, 0.4f, -4f, PaperKind.Newspaper),
                new("Ficha_Canto", 4.75f, -1.15f, 0.55f, 0.4f, 5f, PaperKind.RedBand),
                new("Foto_Testemunha", -2.35f, -0.05f, 0.42f, 0.52f, 8f, PaperKind.Mugshot),
            };

            Dictionary<string, Vector3> pins = new() { { "Foto", photoTop } };
            for (int i = 0; i < items.Length; i++)
            {
                Evidence e = items[i];
                // Cada papel um pouco a frente do anterior: sobreposicoes sem z-fighting.
                Vector3 pos = new(e.center.x, e.center.y, z - 0.003f - i * 0.0025f);
                GameObject paper = Prims.Quad(env, "Evidencia_" + e.name, pos, e.size,
                    EnvMat.LitTexture("evidencia_" + e.kind, PaperTexture(e.kind), Vector2.one, new Color(0.92f, 0.88f, 0.8f), 0.05f), Vector3.back);
                paper.transform.Rotate(0f, 0f, e.angle, Space.Self);

                if (!string.IsNullOrEmpty(e.text))
                    BoardText(env, paper.transform, e, e.handwritten ? hand : typed);

                Vector3 pinPos = paper.transform.TransformPoint(new Vector3(0f, 0.5f - 0.06f / e.size.y, 0f)) + Vector3.back * 0.03f;
                Prims.Ball(env, "Percevejo_" + e.name, pinPos, 0.065f, i % 3 == 1 ? pinBrass : pin, true);
                pins[e.name] = pinPos;
            }

            // Fio vermelho: da foto para o que importa, e entre evidencias relacionadas (nunca cruza a foto).
            string[,] links =
            {
                { "Foto", "Ficha_Detento" }, { "Foto", "Recorte_Jornal" }, { "Foto", "Bilhete_Datilografado" },
                { "Ficha_Testemunha", "Etiqueta_Evidencia" }, { "Ficha_Testemunha", "Bilhete_Amarelo" },
                { "Bilhete_Amarelo", "Foto_Camara" }, { "Recorte_Jornal", "Planta" }, { "Bilhete_Datilografado", "Data" }
            };
            for (int i = 0; i < links.GetLength(0); i++)
                RedString(env, "Fio_" + links[i, 0] + "_" + links[i, 1], pins[links[i, 0]], pins[links[i, 1]], stringM);
            RedString(env, "Fio_Foto_Testemunha", photoBottom, pins["Ficha_Testemunha"], stringM);
            Prims.Ball(env, "Percevejo_Foto_Base", photoBottom, 0.05f, pinBrass, true).transform.SetParent(photo != null ? photo : env, true);

            if (!listeningLanguage)
            {
                listeningLanguage = true;
                Loc.Changed += _ => RefreshBoardTexts();
            }
        }

        private static TMP_FontAsset FontOf(Transform root, string path)
        {
            Transform t = root != null ? root.Find(path) : null;
            TMP_Text text = t != null ? t.GetComponent<TMP_Text>() : null;
            return text != null ? text.font : null;
        }

        /// <summary>Texto 3D sobre o papel, no mesmo angulo, um fio a frente; o tamanho se ajusta a area do papel.</summary>
        private static void BoardText(Transform env, Transform paper, Evidence e, TMP_FontAsset font)
        {
            GameObject go = new("Texto_" + e.name, typeof(RectTransform));
            go.transform.SetParent(env, false);
            Rect a = e.textArea;
            Vector3 local = new(a.center.x - 0.5f, a.center.y - 0.5f, 0f);
            go.transform.SetPositionAndRotation(paper.TransformPoint(local) + Vector3.back * 0.002f, paper.rotation);

            // O escritorio e montado inativo e o Awake do TMP (na ativacao) volta aos padroes de tamanho:
            // o layout do texto e aplicado em RefreshBoardTexts, depois da ativacao e a cada troca de idioma.
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            boardTexts.Add(new BoardLabel
            {
                text = tmp, pt = e.text, font = font, ink = e.ink,
                size = new Vector2(a.width * e.size.x, a.height * e.size.y),
                maxSize = e.handwritten ? 1.6f : 1.1f
            });
        }

        private static void RefreshBoardTexts()
        {
            foreach (BoardLabel label in boardTexts)
            {
                TextMeshPro tmp = label.text;
                if (tmp == null)
                    continue;

                if (label.font != null)
                    tmp.font = label.font;
                tmp.rectTransform.sizeDelta = label.size;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 0.05f;
                tmp.fontSizeMax = label.maxSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.color = label.ink;
                tmp.text = Loc.T(label.pt);
            }
        }

        private static void RedString(Transform env, string name, Vector3 a, Vector3 b, Material m)
        {
            Vector3 dir = b - a;
            Prims.Make(PrimitiveType.Cylinder, env, name, (a + b) * 0.5f, new Vector3(0.024f, dir.magnitude * 0.5f, 0.024f), m, false,
                Quaternion.FromToRotation(Vector3.up, dir), true);
        }

        private static void BoardLight(Transform env, Renderer board, Vector3 roomCenter)
        {
            Bounds b = board.bounds;
            Vector3 normal = FaceNormal(b, roomCenter);
            Vector3 target = b.center + Vector3.up * 0.4f;
            Prims.MakeLight(env, "Luz_Quadro_" + board.name, target + normal * 4.2f + Vector3.up * 3.4f, LightType.Spot,
                new Color(1f, 0.78f, 0.52f), 7f, 12f, false, target, 72f);
        }

        private static void Lights(Transform env, Renderer caseBoard)
        {
            Vector3 boardCenter = caseBoard != null ? caseBoard.bounds.center : new Vector3(1.2f, 0.6f, -3.2f);

            // Luz quente de cima sobre o quadro do caso, com tremida leve de lampada velha.
            Light key = Prims.MakeLight(env, "Luz_Quadro_Caso", boardCenter + new Vector3(-0.6f, 4.6f, -4.6f), LightType.Spot,
                new Color(1f, 0.76f, 0.48f), 9f, 16f, true, boardCenter + Vector3.down * 0.3f, 62f);
            NoirLightFlicker flicker = key.gameObject.AddComponent<NoirLightFlicker>();
            flicker.target = key;
            flicker.amount = 0.05f;
            flicker.speed = 1.4f;
            flicker.dropoutChance = 0.02f;

            // Luar entrando pela persiana (janela fora de quadro, a esquerda): listras frias na parede.
            Vector3 moonTarget = new(-4.5f, 0.4f, -3f);
            Light moon = Prims.MakeLight(env, "Luz_Persiana", new Vector3(-9.5f, 4.2f, -8.5f), LightType.Spot,
                new Color(0.55f, 0.68f, 0.95f), 30f, 20f, true, moonTarget, 78f, EnvMat.Tex("env_cookie_blinds"));
            moon.transform.rotation = Quaternion.LookRotation(moonTarget - moon.transform.position, Vector3.up);

            Prims.MakeLight(env, "Luz_Preenchimento", new Vector3(-3.6f, 2.5f, -9f), LightType.Point,
                new Color(1f, 0.62f, 0.38f), 0.8f, 12f, false);
        }

        /// <summary>Canto da mesa do detetive em primeiro plano (so a camera inicial ve).</summary>
        private static void ForegroundDesk(Transform env, float floorY)
        {
            const float top = -0.38f;
            Material wood = EnvMat.Lit("env_wood_panel", new Vector2(2f, 1f), new Color(0.3f, 0.19f, 0.12f), 0.45f);
            Material brass = EnvMat.Lit(null, Vector2.one, new Color(0.55f, 0.4f, 0.18f), 0.7f, 0.85f);
            Material manila = EnvMat.Lit(null, Vector2.one, new Color(0.72f, 0.6f, 0.4f), 0.15f);
            Material felt = EnvMat.Lit(null, Vector2.one, new Color(0.09f, 0.08f, 0.08f), 0.1f);

            Prims.Box(env, "Mesa_Tampo", new Vector3(-2.2f, top - 0.04f, -10.35f), new Vector3(5.2f, 0.08f, 1.5f), wood);
            Prims.Box(env, "Mesa_Corpo", new Vector3(-2.2f, (top + floorY) * 0.5f - 0.04f, -10.6f), new Vector3(5f, top - floorY, 1.2f), wood);

            // Abajur de banqueiro: luz quente sobre a mesa.
            Vector3 lamp = new(-1.35f, top, -10.15f);
            Prims.Box(env, "Abajur_Base", lamp + new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.04f, 0.2f), brass);
            Prims.Cyl(env, "Abajur_Haste", lamp + new Vector3(0f, 0.2f, 0.05f), new Vector3(0.025f, 0.18f, 0.025f), brass);
            Prims.Cyl(env, "Abajur_Cupula", lamp + new Vector3(0f, 0.4f, 0f), new Vector3(0.2f, 0.22f, 0.2f),
                EnvMat.Lit(null, Vector2.one, new Color(0.08f, 0.32f, 0.16f), 0.85f), Quaternion.Euler(0f, 0f, 90f));
            Prims.Box(env, "Abajur_Brilho", lamp + new Vector3(0f, 0.33f, 0f), new Vector3(0.36f, 0.015f, 0.12f),
                EnvMat.Glow(null, BulbColor, 1.6f), false, null, false);
            Light lampLight = Prims.MakeLight(env, "Luz_Abajur", lamp + new Vector3(0f, 0.28f, 0f), LightType.Point, BulbColor, 0.45f, 2.2f, false);
            NoirLightFlicker f = lampLight.gameObject.AddComponent<NoirLightFlicker>();
            f.target = lampLight; f.amount = 0.04f; f.speed = 2f;

            // Pilha de pastas e o chapeu.
            for (int i = 0; i < 3; i++)
                Prims.Box(env, "Pasta_" + i, new Vector3(-3.2f + i * 0.02f, top + 0.025f + i * 0.045f, -10.25f), new Vector3(0.62f, 0.04f, 0.45f),
                    manila, false, Quaternion.Euler(0f, -8f + i * 7f, 0f));

            Vector2[] hat =
            {
                new(0.01f, 0.2f), new(0.08f, 0.21f), new(0.13f, 0.17f), new(0.14f, 0.06f), new(0.15f, 0.03f),
                new(0.28f, 0.035f), new(0.3f, 0.05f)
            };
            GameObject fedora = Prims.LatheObj(env, "Chapeu", new Vector3(-4.9f, top + 0.01f, -10.3f), hat, felt);
            fedora.transform.rotation = Quaternion.Euler(4f, 20f, -3f);
        }

        // ------------------------------------------------------------------ diorama da foto do caso

        /// <summary>
        /// A foto do caso e uma imagem ao vivo da camera da "Level 1" (fora do mapa). O cubo de prototipo vira
        /// a camara da cadeira: lampada pendurada, cadeira de execucao, azulejos e o relogio parado em 23:58.
        /// </summary>
        private static void BuildCaseDiorama()
        {
            LevelStats level = Object.FindFirstObjectByType<LevelStats>(FindObjectsInactive.Include);
            if (level == null || level.levelCamera == null)
                return;

            GameObject lv = level.gameObject;
            if (lv.TryGetComponent(out Animator animator))
                animator.enabled = false;
            foreach (Transform child in lv.transform)
            {
                if (child != level.levelCamera.transform)
                    child.gameObject.SetActive(false);
            }
            lv.SetActive(true);

            int layer = LayerMask.NameToLayer("FirstLevel");
            if (layer < 0) layer = 0;

            Transform d = new GameObject("Diorama_Caso").transform;
            d.SetParent(lv.transform, false);
            Vector3 o = lv.transform.position;

            Material concrete = EnvMat.Lit("env_concrete", new Vector2(3f, 3f), new Color(0.32f, 0.33f, 0.32f), 0.25f);
            Material tiles = EnvMat.Lit("env_tiles", new Vector2(4f, 1.2f), new Color(0.5f, 0.62f, 0.56f), 0.5f);
            Material plaster = EnvMat.Lit("env_plaster", new Vector2(3f, 1.5f), new Color(0.42f, 0.44f, 0.4f), 0.1f);
            Material wood = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.4f, 0.27f, 0.17f), 0.35f);
            Material metal = EnvMat.Lit(null, Vector2.one, new Color(0.2f, 0.2f, 0.19f), 0.55f, 0.8f);
            Material leather = EnvMat.Lit(null, Vector2.one, new Color(0.08f, 0.06f, 0.05f), 0.3f);

            Prims.Box(d, "Piso", o + new Vector3(0f, -0.05f, 0.5f), new Vector3(8f, 0.1f, 6f), concrete);
            Prims.Box(d, "Azulejos", o + new Vector3(0f, 0.8f, 2.3f), new Vector3(8f, 1.6f, 0.2f), tiles);
            Prims.Box(d, "Parede", o + new Vector3(0f, 2.8f, 2.32f), new Vector3(8f, 2.4f, 0.2f), plaster);

            // Cadeira de execucao.
            Vector3 c = o + new Vector3(0f, 0f, 0.7f);
            Prims.Box(d, "Assento", c + new Vector3(0f, 0.52f, 0f), new Vector3(0.78f, 0.1f, 0.66f), wood);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.33f : 0.33f;
                float z = i < 2 ? -0.27f : 0.27f;
                Prims.Box(d, "Perna_" + i, c + new Vector3(x, 0.24f, z), new Vector3(0.09f, 0.48f, 0.09f), wood);
            }
            Prims.Box(d, "Encosto", c + new Vector3(0f, 1.12f, 0.3f), new Vector3(0.78f, 1.1f, 0.1f), wood);
            for (int side = -1; side <= 1; side += 2)
            {
                Prims.Box(d, "Braco", c + new Vector3(0.39f * side, 0.8f, -0.02f), new Vector3(0.1f, 0.07f, 0.62f), wood);
                Prims.Box(d, "Apoio", c + new Vector3(0.39f * side, 0.66f, -0.28f), new Vector3(0.08f, 0.22f, 0.08f), wood);
                Prims.Box(d, "Correia", c + new Vector3(0.39f * side, 0.82f, -0.05f), new Vector3(0.12f, 0.08f, 0.07f), leather);
            }
            Prims.Box(d, "Haste", c + new Vector3(0f, 1.82f, 0.38f), new Vector3(0.06f, 0.36f, 0.06f), metal);
            Prims.Box(d, "Braco_Capacete", c + new Vector3(0f, 1.98f, 0.22f), new Vector3(0.05f, 0.05f, 0.34f), metal);
            Prims.LatheObj(d, "Capacete", c + new Vector3(0f, 1.74f, 0.06f),
                new[] { new Vector2(0.01f, 0.2f), new Vector2(0.09f, 0.18f), new Vector2(0.14f, 0.1f), new Vector2(0.15f, 0f) }, metal);
            Prims.Cyl(d, "Cabo", c + new Vector3(0.25f, 0.95f, 0.9f), new Vector3(0.025f, 0.95f, 0.025f), leather, Quaternion.Euler(-20f, 0f, 8f));

            // Relogio parado em 23:58.
            Vector3 clock = o + new Vector3(1.35f, 2.3f, 2.2f);
            Prims.Cyl(d, "Relogio_Aro", clock, new Vector3(0.48f, 0.03f, 0.48f), metal, Quaternion.Euler(90f, 0f, 0f));
            Prims.Cyl(d, "Relogio_Mostrador", clock + Vector3.back * 0.035f, new Vector3(0.42f, 0.01f, 0.42f),
                EnvMat.Lit(null, Vector2.one, new Color(0.85f, 0.8f, 0.68f), 0.2f), Quaternion.Euler(90f, 0f, 0f));
            ClockHand(d, clock + Vector3.back * 0.05f, 1f, 0.11f, 0.022f, leather);    // horas: quase meia-noite
            ClockHand(d, clock + Vector3.back * 0.055f, 12f, 0.17f, 0.014f, leather);  // minutos: 58

            // Lampada pendurada sobre a cadeira: a mesma que queima quando o caso e aberto.
            Vector3 bulbPos = o + new Vector3(0f, 2.28f, 0.45f);
            Prims.Cyl(d, "Fio_Lampada", bulbPos + Vector3.up * 0.9f, new Vector3(0.012f, 0.8f, 0.012f), leather);
            Prims.LatheObj(d, "Cupula", bulbPos + Vector3.up * 0.06f,
                new[] { new Vector2(0.025f, 0.14f), new Vector2(0.06f, 0.12f), new Vector2(0.17f, 0.0f), new Vector2(0.2f, -0.03f) }, metal);
            GameObject bulb = Prims.Ball(d, "Lampada", bulbPos, 0.1f, EnvMat.Glow(null, BulbColor, 4f));
            Light bulbLight = Prims.MakeLight(d, "Luz_Lampada", bulbPos + Vector3.down * 0.06f, LightType.Point, BulbColor, 2.2f, 5.5f, true);
            bulbLight.shadowNearPlane = 0.05f;
            Light pool = Prims.MakeLight(d, "Luz_Cone", bulbPos + Vector3.down * 0.02f, LightType.Spot, BulbColor, 3.2f, 6f, true,
                bulbPos + Vector3.down * 2f, 95f);
            NoirLightFlicker idle = bulbLight.gameObject.AddComponent<NoirLightFlicker>();
            idle.target = bulbLight; idle.emissive = bulb.GetComponent<Renderer>(); idle.emissiveColor = BulbColor * 4f;
            idle.amount = 0.06f; idle.speed = 3f; idle.dropoutChance = 0.06f;
            // Rebatimento fraco vindo da frente: a cadeira le como silhueta; apaga junto com a lampada.
            Light bounce = Prims.MakeLight(d, "Luz_Rebatida", o + new Vector3(0.6f, 1.4f, -2.6f), LightType.Point,
                new Color(1f, 0.8f, 0.6f), 0.35f, 7f, false);

            Prims.Dust(d, "Poeira_Diorama", bulbPos + Vector3.down * 0.8f, new Vector3(2f, 1.8f, 1.5f), new Color(1f, 0.85f, 0.6f, 0.5f), 40, 0.03f);

            SetLayer(d.gameObject, layer);
            foreach (Light l in d.GetComponentsInChildren<Light>(true))
                l.cullingMask = 1 << layer;

            Camera cam = level.levelCamera;
            cam.transform.position = o + new Vector3(0.15f, 1.6f, -8.2f);
            cam.transform.rotation = Quaternion.LookRotation(o + new Vector3(0f, 1.72f, 0.6f) - cam.transform.position, Vector3.up);
            cam.fieldOfView = 21f;
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.012f);
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
                data.renderPostProcessing = true;

            CaseFileLauncher launcher = Object.FindFirstObjectByType<CaseFileLauncher>();
            if (launcher != null)
                launcher.SetBulb(new[] { bulbLight, pool, bounce }, bulb.GetComponent<Renderer>(), BulbColor * 4f);
        }

        private static void ClockHand(Transform parent, Vector3 center, float degrees, float length, float width, Material m)
        {
            Quaternion rot = Quaternion.Euler(0f, 0f, degrees);
            Prims.Box(parent, "Ponteiro", center + rot * (Vector3.up * length * 0.5f), new Vector3(width, length, 0.01f), m, false, rot, false);
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayer(child.gameObject, layer);
        }

        // ------------------------------------------------------------------ texturas geradas

        private static Texture2D Cork()
        {
            if (corkTexture != null)
                return corkTexture;

            const int n = 256;
            corkTexture = new Texture2D(n, n, TextureFormat.RGB24, true) { name = "NoirCork", wrapMode = TextureWrapMode.Repeat };
            System.Random rng = new(7);
            Color dark = new(0.33f, 0.21f, 0.12f), light = new(0.74f, 0.56f, 0.36f);
            Color[] px = new Color[n * n];
            float tau = Mathf.PI * 2f / n;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    // Variacao periodica (sem emenda) + graos aleatorios.
                    float v = 0.55f + 0.08f * Mathf.Sin(x * tau * 3f + 1.3f) * Mathf.Cos(y * tau * 2f + 0.4f)
                                    + 0.06f * Mathf.Sin((x + y) * tau * 7f) + ((float)rng.NextDouble() - 0.5f) * 0.45f;
                    if (rng.NextDouble() < 0.05) v -= 0.3f;
                    px[y * n + x] = Color.Lerp(dark, light, Mathf.Clamp01(v));
                }
            }

            corkTexture.SetPixels(px);
            corkTexture.Apply();
            return corkTexture;
        }

        /// <summary>Papeis do quadro: bilhete datilografado, ficha e recorte de jornal (linhas de "texto" falsas).</summary>
        private static Texture2D NoteTexture(int kind)
        {
            if (noteTextures[kind] != null)
                return noteTextures[kind];

            const int w = 256, h = 256;
            Texture2D t = new(w, h, TextureFormat.RGB24, true) { name = "NoirNote" + kind, wrapMode = TextureWrapMode.Clamp };
            System.Random rng = new(31 + kind);
            Color paper = kind == 2 ? new Color(0.86f, 0.83f, 0.74f) : new Color(0.95f, 0.92f, 0.82f);
            Color ink = new(0.18f, 0.16f, 0.15f);
            Color[] px = new Color[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                float stain = (float)rng.NextDouble() * 0.05f;
                px[i] = paper * (1f - stain);
            }

            int top = kind == 2 ? 40 : 34;
            if (kind == 2)
                Fill(px, w, 18, h - 44, w - 18, h - 18, ink * 1.1f); // manchete
            if (kind == 1)
                Fill(px, w, 0, h - 26, w, h - 22, new Color(0.62f, 0.15f, 0.13f)); // faixa vermelha da ficha

            int line = 0;
            for (int y = h - top - 12; y > 22; y -= kind == 2 ? 13 : 17)
            {
                int columns = kind == 2 ? 2 : 1;
                for (int col = 0; col < columns; col++)
                {
                    int x0 = 20 + col * (w / columns);
                    int x1 = x0 + (w / columns) - 40;
                    int x = x0;
                    while (x < x1)
                    {
                        int word = rng.Next(8, 30);
                        Fill(px, w, x, y, Mathf.Min(x + word, x1), y + (kind == 2 ? 5 : 6), ink * (0.9f + (float)rng.NextDouble() * 0.4f));
                        x += word + rng.Next(5, 9);
                        if (line % 5 == 4 && x > x0 + (x1 - x0) * 0.6f) break;
                    }
                }
                line++;
            }

            t.SetPixels(px);
            t.Apply();
            noteTextures[kind] = t;
            return t;
        }

        /// <summary>Papeis das evidencias do quadro (256x256, gerados uma vez). O texto legivel vem do TMP por cima.</summary>
        private static Texture2D PaperTexture(PaperKind kind)
        {
            switch (kind)
            {
                case PaperKind.Typed: return NoteTexture(0);
                case PaperKind.RedBand: return NoteTexture(1);
                case PaperKind.Newspaper: return NoteTexture(2);
            }

            if (paperTextures.TryGetValue(kind, out Texture2D cached) && cached != null)
                return cached;

            const int w = 256, h = 256;
            System.Random rng = new(57 + (int)kind);
            Color ink = new(0.18f, 0.16f, 0.15f);
            Color paper = kind switch
            {
                PaperKind.Tag => new Color(0.8f, 0.68f, 0.47f),
                PaperKind.Sticky => new Color(0.93f, 0.82f, 0.4f),
                PaperKind.Snapshot or PaperKind.Mugshot => new Color(0.94f, 0.92f, 0.86f),
                _ => new Color(0.95f, 0.92f, 0.83f)
            };
            Color[] px = new Color[w * h];
            for (int i = 0; i < px.Length; i++)
                px[i] = paper * (1f - (float)rng.NextDouble() * 0.05f);

            switch (kind)
            {
                case PaperKind.IndexCard:
                    for (int y = 30; y < 190; y += 26)
                        Fill(px, w, 0, y, w, y + 2, new Color(0.55f, 0.65f, 0.8f));
                    Fill(px, w, 0, 200, w, 203, new Color(0.75f, 0.25f, 0.22f));
                    break;
                case PaperKind.Tag:
                    Disc(px, w, 30, h / 2, 13, new Color(0.3f, 0.26f, 0.2f));
                    Ring(px, w, 30, h / 2, 19, 4, new Color(0.62f, 0.5f, 0.3f));
                    break;
                case PaperKind.Mugshot:
                {
                    // Fundo com regua de altura, silhueta e faixa clara embaixo para o numero.
                    Fill(px, w, 14, 52, w - 14, h - 14, new Color(0.36f, 0.4f, 0.43f));
                    for (int y = 64; y < h - 20; y += 18)
                        Fill(px, w, 14, y, w - 14, y + 2, new Color(0.5f, 0.55f, 0.58f));
                    Color shadow = new(0.1f, 0.1f, 0.11f);
                    Disc(px, w, w / 2, 160, 38, shadow);
                    Ellipse(px, w, w / 2, 60, 92, 50, shadow);
                    Fill(px, w, 14, 14, w - 14, 50, paper * 0.98f);
                    break;
                }
                case PaperKind.Snapshot:
                {
                    // Foto pequena da camara: azulejos, lampada e a cadeira em silhueta; borda branca.
                    Fill(px, w, 14, 52, w - 14, h - 14, new Color(0.17f, 0.17f, 0.15f));
                    for (int x = 14; x < w - 14; x += 22)
                        Fill(px, w, x, 52, x + 1, 140, new Color(0.26f, 0.26f, 0.22f));
                    Fill(px, w, 14, 139, w - 14, 141, new Color(0.26f, 0.26f, 0.22f));
                    Disc(px, w, w / 2, h - 52, 30, new Color(0.45f, 0.4f, 0.3f));
                    Disc(px, w, w / 2, h - 52, 9, new Color(0.95f, 0.82f, 0.55f));
                    Color chair = new(0.05f, 0.05f, 0.05f);
                    Fill(px, w, 104, 60, 152, 150, chair);
                    Fill(px, w, 92, 100, 164, 112, chair);
                    break;
                }
                case PaperKind.Map:
                    // Planta a nanquim: tres salas e o patio, com uma rota tracejada.
                    Frame(px, w, 26, 70, 120, 220, 3, ink);
                    Frame(px, w, 120, 70, 230, 150, 3, ink);
                    Frame(px, w, 120, 150, 230, 220, 3, ink);
                    Disc(px, w, 175, 110, 16, ink * 1.6f);
                    for (int x = 40; x < 210; x += 14)
                        Fill(px, w, x, 58, x + 7, 61, new Color(0.6f, 0.14f, 0.12f));
                    break;
                case PaperKind.Sticky:
                    for (int y = 0; y < 26; y++)
                        Fill(px, w, 0, h - 26 + y, w, h - 25 + y, paper * (0.9f + y * 0.004f));
                    break;
            }

            Texture2D t = new(w, h, TextureFormat.RGB24, true) { name = "NoirEvidencia_" + kind, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels(px);
            t.Apply();
            paperTextures[kind] = t;
            return t;
        }

        private static void Disc(Color[] px, int w, int cx, int cy, int r, Color c) => Ellipse(px, w, cx, cy, r, r, c);

        private static void Ellipse(Color[] px, int w, int cx, int cy, int rx, int ry, Color c)
        {
            int h = px.Length / w;
            for (int y = Mathf.Max(0, cy - ry); y <= Mathf.Min(h - 1, cy + ry); y++)
                for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(w - 1, cx + rx); x++)
                {
                    float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                    if (dx * dx + dy * dy <= 1f)
                        px[y * w + x] = c;
                }
        }

        private static void Ring(Color[] px, int w, int cx, int cy, int r, int thickness, Color c)
        {
            int h = px.Length / w;
            for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(h - 1, cy + r); y++)
                for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(w - 1, cx + r); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d <= r && d >= r - thickness)
                        px[y * w + x] = c;
                }
        }

        private static void Frame(Color[] px, int w, int x0, int y0, int x1, int y1, int t, Color c)
        {
            Fill(px, w, x0, y0, x1, y0 + t, c);
            Fill(px, w, x0, y1 - t, x1, y1, c);
            Fill(px, w, x0, y0, x0 + t, y1, c);
            Fill(px, w, x1 - t, y0, x1, y1, c);
        }

        private static void Fill(Color[] px, int w, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = Mathf.Max(0, y0); y < y1 && y < px.Length / w; y++)
                for (int x = Mathf.Max(0, x0); x < x1 && x < w; x++)
                    px[y * w + x] = c;
        }

        private static Renderer R(Transform parent, string path)
        {
            if (parent == null) return null;
            Transform t = parent.Find(path);
            return t != null ? t.GetComponent<Renderer>() : null;
        }
    }
}
