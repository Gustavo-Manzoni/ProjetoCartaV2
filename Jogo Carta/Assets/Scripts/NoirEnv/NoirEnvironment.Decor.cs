using UnityEngine;

namespace ProjetoCarta.NoirEnv
{
    /// <summary>
    /// Mobilia de fundo das tres salas: modelos da <see cref="NoirDecorLibrary"/> e pecas montadas com primitivas
    /// (bancos, poltronas, cabideiro, radiador, quadros, painel eletrico...). So enfeita: nada aqui e interagivel.
    /// As posicoes estao no referencial da sala (x de -5,9 a 5,9; z de -6 na parede da frente a 6 no fundo) e
    /// deixam livres os pontos de onde o jogador usa cada puzzle. Moveis grandes tem collider; o
    /// <see cref="ProjetoCarta.Blink.PlayerSafePlacement"/> tira o jogador de dentro deles depois da piscada.
    /// </summary>
    public static partial class NoirEnvironment
    {
        private static Texture2D[] decorTextures;

        // ------------------------------------------------------------------ salas

        private static void DecorOffice(Transform env, Frame f)
        {
            Transform root = DecorRoot(env);
            System.Random rng = new(11);
            Material wood = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.42f, 0.27f, 0.18f), 0.35f);
            Material brass = EnvMat.Lit("env_metal", Vector2.one, new Color(0.75f, 0.58f, 0.3f), 0.6f, 0.9f);

            // Fundo: estante de livros no canto esquerdo, vaso e comoda com bebidas no canto direito.
            Placed books = Model(root, f, "estante_livros", -4.75f, 5.42f, 180f, 3.0f);
            FillShelves(books, rng, ShelfStyle.Books, 0.95f);
            Plant(Group(root, f, "Vaso", -3.0f, 5.45f, 180f), rng, 1.25f);
            Placed dresser = Model(root, f, "comoda", 4.55f, 5.45f, 180f, 1.25f);
            if (dresser.ok)
            {
                DecanterSet(At(dresser, -0.45f, 0.02f), rng);
                PaperStack(At(dresser, 0.55f, 0f), rng, 3, new Vector2(0.38f, 0.3f));
            }
            Picture(Wall(root, f, "Quadro_Paisagem", 4.55f, 2.75f, 6f, 180f), new Vector2(1.5f, 1.0f), 0, wood);

            // Frente: canto de leitura (poltronas, mesinha, luminaria de pe) e estantes.
            ClubArmchair(Group(root, f, "Poltrona_A", -3.65f, -5.05f, 15f), 1.3f);
            ClubArmchair(Group(root, f, "Poltrona_B", -1.35f, -5.05f, -15f), 1.3f);
            SideTable(Group(root, f, "Mesinha", -2.5f, -5.45f, 0f), rng, 1.25f);
            FloorLamp(Group(root, f, "Luminaria_Pe", -5.25f, -5.3f, 0f), 1.25f, new Color(1f, 0.7f, 0.42f));
            Picture(Wall(root, f, "Quadro_Retrato", -2.5f, 2.95f, -6f, 0f), new Vector2(1.1f, 1.4f), 1, wood);
            Picture(Wall(root, f, "Diploma_A", -4.35f, 2.55f, -6f, 0f), new Vector2(0.55f, 0.72f), 2, wood);
            Picture(Wall(root, f, "Diploma_B", -0.65f, 2.55f, -6f, 0f), new Vector2(0.55f, 0.72f), 2, wood);

            Placed shelfA = Model(root, f, "estante_alta", 2.35f, -5.45f, 0f, 3.1f);
            FillShelves(shelfA, rng, ShelfStyle.Mixed, 0.85f);
            Placed shelfB = Model(root, f, "estante_livros", 4.65f, -5.42f, 0f, 3.0f);
            FillShelves(shelfB, rng, ShelfStyle.Books, 0.9f);

            // Laterais: cabideiro com chapeu e sobretudo, relogio acima dos baus, radiador sob a janela.
            CoatRack(Group(root, f, "Cabideiro", -5.25f, -3.3f, 60f), 1.2f);
            WallClock(Wall(root, f, "Relogio_Parede", 5.9f, 3.1f, 0.3f, -90f), 0.75f, wood, brass);
            Radiator(Wall(root, f, "Radiador", -5.9f, 0f, 1.5f, 90f), 1.9f, new Color(0.55f, 0.47f, 0.38f));
            Picture(Wall(root, f, "Quadro_Lateral", -5.9f, 2.8f, -3.3f, 90f), new Vector2(0.9f, 0.7f), 0, wood);
        }

        private static void DecorChamber(Transform env, Frame f)
        {
            Transform root = DecorRoot(env);
            System.Random rng = new(23);
            Color paint = new(0.3f, 0.38f, 0.36f);
            Material metal = EnvMat.Lit("env_metal", Vector2.one, paint, 0.45f, 0.6f);

            // Sala das testemunhas, atras do vidro: fileira de cadeiras de madeira viradas para a cadeira eletrica.
            for (int i = 0; i < 6; i++)
                WoodChair(Group(root, f, "Cadeira_Testemunha", -4.85f, -3.9f + i * 1.25f, 90f + (float)(rng.NextDouble() - 0.5) * 8f), 1.3f);
            for (int i = 0; i < 3; i++)
                WoodChair(Group(root, f, "Cadeira_Testemunha", -5.6f, -3.3f + i * 2.5f, 90f + (float)(rng.NextDouble() - 0.5) * 10f), 1.3f);
            Light exit = Prims.MakeLight(root, "Luz_Testemunhas", f.P(-5.6f, 3.6f, 0f), LightType.Point, new Color(1f, 0.55f, 0.3f), 0.5f, 5f, false);
            exit.renderMode = LightRenderMode.ForceVertex;

            // Fundo: maca com lencol e lata de material ao lado do vidro; painel eletrico no canto direito.
            Gurney(Group(root, f, "Maca_Hospitalar", -1.95f, 4.95f, 90f), 1.25f);
            Placed locker = Model(root, f, "arquivo_aco", -3.45f, 5.35f, 180f, 1.85f);
            if (locker.ok)
            {
                Tint(locker.root, new Color(0.55f, 0.62f, 0.6f));
                VaryDrawers(locker, rng);
            }
            ElectricPanel(Group(root, f, "Painel_Eletrico", 4.35f, 5.47f, 180f), f, metal);
            FuseBox(Wall(root, f, "Caixa_Fusiveis", 5.9f, 1.75f, 3.9f, -90f), metal);
            Extinguisher(Wall(root, f, "Extintor", 5.9f, 1.05f, 2.6f, -90f));

            // Frente: cadeira e mesinha do capelao, balde com esfregao, caixotes no canto.
            WoodChair(Group(root, f, "Cadeira_Capelao", -2.95f, -5.2f, 25f), 1.3f);
            SmallTable(Group(root, f, "Mesa_Capelao", -2.0f, -5.5f, 0f), rng, 1.25f);
            Bucket(Group(root, f, "Balde_Esfregao", -3.4f, -2.4f, 30f), 1.2f);
            CrateStack(root, f, 4.95f, -5.25f, 0f, rng);

            // Cabo grosso da caixa de controle ate o canto e subindo pela parede.
            Material cable = EnvMat.Lit(null, Vector2.one, new Color(0.04f, 0.04f, 0.04f), 0.35f);
            Pipe(root, f.P(1.9f, 0.05f, -4.25f), f.P(5.75f, 0.05f, -4.25f), 0.09f, cable);
            Pipe(root, f.P(5.75f, 0.05f, -4.25f), f.P(5.75f, f.ceil - 0.2f, -4.25f), 0.09f, cable);
            Pipe(root, f.P(5.65f, 0.05f, -4.55f), f.P(5.65f, f.ceil - 0.2f, -4.55f), 0.07f, metal);
            Pipe(root, f.P(-3.7f, 0.05f, 5.85f), f.P(-3.7f, f.ceil - 0.2f, 5.85f), 0.08f, metal);

        }

        private static void DecorArchive(Transform env, Frame f)
        {
            Transform root = DecorRoot(env);
            System.Random rng = new(37);
            Material wood = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.38f, 0.3f, 0.24f), 0.3f);
            Material metal = EnvMat.Lit("env_metal", Vector2.one, new Color(0.45f, 0.48f, 0.47f), 0.45f, 0.6f);

            // Parede direita: fileira de arquivos de aco (nao perfeitamente alinhados, cada um com as gavetas num
            // ponto e coisas diferentes em cima), duas estantes de caixas-arquivo e caixotes no canto.
            float[] cabinets = { 1.55f, 0.92f, 0.29f, -0.34f };
            for (int i = 0; i < cabinets.Length; i++)
            {
                float nudge = (float)(rng.NextDouble() - 0.5) * 0.04f;
                float yaw = -90f + (float)(rng.NextDouble() - 0.5) * 3f;
                Placed c = Model(root, f, "arquivo_aco", 5.32f + nudge, cabinets[i], yaw, 1.85f);
                if (!c.ok)
                    continue;

                Tint(c.root, new Color(0.58f, 0.62f, 0.58f));
                VaryDrawers(c, rng);
                if (i == 0)
                    ArchiveBoxes(At(c, -0.15f, 0.05f), rng, 1, 0.38f);
                else if (i == 1)
                    PaperStack(At(c, 0.1f, -0.1f), rng, 2, new Vector2(0.4f, 0.3f));
                else if (i == 3)
                {
                    ArchiveBoxes(At(c, 0.05f, 0f), rng, 2, 0.34f);
                    PaperStack(At(c, -0.55f, 0.1f), rng, 1, new Vector2(0.36f, 0.28f));
                }
            }
            Placed rackA = Model(root, f, "estante_alta", 5.42f, -1.75f, -90f, 3.1f);
            FillShelves(rackA, rng, ShelfStyle.Archive, 0.9f);
            Placed rackB = Model(root, f, "estante_media", 5.42f, -3.75f, -90f, 3.1f);
            FillShelves(rackB, rng, ShelfStyle.Mixed, 0.95f);
            CrateStack(root, f, 5.1f, -5.3f, -90f, rng);
            WallClock(Wall(root, f, "Relogio_Parede", 5.9f, 3.25f, 0.6f, -90f), 0.7f, metal, metal);

            // Fundo: quadro de avisos, estante baixa de pastas, retrato oficial e um vaso.
            Placed cork = Model(root, f, "quadro_cortica", -0.55f, 5.94f, 180f, 0f, false, 1.75f, 2.5f);
            Placed low = Model(root, f, "estante_baixa", -0.55f, 5.42f, 180f, 1.3f);
            FillShelves(low, rng, ShelfStyle.Binders, 0.9f);
            if (low.ok)
                PaperStack(At(low, -0.4f, 0f), rng, 4, new Vector2(0.36f, 0.28f));
            Picture(Wall(root, f, "Retrato_Oficial", 1.45f, 2.9f, 6f, 180f), new Vector2(0.75f, 0.95f), 1, wood);
            Plant(Group(root, f, "Vaso", 1.55f, 5.45f, 180f), rng, 1.2f);

            // Esquerda: radiador sob a janela e bebedouro. Entre a janela e o armario alto fica a caixa de
            // manutencao da manivela (na cena): a parede ali fica livre para ela.
            Radiator(Wall(root, f, "Radiador", -5.9f, 0f, 0f, 90f), 2.2f, new Color(0.62f, 0.64f, 0.6f));
            WaterCooler(Group(root, f, "Bebedouro", -5.4f, -5.35f, 45f), 1.25f);

            // Frente: banco de espera, cabideiro, comoda com papeis e lixeira. (O gaveteiro do kit tem as gavetas
            // abertas em escadinha na propria malha; a comoda tem gavetas fechadas.)
            Bench(Group(root, f, "Banco_Espera", -2.45f, -5.55f, 0f), 2.6f, wood, metal);
            Picture(Wall(root, f, "Aviso_A", -3.15f, 2.7f, -6f, 0f), new Vector2(0.6f, 0.8f), 3, metal);
            Picture(Wall(root, f, "Aviso_B", -1.75f, 2.7f, -6f, 0f), new Vector2(0.6f, 0.8f), 2, metal);
            CoatRack(Group(root, f, "Cabideiro", -0.45f, -5.45f, 20f), 1.2f);
            Placed chest = Model(root, f, "comoda", 1.75f, -5.4f, 0f, 1.2f);
            if (chest.ok)
            {
                PaperStack(At(chest, -0.45f, 0.05f), rng, 4, new Vector2(0.42f, 0.32f));
                ArchiveBoxes(At(chest, 0.5f, -0.1f), rng, 1, 0.34f);
            }
            TrashBin(Group(root, f, "Lixeira", 3.25f, -5.55f, 0f), rng, new Color(0.36f, 0.38f, 0.37f));

            // Mesa do juiz no fundo, com o cofre numerico no meio do tampo (o cofre esta na cena): papeis nas pontas.
            // A mesa do arquivo (bandeja de protocolo e gaveta da pasta) fica na cena, no meio da parede direita;
            // a cadeira do escrivao fica atras dela.
            Placed desk = Model(root, f, "mesa_antiga", 3.68f, 5.2f, 180f, 0.96f);
            if (desk.ok)
            {
                PaperStack(At(desk, -0.55f, 0.1f), rng, 3, new Vector2(0.36f, 0.28f));
                ArchiveBoxes(At(desk, 0.58f, -0.05f), rng, 1, 0.3f);
            }
            Model(root, f, "cadeira_couro", 4.3f, -2.0f, -90f + 12f, 1.55f);
            FloorPapers(root, f, rng, new Vector2(3.9f, 0.3f), 4);
            FloorPapers(root, f, rng, new Vector2(4.2f, -4.6f), 3);

        }

        // ------------------------------------------------------------------ modelos

        private struct Placed
        {
            public bool ok;
            public Transform root;
            public Vector3 size;        // caixa do movel no espaco local (frente = +z, base em y = 0)
            public float[] shelves;
        }

        private static Transform DecorRoot(Transform env)
        {
            GameObject go = new("Mobilia");
            go.transform.SetParent(env, false);
            return go.transform;
        }

        private static Quaternion Yaw(Frame f, float yaw)
        {
            float r = yaw * Mathf.Deg2Rad;
            return Quaternion.LookRotation(f.D(Mathf.Sin(r), 0f, Mathf.Cos(r)), Vector3.up);
        }

        private static Transform Group(Transform parent, Frame f, string name, float x, float z, float yaw, float y = 0f)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(f.P(x, y, z), Yaw(f, yaw));
            return t;
        }

        /// <summary>Grupo encostado numa parede: (x, z) e o ponto na superficie da parede.</summary>
        private static Transform Wall(Transform parent, Frame f, string name, float x, float y, float z, float yaw)
        {
            Transform t = Group(parent, f, name, x, z, yaw, y);
            t.position += t.forward * 0.03f;
            return t;
        }

        /// <summary>
        /// Instancia um modelo da biblioteca, com a base no chao em (x, z), a frente virada para yaw e a altura
        /// pedida (ou a largura, se width > 0). Retorna ok = false se a biblioteca nao tiver o modelo.
        /// </summary>
        private static Placed Model(Transform parent, Frame f, string id, float x, float z, float yaw, float height,
            bool solid = true, float y = 0f, float width = 0f)
        {
            NoirDecorLibrary lib = NoirDecorLibrary.Instance;
            if (lib == null || !lib.TryGet(id, out NoirDecorLibrary.Entry e))
                return default;

            GameObject inst = Object.Instantiate(e.model, Vector3.zero, e.model.transform.rotation);
            Transform keep = inst.transform;
            if (!string.IsNullOrEmpty(e.child))
            {
                keep = Deep(inst.transform, e.child);
                if (keep == null)
                {
                    Object.Destroy(inst);
                    return default;
                }
            }

            // Montado fora do cenario (que ainda esta inativo): Renderer.bounds de objeto inativo vem vazio.
            Transform root = new GameObject("Movel_" + id).transform;
            Transform norm = new GameObject("Modelo").transform;
            norm.SetParent(root, false);
            keep.SetParent(norm, true);
            if (keep != inst.transform)
                Object.Destroy(inst);
            keep.gameObject.SetActive(true);
            norm.localRotation = Quaternion.Euler(0f, e.frontYaw, 0f);

            // Moveis de fundo nao tem comportamento: sai collider, script e animacao do modelo original.
            foreach (Component c in keep.GetComponentsInChildren<Component>(true))
                if (c is Collider || c is MonoBehaviour || c is Animator || c is Rigidbody)
                    Object.Destroy(c);

            Renderer[] rs = keep.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0)
            {
                Object.Destroy(root.gameObject);
                return default;
            }
            if (e.material != null)
            {
                foreach (Renderer r in rs)
                {
                    Material[] ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) ms[i] = e.material;
                    r.sharedMaterials = ms;
                }
            }

            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            float s = width > 0f ? width / Mathf.Max(0.001f, b.size.x) : height / Mathf.Max(0.001f, b.size.y);
            norm.localScale = Vector3.one * s;
            norm.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z) * s;

            root.SetParent(parent, false);
            root.SetPositionAndRotation(f.P(x, y, z), Yaw(f, yaw));
            Vector3 size = b.size * s;
            if (solid)
            {
                BoxCollider col = root.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, size.y * 0.5f, 0f);
                col.size = size;
            }

            return new Placed { ok = true, root = root, size = size, shelves = e.shelves };
        }

        /// <summary>Ponto sobre o tampo do movel (fracoes da largura e da profundidade, a partir do centro).</summary>
        private static Transform At(Placed p, float u, float v)
        {
            Transform t = new GameObject("Sobre").transform;
            t.SetParent(p.root, false);
            t.localPosition = new Vector3(u * p.size.x * 0.5f, p.size.y, v * p.size.z * 0.5f);
            return t;
        }

        private static void Tint(Transform t, Color c)
        {
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                Material[] ms = r.materials;
                foreach (Material m in ms)
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * c);
                }
                r.materials = ms;
            }
        }

        /// <summary>
        /// O arquivo de aco vem com as quatro gavetas abertas em escadinha (01 a mais aberta, 04 quase fechada).
        /// Aqui cada gaveta fica num ponto proprio: quase sempre fechada, as vezes entreaberta, raramente puxada
        /// de vez, com um leve desalinhamento, como num arquivo usado de verdade.
        /// </summary>
        private static void VaryDrawers(Placed p, System.Random rng)
        {
            Transform[] drawers = new Transform[4];
            for (int i = 0; i < drawers.Length; i++)
            {
                drawers[i] = Deep(p.root, "0" + (i + 1));
                if (drawers[i] == null)
                    return;
            }

            // Medido no mundo pela malha (o movel ainda esta inativo: Renderer.bounds viria vazio). Fechada = centro
            // da gaveta alinhado ao centro do gabinete na direcao da frente.
            Transform body = drawers[0].parent;
            MeshFilter bodyMesh = body.GetComponent<MeshFilter>();
            if (bodyMesh == null || bodyMesh.sharedMesh == null)
                return;

            Vector3 front = p.root.forward;
            Vector3 side = p.root.right;
            float bodyCenter = Vector3.Dot(body.TransformPoint(bodyMesh.sharedMesh.bounds.center), front);
            foreach (Transform drawer in drawers)
            {
                MeshFilter mesh = drawer.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null)
                    continue;

                double roll = rng.NextDouble();
                float open = roll < 0.55 ? 0f
                    : roll < 0.8 ? 0.03f + (float)rng.NextDouble() * 0.06f
                    : roll < 0.95 ? 0.12f + (float)rng.NextDouble() * 0.1f
                    : 0.28f + (float)rng.NextDouble() * 0.1f;
                float skew = open > 0f ? (float)(rng.NextDouble() - 0.5) * 0.012f : 0f;
                float current = Vector3.Dot(drawer.TransformPoint(mesh.sharedMesh.bounds.center), front) - bodyCenter;
                drawer.localPosition += body.InverseTransformVector(front * (open - current) + side * skew);
            }
        }

        // ------------------------------------------------------------------ prateleiras

        private enum ShelfStyle { Books, Archive, Binders, Mixed }

        private static readonly Color[] BookColors =
        {
            new(0.35f, 0.08f, 0.06f), new(0.12f, 0.2f, 0.12f), new(0.1f, 0.12f, 0.22f), new(0.4f, 0.28f, 0.16f),
            new(0.22f, 0.12f, 0.08f), new(0.5f, 0.42f, 0.3f), new(0.08f, 0.08f, 0.08f), new(0.45f, 0.15f, 0.1f)
        };

        private static readonly Color[] BinderColors =
        {
            new(0.08f, 0.08f, 0.09f), new(0.13f, 0.2f, 0.15f), new(0.3f, 0.09f, 0.08f), new(0.12f, 0.14f, 0.22f),
            new(0.42f, 0.36f, 0.24f)
        };

        private static void FillShelves(Placed p, System.Random rng, ShelfStyle style, float fullness)
        {
            if (!p.ok || p.shelves == null || p.shelves.Length == 0)
                return;

            float inner = p.size.x * 0.86f;
            float depth = p.size.z * 0.7f;
            for (int i = 0; i < p.shelves.Length; i++)
            {
                float y = p.shelves[i] * p.size.y;
                float next = i + 1 < p.shelves.Length ? p.shelves[i + 1] * p.size.y : p.size.y;
                bool top = p.shelves[i] > 0.97f;
                float gap = top ? 0.45f : next - y - 0.04f;
                if (gap < 0.12f || rng.NextDouble() > fullness + 0.1f)
                    continue;

                ShelfStyle s = style;
                if (style == ShelfStyle.Mixed)
                    s = (ShelfStyle)rng.Next(0, 3);
                if (top)
                    s = ShelfStyle.Archive;

                Transform row = new GameObject("Prateleira").transform;
                row.SetParent(p.root, false);
                row.localPosition = new Vector3(0f, y + 0.002f, -p.size.z * 0.06f);
                ShelfRow(row, rng, s, inner, depth, Mathf.Min(gap, 0.5f), fullness);
            }
        }

        private static void ShelfRow(Transform row, System.Random rng, ShelfStyle s, float width, float depth, float gap, float fullness)
        {
            float x = -width * 0.5f;
            float end = width * 0.5f;
            Material paper = EnvMat.Lit(null, Vector2.one, new Color(0.82f, 0.79f, 0.7f), 0.1f);
            while (x < end - 0.05f)
            {
                // Vaos: prateleira de arquivo de verdade nunca esta cheia por igual.
                if (rng.NextDouble() > fullness)
                {
                    x += 0.15f + (float)rng.NextDouble() * 0.3f;
                    continue;
                }

                if (s == ShelfStyle.Archive)
                {
                    float w = 0.3f + (float)rng.NextDouble() * 0.08f;
                    if (x + w > end) break;
                    float h = Mathf.Min(gap * 0.85f, 0.36f);
                    ArchiveBox(row, new Vector3(x + w * 0.5f, 0f, 0f), new Vector3(w - 0.02f, h, Mathf.Min(depth, 0.55f)), rng);
                    x += w;
                }
                else if (s == ShelfStyle.Binders)
                {
                    int n = rng.Next(3, 9);
                    for (int k = 0; k < n && x < end - 0.08f; k++)
                    {
                        float w = 0.07f + (float)rng.NextDouble() * 0.03f;
                        float h = Mathf.Min(gap * 0.88f, 0.42f);
                        Material m = EnvMat.Lit("env_plaster", Vector2.one, BinderColors[rng.Next(BinderColors.Length)], 0.3f);
                        Part(row, "Pasta", PrimitiveType.Cube, new Vector3(x + w * 0.5f, h * 0.5f, 0f), new Vector3(w - 0.006f, h, Mathf.Min(depth, 0.4f)), m);
                        Part(row, "Etiqueta", PrimitiveType.Cube, new Vector3(x + w * 0.5f, h * 0.62f, Mathf.Min(depth, 0.4f) * 0.5f + 0.002f), new Vector3(w * 0.6f, 0.08f, 0.004f), paper, null, false);
                        x += w;
                    }
                    x += 0.06f + (float)rng.NextDouble() * 0.12f;
                }
                else
                {
                    int n = rng.Next(4, 14);
                    for (int k = 0; k < n && x < end - 0.06f; k++)
                    {
                        float w = 0.035f + (float)rng.NextDouble() * 0.05f;
                        float h = Mathf.Min(gap * 0.92f, 0.22f + (float)rng.NextDouble() * 0.16f);
                        float d = Mathf.Min(depth, 0.18f + (float)rng.NextDouble() * 0.1f);
                        Material m = EnvMat.Lit("env_plaster", Vector2.one, BookColors[rng.Next(BookColors.Length)] * (0.8f + (float)rng.NextDouble() * 0.4f), 0.25f);
                        Part(row, "Livro", PrimitiveType.Cube, new Vector3(x + w * 0.5f, h * 0.5f, depth * 0.5f - d * 0.5f), new Vector3(w - 0.004f, h, d), m);
                        x += w;
                    }
                    // Um livro tombado no fim da fileira ou uma pilha deitada.
                    if (rng.NextDouble() < 0.5 && x < end - 0.3f)
                    {
                        int stack = rng.Next(2, 5);
                        for (int k = 0; k < stack; k++)
                        {
                            Material m = EnvMat.Lit("env_plaster", Vector2.one, BookColors[rng.Next(BookColors.Length)], 0.25f);
                            float t = 0.04f + (float)rng.NextDouble() * 0.03f;
                            Part(row, "Livro_Deitado", PrimitiveType.Cube, new Vector3(x + 0.16f, k * 0.06f + t * 0.5f, depth * 0.5f - 0.13f),
                                new Vector3(0.26f, t, 0.2f), m, new Vector3(0f, (float)rng.NextDouble() * 14f - 7f, 0f));
                        }
                        x += 0.34f;
                    }
                    x += 0.03f + (float)rng.NextDouble() * 0.1f;
                }
            }
        }

        private static void ArchiveBox(Transform parent, Vector3 basePos, Vector3 size, System.Random rng)
        {
            float shade = 0.75f + (float)rng.NextDouble() * 0.3f;
            Material card = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.62f, 0.5f, 0.34f) * shade, 0.15f);
            Material lid = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.55f, 0.44f, 0.3f) * shade, 0.15f);
            Material label = EnvMat.Lit(null, Vector2.one, new Color(0.86f, 0.83f, 0.74f), 0.1f);
            Transform box = Part(parent, "Caixa_Arquivo", PrimitiveType.Cube, basePos + Vector3.up * size.y * 0.5f, size, card).transform;
            box.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 6f - 3f, 0f);
            Part(parent, "Tampa", PrimitiveType.Cube, basePos + Vector3.up * (size.y - 0.03f), new Vector3(size.x + 0.012f, 0.06f, size.z + 0.012f), lid);
            Part(parent, "Etiqueta", PrimitiveType.Cube, basePos + new Vector3(0f, size.y * 0.45f, size.z * 0.5f + 0.003f),
                new Vector3(size.x * 0.55f, size.y * 0.3f, 0.004f), label, null, false);
        }

        private static void ArchiveBoxes(Transform at, System.Random rng, int count, float size)
        {
            for (int i = 0; i < count; i++)
                ArchiveBox(at, new Vector3((float)rng.NextDouble() * 0.06f - 0.03f, i * size * 0.82f, 0f), new Vector3(size, size * 0.8f, size * 1.3f), rng);
        }

        private static void PaperStack(Transform at, System.Random rng, int stacks, Vector2 sheet)
        {
            Material paper = EnvMat.Lit(null, Vector2.one, new Color(0.86f, 0.84f, 0.76f), 0.12f);
            Material old = EnvMat.Lit(null, Vector2.one, new Color(0.74f, 0.68f, 0.55f), 0.12f);
            Material folder = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.6f, 0.48f, 0.3f), 0.2f);
            float y = 0f;
            for (int i = 0; i < stacks; i++)
            {
                float h = 0.015f + (float)rng.NextDouble() * 0.05f;
                Material m = i % 3 == 2 ? folder : (rng.NextDouble() < 0.5 ? paper : old);
                Part(at, "Papeis", PrimitiveType.Cube, new Vector3((float)rng.NextDouble() * 0.06f - 0.03f, y + h * 0.5f, (float)rng.NextDouble() * 0.06f - 0.03f),
                    new Vector3(sheet.x, h, sheet.y), m, new Vector3(0f, (float)rng.NextDouble() * 20f - 10f, 0f));
                y += h;
            }
        }

        private static void FloorPapers(Transform root, Frame f, System.Random rng, Vector2 center, int count)
        {
            Material paper = EnvMat.Lit(null, Vector2.one, new Color(0.8f, 0.78f, 0.7f), 0.1f);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = f.P(center.x + (float)rng.NextDouble() * 1.2f - 0.6f, 0.006f + i * 0.002f, center.y + (float)rng.NextDouble() * 1.2f - 0.6f);
                Prims.Box(root, "Papel_Chao", p, new Vector3(0.3f, 0.003f, 0.4f), paper, false,
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), false);
            }
        }

        // ------------------------------------------------------------------ pecas montadas

        private static GameObject Part(Transform parent, string n, PrimitiveType type, Vector3 local, Vector3 size, Material m,
            Vector3? euler = null, bool shadows = true)
        {
            GameObject go = Prims.Make(type, parent, n, parent.position, size, m, false, null, shadows);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = size;
            return go;
        }

        private static GameObject Lathe(Transform parent, string n, Vector3 local, Vector2[] profile, Material m)
        {
            GameObject go = Prims.LatheObj(parent, n, parent.position, profile, m);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static void Solid(Transform t, Vector3 center, Vector3 size)
        {
            BoxCollider c = t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        private static void Pipe(Transform parent, Vector3 a, Vector3 b, float d, Material m)
        {
            Vector3 dir = b - a;
            Prims.Make(PrimitiveType.Cylinder, parent, "Cano", (a + b) * 0.5f, new Vector3(d, dir.magnitude * 0.5f, d), m, false,
                Quaternion.FromToRotation(Vector3.up, dir.normalized));
        }

        private static void Bench(Transform t, float length, Material wood, Material metal)
        {
            for (int i = 0; i < 3; i++)
                Part(t, "Ripa_Assento", PrimitiveType.Cube, new Vector3(0f, 0.62f, -0.16f + i * 0.15f), new Vector3(length, 0.045f, 0.12f), wood);
            for (int i = 0; i < 2; i++)
                Part(t, "Ripa_Encosto", PrimitiveType.Cube, new Vector3(0f, 0.9f + i * 0.2f, -0.27f), new Vector3(length, 0.12f, 0.035f), wood, new Vector3(-10f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (length * 0.5f - 0.18f);
                Part(t, "Pe", PrimitiveType.Cube, new Vector3(x, 0.3f, 0.12f), new Vector3(0.05f, 0.6f, 0.05f), metal);
                Part(t, "Pe", PrimitiveType.Cube, new Vector3(x, 0.62f, -0.28f), new Vector3(0.05f, 1.24f, 0.05f), metal, new Vector3(-6f, 0f, 0f));
                Part(t, "Travessa", PrimitiveType.Cube, new Vector3(x, 0.58f, -0.05f), new Vector3(0.05f, 0.04f, 0.5f), metal);
                Part(t, "Travessa", PrimitiveType.Cube, new Vector3(x, 0.12f, -0.07f), new Vector3(0.04f, 0.04f, 0.44f), metal);
            }
            Solid(t, new Vector3(0f, 0.55f, -0.05f), new Vector3(length, 1.1f, 0.55f));
        }

        private static void WoodChair(Transform t, float k)
        {
            Material wood = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.36f, 0.24f, 0.16f), 0.3f);
            Part(t, "Assento", PrimitiveType.Cube, new Vector3(0f, 0.47f, 0f), new Vector3(0.46f, 0.04f, 0.44f), wood);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 0.2f, z = (i < 2 ? -1 : 1) * 0.19f;
                Part(t, "Perna", PrimitiveType.Cube, new Vector3(x, 0.23f, z), new Vector3(0.04f, 0.46f, 0.04f), wood);
            }
            for (int s = -1; s <= 1; s += 2)
                Part(t, "Montante", PrimitiveType.Cube, new Vector3(s * 0.2f, 0.72f, -0.2f), new Vector3(0.04f, 0.5f, 0.04f), wood, new Vector3(-5f, 0f, 0f));
            for (int i = 0; i < 3; i++)
                Part(t, "Travessa", PrimitiveType.Cube, new Vector3(0f, 0.62f + i * 0.13f, -0.215f - i * 0.011f), new Vector3(0.38f, 0.05f, 0.02f), wood, new Vector3(-5f, 0f, 0f));
            Part(t, "Travessa_Baixa", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.38f, 0.025f, 0.025f), wood);
            t.localScale = Vector3.one * k;
            Solid(t, new Vector3(0f, 0.45f, 0f), new Vector3(0.48f, 0.9f, 0.46f));
        }

        private static void ClubArmchair(Transform t, float k)
        {
            Material leather = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.3f, 0.09f, 0.06f), 0.55f);
            Material cushion = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.34f, 0.11f, 0.07f), 0.5f);
            Material foot = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.15f, 0.09f, 0.06f), 0.4f);
            Part(t, "Base", PrimitiveType.Cube, new Vector3(0f, 0.27f, 0f), new Vector3(0.92f, 0.36f, 0.86f), leather);
            Part(t, "Almofada", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.05f), new Vector3(0.62f, 0.12f, 0.7f), cushion);
            Part(t, "Encosto", PrimitiveType.Cube, new Vector3(0f, 0.72f, -0.34f), new Vector3(0.92f, 0.68f, 0.2f), leather, new Vector3(-8f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                Part(t, "Braco", PrimitiveType.Cube, new Vector3(s * 0.39f, 0.55f, 0.02f), new Vector3(0.16f, 0.3f, 0.84f), leather);
                Part(t, "Rolo", PrimitiveType.Cylinder, new Vector3(s * 0.39f, 0.71f, 0.02f), new Vector3(0.2f, 0.42f, 0.2f), leather, new Vector3(90f, 0f, 0f));
                for (int z = -1; z <= 1; z += 2)
                    Part(t, "Pe", PrimitiveType.Cylinder, new Vector3(s * 0.38f, 0.045f, z * 0.34f), new Vector3(0.07f, 0.045f, 0.07f), foot);
            }
            t.localScale = Vector3.one * k;
            Solid(t, new Vector3(0f, 0.5f, 0f), new Vector3(0.95f, 1f, 0.9f));
        }

        private static void SideTable(Transform t, System.Random rng, float k)
        {
            Material wood = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.3f, 0.18f, 0.11f), 0.45f);
            Part(t, "Tampo", PrimitiveType.Cylinder, new Vector3(0f, 0.66f, 0f), new Vector3(0.56f, 0.02f, 0.56f), wood);
            Part(t, "Coluna", PrimitiveType.Cylinder, new Vector3(0f, 0.34f, 0f), new Vector3(0.07f, 0.31f, 0.07f), wood);
            Part(t, "Pe", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.38f, 0.02f, 0.38f), wood);
            Material glass = EnvMat.Glass(new Color(0.35f, 0.3f, 0.22f), 0f);
            Lathe(t, "Copo", new Vector3(0.1f, 0.68f, 0.05f), new[] { new Vector2(0.001f, 0f), new Vector2(0.04f, 0f), new Vector2(0.045f, 0.09f) }, glass);
            Material ash = EnvMat.Lit("env_metal", Vector2.one, new Color(0.2f, 0.2f, 0.2f), 0.6f, 0.8f);
            Lathe(t, "Cinzeiro", new Vector3(-0.1f, 0.68f, -0.05f), new[] { new Vector2(0.001f, 0f), new Vector2(0.08f, 0f), new Vector2(0.09f, 0.03f), new Vector2(0.06f, 0.025f) }, ash);
            t.localScale = Vector3.one * k;
        }

        private static void FloorLamp(Transform t, float k, Color c)
        {
            Material brass = EnvMat.Lit("env_metal", Vector2.one, new Color(0.55f, 0.42f, 0.22f), 0.6f, 0.9f);
            Material shade = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.75f, 0.62f, 0.45f), 0.1f);
            Part(t, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.36f, 0.02f, 0.36f), brass);
            Part(t, "Haste", PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(0.03f, 0.85f, 0.03f), brass);
            Lathe(t, "Cupula", new Vector3(0f, 1.55f, 0f), new[] { new Vector2(0.28f, 0f), new Vector2(0.24f, 0.12f), new Vector2(0.15f, 0.32f) }, shade);
            Prims.Ball(t, "Lampada", t.TransformPoint(new Vector3(0f, 1.7f * 1f, 0f)), 0.1f, EnvMat.Glow(null, c, 2.5f));
            t.localScale = Vector3.one * k;
            Prims.MakeLight(t, "Luz_Luminaria", t.TransformPoint(new Vector3(0f, 1.62f, 0f)), LightType.Point, c, 0.55f, 4.5f, false);
            Solid(t, new Vector3(0f, 0.9f, 0f), new Vector3(0.4f, 1.8f, 0.4f));
        }

        private static void CoatRack(Transform t, float k)
        {
            Material wood = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.28f, 0.17f, 0.1f), 0.4f);
            Material felt = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.13f, 0.12f, 0.11f), 0.15f);
            Material coat = EnvMat.Lit(null, Vector2.one, new Color(0.14f, 0.12f, 0.1f), 0.12f);
            Part(t, "Haste", PrimitiveType.Cylinder, new Vector3(0f, 0.95f, 0f), new Vector3(0.05f, 0.95f, 0.05f), wood);
            Part(t, "Pomo", PrimitiveType.Sphere, new Vector3(0f, 1.92f, 0f), Vector3.one * 0.08f, wood);
            for (int i = 0; i < 3; i++)
                Part(t, "Pe", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.05f, 0.04f, 0.55f), wood, new Vector3(0f, i * 60f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Part(t, "Gancho", PrimitiveType.Cube, new Vector3(0f, 1.78f, 0f) + dir * 0.1f, new Vector3(0.025f, 0.025f, 0.2f), wood, new Vector3(-30f, a, 0f));
            }
            // Chapeu fedora num gancho e sobretudo escuro pendurado pela gola no gancho da frente.
            Lathe(t, "Chapeu", new Vector3(-0.12f, 1.86f, -0.12f), new[] { new Vector2(0.2f, 0f), new Vector2(0.21f, 0.015f), new Vector2(0.11f, 0.02f), new Vector2(0.11f, 0.12f), new Vector2(0.06f, 0.15f) }, felt);
            Part(t, "Gola", PrimitiveType.Cube, new Vector3(0f, 1.7f, 0.13f), new Vector3(0.16f, 0.1f, 0.08f), coat, new Vector3(-10f, 0f, 0f));
            Part(t, "Ombros", PrimitiveType.Capsule, new Vector3(0f, 1.6f, 0.13f), new Vector3(0.14f, 0.2f, 0.12f), coat, new Vector3(0f, 0f, 90f));
            Part(t, "Sobretudo", PrimitiveType.Cube, new Vector3(0f, 1.2f, 0.14f), new Vector3(0.36f, 0.72f, 0.1f), coat, new Vector3(-3f, 0f, 0f));
            Part(t, "Sobretudo_Barra", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0.16f), new Vector3(0.42f, 0.3f, 0.09f), coat, new Vector3(-4f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
                Part(t, "Manga", PrimitiveType.Capsule, new Vector3(s * 0.19f, 1.25f, 0.13f), new Vector3(0.1f, 0.33f, 0.1f), coat, new Vector3(0f, 0f, s * 4f));
            t.localScale = Vector3.one * k;
            Solid(t, new Vector3(0f, 1f, 0f), new Vector3(0.5f, 2f, 0.5f));
        }

        private static void Radiator(Transform t, float length, Color c)
        {
            Material m = EnvMat.Lit("env_metal", Vector2.one, c, 0.4f, 0.4f);
            int fins = Mathf.Max(4, Mathf.RoundToInt(length / 0.1f));
            for (int i = 0; i < fins; i++)
            {
                float x = -length * 0.5f + (i + 0.5f) * length / fins;
                Part(t, "Aleta", PrimitiveType.Cube, new Vector3(x, 0.62f, 0.12f), new Vector3(0.06f, 0.75f, 0.16f), m);
            }
            Part(t, "Tubo_Cima", PrimitiveType.Cylinder, new Vector3(0f, 0.98f, 0.12f), new Vector3(0.06f, length * 0.5f, 0.06f), m, new Vector3(0f, 0f, 90f));
            Part(t, "Tubo_Baixo", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0.12f), new Vector3(0.06f, length * 0.5f, 0.06f), m, new Vector3(0f, 0f, 90f));
            Part(t, "Pe", PrimitiveType.Cube, new Vector3(-length * 0.45f, 0.11f, 0.12f), new Vector3(0.05f, 0.22f, 0.12f), m);
            Part(t, "Pe", PrimitiveType.Cube, new Vector3(length * 0.45f, 0.11f, 0.12f), new Vector3(0.05f, 0.22f, 0.12f), m);
            Part(t, "Valvula", PrimitiveType.Cylinder, new Vector3(length * 0.5f + 0.05f, 0.25f, 0.12f), new Vector3(0.08f, 0.03f, 0.08f), m);
        }

        private static void WaterCooler(Transform t, float k)
        {
            Material body = EnvMat.Lit("env_metal", Vector2.one, new Color(0.7f, 0.7f, 0.66f), 0.35f, 0.3f);
            Material glass = EnvMat.Glass(new Color(0.25f, 0.4f, 0.48f), 0.05f);
            Part(t, "Gabinete", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(0.42f, 1f, 0.42f), body);
            Part(t, "Torneira", PrimitiveType.Cube, new Vector3(0f, 0.86f, 0.23f), new Vector3(0.08f, 0.05f, 0.06f), EnvMat.Lit("env_metal", Vector2.one, new Color(0.2f, 0.25f, 0.6f), 0.5f, 0.6f));
            Part(t, "Bandeja", PrimitiveType.Cube, new Vector3(0f, 0.7f, 0.23f), new Vector3(0.2f, 0.02f, 0.08f), body);
            Lathe(t, "Garrafao", new Vector3(0f, 1f, 0f), new[] { new Vector2(0.04f, 0f), new Vector2(0.06f, 0.06f), new Vector2(0.17f, 0.12f), new Vector2(0.18f, 0.42f), new Vector2(0.16f, 0.5f), new Vector2(0.001f, 0.52f) }, glass);
            t.localScale = Vector3.one * k;
            Solid(t, new Vector3(0f, 0.75f, 0f), new Vector3(0.45f, 1.5f, 0.45f));
        }

        private static void TrashBin(Transform t, System.Random rng, Color c)
        {
            Material m = EnvMat.Lit("env_metal", Vector2.one, c, 0.4f, 0.5f);
            Material paper = EnvMat.Lit(null, Vector2.one, new Color(0.82f, 0.8f, 0.72f), 0.1f);
            Lathe(t, "Cesto", Vector3.zero, new[] { new Vector2(0.001f, 0.01f), new Vector2(0.17f, 0.01f), new Vector2(0.2f, 0.45f) }, m);
            for (int i = 0; i < 4; i++)
                Part(t, "Bolinha", PrimitiveType.Sphere, new Vector3((float)rng.NextDouble() * 0.14f - 0.07f, 0.38f + i * 0.03f, (float)rng.NextDouble() * 0.14f - 0.07f), Vector3.one * 0.09f, paper);
            Part(t, "Bolinha_Chao", PrimitiveType.Sphere, new Vector3(0.32f, 0.045f, 0.18f), Vector3.one * 0.09f, paper);
            t.localScale = Vector3.one * 1.2f;
        }

        private static void Plant(Transform t, System.Random rng, float k)
        {
            Material pot = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.45f, 0.24f, 0.16f), 0.2f);
            Material soil = EnvMat.Lit(null, Vector2.one, new Color(0.08f, 0.06f, 0.04f), 0.05f);
            Material leaf = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.11f, 0.2f, 0.1f), 0.45f);
            Material stem = EnvMat.Lit(null, Vector2.one, new Color(0.18f, 0.14f, 0.08f), 0.2f);
            Lathe(t, "Vaso", Vector3.zero, new[] { new Vector2(0.001f, 0f), new Vector2(0.17f, 0f), new Vector2(0.23f, 0.45f), new Vector2(0.25f, 0.48f) }, pot);
            Part(t, "Terra", PrimitiveType.Cylinder, new Vector3(0f, 0.43f, 0f), new Vector3(0.42f, 0.01f, 0.42f), soil);
            Part(t, "Caule", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.03f, 0.48f, 0.03f), stem);
            for (int i = 0; i < 16; i++)
            {
                float a = i * 137.5f;
                float h = 0.55f + i * 0.055f;
                float r = 0.12f + (float)rng.NextDouble() * 0.12f;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Part(t, "Folha", PrimitiveType.Sphere, new Vector3(0f, h, 0f) + dir * r, new Vector3(0.11f, 0.03f, 0.3f), leaf,
                    new Vector3(-20f - (float)rng.NextDouble() * 25f, a, 0f));
            }
            t.localScale = Vector3.one * k;
            Solid(t, new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 1.4f, 0.5f));
        }

        private static void DecanterSet(Transform t, System.Random rng)
        {
            Material tray = EnvMat.Lit("env_metal", Vector2.one, new Color(0.7f, 0.6f, 0.4f), 0.7f, 0.9f);
            Material glass = EnvMat.Glass(new Color(0.3f, 0.32f, 0.3f), 0f);
            Material whisky = EnvMat.Lit(null, Vector2.one, new Color(0.35f, 0.15f, 0.03f), 0.8f);
            Part(t, "Bandeja", PrimitiveType.Cylinder, new Vector3(0f, 0.008f, 0f), new Vector3(0.55f, 0.008f, 0.55f), tray);
            Lathe(t, "Licor", new Vector3(0f, 0.02f, 0f), new[] { new Vector2(0.001f, 0f), new Vector2(0.095f, 0f), new Vector2(0.1f, 0.12f), new Vector2(0.001f, 0.12f) }, whisky);
            Lathe(t, "Licoreira", new Vector3(0f, 0.016f, 0f), new[] { new Vector2(0.001f, 0f), new Vector2(0.11f, 0f), new Vector2(0.12f, 0.14f), new Vector2(0.06f, 0.24f), new Vector2(0.035f, 0.32f), new Vector2(0.045f, 0.34f) }, glass);
            Part(t, "Tampa", PrimitiveType.Sphere, new Vector3(0f, 0.38f, 0f), Vector3.one * 0.07f, glass);
            for (int i = 0; i < 2; i++)
                Lathe(t, "Copo", new Vector3(0.16f - i * 0.3f, 0.016f, 0.14f), new[] { new Vector2(0.001f, 0f), new Vector2(0.045f, 0f), new Vector2(0.05f, 0.1f) }, glass);
            t.localScale = Vector3.one * 1.2f;
        }

        private static void SmallTable(Transform t, System.Random rng, float k)
        {
            Material wood = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.32f, 0.21f, 0.14f), 0.3f);
            Part(t, "Tampo", PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), new Vector3(0.7f, 0.04f, 0.5f), wood);
            for (int i = 0; i < 4; i++)
                Part(t, "Perna", PrimitiveType.Cube, new Vector3((i % 2 == 0 ? -1 : 1) * 0.3f, 0.34f, (i < 2 ? -1 : 1) * 0.2f), new Vector3(0.045f, 0.68f, 0.045f), wood);
            // Biblia, vela e copo d'agua.
            Material book = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.08f, 0.07f, 0.06f), 0.35f);
            Part(t, "Biblia", PrimitiveType.Cube, new Vector3(-0.1f, 0.745f, 0f), new Vector3(0.2f, 0.05f, 0.27f), book, new Vector3(0f, 12f, 0f));
            Part(t, "Vela", PrimitiveType.Cylinder, new Vector3(0.2f, 0.79f, 0.08f), new Vector3(0.05f, 0.07f, 0.05f), EnvMat.Lit(null, Vector2.one, new Color(0.85f, 0.82f, 0.7f), 0.2f));
            Part(t, "Chama", PrimitiveType.Sphere, new Vector3(0.2f, 0.875f, 0.08f), new Vector3(0.02f, 0.04f, 0.02f), EnvMat.Glow(null, new Color(1f, 0.6f, 0.25f), 3f), null, false);
            t.localScale = Vector3.one * k;
            Solid(t, new Vector3(0f, 0.36f, 0f), new Vector3(0.72f, 0.72f, 0.52f));
        }

        private static void Gurney(Transform t, float k)
        {
            Material metal = EnvMat.Lit("env_metal", Vector2.one, new Color(0.6f, 0.62f, 0.6f), 0.55f, 0.8f);
            Material sheet = EnvMat.Lit(null, Vector2.one, new Color(0.62f, 0.66f, 0.64f), 0.05f);
            Material strap = EnvMat.Lit("env_plaster", Vector2.one, new Color(0.22f, 0.14f, 0.08f), 0.3f);
            Part(t, "Estrado", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0f), new Vector3(0.7f, 0.04f, 1.95f), metal);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new((i % 2 == 0 ? -1 : 1) * 0.3f, 0.36f, (i < 2 ? -1 : 1) * 0.88f);
                Part(t, "Perna", PrimitiveType.Cylinder, p, new Vector3(0.035f, 0.36f, 0.035f), metal);
                Part(t, "Roda", PrimitiveType.Cylinder, new Vector3(p.x, 0.05f, p.z), new Vector3(0.1f, 0.02f, 0.1f), strap, new Vector3(0f, 0f, 90f));
            }
            Part(t, "Colchao", PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(0.66f, 0.12f, 1.88f), sheet);
            Part(t, "Travesseiro", PrimitiveType.Cube, new Vector3(0f, 0.9f, -0.75f), new Vector3(0.5f, 0.08f, 0.3f), sheet);
            for (int i = 0; i < 3; i++)
                Part(t, "Correia", PrimitiveType.Cube, new Vector3(0f, 0.865f, -0.3f + i * 0.5f), new Vector3(0.7f, 0.015f, 0.08f), strap);
            t.localScale = Vector3.one * k;
            Solid(t, new Vector3(0f, 0.45f, 0f), new Vector3(0.72f, 0.9f, 1.97f));
        }

        private static void ElectricPanel(Transform t, Frame f, Material metal)
        {
            Material dark = EnvMat.Lit("env_metal", Vector2.one, new Color(0.08f, 0.09f, 0.09f), 0.4f, 0.5f);
            Material copper = EnvMat.Lit("env_metal", Vector2.one, new Color(0.75f, 0.42f, 0.22f), 0.7f, 1f);
            Part(t, "Gabinete", PrimitiveType.Cube, new Vector3(0f, 1.25f, -0.25f), new Vector3(1.7f, 2.5f, 0.5f), metal);
            Part(t, "Junta", PrimitiveType.Cube, new Vector3(0f, 1.25f, 0.002f), new Vector3(0.015f, 2.3f, 0.01f), dark);
            Part(t, "Rodape", PrimitiveType.Cube, new Vector3(0f, 0.05f, -0.24f), new Vector3(1.72f, 0.1f, 0.5f), dark);
            // Chave-faca: a peca mais reconhecivel da camara.
            Part(t, "Placa_Chave", PrimitiveType.Cube, new Vector3(0f, 1.25f, 0.02f), new Vector3(0.6f, 0.75f, 0.03f), EnvMat.Lit("env_plaster", Vector2.one, new Color(0.15f, 0.12f, 0.1f), 0.3f));
            for (int s = -1; s <= 1; s += 2)
                Part(t, "Lamina", PrimitiveType.Cube, new Vector3(s * 0.12f, 1.38f, 0.14f), new Vector3(0.04f, 0.45f, 0.012f), copper, new Vector3(-55f, 0f, 0f));
            Part(t, "Cabo_Chave", PrimitiveType.Cylinder, new Vector3(0f, 1.52f, 0.33f), new Vector3(0.05f, 0.17f, 0.05f), dark, new Vector3(0f, 0f, 90f));
            GameObject lamp = Part(t, "Lampada_Aviso", PrimitiveType.Sphere, new Vector3(0.62f, 2.3f, 0.03f), Vector3.one * 0.1f, EnvMat.Glow(null, new Color(1f, 0.15f, 0.08f), 2.5f), null, false);
            Light warn = Prims.MakeLight(t, "Luz_Aviso", lamp.transform.position + t.forward * 0.15f, LightType.Point, new Color(1f, 0.15f, 0.08f), 0.6f, 2.5f, false);
            NoirLightFlicker fl = warn.gameObject.AddComponent<NoirLightFlicker>();
            fl.target = warn; fl.emissive = lamp.GetComponent<Renderer>(); fl.emissiveColor = new Color(1f, 0.15f, 0.08f) * 2.5f; fl.pulseSpeed = 1.4f;
            // Eletrodutos subindo ate o teto.
            for (int i = 0; i < 3; i++)
            {
                Vector3 a = t.TransformPoint(new Vector3(-0.55f + i * 0.55f, 2.5f, -0.25f));
                Pipe(t, a, new Vector3(a.x, f.O.y + f.ceil - 0.1f, a.z), 0.09f, dark);
            }
            Solid(t, new Vector3(0f, 1.25f, -0.25f), new Vector3(1.7f, 2.5f, 0.5f));
        }

        private static void FuseBox(Transform t, Material metal)
        {
            Material dark = EnvMat.Lit("env_metal", Vector2.one, new Color(0.08f, 0.09f, 0.09f), 0.4f, 0.5f);
            Part(t, "Caixa", PrimitiveType.Cube, new Vector3(0f, 0f, 0.1f), new Vector3(0.6f, 0.8f, 0.2f), metal);
            Part(t, "Trinco", PrimitiveType.Cube, new Vector3(0.24f, 0f, 0.21f), new Vector3(0.03f, 0.1f, 0.03f), dark);
            Part(t, "Eletroduto", PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0.08f), new Vector3(0.07f, 1.2f, 0.07f), dark);
        }

        private static void Extinguisher(Transform t)
        {
            Material red = EnvMat.Lit("env_metal", Vector2.one, new Color(0.55f, 0.06f, 0.04f), 0.6f, 0.3f);
            Material dark = EnvMat.Lit(null, Vector2.one, new Color(0.05f, 0.05f, 0.05f), 0.4f);
            Lathe(t, "Cilindro", new Vector3(0f, -0.35f, 0.12f), new[] { new Vector2(0.001f, 0f), new Vector2(0.1f, 0f), new Vector2(0.1f, 0.5f), new Vector2(0.06f, 0.58f), new Vector2(0.02f, 0.6f) }, red);
            Part(t, "Valvula", PrimitiveType.Cube, new Vector3(0f, 0.28f, 0.12f), new Vector3(0.05f, 0.06f, 0.12f), dark);
            Part(t, "Mangueira", PrimitiveType.Cylinder, new Vector3(0.08f, 0.05f, 0.2f), new Vector3(0.025f, 0.22f, 0.025f), dark, new Vector3(0f, 0f, 12f));
            Part(t, "Suporte", PrimitiveType.Cube, new Vector3(0f, 0f, 0.02f), new Vector3(0.12f, 0.3f, 0.03f), dark);
            t.localScale = Vector3.one * 1.2f;
        }

        private static void Bucket(Transform t, float k)
        {
            Material zinc = EnvMat.Lit("env_metal", Vector2.one, new Color(0.5f, 0.52f, 0.5f), 0.5f, 0.8f);
            Material water = EnvMat.Lit(null, Vector2.one, new Color(0.08f, 0.09f, 0.08f), 0.9f);
            Material wood = EnvMat.Lit("env_wood_panel", Vector2.one, new Color(0.45f, 0.36f, 0.24f), 0.3f);
            Lathe(t, "Balde", Vector3.zero, new[] { new Vector2(0.001f, 0f), new Vector2(0.15f, 0f), new Vector2(0.19f, 0.32f), new Vector2(0.2f, 0.33f) }, zinc);
            Part(t, "Agua", PrimitiveType.Cylinder, new Vector3(0f, 0.24f, 0f), new Vector3(0.34f, 0.005f, 0.34f), water);
            Part(t, "Cabo_Esfregao", PrimitiveType.Cylinder, new Vector3(0.05f, 0.75f, -0.06f), new Vector3(0.03f, 0.65f, 0.03f), wood, new Vector3(-14f, 0f, 8f));
            t.localScale = Vector3.one * k;
        }

        private static void Typewriter(Transform t, float k)
        {
            Material body = EnvMat.Lit("env_metal", Vector2.one, new Color(0.06f, 0.06f, 0.06f), 0.65f, 0.4f);
            Material key = EnvMat.Lit(null, Vector2.one, new Color(0.8f, 0.78f, 0.7f), 0.4f);
            Material paper = EnvMat.Lit(null, Vector2.one, new Color(0.9f, 0.88f, 0.8f), 0.1f);
            Part(t, "Corpo", PrimitiveType.Cube, new Vector3(0f, 0.07f, 0f), new Vector3(0.42f, 0.14f, 0.34f), body);
            Part(t, "Teclado", PrimitiveType.Cube, new Vector3(0f, 0.09f, 0.16f), new Vector3(0.4f, 0.06f, 0.16f), body, new Vector3(18f, 0f, 0f));
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 9; c++)
                    Part(t, "Tecla", PrimitiveType.Cylinder, new Vector3(-0.16f + c * 0.04f + r * 0.01f, 0.115f + r * 0.018f, 0.23f - r * 0.035f), new Vector3(0.024f, 0.006f, 0.024f), key, null, false);
            Part(t, "Rolo", PrimitiveType.Cylinder, new Vector3(0f, 0.17f, -0.12f), new Vector3(0.06f, 0.26f, 0.06f), body, new Vector3(0f, 0f, 90f));
            Part(t, "Folha", PrimitiveType.Cube, new Vector3(0f, 0.28f, -0.14f), new Vector3(0.24f, 0.24f, 0.003f), paper, new Vector3(-12f, 0f, 0f), false);
            t.localScale = Vector3.one * k;
        }

        private static void CrateStack(Transform root, Frame f, float x, float z, float yaw, System.Random rng)
        {
            // Dois caixotes lado a lado (ao longo da parede da frente) e um terceiro em cima.
            Placed a = Model(root, f, "caixote", x, z, yaw + 4f, 0f, true, 0f, 1.05f);
            Placed b = Model(root, f, "caixote", x - 1.1f, z + 0.05f, yaw - 7f, 0f, true, 0f, 0.95f);
            if (a.ok)
                Model(root, f, "caixote", x + 0.05f, z + 0.05f, yaw + 15f, 0f, true, a.size.y, 0.85f);
            if (!a.ok && !b.ok)
            {
                // Sem a biblioteca: caixas de papelao no lugar dos caixotes.
                Transform t = Group(root, f, "Caixas", x, z, yaw);
                ArchiveBoxes(t, rng, 3, 0.5f);
            }
        }

        // ------------------------------------------------------------------ parede

        private static void WallClock(Transform t, float d, Material rim, Material hands)
        {
            Material face = EnvMat.Lit(null, Vector2.one, new Color(0.86f, 0.82f, 0.7f), 0.3f);
            Material ink = EnvMat.Lit(null, Vector2.one, new Color(0.08f, 0.07f, 0.06f), 0.3f);
            Part(t, "Aro", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.04f), new Vector3(d, 0.04f, d), rim, new Vector3(90f, 0f, 0f));
            Part(t, "Mostrador", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.082f), new Vector3(d * 0.86f, 0.003f, d * 0.86f), face, new Vector3(90f, 0f, 0f), false);
            for (int i = 0; i < 12; i++)
            {
                Quaternion q = Quaternion.Euler(0f, 0f, i * 30f);
                Part(t, "Marca", PrimitiveType.Cube, q * new Vector3(0f, d * 0.36f, 0f) + new Vector3(0f, 0f, 0.087f), new Vector3(0.012f, i % 3 == 0 ? 0.06f : 0.03f, 0.004f), ink, new Vector3(0f, 0f, i * 30f), false);
            }
            // Parado nas 11h55: o tempo que falta.
            Part(t, "Ponteiro_Hora", PrimitiveType.Cube, Quaternion.Euler(0f, 0f, 32f) * new Vector3(0f, d * 0.12f, 0f) + new Vector3(0f, 0f, 0.092f), new Vector3(0.02f, d * 0.24f, 0.004f), ink, new Vector3(0f, 0f, 32f), false);
            Part(t, "Ponteiro_Min", PrimitiveType.Cube, Quaternion.Euler(0f, 0f, 30f) * new Vector3(0f, d * 0.17f, 0f) + new Vector3(0f, 0f, 0.096f), new Vector3(0.012f, d * 0.34f, 0.004f), ink, new Vector3(0f, 0f, 30f), false);
            Part(t, "Eixo", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.1f), new Vector3(0.03f, 0.006f, 0.03f), hands, new Vector3(90f, 0f, 0f), false);
        }

        /// <summary>Quadro na parede. kind: 0 paisagem, 1 retrato, 2 diploma, 3 aviso datilografado.</summary>
        private static void Picture(Transform t, Vector2 size, int kind, Material frame)
        {
            const float b = 0.07f;
            Part(t, "Moldura_Fundo", PrimitiveType.Cube, new Vector3(0f, 0f, 0.012f), new Vector3(size.x + b * 2f, size.y + b * 2f, 0.024f), frame);
            Part(t, "Moldura_Topo", PrimitiveType.Cube, new Vector3(0f, size.y * 0.5f + b * 0.5f, 0.03f), new Vector3(size.x + b * 2f, b, 0.03f), frame);
            Part(t, "Moldura_Base", PrimitiveType.Cube, new Vector3(0f, -size.y * 0.5f - b * 0.5f, 0.03f), new Vector3(size.x + b * 2f, b, 0.03f), frame);
            Part(t, "Moldura_E", PrimitiveType.Cube, new Vector3(-size.x * 0.5f - b * 0.5f, 0f, 0.03f), new Vector3(b, size.y, 0.03f), frame);
            Part(t, "Moldura_D", PrimitiveType.Cube, new Vector3(size.x * 0.5f + b * 0.5f, 0f, 0.03f), new Vector3(b, size.y, 0.03f), frame);
            Material art = EnvMat.LitTexture("decor" + kind, DecorTexture(kind), Vector2.one, Color.white, kind == 0 || kind == 1 ? 0.35f : 0.1f);
            // O quad aparece para quem olha ao longo de +z; virado 180 graus, aparece para a sala.
            Part(t, "Tela", PrimitiveType.Quad, new Vector3(0f, 0f, 0.0255f), new Vector3(size.x, size.y, 1f), art, new Vector3(0f, 180f, 0f), false);
        }

        // ------------------------------------------------------------------ texturas geradas

        private static Texture2D DecorTexture(int kind)
        {
            decorTextures ??= new Texture2D[4];
            if (decorTextures[kind] != null)
                return decorTextures[kind];

            const int w = 128, h = 160;
            Texture2D t = new(w, h, TextureFormat.RGB24, true) { name = "NoirDecor" + kind, wrapMode = TextureWrapMode.Clamp };
            System.Random rng = new(101 + kind);
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h, n = (float)rng.NextDouble() * 0.06f;
                    Color c;
                    switch (kind)
                    {
                        case 0:
                            // Paisagem a oleo escurecida: ceu de entardecer, colinas e um rio.
                            float hill = 0.38f + 0.08f * Mathf.Sin(u * 7f + 1f) + 0.05f * Mathf.Sin(u * 17f);
                            if (v > hill) c = Color.Lerp(new Color(0.55f, 0.42f, 0.28f), new Color(0.22f, 0.24f, 0.28f), (v - hill) / (1f - hill));
                            else if (Mathf.Abs(v - 0.18f - 0.05f * Mathf.Sin(u * 5f)) < 0.025f) c = new Color(0.45f, 0.42f, 0.36f);
                            else c = Color.Lerp(new Color(0.1f, 0.12f, 0.07f), new Color(0.22f, 0.2f, 0.12f), v / hill);
                            break;
                        case 1:
                            // Retrato sobre fundo escuro: busto e cabeca em silhueta iluminada de lado.
                            float dx = u - 0.5f;
                            bool head = dx * dx / 0.028f + (v - 0.62f) * (v - 0.62f) / 0.034f < 1f;
                            bool body = v < 0.42f && Mathf.Abs(dx) < 0.18f + (0.42f - v) * 0.9f;
                            c = new Color(0.12f, 0.09f, 0.07f) + new Color(0.08f, 0.05f, 0.03f) * (1f - Mathf.Abs(dx) * 2f);
                            if (head) c = Color.Lerp(new Color(0.55f, 0.4f, 0.3f), new Color(0.2f, 0.14f, 0.1f), u);
                            if (body) c = Color.Lerp(new Color(0.16f, 0.15f, 0.16f), new Color(0.05f, 0.05f, 0.06f), u);
                            break;
                        case 2:
                            // Diploma: papel envelhecido, titulo, linhas de texto e selo vermelho.
                            c = new Color(0.86f, 0.8f, 0.64f);
                            if (v > 0.78f && v < 0.84f && Mathf.Abs(u - 0.5f) < 0.3f) c = new Color(0.2f, 0.16f, 0.12f);
                            else if (v > 0.32f && v < 0.68f && Mathf.Abs(u - 0.5f) < 0.34f && (y / 6) % 2 == 0 && rng.NextDouble() < 0.85) c = new Color(0.35f, 0.3f, 0.24f);
                            if ((u - 0.72f) * (u - 0.72f) + (v - 0.17f) * (v - 0.17f) * 0.64f < 0.006f) c = new Color(0.55f, 0.08f, 0.06f);
                            break;
                        default:
                            // Aviso datilografado com cabecalho e carimbo.
                            c = new Color(0.9f, 0.88f, 0.8f);
                            if (v > 0.84f && v < 0.9f && Mathf.Abs(u - 0.5f) < 0.36f) c = new Color(0.1f, 0.1f, 0.1f);
                            else if (v > 0.12f && v < 0.76f && u > 0.1f && u < 0.9f && (y / 5) % 2 == 0 && rng.NextDouble() < 0.8) c = new Color(0.25f, 0.25f, 0.25f);
                            if (Mathf.Abs(u - 0.68f) < 0.15f && Mathf.Abs(v - 0.2f) < 0.06f && rng.NextDouble() < 0.6) c = new Color(0.5f, 0.12f, 0.12f);
                            break;
                    }
                    px[y * w + x] = c * (1f - n);
                }
            }

            t.SetPixels(px);
            t.Apply();
            decorTextures[kind] = t;
            return t;
        }
    }
}
