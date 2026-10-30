using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Titulo do menu: entrada com letras se abrindo, respiracao leve, tremida de lampada e divisor ornamental.
    /// O divisor sao dois tracos (as pontas do noir_divider, que podem esticar) com o olho de Piscada no meio, na
    /// proporcao original; de vez em quando o olho pisca.
    /// </summary>
    public sealed class NoirTitleJuice : MonoBehaviour
    {
        private const float EyeSize = 92f;
        private const float EyeGap = 10f;

        private static Sprite lineLeft;
        private static Sprite lineRight;

        private TMP_Text text;
        private Color baseColor;
        private float baseSpacing;
        private Vector3 baseScale;
        private float seed;
        private RectTransform divider;
        private RectTransform eye;
        private float nextBlink;
        private string placedFor;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
            seed = Random.value * 100f;
        }

        private void OnEnable()
        {
            baseColor = text.color;
            baseSpacing = text.characterSpacing;
            baseScale = transform.localScale;

            if (divider == null)
                BuildDivider();

            text.characterSpacing = baseSpacing + 28f;
            DOTween.To(() => text.characterSpacing, v => text.characterSpacing = v, baseSpacing, 1.8f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this);
            DOTween.To(() => text.color, c => text.color = c, new Color(baseColor.r, baseColor.g, baseColor.b, 0f), 0f).SetTarget(this);
            DOTween.To(() => text.color, c => text.color = c, baseColor, 1.4f).SetDelay(0.3f).SetUpdate(true).SetTarget(this);

            divider.localScale = new Vector3(0f, 1f, 1f);
            divider.DOScaleX(1f, 1.1f).SetDelay(0.9f).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this);
            nextBlink = Time.unscaledTime + 4f + Random.value * 3f;
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            if (eye != null)
            {
                eye.DOKill();
                eye.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime + seed;
            transform.localScale = baseScale * (1f + Mathf.Sin(t * 0.8f) * 0.008f);

            // lampada falhando: quedas bruscas e raras de brilho
            float n = Mathf.PerlinNoise(t * 2.3f, seed);
            float drop = n < 0.12f ? Mathf.Lerp(0.72f, 1f, n / 0.12f) : 1f;
            Color c = text.color;
            if (c.a > 0.9f * baseColor.a)
            {
                float k = drop;
                text.color = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
            }

            // O titulo segue o idioma (o LocSceneTexts troca o texto): o divisor acompanha a nova largura.
            if (divider != null && text.text != placedFor)
                PlaceDivider();

            // A piscada do olho do divisor: rapida e rara, nunca no meio da entrada.
            if (eye != null && Time.unscaledTime >= nextBlink && divider.localScale.x >= 0.99f)
            {
                nextBlink = Time.unscaledTime + 6f + Random.value * 5f;
                eye.DOKill();
                eye.localScale = Vector3.one;
                eye.DOScaleY(0.08f, 0.07f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
            }
        }

        private void BuildDivider()
        {
            divider = new GameObject("NoirTitleDivider", typeof(RectTransform)).transform as RectTransform;
            divider.SetParent(transform, false);
            PlaceDivider();

            // Sem o olho no meio: o divisor e so o traco (eye fica null, o resto do codigo ja checa).
            EnsureLineSprites();
            float inner = 0f;
            Line("Traco_Esquerdo", lineLeft, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -inner));
            Line("Traco_Direito", lineRight, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(inner, 0f));
        }

        /// <summary>
        /// Logo abaixo das letras e com a largura do titulo (o retangulo do texto e bem maior que ele). Refeito quando
        /// o idioma troca o titulo (PISCADA FATAL / FATAL BLINK tem larguras diferentes).
        /// </summary>
        private void PlaceDivider()
        {
            RectTransform rt = (RectTransform)transform;
            // Mede com o espacamento final (a entrada anima o espacamento das letras).
            float spacing = text.characterSpacing;
            text.characterSpacing = baseSpacing;
            text.ForceMeshUpdate();
            Bounds glyphs = text.textBounds;
            text.characterSpacing = spacing;
            placedFor = text.text;
            bool hasText = glyphs.size.x > 1f;
            divider.anchorMin = divider.anchorMax = rt.pivot;
            divider.pivot = new Vector2(0.5f, 1f);
            divider.anchoredPosition = hasText
                ? new Vector2(glyphs.center.x, glyphs.min.y - 6f)
                : new Vector2(0f, -rt.rect.height * rt.pivot.y);
            divider.sizeDelta = new Vector2(Mathf.Max(420f, (hasText ? glyphs.size.x : rt.rect.width * 0.6f) * 1.05f), EyeSize);
        }

        private void Line(string lineName, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetsX)
        {
            Image line = NewImage(lineName, sprite, Color.white);
            RectTransform rt = line.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(offsetsX.x, -12f);
            rt.offsetMax = new Vector2(offsetsX.y, 12f);
        }

        private Image NewImage(string imageName, Sprite sprite, Color color)
        {
            GameObject go = new(imageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(divider, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>As duas pontas do noir_divider, sem o losango do meio: so traco, que estica sem deformar nada.</summary>
        private static void EnsureLineSprites()
        {
            if (lineLeft != null && lineRight != null)
                return;

            Texture2D tex = NoirTheme.Tex("noir_divider");
            float half = tex.width * 0.5f, cut = tex.width * 0.06f;
            lineLeft = Sprite.Create(tex, new Rect(0f, 0f, half - cut, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            lineRight = Sprite.Create(tex, new Rect(half + cut, 0f, half - cut, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            lineLeft.name = "noir_divider_esquerda";
            lineRight.name = "noir_divider_direita";
        }
    }
}
