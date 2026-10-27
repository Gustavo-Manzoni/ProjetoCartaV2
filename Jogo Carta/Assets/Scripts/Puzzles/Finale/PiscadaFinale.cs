using System.Collections;
using DG.Tweening;
using ProjetoCarta.Core;
using ProjetoCarta.NoirUI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Final simbolico na Ultima Noite (P2): a porta abre para a escuridao, uma figura indistinta aparece
    /// por um instante, as luzes cedem e o texto fecha a historia ("Ainda dá tempo.").
    /// Dispara uma unica vez. Sempre termina com botoes de saida: nunca prende o jogador numa tela preta.
    /// </summary>
    public sealed class PiscadaFinale : MonoBehaviour
    {
        [Header("Cena")]
        [Tooltip("Escuridao e silhueta atras da porta (comecam inativas).")]
        [SerializeField] private GameObject doorwayReveal;
        [Tooltip("Plano escuro do vão (filho de doorwayReveal). Ao abrir, é ajustado para cobrir a folha fechada. Vazio = filho \"Vazio\".")]
        [SerializeField] private Transform doorwayVoid;
        [Tooltip("Quanto o plano escuro passa da folha da porta em cada borda (metros).")]
        [SerializeField, Min(0f)] private float voidMargin = 0.06f;
        [SerializeField] private Renderer silhouette;
        [SerializeField] private Light doorwayLight;
        [SerializeField, Min(0.1f)] private float doorOpenDuration = 2.6f;

        [Header("Texto")]
        [SerializeField, TextArea(1, 2)]
        private string[] lines =
        {
            "23:58.",
            "Alguém chegou antes da hora.",
            "Ainda dá tempo."
        };
        [SerializeField] private string menuSceneName = "MainMenu";

        private bool played;
        private Canvas canvas;
        private Image black;
        private TMP_Text lineText;
        private TMP_Text titleText;
        private CanvasGroup buttonsGroup;
        private bool waitingForChoice;

        private void Awake()
        {
            if (doorwayReveal != null)
            {
                if (doorwayVoid == null)
                    doorwayVoid = doorwayReveal.transform.Find("Vazio");
                doorwayReveal.SetActive(false);
            }
        }

        public void Play(FinalDoor door)
        {
            if (played)
                return;

            played = true;
            GameplayLocks.Acquire(this, GameplayLock.Cutscene);
            BuildOverlay();

            // A corrotina roda num objeto que nao some com a piscada nem com o momento.
            CoroutineHost.Run(Sequence(door));
        }

        private IEnumerator Sequence(FinalDoor door)
        {
            NoirAmbience.FadeAll(0.15f, 2f);
            NoirAudio.Play(NoirSfx.DoorUnlatch, 1f);

            // A folha ainda esta fechada aqui: e a referencia do tamanho do vao.
            FitRevealToDoor(door);
            if (doorwayReveal != null)
                doorwayReveal.SetActive(true);

            SetSilhouetteAlpha(0f);
            if (doorwayLight != null)
                doorwayLight.intensity = 0f;

            if (door != null && door.DoorLeaf != null)
            {
                NoirAudio.Play(NoirSfx.DoorCreak, 1f);
                Transform leaf = door.DoorLeaf;
                Quaternion start = leaf.localRotation;
                Quaternion end = start * Quaternion.Euler(door.OpenLocalEulerOffset);
                float elapsed = 0f;
                while (elapsed < doorOpenDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / doorOpenDuration);
                    // A porta hesita no comeco e depois cede.
                    float eased = t < 0.15f ? t * 0.4f : Mathf.Lerp(0.06f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.15f) / 0.85f));
                    leaf.localRotation = Quaternion.Slerp(start, end, eased);
                    if (doorwayLight != null)
                        doorwayLight.intensity = eased * 1.6f;
                    yield return null;
                }
            }

            // A figura aparece por um instante e some na escuridao.
            NoirAudio.Play(NoirSfx.Heartbeat, 1f);
            yield return FadeSilhouette(0f, 0.85f, 1.4f);
            yield return new WaitForSeconds(1.3f);
            NoirAudio.Play(NoirSfx.Heartbeat, 0.8f, 0.9f);
            yield return FadeSilhouette(0.85f, 0f, 1.8f);

            black.DOFade(1f, 1.6f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(2.2f);

            for (int i = 0; i < lines.Length; i++)
            {
                bool last = i == lines.Length - 1;
                lineText.text = Loc.T(lines[i]);
                lineText.fontSize = last ? 84f : 54f;
                lineText.alpha = 0f;
                lineText.characterSpacing = last ? 22f : 10f;
                if (last)
                    NoirAudio.Play(NoirSfx.Title, 0.9f);

                DOTween.To(() => lineText.alpha, v => lineText.alpha = v, 1f, 1.2f).SetUpdate(true);
                DOTween.To(() => lineText.characterSpacing, v => lineText.characterSpacing = v, last ? 8f : 4f, 3f)
                    .SetEase(Ease.OutCubic).SetUpdate(true);
                yield return new WaitForSecondsRealtime(last ? 3.6f : 2.8f);

                if (!last)
                {
                    DOTween.To(() => lineText.alpha, v => lineText.alpha = v, 0f, 0.9f).SetUpdate(true);
                    yield return new WaitForSecondsRealtime(1.1f);
                }
            }

            ShowEnding();
        }

        private void ShowEnding()
        {
            // Fim: libera o cursor para os botoes. Nada aqui depende de cena ou momento.
            GameplayLocks.Acquire(this, GameplayLock.Modal);
            titleText.alpha = 0f;
            DOTween.To(() => titleText.alpha, v => titleText.alpha = v, 1f, 1.4f).SetUpdate(true);
            buttonsGroup.alpha = 0f;
            buttonsGroup.interactable = true;
            buttonsGroup.blocksRaycasts = true;
            buttonsGroup.DOFade(1f, 1.2f).SetDelay(0.6f).SetUpdate(true);
            waitingForChoice = true;
        }

        private void Update()
        {
            if (!waitingForChoice || Keyboard.current == null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
            {
                GameplayLocks.ConsumeEscape();
                ReturnToMenu();
            }
        }

        private void ReturnToMenu()
        {
            if (!waitingForChoice)
                return;

            waitingForChoice = false;
            GameplayLocks.DisableCursorManagement();
            DOTween.KillAll();
            SceneManager.LoadScene(menuSceneName);
        }

        private void Quit()
        {
            waitingForChoice = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // O vazio fica logo atras da folha fechada e um pouco maior que ela. Assim cobre o vao inteiro e fica
        // na frente do rodape, do lambril e dos frisos que o NoirEnvironment cola na parede em runtime.
        // A silhueta vai junto, entre a folha e o vazio, para nao sumir atras dele.
        private void FitRevealToDoor(FinalDoor door)
        {
            const float gap = 0.03f;
            const float thickness = 0.01f;

            if (doorwayVoid == null || door == null || door.DoorLeaf == null || door.DoorLeaf.parent == null)
                return;

            Transform frame = door.DoorLeaf.parent;
            if (!TryLocalBounds(door.DoorLeaf, frame, out Bounds leaf))
                return;

            // Lado da parede: onde o vazio ja estava em relacao a folha.
            float side = frame.InverseTransformPoint(doorwayVoid.position).z >= leaf.center.z ? 1f : -1f;
            Vector3 frameScale = frame.lossyScale;
            Vector3 normal = frame.forward * side;
            Vector3 back = frame.TransformPoint(new Vector3(leaf.center.x, leaf.center.y, side > 0f ? leaf.max.z : leaf.min.z));
            float width = leaf.size.x * Mathf.Abs(frameScale.x) + voidMargin * 2f;
            float height = leaf.size.y * Mathf.Abs(frameScale.y) + voidMargin * 2f;

            doorwayVoid.position = back + normal * (gap + thickness * 0.5f);

            // Mantem a rotacao do plano (a textura continua igual) e mede cada eixo dele nos eixos do batente.
            MeshFilter filter = doorwayVoid.GetComponent<MeshFilter>();
            Vector3 meshSize = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.size : Vector3.one;
            Vector3 parentScale = doorwayVoid.parent != null ? doorwayVoid.parent.lossyScale : Vector3.one;
            Vector3 scale = doorwayVoid.localScale;
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = Vector3.zero;
                axis[i] = 1f;
                axis = doorwayVoid.rotation * axis;
                float size = Mathf.Abs(Vector3.Dot(axis, frame.right)) * width
                             + Mathf.Abs(Vector3.Dot(axis, frame.up)) * height
                             + Mathf.Abs(Vector3.Dot(axis, frame.forward)) * thickness;
                float divisor = Mathf.Max(0.0001f, meshSize[i] * Mathf.Abs(parentScale[i]));
                scale[i] = size / divisor;
            }

            doorwayVoid.localScale = scale;

            if (silhouette != null)
            {
                Transform figure = silhouette.transform;
                Vector3 offset = figure.position - back;
                figure.position += normal * (gap * 0.5f - Vector3.Dot(offset, normal));
            }
        }

        private static bool TryLocalBounds(Transform root, Transform space, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;

                Bounds mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 point = space.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (any)
                    {
                        bounds.Encapsulate(point);
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                }
            }

            return any;
        }

        private IEnumerator FadeSilhouette(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Tremula como algo visto pelo canto do olho.
                float flicker = 1f - Mathf.PerlinNoise(Time.time * 7f, 0.3f) * 0.35f;
                SetSilhouetteAlpha(Mathf.Lerp(from, to, t) * flicker);
                yield return null;
            }

            SetSilhouetteAlpha(to);
        }

        private void SetSilhouetteAlpha(float alpha)
        {
            if (silhouette == null)
                return;

            MaterialPropertyBlock block = new();
            silhouette.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(0f, 0f, 0f, alpha));
            silhouette.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------ UI

        private void BuildOverlay()
        {
            GameObject root = new("NoirFinale", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(NoirOwned));
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            black = NewImage("Preto", root.transform, new Color(0f, 0f, 0f, 0f));
            Stretch(black.rectTransform);
            black.raycastTarget = false;

            lineText = NewText("Linha", root.transform, string.Empty, 54f, NoirTheme.Paper);
            lineText.rectTransform.anchorMin = lineText.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
            lineText.rectTransform.sizeDelta = new Vector2(1600f, 220f);

            titleText = NewText("Titulo", root.transform,
                Loc.T("PISCADA FATAL") + "\n<size=40%><color=#BEBAAC>" + Loc.T("FIM") + "</color></size>", 64f, NoirTheme.Paper);
            titleText.rectTransform.anchorMin = titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.3f);
            titleText.rectTransform.sizeDelta = new Vector2(1400f, 200f);
            titleText.characterSpacing = 14f;
            titleText.alpha = 0f;

            GameObject buttons = new("Botoes", typeof(RectTransform), typeof(CanvasGroup));
            buttons.transform.SetParent(root.transform, false);
            RectTransform buttonsRect = (RectTransform)buttons.transform;
            buttonsRect.anchorMin = buttonsRect.anchorMax = new Vector2(0.5f, 0.14f);
            buttonsRect.sizeDelta = new Vector2(1000f, 90f);
            buttonsGroup = buttons.GetComponent<CanvasGroup>();
            buttonsGroup.alpha = 0f;
            buttonsGroup.interactable = false;
            buttonsGroup.blocksRaycasts = false;

            NewButton(buttonsRect, "VOLTAR AO MENU", new Vector2(-230f, 0f), ReturnToMenu);
            NewButton(buttonsRect, "SAIR DO JOGO", new Vector2(230f, 0f), Quit);
        }

        private static void NewButton(RectTransform parent, string label, Vector2 position, System.Action onClick)
        {
            GameObject go = new("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(420f, 76f);
            Image hit = go.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = hit;
            button.onClick.AddListener(() => onClick());
            // Traduz antes do NoirButtonJuice (adicionado abaixo) medir o rotulo; o nome do objeto fica em PT.
            TMP_Text text = NewText("Label", go.transform, Loc.T(label), 30f, NoirTheme.Paper);
            Stretch(text.rectTransform);
            text.characterSpacing = 8f;
            go.AddComponent<NoirButtonJuice>();
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = NoirTheme.Font();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.richText = true;
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
