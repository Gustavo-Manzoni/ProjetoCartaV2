using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjetoCarta.EditorTools
{
    /// <summary>
    /// Arte das fotos do menu principal (cena MainMenu), desenhada por codigo e salva como PNG:
    /// - quatro polaroides das opcoes (Audio, Idioma, Graficos, Creditos), cada uma com um icone claro;
    /// - papel da foto do caso (moldura creme da polaroide).
    /// Cria os materiais e aplica nas fotos da cena aberta.
    /// </summary>
    public static class MenuArtTools
    {
        public const string OutputFolder = "Assets/Menu/Arte";
        private const int Width = 512, Height = 600;

        private static readonly Color Frame = new(0.92f, 0.89f, 0.82f);
        private static readonly Color Ink = new(0.93f, 0.86f, 0.7f);
        private static readonly Color BackTop = new(0.24f, 0.18f, 0.13f);
        private static readonly Color BackBottom = new(0.1f, 0.075f, 0.06f);

        [MenuItem("Projeto Carta/Menu/Gerar fotos das opcoes")]
        public static void GenerateAndApply()
        {
            Directory.CreateDirectory(OutputFolder);
            foreach (OptionsPhotoType type in System.Enum.GetValues(typeof(OptionsPhotoType)))
            {
                string path = $"{OutputFolder}/foto_{type.ToString().ToLowerInvariant()}.png";
                File.WriteAllBytes(path, Draw(type).EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                ConfigureImporter(path);
                Material m = MaterialFor(path, $"{OutputFolder}/Foto_{type}.mat", Color.white);
                ApplyToScene(type, m);
            }

            Material caseFrame = MaterialFor(null, $"{OutputFolder}/Foto_Caso_Moldura.mat", Frame);
            foreach (CaseFileLauncher launcher in Object.FindObjectsByType<CaseFileLauncher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform visual = launcher.transform.parent != null ? launcher.transform.parent.Find("Visual") : null;
                if (visual != null && visual.TryGetComponent(out Renderer r))
                {
                    Undo.RecordObject(r, "Foto do caso");
                    r.sharedMaterial = caseFrame;
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void ConfigureImporter(string path)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        private static Material MaterialFor(string texturePath, string materialPath, Color tint)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, materialPath);
            }

            Texture2D tex = texturePath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) : null;
            m.SetTexture("_BaseMap", tex);
            // A face visivel do cubo da foto (-z local, girado 270 graus no quadro) mostra a UV girada 180 graus.
            m.SetTextureScale("_BaseMap", tex != null ? new Vector2(-1f, -1f) : Vector2.one);
            m.SetTextureOffset("_BaseMap", tex != null ? Vector2.one : Vector2.zero);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", 0.18f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void ApplyToScene(OptionsPhotoType type, Material m)
        {
            foreach (OptionsPhotoBehaviour photo in Object.FindObjectsByType<OptionsPhotoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject so = new(photo);
                if (so.FindProperty("optionType").enumValueIndex != (int)type)
                    continue;

                Transform visual = photo.transform.parent != null ? photo.transform.parent.Find("Visual") : null;
                if (visual != null && visual.TryGetComponent(out Renderer r))
                {
                    Undo.RecordObject(r, "Foto de opcao");
                    r.sharedMaterial = m;
                }
            }
        }

        // ------------------------------------------------------------------ desenho

        private static Texture2D Draw(OptionsPhotoType type)
        {
            Texture2D t = new(Width, Height, TextureFormat.RGB24, true);
            Color[] px = new Color[Width * Height];
            // Area da imagem da polaroide (margem maior embaixo).
            const int left = 34, right = Width - 34, top = Height - 34, bottom = 128;
            System.Random rng = new(5 + (int)type);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Color c;
                    if (x < left || x >= right || y < bottom || y >= top)
                    {
                        c = Frame * (0.97f + (float)rng.NextDouble() * 0.03f);
                    }
                    else
                    {
                        // Fundo sepia escuro com vinheta.
                        float u = (x - left) / (float)(right - left);
                        float v = (y - bottom) / (float)(top - bottom);
                        Vector2 p = new(u, v);
                        float vig = Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(0.5f, 0.55f)) * 0.9f);
                        c = Color.Lerp(BackBottom, BackTop, v) * (0.55f + 0.45f * vig);
                        c *= 0.96f + (float)rng.NextDouble() * 0.08f;
                        float a = Icon(type, p, 1f / (right - left));
                        c = Color.Lerp(c, Ink, a);
                    }

                    px[y * Width + x] = c;
                }
            }

            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Cobertura (0..1) do icone no ponto p (0..1 dentro da imagem).</summary>
        private static float Icon(OptionsPhotoType type, Vector2 p, float pixel)
        {
            float aa = pixel * 1.5f;
            switch (type)
            {
                case OptionsPhotoType.Audio:
                {
                    float body = Fill(Box(p, new Vector2(0.24f, 0.42f), new Vector2(0.36f, 0.58f)), aa);
                    float cone = Fill(Cone(p), aa);
                    Vector2 c = new(0.42f, 0.5f);
                    float angle = Mathf.Abs(Mathf.Atan2(p.y - c.y, p.x - c.x));
                    float arcMask = angle < 0.85f ? 1f : 0f;
                    float r1 = Fill(Mathf.Abs(Vector2.Distance(p, c) - 0.17f) - 0.018f, aa) * arcMask;
                    float r2 = Fill(Mathf.Abs(Vector2.Distance(p, c) - 0.27f) - 0.018f, aa) * arcMask;
                    return Mathf.Max(body, cone, r1, r2);
                }
                case OptionsPhotoType.Graphics:
                {
                    float screen = Stroke(RoundBox(p, new Vector2(0.5f, 0.56f), new Vector2(0.3f, 0.21f), 0.03f), 0.018f, aa);
                    float stand = Fill(Box(p, new Vector2(0.46f, 0.25f), new Vector2(0.54f, 0.35f)), aa);
                    float foot = Fill(RoundBox(p, new Vector2(0.5f, 0.235f), new Vector2(0.14f, 0.018f), 0.012f), aa);
                    // paisagem na tela: montanha e sol
                    float mountain = Fill(Mountain(p), aa);
                    float sun = Fill(Vector2.Distance(p, new Vector2(0.62f, 0.64f)) - 0.05f, aa);
                    return Mathf.Max(screen, stand, foot, mountain, sun);
                }
                case OptionsPhotoType.Language:
                {
                    float a = Stroke(RoundBox(p, new Vector2(0.4f, 0.64f), new Vector2(0.21f, 0.13f), 0.05f), 0.016f, aa);
                    float tailA = Fill(Triangle(p, new Vector2(0.27f, 0.53f), new Vector2(0.35f, 0.53f), new Vector2(0.24f, 0.44f)), aa);
                    float b = Fill(RoundBox(p, new Vector2(0.6f, 0.36f), new Vector2(0.21f, 0.12f), 0.05f), aa);
                    float tailB = Fill(Triangle(p, new Vector2(0.66f, 0.26f), new Vector2(0.74f, 0.26f), new Vector2(0.78f, 0.17f)), aa);
                    // linhas de "texto": claras no balao vazado, escuras (recortadas) no balao cheio
                    float linesA = 0f;
                    for (int i = 0; i < 2; i++)
                        linesA = Mathf.Max(linesA, Fill(RoundBox(p, new Vector2(0.4f - i * 0.03f, 0.68f - i * 0.075f), new Vector2(0.13f - i * 0.03f, 0.014f), 0.012f), aa));
                    float cut = 0f;
                    for (int i = 0; i < 2; i++)
                        cut = Mathf.Max(cut, Fill(RoundBox(p, new Vector2(0.6f - i * 0.025f, 0.4f - i * 0.075f), new Vector2(0.13f - i * 0.025f, 0.014f), 0.012f), aa));
                    return Mathf.Max(a, tailA, linesA, Mathf.Max(b, tailB) * (1f - cut));
                }
                default:
                {
                    // Claquete de cinema (creditos).
                    float body = Fill(RoundBox(p, new Vector2(0.5f, 0.4f), new Vector2(0.27f, 0.17f), 0.02f), aa);
                    float line = Fill(Box(p, new Vector2(0.27f, 0.47f), new Vector2(0.73f, 0.49f)), aa);
                    Vector2 hinge = new(0.24f, 0.6f);
                    Vector2 q = Rotate(p - hinge, 14f * Mathf.Deg2Rad) + hinge;
                    float clap = Fill(Box(q, new Vector2(0.24f, 0.6f), new Vector2(0.77f, 0.69f)), aa);
                    float stripes = Mathf.Repeat((q.x + q.y) * 9f, 1f) < 0.5f ? 1f : 0f;
                    float topBar = clap * (0.25f + 0.75f * stripes);
                    float bottomStripes = Fill(Box(p, new Vector2(0.23f, 0.52f), new Vector2(0.77f, 0.57f)), aa) *
                                          (Mathf.Repeat((p.x - p.y) * 9f, 1f) < 0.5f ? 1f : 0.25f);
                    return Mathf.Max(body * (1f - line * 0.85f), topBar, bottomStripes);
                }
            }
        }

        private static float Fill(float distance, float aa) => Mathf.Clamp01(0.5f - distance / aa);
        private static float Stroke(float distance, float halfWidth, float aa) => Fill(Mathf.Abs(distance) - halfWidth, aa);

        private static float Box(Vector2 p, Vector2 min, Vector2 max)
        {
            Vector2 c = (min + max) * 0.5f, h = (max - min) * 0.5f;
            return RoundBox(p, c, h, 0f);
        }

        private static float RoundBox(Vector2 p, Vector2 center, Vector2 half, float r)
        {
            Vector2 d = new(Mathf.Abs(p.x - center.x) - half.x + r, Mathf.Abs(p.y - center.y) - half.y + r);
            Vector2 outside = new(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - r;
        }

        private static float Cone(Vector2 p)
        {
            // trapezio: estreito no corpo do alto-falante, largo na boca
            float x0 = 0.36f, x1 = 0.5f;
            if (p.x < x0 - 0.01f || p.x > x1) return Mathf.Max(x0 - p.x, p.x - x1);
            float t = Mathf.InverseLerp(x0, x1, p.x);
            float half = Mathf.Lerp(0.08f, 0.2f, t);
            return Mathf.Abs(p.y - 0.5f) - half;
        }

        private static float Mountain(Vector2 p)
        {
            // dois picos dentro da tela
            float baseY = 0.4f;
            float h1 = 0.2f - Mathf.Abs(p.x - 0.42f) * 1.1f;
            float h2 = 0.13f - Mathf.Abs(p.x - 0.6f) * 1.0f;
            float h = Mathf.Max(h1, h2);
            if (p.x < 0.23f || p.x > 0.77f || p.y < baseY - 0.02f) return 1f;
            return p.y - (baseY + h);
        }

        private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float e0 = Edge(p, a, b), e1 = Edge(p, b, c), e2 = Edge(p, c, a);
            bool ccw = Edge(c, a, b) > 0f;
            return ccw ? -Mathf.Min(e0, Mathf.Min(e1, e2)) : Mathf.Max(e0, Mathf.Max(e1, e2));
        }

        private static float Edge(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            Vector2 n = new Vector2(-e.y, e.x).normalized;
            return Vector2.Dot(p - a, n);
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
