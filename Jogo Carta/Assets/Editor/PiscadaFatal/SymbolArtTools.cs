using System.IO;
using ProjetoCarta.Puzzles;
using UnityEditor;
using UnityEngine;

namespace ProjetoCarta.EditorTools
{
    /// <summary>
    /// Arte do cofre de simbolos (P1), toda derivada do papel original SenhaCofre.png:
    /// - recorta os cinco quadrados coloridos em sprites com transparencia (botoes do keypad);
    /// - gera um papel em branco e "assa" o papel da senha na ordem de <see cref="SymbolCodeDefinition.Solution"/>.
    /// O papel e reassado automaticamente quando o SymbolCodeDefinition muda no Inspector.
    /// </summary>
    [InitializeOnLoad]
    public static class SymbolArtTools
    {
        public const string SourcePaper = "Assets/Model/ModelsPF/Folha/SenhaCofre.png";
        public const string OutputFolder = "Assets/PiscadaFatal/Simbolos";
        public const string BakedPaper = OutputFolder + "/papel_senha_simbolos.png";
        public const string BlankPaper = OutputFolder + "/papel_em_branco.png";

        // Centros (em pixels, origem no topo) dos cinco quadrados do papel original.
        private static readonly int[] SlotCentersFromTop = { 318, 658, 999, 1340, 1680 };
        private const int SlotCenterX = 707;
        private const int CropSize = 344;
        private static readonly string[] SymbolFiles = { "simbolo_vermelho", "simbolo_azul", "simbolo_amarelo", "simbolo_cinza", "simbolo_marrom" };

        static SymbolArtTools()
        {
            SymbolCodeDefinition.EditorValidated -= OnCodeValidated;
            SymbolCodeDefinition.EditorValidated += OnCodeValidated;
        }

        private static void OnCodeValidated(SymbolCodeDefinition code)
        {
            EditorApplication.delayCall += () =>
            {
                if (code != null && File.Exists(BlankPaper))
                    BakePaper(code);
            };
        }

        [MenuItem("Projeto Carta/Piscada Fatal/Recortar simbolos do papel")]
        public static void ExtractSymbols()
        {
            Texture2D source = LoadReadable(SourcePaper);
            EnsureFolder();

            for (int i = 0; i < SymbolFiles.Length; i++)
            {
                Texture2D crop = CropWithAlpha(source, SlotCenterX, SlotCentersFromTop[i]);
                WritePng(crop, OutputFolder + "/" + SymbolFiles[i] + ".png", true);
                Object.DestroyImmediate(crop);
            }

            Texture2D blank = MakeBlankPaper(source);
            WritePng(blank, BlankPaper, false);
            Object.DestroyImmediate(blank);
            Object.DestroyImmediate(source);
            AssetDatabase.Refresh();
        }

        public static Sprite LoadSymbolSprite(int index)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(OutputFolder + "/" + SymbolFiles[index] + ".png");
        }

        /// <summary>Gera o papel com os simbolos na ordem da solucao e devolve a textura importada.</summary>
        public static Texture2D BakePaper(SymbolCodeDefinition code)
        {
            Texture2D paper = LoadReadable(BlankPaper);
            for (int slot = 0; slot < code.Length && slot < SlotCentersFromTop.Length; slot++)
            {
                SymbolCodeDefinition.Symbol symbol = code.GetSymbol(code.Solution[slot]);
                if (symbol?.sprite == null)
                    continue;

                Texture2D art = LoadReadable(AssetDatabase.GetAssetPath(symbol.sprite.texture));
                Stamp(paper, art, SlotCenterX, paper.height - SlotCentersFromTop[slot]);
                Object.DestroyImmediate(art);
            }

            WritePng(paper, BakedPaper, false);
            Object.DestroyImmediate(paper);
            AssetDatabase.ImportAsset(BakedPaper, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(BakedPaper);
        }

        // ------------------------------------------------------------ processamento

        private static Texture2D CropWithAlpha(Texture2D source, int centerX, int centerFromTop)
        {
            int half = CropSize / 2;
            int x0 = Mathf.Clamp(centerX - half, 0, source.width - CropSize);
            int y0 = Mathf.Clamp(source.height - centerFromTop - half, 0, source.height - CropSize);
            Color[] pixels = source.GetPixels(x0, y0, CropSize, CropSize);

            // Cor do papel = media da borda do recorte.
            Color paper = Color.black;
            int samples = 0;
            for (int i = 0; i < CropSize; i++)
            {
                for (int edge = 0; edge < 6; edge++)
                {
                    paper += pixels[edge * CropSize + i];
                    paper += pixels[(CropSize - 1 - edge) * CropSize + i];
                    paper += pixels[i * CropSize + edge];
                    paper += pixels[i * CropSize + CropSize - 1 - edge];
                    samples += 4;
                }
            }

            paper /= samples;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float distance = Mathf.Sqrt(Sq(c.r - paper.r) + Sq(c.g - paper.g) + Sq(c.b - paper.b)) * 255f;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16f, 70f, distance));
                if (alpha <= 0.001f)
                {
                    pixels[i] = new Color(paper.r, paper.g, paper.b, 0f);
                    continue;
                }

                // Remove o papel misturado na borda do traco.
                Color ink = (c - paper * (1f - alpha)) / alpha;
                pixels[i] = new Color(Mathf.Clamp01(ink.r), Mathf.Clamp01(ink.g), Mathf.Clamp01(ink.b), alpha);
            }

            Texture2D crop = new(CropSize, CropSize, TextureFormat.RGBA32, false);
            crop.SetPixels(pixels);
            crop.Apply();
            return crop;
        }

        private static Texture2D MakeBlankPaper(Texture2D source)
        {
            Texture2D blank = new(source.width, source.height, TextureFormat.RGBA32, false);
            Color[] pixels = source.GetPixels();
            int width = source.width;
            int left = SlotCenterX - CropSize / 2 - 30;
            int right = SlotCenterX + CropSize / 2 + 30;
            int offset = right - left + 40;
            const int feather = 24;

            for (int y = 0; y < source.height; y++)
            {
                for (int x = left; x < right; x++)
                {
                    int sx = Mathf.Clamp(x - offset, 0, width - 1);
                    Color patch = pixels[y * width + sx];
                    float edge = Mathf.Min(x - left, right - 1 - x) / (float)feather;
                    float t = Mathf.Clamp01(edge);
                    pixels[y * width + x] = Color.Lerp(pixels[y * width + x], patch, t);
                }
            }

            blank.SetPixels(pixels);
            blank.Apply();
            return blank;
        }

        private static void Stamp(Texture2D paper, Texture2D art, int centerX, int centerY)
        {
            int x0 = centerX - art.width / 2;
            int y0 = centerY - art.height / 2;
            for (int y = 0; y < art.height; y++)
            {
                int py = y0 + y;
                if (py < 0 || py >= paper.height)
                    continue;

                for (int x = 0; x < art.width; x++)
                {
                    int px = x0 + x;
                    if (px < 0 || px >= paper.width)
                        continue;

                    Color ink = art.GetPixel(x, y);
                    if (ink.a <= 0.001f)
                        continue;

                    Color under = paper.GetPixel(px, py);
                    paper.SetPixel(px, py, Color.Lerp(under, new Color(ink.r, ink.g, ink.b, 1f), ink.a));
                }
            }

            paper.Apply();
        }

        // ------------------------------------------------------------ arquivos

        private static Texture2D LoadReadable(string assetPath)
        {
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(assetPath));
            return texture;
        }

        private static void WritePng(Texture2D texture, string assetPath, bool asSprite)
        {
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return;

            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = !asSprite;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureType = asSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (asSprite)
                importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/PiscadaFatal", "Simbolos");
        }

        private static float Sq(float value)
        {
            return value * value;
        }
    }
}
