using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>Marca widgets que ja receberam a pele noir.</summary>
    public sealed class NoirSkinned : MonoBehaviour { }

    /// <summary>
    /// Troca a aparencia padrao do Unity (UISprite esticado, quadrados brancos) dos Sliders, Toggles,
    /// Dropdowns e paineis do menu de opcoes por uma versao noir com juice.
    /// </summary>
    public static class NoirSkin
    {
        private static Sprite circle, diamond;
        private static readonly HashSet<int> doneImages = new();

        public static Sprite Circle()
        {
            if (circle != null) return circle;
            const int n = 64;
            Texture2D t = new(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01((0.97f - d) / 0.05f);
                    float ring = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.7f) / 0.08f));
                    float v = Mathf.Lerp(1f, 0.55f, ring) * Mathf.Lerp(1f, 0.88f, Mathf.Clamp01(d));
                    px[y * n + x] = new Color(v, v, v, a);
                }
            t.SetPixels(px); t.Apply();
            circle = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            circle.name = "NoirCircle";
            return circle;
        }

        public static Sprite Diamond()
        {
            if (diamond != null) return diamond;
            const int n = 32;
            Texture2D t = new(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = (Mathf.Abs(x + 0.5f - n * 0.5f) + Mathf.Abs(y + 0.5f - n * 0.5f)) / (n * 0.5f);
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01((0.95f - d) / 0.08f));
                }
            t.SetPixels(px); t.Apply();
            diamond = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            diamond.name = "NoirDiamond";
            return diamond;
        }

        public static void Apply()
        {
            HashSet<Canvas> canvases = new();

            foreach (Slider s in Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Eligible(s)) { SkinSlider(s); canvases.Add(s.GetComponentInParent<Canvas>(true)); }

            foreach (TMP_Dropdown d in Object.FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Eligible(d)) { SkinDropdown(d); canvases.Add(d.GetComponentInParent<Canvas>(true)); }

            foreach (Toggle t in Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Eligible(t) && t.GetComponentInParent<TMP_Dropdown>(true) == null) { SkinToggle(t); canvases.Add(t.GetComponentInParent<Canvas>(true)); }

            foreach (Scrollbar sb in Object.FindObjectsByType<Scrollbar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Eligible(sb)) SkinScrollbar(sb);

            foreach (Canvas c in canvases)
                if (c != null) SkinPanelsAndTexts(c.rootCanvas);
        }

        private static bool Eligible(Component c) =>
            c != null && c.GetComponent<NoirSkinned>() == null && c.GetComponentInParent<NoirOwned>(true) == null;

        private static void Mark(Component c) { if (c.GetComponent<NoirSkinned>() == null) c.gameObject.AddComponent<NoirSkinned>(); }

        private static void Sliced(Image i, string sprite, float border, float mult, Color c)
        {
            if (i == null) return;
            i.sprite = NoirTheme.GetSprite(sprite, border);
            i.type = Image.Type.Sliced;
            i.pixelsPerUnitMultiplier = mult;
            i.color = c;
            doneImages.Add(i.GetInstanceID());
        }

        private static void Plain(Image i, Sprite s, Color c)
        {
            if (i == null) return;
            i.sprite = s;
            i.type = Image.Type.Simple;
            i.color = c;
            doneImages.Add(i.GetInstanceID());
        }

        private static void CenterBar(RectTransform r, float xIn, float half)
        {
            if (r == null) return;
            r.anchorMin = new Vector2(0, 0.5f);
            r.anchorMax = new Vector2(1, 0.5f);
            r.offsetMin = new Vector2(xIn, -half);
            r.offsetMax = new Vector2(-xIn, half);
        }

        // ------------------------------------------------------------------ slider

        public static void SkinSlider(Slider s)
        {
            Mark(s);
            s.transition = Selectable.Transition.None;

            Transform bg = s.transform.Find("Background");
            if (bg != null)
            {
                Plain(bg.GetComponent<Image>(), null, NoirTheme.WithAlpha(NoirTheme.Ash, 0.28f));
                CenterBar((RectTransform)bg, 0f, 3f);
            }

            if (s.fillRect != null)
            {
                CenterBar(s.fillRect.parent as RectTransform, 12f, 3f);
                Plain(s.fillRect.GetComponent<Image>(), null, NoirTheme.Amber);
                s.fillRect.sizeDelta = Vector2.zero;
            }

            Image handle = null;
            if (s.handleRect != null)
            {
                RectTransform area = s.handleRect.parent as RectTransform;
                if (area != null)
                {
                    area.anchorMin = new Vector2(0, 0.5f);
                    area.anchorMax = new Vector2(1, 0.5f);
                    area.offsetMin = new Vector2(12f, -14f);
                    area.offsetMax = new Vector2(-12f, 14f);
                }
                s.handleRect.sizeDelta = new Vector2(28f, 0f);
                handle = s.handleRect.GetComponent<Image>();
                Plain(handle, Circle(), NoirTheme.Paper);
                if (handle != null) { handle.raycastTarget = true; s.targetGraphic = handle; }
            }

            s.gameObject.AddComponent<NoirSliderFx>().handle = s.handleRect;
        }

        // ------------------------------------------------------------------ toggle

        public static void SkinToggle(Toggle t)
        {
            Mark(t);
            t.transition = Selectable.Transition.None;

            Image box = t.targetGraphic as Image;
            if (box != null)
            {
                Sliced(box, "noir_btn_normal", 14f, 2.6f, NoirTheme.Paper);
                RectTransform r = box.rectTransform;
                if (r.anchorMin == r.anchorMax && r.anchorMin == new Vector2(0, 1))
                {
                    r.sizeDelta = new Vector2(28, 28);
                    r.anchoredPosition = new Vector2(14, -14);
                    RectTransform label = t.transform.Find("Label") as RectTransform;
                    if (label != null) label.offsetMin = new Vector2(40, label.offsetMin.y);
                }
            }

            Image check = t.graphic as Image;
            if (check != null)
            {
                Plain(check, Diamond(), NoirTheme.Amber);
                check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                check.rectTransform.anchoredPosition = Vector2.zero;
                check.rectTransform.sizeDelta = new Vector2(16, 16);
            }

            NoirToggleFx fx = t.gameObject.AddComponent<NoirToggleFx>();
            fx.box = box != null ? box.rectTransform : null;
            fx.check = check != null ? check.rectTransform : null;
        }

        // ------------------------------------------------------------------ dropdown

        public static void SkinDropdown(TMP_Dropdown d)
        {
            Mark(d);
            d.transition = Selectable.Transition.None;
            Image main = d.GetComponent<Image>();
            Sliced(main, "noir_btn_normal", 14f, 2f, NoirTheme.Paper);
            if (d.captionText != null) { d.captionText.color = NoirTheme.Paper; d.captionText.font = NoirTheme.Font(); }
            if (d.itemText != null) { d.itemText.color = NoirTheme.Paper; d.itemText.font = NoirTheme.Font(); }
            Transform arrow = d.transform.Find("Arrow");
            if (arrow != null) Plain(arrow.GetComponent<Image>(), arrow.GetComponent<Image>()?.sprite, NoirTheme.Amber);

            RectTransform template = d.template;
            if (template != null)
            {
                Sliced(template.GetComponent<Image>(), "noir_panel", 60f, 2.4f, new Color(1f, 1f, 1f, 0.97f));
                foreach (Scrollbar sb in template.GetComponentsInChildren<Scrollbar>(true)) SkinScrollbar(sb);
                foreach (Toggle item in template.GetComponentsInChildren<Toggle>(true))
                {
                    Mark(item);
                    Image bg = item.targetGraphic as Image;
                    Plain(bg, null, Color.white);
                    item.transition = Selectable.Transition.ColorTint;
                    ColorBlock cb = item.colors;
                    cb.normalColor = new Color(1, 1, 1, 0f);
                    cb.highlightedColor = NoirTheme.WithAlpha(NoirTheme.Amber, 0.28f);
                    cb.selectedColor = NoirTheme.WithAlpha(NoirTheme.Amber, 0.16f);
                    cb.pressedColor = NoirTheme.WithAlpha(NoirTheme.Amber, 0.45f);
                    cb.colorMultiplier = 1f;
                    cb.fadeDuration = 0.08f;
                    item.colors = cb;
                    Image check = item.graphic as Image;
                    if (check != null) Plain(check, Diamond(), NoirTheme.Amber);
                    foreach (TMP_Text tx in item.GetComponentsInChildren<TMP_Text>(true))
                    { tx.color = NoirTheme.Paper; tx.font = NoirTheme.Font(); }
                }
            }
            d.gameObject.AddComponent<NoirToggleFx>().box = main != null ? main.rectTransform : null;
        }

        public static void SkinScrollbar(Scrollbar sb)
        {
            Mark(sb);
            sb.transition = Selectable.Transition.None;
            Plain(sb.GetComponent<Image>(), null, NoirTheme.WithAlpha(NoirTheme.Ash, 0.2f));
            if (sb.handleRect != null) Plain(sb.handleRect.GetComponent<Image>(), null, NoirTheme.Amber);
        }

        // ------------------------------------------------------------------ paineis e textos

        private static void SkinPanelsAndTexts(Canvas root)
        {
            if (root == null || root.GetComponent<NoirSkinned>() != null || root.GetComponentInParent<NoirOwned>(true) != null)
                return;
            Mark(root);

            foreach (Image img in root.GetComponentsInChildren<Image>(true))
            {
                if (img == null || doneImages.Contains(img.GetInstanceID()) || img.sprite == null) continue;
                if (img.GetComponentInParent<NoirOwned>(true) != null) continue;
                string n = img.sprite.name;
                if (n == "UISprite" || n == "Background" || n == "InputFieldBackground")
                    Sliced(img, "noir_panel", 60f, 2.4f, new Color(1f, 1f, 1f, 0.92f));
            }

            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == null || t.GetComponentInParent<NoirOwned>(true) != null) continue;
                t.font = NoirTheme.Font();
                t.color = NoirTheme.Paper;
            }
        }
    }

    public sealed class NoirSliderFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform handle;
        private Slider slider;
        private bool hover, down;
        private float lastTick;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            if (slider != null) slider.onValueChanged.AddListener(OnChanged);
        }

        private void OnChanged(float v)
        {
            if (Time.unscaledTime - lastTick < 0.05f) return;
            lastTick = Time.unscaledTime;
            float range = Mathf.Max(0.0001f, slider.maxValue - slider.minValue);
            NoirAudio.Play(NoirSfx.Tick, 0.7f, 0.8f + 0.6f * (slider.value - slider.minValue) / range);
        }

        private void Refresh()
        {
            if (handle == null) return;
            handle.DOKill();
            handle.DOScale(down ? 1.35f : hover ? 1.18f : 1f, 0.14f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void OnPointerEnter(PointerEventData e) { hover = true; NoirAudio.Play(NoirSfx.Hover, 0.5f); Refresh(); }
        public void OnPointerExit(PointerEventData e) { hover = false; Refresh(); }
        public void OnPointerDown(PointerEventData e) { down = true; NoirAudio.Play(NoirSfx.Click, 0.6f); Refresh(); }
        public void OnPointerUp(PointerEventData e) { down = false; Refresh(); }

        private void OnDisable() { if (handle != null) { handle.DOKill(); handle.localScale = Vector3.one; } }
    }

    public sealed class NoirToggleFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform box, check;
        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            if (toggle != null) toggle.onValueChanged.AddListener(OnChanged);
        }

        private void OnChanged(bool on)
        {
            NoirAudio.Play(on ? NoirSfx.Click : NoirSfx.Back, 0.7f);
            if (box != null) { box.DOKill(true); box.DOPunchScale(Vector3.one * 0.18f, 0.22f, 8, 0.6f).SetUpdate(true); }
            if (check != null && on) { check.DOKill(true); check.localScale = Vector3.zero; check.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true); }
        }

        public void OnPointerEnter(PointerEventData e) { NoirAudio.Play(NoirSfx.Hover, 0.45f); if (box != null) box.DOScale(1.06f, 0.12f).SetUpdate(true); }
        public void OnPointerExit(PointerEventData e) { if (box != null) box.DOScale(1f, 0.12f).SetUpdate(true); }

        private void OnDisable() { if (box != null) { box.DOKill(); box.localScale = Vector3.one; } }
    }
}
