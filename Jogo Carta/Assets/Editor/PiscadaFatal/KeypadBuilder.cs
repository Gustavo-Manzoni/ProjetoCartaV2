using System.Collections.Generic;
using ProjetoCarta.Interaction;
using ProjetoCarta.NoirUI;
using ProjetoCarta.Puzzles;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjetoCarta.EditorTools
{
    /// <summary>
    /// Monta o keypad fisico de um cofre: Canvas World Space colado na porta (substitui a malha do teclado),
    /// telinha com bolinhas, botoes e a camera de aproximacao. Pode ser rodado de novo sem duplicar nada.
    /// </summary>
    public static class KeypadBuilder
    {
        private const string CanvasName = "Keypad_Canvas";
        private const float MillimetersToMeters = 0.001f;

        private static readonly Color PlateColor = new(0.11f, 0.11f, 0.1f, 1f);
        private static readonly Color PlateEdge = new(0.32f, 0.27f, 0.18f, 1f);
        private static readonly Color KeyColor = new(0.84f, 0.79f, 0.66f, 1f);
        private static readonly Color KeyText = new(0.12f, 0.1f, 0.08f, 1f);
        private static readonly Color EraseColor = new(0.55f, 0.24f, 0.2f, 1f);
        private static readonly Color EnterColor = new(0.27f, 0.42f, 0.27f, 1f);

        public static NumericKeypad BuildNumeric(SafeInteractable safe, Renderer keypadMesh, string code)
        {
            const float key = 72f, gap = 12f, pad = 18f, display = 64f;
            float width = pad * 2f + key * 3f + gap * 2f;
            float height = pad * 2f + display + gap + key * 4f + gap * 3f;

            Canvas canvas = CreateCanvas(safe, keypadMesh, width, height, out RectTransform plate);
            NumericKeypad keypad = canvas.gameObject.AddComponent<NumericKeypad>();
            CreateDisplay(plate, width, height, pad, display, code.Length, out Image displayBackground, out Image[] dots, out TMP_Text status);

            Button[] digits = new Button[10];
            string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "APAGAR", "0", "ENTER" };
            Button enter = null;
            HoldButton erase = null;
            float top = height * 0.5f - pad - display - gap;
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i / 3, col = i % 3;
                Vector2 position = new(-width * 0.5f + pad + key * 0.5f + col * (key + gap), top - key * 0.5f - row * (key + gap));
                bool isErase = labels[i] == "APAGAR", isEnter = labels[i] == "ENTER";
                Color color = isErase ? EraseColor : isEnter ? EnterColor : KeyColor;
                Color textColor = isErase || isEnter ? new Color(0.95f, 0.92f, 0.85f) : KeyText;
                Button button = CreateKey(plate, "Tecla_" + labels[i], position, new Vector2(key, key), color, labels[i],
                    isErase || isEnter ? 15f : 34f, textColor);

                if (isErase)
                    erase = button.gameObject.AddComponent<HoldButton>();
                else if (isEnter)
                    enter = button;
                else
                    digits[int.Parse(labels[i])] = button;
            }

            SerializedObject so = new(keypad);
            WireBase(so, canvas, plate, displayBackground, dots, status);
            so.FindProperty("code").stringValue = code;
            SerializedProperty digitArray = so.FindProperty("digitButtons");
            digitArray.arraySize = 10;
            for (int i = 0; i < 10; i++)
                digitArray.GetArrayElementAtIndex(i).objectReferenceValue = digits[i];
            so.FindProperty("eraseButton").objectReferenceValue = erase;
            so.FindProperty("enterButton").objectReferenceValue = enter;
            so.ApplyModifiedPropertiesWithoutUndo();

            WireSafe(safe, canvas, keypad, keypadMesh);
            return keypad;
        }

        public static SymbolKeypad BuildSymbols(SafeInteractable safe, Renderer keypadMesh, SymbolCodeDefinition code)
        {
            const float key = 86f, gap = 12f, pad = 18f, display = 56f;
            float width = pad * 2f + key * 3f + gap * 2f;
            float height = pad * 2f + display + gap + key * 2f + gap;

            Canvas canvas = CreateCanvas(safe, keypadMesh, width, height, out RectTransform plate);
            SymbolKeypad keypad = canvas.gameObject.AddComponent<SymbolKeypad>();
            CreateDisplay(plate, width, height, pad, display, code.Length, out Image displayBackground, out Image[] dots, out TMP_Text status);

            List<Button> buttons = new();
            List<Image> images = new();
            List<TMP_Text> badges = new();
            Button enter = null;
            float top = height * 0.5f - pad - display - gap;
            for (int slot = 0; slot < 6; slot++)
            {
                int row = slot / 3, col = slot % 3;
                Vector2 position = new(-width * 0.5f + pad + key * 0.5f + col * (key + gap), top - key * 0.5f - row * (key + gap));
                if (slot == 5)
                {
                    enter = CreateKey(plate, "Tecla_ENTER", position, new Vector2(key, key), EnterColor, "ENTER", 16f,
                        new Color(0.95f, 0.92f, 0.85f));
                    continue;
                }

                int symbolIndex = slot < code.KeypadLayout.Count ? code.KeypadLayout[slot] : slot;
                SymbolCodeDefinition.Symbol symbol = code.GetSymbol(symbolIndex);
                Button button = CreateKey(plate, "Tecla_" + (symbol != null ? symbol.id : slot.ToString()), position,
                    new Vector2(key, key), KeyColor, string.Empty, 10f, KeyText);

                Image icon = NewImage("Simbolo", button.transform, symbol?.KeypadSprite, Color.white);
                Stretch(icon.rectTransform, 10f);
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                TMP_Text badge = NewText("Ordem", button.transform, string.Empty, 15f, KeyText);
                badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 1f);
                badge.rectTransform.pivot = new Vector2(1f, 1f);
                badge.rectTransform.anchoredPosition = new Vector2(-5f, -3f);
                badge.rectTransform.sizeDelta = new Vector2(46f, 20f);
                badge.alignment = TextAlignmentOptions.TopRight;
                badge.fontStyle = FontStyles.Bold;

                buttons.Add(button);
                images.Add(icon);
                badges.Add(badge);
            }

            SerializedObject so = new(keypad);
            WireBase(so, canvas, plate, displayBackground, dots, status);
            so.FindProperty("code").objectReferenceValue = code;
            SetArray(so.FindProperty("symbolButtons"), buttons.ToArray());
            SetArray(so.FindProperty("symbolImages"), images.ToArray());
            SetArray(so.FindProperty("orderBadges"), badges.ToArray());
            so.FindProperty("enterButton").objectReferenceValue = enter;
            so.ApplyModifiedPropertiesWithoutUndo();

            WireSafe(safe, canvas, keypad, keypadMesh);
            return keypad;
        }

        // ------------------------------------------------------------ montagem

        private static Canvas CreateCanvas(SafeInteractable safe, Renderer keypadMesh, float width, float height, out RectTransform plate)
        {
            Transform door = keypadMesh.transform.parent != null ? keypadMesh.transform.parent : safe.transform;
            Transform existing = door.Find(CanvasName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            Vector3 forward = safe.transform.forward;
            Bounds bounds = keypadMesh.bounds;
            float depth = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z))));

            GameObject go = new(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(NoirOwned));
            go.transform.SetParent(door, false);
            go.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);
            rect.position = bounds.center + forward * (depth + 0.004f);
            // Canvas World Space e visto por quem olha na direcao do seu +Z: o +Z aponta para dentro do cofre.
            rect.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            float parentScale = door.lossyScale.x != 0f ? door.lossyScale.x : 1f;
            rect.localScale = Vector3.one * (MillimetersToMeters / parentScale);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 6f;

            Image back = NewImage("Placa", rect, BuiltinSprite("UI/Skin/UISprite.psd"), PlateEdge);
            back.type = Image.Type.Sliced;
            Stretch(back.rectTransform, -6f);
            back.raycastTarget = true;

            Image inner = NewImage("Placa_Interna", back.transform, BuiltinSprite("UI/Skin/UISprite.psd"), PlateColor);
            inner.type = Image.Type.Sliced;
            Stretch(inner.rectTransform, 6f);
            inner.raycastTarget = false;

            plate = NewRect("Painel", rect);
            Stretch(plate, 0f);
            return canvas;
        }

        private static void CreateDisplay(RectTransform plate, float width, float height, float pad, float displayHeight,
            int length, out Image background, out Image[] dots, out TMP_Text status)
        {
            background = NewImage("Telinha", plate, BuiltinSprite("UI/Skin/UISprite.psd"), new Color(0.05f, 0.06f, 0.06f));
            background.type = Image.Type.Sliced;
            RectTransform rt = background.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width - pad * 2f, displayHeight);
            rt.anchoredPosition = new Vector2(0f, height * 0.5f - pad - displayHeight * 0.5f);

            dots = new Image[length];
            float dot = Mathf.Min(22f, (width - pad * 4f) / (length * 1.8f));
            float spacing = dot * 1.8f;
            float startX = -(length - 1) * spacing * 0.5f;
            for (int i = 0; i < length; i++)
            {
                Image d = NewImage("Bolinha_" + (i + 1), rt, BuiltinSprite("UI/Skin/Knob.psd"), new Color(1f, 1f, 1f, 0.12f));
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                d.rectTransform.sizeDelta = new Vector2(dot, dot);
                d.rectTransform.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                d.raycastTarget = false;
                dots[i] = d;
            }

            status = NewText("Estado", rt, string.Empty, 12f, new Color(0.95f, 0.92f, 0.85f, 0.85f));
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(1f, 0f);
            status.rectTransform.pivot = new Vector2(1f, 0f);
            status.rectTransform.anchoredPosition = new Vector2(-6f, 3f);
            status.rectTransform.sizeDelta = new Vector2(90f, 18f);
            status.alignment = TextAlignmentOptions.BottomRight;
            status.characterSpacing = 6f;
        }

        private static Button CreateKey(RectTransform parent, string name, Vector2 position, Vector2 size, Color color,
            string label, float fontSize, Color textColor)
        {
            Image image = NewImage(name, parent, BuiltinSprite("UI/Skin/UISprite.psd"), color);
            image.type = Image.Type.Sliced;
            RectTransform rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            image.raycastTarget = true;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.08f, 0.95f);
            colors.pressedColor = new Color(0.72f, 0.68f, 0.6f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.85f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            if (!string.IsNullOrEmpty(label))
            {
                TMP_Text text = NewText("Rotulo", rt, label, fontSize, textColor);
                Stretch(text.rectTransform, 2f);
                text.characterSpacing = label.Length > 1 ? 4f : 0f;
            }

            return button;
        }

        private static void WireBase(SerializedObject so, Canvas canvas, RectTransform plate, Image displayBackground,
            Image[] dots, TMP_Text status)
        {
            so.FindProperty("canvasGroup").objectReferenceValue = canvas.GetComponent<CanvasGroup>();
            so.FindProperty("panelRoot").objectReferenceValue = plate;
            so.FindProperty("displayBackground").objectReferenceValue = displayBackground;
            SetArray(so.FindProperty("displayDots"), dots);
            so.FindProperty("statusText").objectReferenceValue = status;
        }

        private static void WireSafe(SafeInteractable safe, Canvas canvas, KeypadPanel keypad, Renderer keypadMesh)
        {
            // A malha do teclado original e substituida pelo Canvas (mesmo lugar, botoes reais).
            keypadMesh.enabled = false;
            foreach (Collider c in keypadMesh.GetComponents<Collider>())
                c.enabled = false;

            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;

            SerializedObject so = new(safe);
            so.FindProperty("safeInterface").objectReferenceValue = group;
            so.FindProperty("keypad").objectReferenceValue = keypad;
            if (so.FindProperty("doorAnimator").objectReferenceValue == null)
                so.FindProperty("doorAnimator").objectReferenceValue = safe.GetComponent<Animator>();

            // Camera de aproximacao: de frente para o teclado, perto o bastante para ocupar a tela.
            CinemachineCamera vcam = so.FindProperty("safeCamera").objectReferenceValue as CinemachineCamera;
            if (vcam != null)
            {
                Vector3 forward = safe.transform.forward;
                Vector3 center = canvas.transform.position;
                // Distancia para o keypad ocupar ~80% da altura da tela com FOV 42.
                RectTransform canvasRect = (RectTransform)canvas.transform;
                float worldHeight = (canvasRect.sizeDelta.y + 12f) * MillimetersToMeters;
                float distance = worldHeight * 0.62f / Mathf.Tan(21f * Mathf.Deg2Rad);
                vcam.transform.SetPositionAndRotation(center + forward * distance + Vector3.up * 0.02f,
                    Quaternion.LookRotation(-forward + Vector3.down * 0.04f, Vector3.up));
                LensSettings lens = vcam.Lens;
                lens.FieldOfView = 42f;
                lens.NearClipPlane = 0.02f;
                vcam.Lens = lens;
                vcam.Priority = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(safe);
        }

        // ------------------------------------------------------------ helpers

        private static TMP_FontAsset font;

        private static TMP_FontAsset Font()
        {
            if (font != null)
                return font;

            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset Averia"))
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null)
                    return font;
            }

            font = TMP_Settings.defaultFontAsset;
            return font;
        }

        private static Sprite BuiltinSprite(string path)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = Font();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
