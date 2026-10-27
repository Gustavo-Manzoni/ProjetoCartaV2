using UnityEngine;

namespace ProjetoCarta.Inventory.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RadialInventoryGraphic : UnityEngine.UI.Graphic
    {
        [Header("Main ring")]
        [SerializeField, Range(0.05f, 0.95f)] private float innerRadius = 0.56f;
        [SerializeField, Range(0f, 20f)] private float gapDegrees = 3f;
        [SerializeField, Range(32, 256)] private int circleResolution = 128;
        [SerializeField] private float firstSegmentAngle = 90f;

        [Header("Outer ring")]
        [SerializeField, Min(0f)] private float outerRingGap = 9f;
        [SerializeField, Min(1f)] private float outerRingThickness = 5f;
        [SerializeField] private Color outerRingColor = new(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color selectedOuterRingColor = new(1f, 0.78f, 0.25f, 1f);

        [Header("Selection animation")]
        [SerializeField, Min(0f)] private float selectedScale = 0.08f;
        [SerializeField, Min(0f)] private float selectedOutwardOffset = 7f;
        [SerializeField, Min(0f)] private float selectedOuterRingOffset = 8f;
        [SerializeField, Min(0.01f)] private float animationSharpness = 14f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new(0.12f, 0.12f, 0.14f, 0.94f);
        [SerializeField] private Color selectedColor = new(0.85f, 0.64f, 0.18f, 1f);

        [Header("Noir")]
        [Tooltip("Quanto a borda interna de cada setor escurece (gradiente radial, como baquelite).")]
        [SerializeField, Range(0f, 1f)] private float innerShade = 0.45f;
        [SerializeField, Min(0f)] private float tickLength = 10f;
        [SerializeField, Min(0f)] private float tickWidth = 2f;
        [SerializeField] private Color tickColor = new(1f, 1f, 1f, 0.3f);

        private int segmentCount = 1;
        private int selectedSegment = -1;
        private float[] selectionAmounts = { 0f };
        private float angleOffset;
        private float flash;

        public int SegmentCount => segmentCount;
        public int SelectedSegment => selectedSegment;

        /// <summary>Angulo do primeiro setor, incluindo o giro de abertura (icones e mouse seguem o mesmo valor).</summary>
        public float FirstSegmentAngle => firstSegmentAngle + angleOffset;

        /// <summary>Giro extra do disco (animacao de abertura, como um telefone de disco voltando).</summary>
        public float AngleOffset
        {
            get => angleOffset;
            set
            {
                if (Mathf.Approximately(angleOffset, value))
                    return;
                angleOffset = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Troca as cores do anel (a camada noir aplica a paleta do jogo em runtime).</summary>
        public void SetPalette(Color normal, Color selected, Color outer, Color selectedOuter, Color ticks)
        {
            normalColor = normal;
            selectedColor = selected;
            outerRingColor = outer;
            selectedOuterRingColor = selectedOuter;
            tickColor = ticks;
            SetVerticesDirty();
        }

        /// <summary>Clarao no setor selecionado (confirmacao de escolha).</summary>
        public void Flash(float amount = 1f)
        {
            flash = Mathf.Max(flash, amount);
            SetVerticesDirty();
        }

        public void SetSegments(int count, int selectedIndex)
        {
            int validCount = Mathf.Max(1, count);
            int validSelection = Mathf.Clamp(selectedIndex, -1, validCount - 1);
            bool countChanged = segmentCount != validCount;

            if (countChanged)
            {
                float[] resizedAmounts = new float[validCount];
                int copyCount = Mathf.Min(selectionAmounts.Length, resizedAmounts.Length);
                for (int i = 0; i < copyCount; i++)
                    resizedAmounts[i] = selectionAmounts[i];

                selectionAmounts = resizedAmounts;
                segmentCount = validCount;
            }

            if (!countChanged && selectedSegment == validSelection)
                return;

            selectedSegment = validSelection;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (selectionAmounts == null || selectionAmounts.Length != segmentCount)
                selectionAmounts = new float[segmentCount];

            float interpolation = 1f - Mathf.Exp(-animationSharpness * Time.unscaledDeltaTime);
            bool changed = false;

            for (int i = 0; i < selectionAmounts.Length; i++)
            {
                float target = i == selectedSegment ? 1f : 0f;
                float next = Mathf.Lerp(selectionAmounts[i], target, interpolation);

                if (Mathf.Abs(next - target) < 0.001f)
                    next = target;

                if (!Mathf.Approximately(next, selectionAmounts[i]))
                {
                    selectionAmounts[i] = next;
                    changed = true;
                }
            }

            if (flash > 0f)
            {
                flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 3f);
                changed = true;
            }

            if (changed)
                SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;
            float maximumRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (maximumRadius <= 0f)
                return;

            float reservedOutsideSpace = outerRingGap + outerRingThickness + selectedOuterRingOffset +
                                         selectedOutwardOffset + 1f;
            float baseOuterRadius = Mathf.Max(1f, maximumRadius - reservedOutsideSpace);
            float baseInnerRadius = baseOuterRadius * innerRadius;
            float anglePerSegment = 360f / segmentCount;
            float visibleAngle = segmentCount == 1
                ? 360f
                : Mathf.Max(0.1f, anglePerSegment - Mathf.Min(gapDegrees, anglePerSegment - 0.1f));
            int steps = Mathf.Max(2, Mathf.CeilToInt(circleResolution * visibleAngle / 360f));
            Vector2 center = rect.center;

            for (int segment = 0; segment < segmentCount; segment++)
            {
                float selection = segment < selectionAmounts.Length ? selectionAmounts[segment] : 0f;
                float scale = 1f + selectedScale * selection;
                float outward = selectedOutwardOffset * selection;
                float inner = baseInnerRadius * scale + outward;
                float outer = baseOuterRadius * scale + outward;
                Color mainColor = Color.Lerp(normalColor, selectedColor, selection);
                if (segment == selectedSegment && flash > 0f)
                    mainColor = Color.Lerp(mainColor, Color.white, flash * 0.6f);
                Color innerColor = Color.Lerp(mainColor, new Color(0f, 0f, 0f, mainColor.a), innerShade);

                AddRingSegment(
                    vertexHelper,
                    center,
                    segment,
                    anglePerSegment,
                    visibleAngle,
                    steps,
                    inner,
                    outer,
                    mainColor,
                    innerColor);

                // Marca entre setores, como os numeros de um disco de telefone.
                if (tickLength > 0f && segmentCount > 1)
                {
                    float boundary = (FirstSegmentAngle - segment * anglePerSegment + anglePerSegment * 0.5f) * Mathf.Deg2Rad;
                    AddTick(vertexHelper, center, boundary, baseOuterRadius + outerRingGap * 0.5f - tickLength,
                        baseOuterRadius + outerRingGap * 0.5f + tickLength * 0.4f, tickWidth, tickColor);
                }

                float outerRingInner = baseOuterRadius + outerRingGap +
                                       (selectedOutwardOffset + selectedOuterRingOffset) * selection;
                float outerRingOuter = outerRingInner + outerRingThickness * (1f + selectedScale * selection);
                Color accentColor = Color.Lerp(outerRingColor, selectedOuterRingColor, selection);

                AddRingSegment(
                    vertexHelper,
                    center,
                    segment,
                    anglePerSegment,
                    visibleAngle,
                    steps,
                    outerRingInner,
                    outerRingOuter,
                    accentColor,
                    accentColor);
            }
        }

        private static void AddTick(UnityEngine.UI.VertexHelper vertexHelper, Vector2 center, float angle, float from,
            float to, float width, Color color)
        {
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            int first = vertexHelper.currentVertCount;
            Color32 c = color;
            vertexHelper.AddVert(center + direction * from - side, c, Vector2.zero);
            vertexHelper.AddVert(center + direction * from + side, c, Vector2.zero);
            vertexHelper.AddVert(center + direction * to + side, c, Vector2.zero);
            vertexHelper.AddVert(center + direction * to - side, c, Vector2.zero);
            vertexHelper.AddTriangle(first, first + 1, first + 2);
            vertexHelper.AddTriangle(first, first + 2, first + 3);
        }

        private void AddRingSegment(
            UnityEngine.UI.VertexHelper vertexHelper,
            Vector2 center,
            int segment,
            float anglePerSegment,
            float visibleAngle,
            int steps,
            float inner,
            float outer,
            Color outerColor,
            Color innerColor)
        {
            int firstVertex = vertexHelper.currentVertCount;
            float centerAngle = FirstSegmentAngle - segment * anglePerSegment;
            float startAngle = centerAngle + visibleAngle * 0.5f;
            Color32 outerVertex = outerColor;
            Color32 innerVertex = innerColor;

            for (int step = 0; step <= steps; step++)
            {
                float progress = step / (float)steps;
                float angle = (startAngle - visibleAngle * progress) * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));

                vertexHelper.AddVert(center + direction * outer, outerVertex, Vector2.zero);
                vertexHelper.AddVert(center + direction * inner, innerVertex, Vector2.zero);
            }

            for (int step = 0; step < steps; step++)
            {
                int outerCurrent = firstVertex + step * 2;
                int innerCurrent = outerCurrent + 1;
                int outerNext = outerCurrent + 2;
                int innerNext = outerCurrent + 3;

                vertexHelper.AddTriangle(outerCurrent, outerNext, innerCurrent);
                vertexHelper.AddTriangle(outerNext, innerNext, innerCurrent);
            }
        }

       
    }
}
