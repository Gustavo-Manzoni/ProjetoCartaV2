using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjetoCarta.Puzzles;
using UnityEditor;
using UnityEngine;

namespace ProjetoCarta.EditorTools
{
    /// <summary>
    /// Arte desenhada por codigo para os documentos novos (sem texto na imagem: o texto vem do TMP e do Loc):
    /// - placa_poco.png: placa esmaltada do guincho com os pictogramas balde/manivela, pasta->envelope e ima->relogio.
    /// O relatorio de manutencao nao tem desenho: a pista da trava de discos sao os horarios escritos no texto
    /// (o ProjectDataTests confere que batem com a solucao do <see cref="DialLockMechanism"/>).
    /// </summary>
    public static class CaseDocumentArtTools
    {
        public const string OutputFolder = "Assets/PiscadaFatal/Documentos";
        public const string PlaquePath = OutputFolder + "/placa_poco.png";

        private static readonly Color Mark = new Color32(150, 36, 32, 255);
        private static readonly Color Enamel = new Color32(226, 218, 194, 255);
        private static readonly Color Navy = new Color32(30, 44, 70, 255);
        // Coordenadas da imagem tem y para baixo.
        private static readonly Vector2 VisualDown = new(0f, 1f);
        private static readonly Vector2 VisualUp = new(0f, -1f);

        [MenuItem("Projeto Carta/Piscada Fatal/Gerar arte dos documentos do caso")]
        public static void GenerateAll()
        {
            GeneratePlaque();
        }

        // ------------------------------------------------------------ placa do poco

        public static void GeneratePlaque()
        {
            Raster c = new(1200, 900, Enamel);
            c.FrameRect(24f, 24f, 1152f, 852f, 14f, Navy);
            c.FrameRect(48f, 48f, 1104f, 804f, 3f, Navy);
            c.Line(424f, 80f, 424f, 820f, 4f, Navy);
            c.Line(788f, 80f, 788f, 820f, 4f, Navy);

            DrawWinchPanel(c, 236f);
            DrawFolderPanel(c, 606f);
            DrawMagnetPanel(c, 970f);

            WriteTexture(c, PlaquePath, null);
        }

        private static void DrawWinchPanel(Raster c, float x)
        {
            x -= 40f;
            // Tambor com a corda, a manivela encaixada na ponta do eixo e a seta de giro.
            c.Line(x - 150f, 170f, x + 110f, 170f, 10f, Navy);
            c.FillRect(x - 90f, 130f, 150f, 80f, Navy);
            for (int i = 0; i < 5; i++)
                c.Line(x - 80f + i * 30f, 132f, x - 66f + i * 30f, 208f, 4f, Enamel);
            c.FillRect(x + 110f, 158f, 24f, 24f, Navy);
            c.Line(x + 122f, 170f, x + 122f, 255f, 12f, Navy);
            c.Line(x + 122f, 255f, x + 165f, 255f, 16f, Navy);
            c.Arc(x + 122f, 170f, 64f, -60f, 105f, 6f, Navy);
            c.Arrowhead(Polar(x + 122f, 170f, 64f, 105f), new Vector2(-Mathf.Sin(105f * Mathf.Deg2Rad), Mathf.Cos(105f * Mathf.Deg2Rad)), 20f, Navy);

            // Corda, gancho e balde preso nele.
            c.Line(x - 15f, 210f, x - 15f, 520f, 6f, Navy);
            c.Arc(x - 15f, 540f, 20f, 180f, 420f, 6f, Navy);
            c.Arc(x - 15f, 620f, 60f, 200f, 340f, 6f, Navy);
            c.Polygon(new[] { new Vector2(x - 85f, 610f), new Vector2(x + 55f, 610f), new Vector2(x + 35f, 760f), new Vector2(x - 65f, 760f) }, Navy);
            c.Line(x - 90f, 610f, x + 60f, 610f, 10f, Navy);
        }

        private static void DrawFolderPanel(Raster c, float x)
        {
            // Pasta com o clipe de metal.
            DrawFolder(c, x - 70f, 120f, 140f, 110f);
            DrawClip(c, x + 10f, 108f, 1f);
            DrawDescent(c, x, 260f);
            // Envelope lacrado subindo preso pelo clipe.
            DrawEnvelope(c, x - 75f, 650f, 150f, 100f);
            DrawClip(c, x - 10f, 638f, 1f);
            c.Arrowhead(new Vector2(x + 115f, 560f), VisualUp, 30f, Navy);
            c.Line(x + 115f, 590f, x + 115f, 740f, 6f, Navy);
        }

        private static void DrawMagnetPanel(Raster c, float x)
        {
            DrawMagnet(c, x, 120f);
            DrawDescent(c, x, 260f);
            // Ima subindo com o relogio de bolso grudado nos polos.
            DrawMagnet(c, x, 580f);
            DrawWatch(c, x, 760f, 52f);
            c.Arrowhead(new Vector2(x + 115f, 560f), VisualUp, 30f, Navy);
            c.Line(x + 115f, 590f, x + 115f, 740f, 6f, Navy);
        }

        private static void DrawDescent(Raster c, float x, float y)
        {
            // Seta para baixo ate a agua do fundo do poco.
            c.Line(x, y, x, y + 140f, 6f, Navy);
            c.Arrowhead(new Vector2(x, y + 175f), VisualDown, 30f, Navy);
            for (int row = 0; row < 2; row++)
            {
                float wy = y + 215f + row * 26f;
                for (float wx = x - 110f; wx < x + 110f; wx += 6f)
                    c.Line(wx, wy + Mathf.Sin(wx * 0.09f) * 7f, wx + 6f, wy + Mathf.Sin((wx + 6f) * 0.09f) * 7f, 4f, Navy);
            }
        }

        private static void DrawFolder(Raster c, float x, float y, float w, float h)
        {
            c.FillRect(x, y + 14f, w, h, Navy);
            c.FillRect(x + 8f, y, w * 0.4f, 20f, Navy);
            c.Line(x + 12f, y + 40f, x + w - 12f, y + 40f, 3f, Enamel);
        }

        private static void DrawEnvelope(Raster c, float x, float y, float w, float h)
        {
            c.FrameRect(x, y, w, h, 7f, Navy);
            c.Line(x, y, x + w * 0.5f, y + h * 0.55f, 6f, Navy);
            c.Line(x + w, y, x + w * 0.5f, y + h * 0.55f, 6f, Navy);
            c.Disc(x + w * 0.5f, y + h * 0.55f, 13f, Navy);
        }

        private static void DrawClip(Raster c, float x, float y, float scale)
        {
            // Clipe de papel: duas voltas alongadas.
            c.Capsule(x, y, x, y + 70f * scale, 15f * scale, 5f, Mark);
            c.Capsule(x + 2f, y + 10f * scale, x + 2f, y + 56f * scale, 8f * scale, 4f, Mark);
        }

        private static void DrawMagnet(Raster c, float x, float y)
        {
            // Ferradura: arco grosso e dois polos claros.
            c.Arc(x, y + 30f, 55f, 180f, 360f, 34f, Navy);
            c.Line(x - 55f, y + 30f, x - 55f, y + 110f, 34f, Navy);
            c.Line(x + 55f, y + 30f, x + 55f, y + 110f, 34f, Navy);
            c.FillRect(x - 72f, y + 108f, 34f, 26f, Mark);
            c.FillRect(x + 38f, y + 108f, 34f, 26f, Mark);
        }

        private static void DrawWatch(Raster c, float x, float y, float r)
        {
            c.Ring(x, y, r, 8f, Navy);
            c.Disc(x, y - r - 10f, 10f, Navy);
            c.Line(x, y, x, y - r * 0.6f, 5f, Navy);
            c.Line(x, y, x + r * 0.45f, y + r * 0.2f, 5f, Navy);
            c.Disc(x, y, 6f, Navy);
        }

        private static Vector2 Polar(float cx, float cy, float r, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(cx + Mathf.Cos(rad) * r, cy + Mathf.Sin(rad) * r);
        }

        // ------------------------------------------------------------ escrita

        private static void WriteTexture(Raster canvas, string path, string userData)
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/PiscadaFatal", "Documentos");

            File.WriteAllBytes(path, canvas.EncodePng());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            // Sem redimensionar para potencia de 2: a leitura em tela cheia usa a proporcao da textura importada.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.userData = userData ?? string.Empty;
            importer.SaveAndReimport();
        }

        /// <summary>Rasterizador minimo (coordenadas com origem no topo, cobertura por distancia para suavizar).</summary>
        private sealed class Raster
        {
            private readonly Color[] pixels;

            public int Width { get; }
            public int Height { get; }

            public Raster(int width, int height, Color background)
            {
                Width = width;
                Height = height;
                pixels = Enumerable.Repeat(background, width * height).ToArray();
            }

            private Raster(Texture2D source)
            {
                Width = source.width;
                Height = source.height;
                pixels = source.GetPixels();
            }

            public static Raster FromPaper(string assetPath)
            {
                Texture2D tex = new(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(assetPath));
                Raster canvas = new(tex);
                Object.DestroyImmediate(tex);
                return canvas;
            }

            public byte[] EncodePng()
            {
                Texture2D tex = new(Width, Height, TextureFormat.RGBA32, false);
                tex.SetPixels(pixels);
                tex.Apply();
                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                return png;
            }

            private void Blend(int x, int y, Color color, float coverage)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height || coverage <= 0f)
                    return;

                int index = (Height - 1 - y) * Width + x;
                float a = Mathf.Clamp01(coverage) * color.a;
                Color dst = pixels[index];
                pixels[index] = new Color(
                    Mathf.Lerp(dst.r, color.r, a),
                    Mathf.Lerp(dst.g, color.g, a),
                    Mathf.Lerp(dst.b, color.b, a),
                    Mathf.Max(dst.a, a));
            }

            /// <summary>Pinta pela distancia ate a forma (negativa dentro), com 1 px de suavizacao.</summary>
            private void Shape(float minX, float minY, float maxX, float maxY, System.Func<Vector2, float> distance, Color color)
            {
                int x0 = Mathf.FloorToInt(minX) - 2, y0 = Mathf.FloorToInt(minY) - 2;
                int x1 = Mathf.CeilToInt(maxX) + 2, y1 = Mathf.CeilToInt(maxY) + 2;
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = distance(new Vector2(x + 0.5f, y + 0.5f));
                        Blend(x, y, color, 0.5f - d);
                    }
                }
            }

            public void Disc(float cx, float cy, float r, Color color)
            {
                Vector2 c = new(cx, cy);
                Shape(cx - r, cy - r, cx + r, cy + r, p => Vector2.Distance(p, c) - r, color);
            }

            public void Ring(float cx, float cy, float r, float width, Color color)
            {
                Vector2 c = new(cx, cy);
                float outer = r + width * 0.5f;
                Shape(cx - outer, cy - outer, cx + outer, cy + outer, p => Mathf.Abs(Vector2.Distance(p, c) - r) - width * 0.5f, color);
            }

            public void Arc(float cx, float cy, float r, float fromDegrees, float toDegrees, float width, Color color)
            {
                Vector2 c = new(cx, cy);
                float outer = r + width;
                float from = fromDegrees * Mathf.Deg2Rad, to = toDegrees * Mathf.Deg2Rad;
                Shape(cx - outer, cy - outer, cx + outer, cy + outer, p =>
                {
                    Vector2 d = p - c;
                    float angle = Mathf.Atan2(d.y, d.x);
                    while (angle < from) angle += 2f * Mathf.PI;
                    if (angle <= to)
                        return Mathf.Abs(d.magnitude - r) - width * 0.5f;

                    Vector2 a = c + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * r;
                    Vector2 b = c + new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * r;
                    return Mathf.Min(Vector2.Distance(p, a), Vector2.Distance(p, b)) - width * 0.5f;
                }, color);
            }

            public void Line(float x0, float y0, float x1, float y1, float width, Color color)
            {
                Capsule(x0, y0, x1, y1, width * 0.5f, 0f, color);
            }

            /// <summary>Segmento com raio (cheio se stroke = 0; contorno de espessura stroke se maior).</summary>
            public void Capsule(float x0, float y0, float x1, float y1, float radius, float stroke, Color color)
            {
                Vector2 a = new(x0, y0), b = new(x1, y1);
                float pad = radius + stroke;
                Shape(Mathf.Min(x0, x1) - pad, Mathf.Min(y0, y1) - pad, Mathf.Max(x0, x1) + pad, Mathf.Max(y0, y1) + pad, p =>
                {
                    Vector2 ab = b - a;
                    float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                    float d = Vector2.Distance(p, a + ab * t) - radius;
                    return stroke > 0f ? Mathf.Abs(d) - stroke * 0.5f : d;
                }, color);
            }

            public void FillRect(float x, float y, float w, float h, Color color)
            {
                Shape(x, y, x + w, y + h, p =>
                {
                    float dx = Mathf.Max(x - p.x, p.x - (x + w));
                    float dy = Mathf.Max(y - p.y, p.y - (y + h));
                    return Mathf.Max(dx, dy);
                }, color);
            }

            public void FrameRect(float x, float y, float w, float h, float width, Color color)
            {
                Line(x, y, x + w, y, width, color);
                Line(x + w, y, x + w, y + h, width, color);
                Line(x + w, y + h, x, y + h, width, color);
                Line(x, y + h, x, y, width, color);
            }

            public void Polygon(IReadOnlyList<Vector2> points, Color color)
            {
                float minX = points.Min(p => p.x), maxX = points.Max(p => p.x);
                float minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
                Shape(minX, minY, maxX, maxY, p =>
                {
                    float d = float.MaxValue;
                    bool inside = false;
                    for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                    {
                        Vector2 a = points[j], b = points[i];
                        Vector2 ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                        d = Mathf.Min(d, Vector2.Distance(p, a + ab * t));
                        if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                            inside = !inside;
                    }

                    return inside ? -d : d;
                }, color);
            }

            public void Arrowhead(Vector2 tip, Vector2 direction, float size, Color color)
            {
                Vector2 dir = direction.normalized;
                Vector2 side = new(-dir.y, dir.x);
                Polygon(new[] { tip, tip - dir * size * 1.4f + side * size, tip - dir * size * 1.4f - side * size }, color);
            }
        }
    }
}
