using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProjetoCarta.NoirUI
{
    /// <summary>Paleta, sprites (carregados de Resources/NoirUI) e fonte compartilhados pela UI noir.</summary>
    public static class NoirTheme
    {
        public static readonly Color Ink = new Color32(12, 12, 14, 255);
        public static readonly Color Paper = new Color32(232, 224, 200, 255);
        public static readonly Color Ash = new Color32(190, 186, 172, 255);
        public static readonly Color Amber = new Color32(222, 168, 70, 255);
        public static readonly Color Blood = new Color32(168, 52, 56, 255);
        public static readonly Color Moon = new Color32(122, 156, 168, 255);

        private static readonly Dictionary<string, Texture2D> textures = new();
        private static readonly Dictionary<string, Sprite> sprites = new();
        private static Texture2D fallback;
        private static TMP_FontAsset font;

        public static Texture2D Tex(string name)
        {
            if (textures.TryGetValue(name, out Texture2D cached) && cached != null)
                return cached;

            Texture2D tex = Resources.Load<Texture2D>("NoirUI/" + name);
            if (tex == null)
            {
                if (fallback == null)
                {
                    fallback = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    Color[] px = new Color[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    fallback.SetPixels(px);
                    fallback.Apply();
                }
                tex = fallback;
            }

            textures[name] = tex;
            return tex;
        }

        /// <summary>Sprite com 9-slice definido por codigo (nao depende da importacao no Unity).</summary>
        public static Sprite GetSprite(string name, float border = 0f)
        {
            string key = name + "|" + border;
            if (sprites.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            Texture2D tex = Tex(name);
            Sprite sprite = UnityEngine.Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = key;
            sprites[key] = sprite;
            return sprite;
        }

        public static TMP_FontAsset Font()
        {
            if (font != null)
                return font;

            TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (TMP_FontAsset candidate in all)
            {
                if (candidate != null && candidate.name.Contains("Averia"))
                {
                    font = candidate;
                    return font;
                }
            }

            TMP_Text any = Object.FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
            font = any != null ? any.font : TMP_Settings.defaultFontAsset;
            return font;
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
