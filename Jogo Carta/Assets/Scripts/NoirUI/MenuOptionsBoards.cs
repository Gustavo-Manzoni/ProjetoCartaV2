using System.Collections.Generic;
using DG.Tweening;
using ProjetoCarta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Opcoes do menu principal. O botao Opcoes leva a camera direto ao quadro de opcoes (FirstRoom/Options/Sound
    /// Board), onde uma ficha de dossie World Space mostra o mesmo painel da pausa (PauseOptions) mais a aba de
    /// creditos, que so existe aqui. Os controles antigos do quadro ficam escondidos (o idioma e o dropdown).
    /// O Esc fecha o dropdown ou volta ao menu, e a ficha se reconstroi quando o idioma muda.
    /// Criado pelo NoirUIBootstrap no menu; nao precisa de nada na cena.
    /// </summary>
    public sealed class MenuOptionsBoards : MonoBehaviour
    {
        private const float CardWidth = 1920f;
        private const float CardHeight = 1080f;

        /// <summary>O unico quadro de opcoes (antigo quadro de Som, em FirstRoom/Options/Sound Board).</summary>
        private const string OptionsCanvasName = "Sound Canvas";

        private sealed class Board
        {
            public string canvasName;
            public int tab;
            public Canvas canvas;
            public RectTransform root;
            public RectTransform card;
            public PauseOptions options;
            public Quaternion faceRotation;
        }

        private readonly List<Board> boards = new()
        {
            new Board { canvasName = OptionsCanvasName, tab = 0 },
        };

        private MainMenuManager manager;
        private Board active;
        private IMenuState lastState;
        private bool rebuildPending;

        private void Start()
        {
            manager = FindFirstObjectByType<MainMenuManager>();
            BuildAll();
            Loc.Changed += OnLanguageChanged;
            GameSettings.Changed += OnSettingsChanged;
        }

        private void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;
            GameSettings.Changed -= OnSettingsChanged;
            DOTween.Kill(this);
        }

        // ------------------------------------------------------------ construcao

        private void BuildAll()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Board board in boards)
            {
                board.canvas = null;
                foreach (Canvas c in canvases)
                {
                    if (c != null && c.name == board.canvasName && c.renderMode == RenderMode.WorldSpace)
                    {
                        board.canvas = c;
                        break;
                    }
                }

                if (board.canvas != null)
                    Build(board);
            }
        }

        private void Build(Board board)
        {
            Canvas canvas = board.canvas;
            if (canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;

            // Os controles antigos (sliders, toggles, BR/EN) saem de cena; a ficha nova faz tudo.
            Transform old = canvas.transform.Find("Sliders");
            Quaternion faceRotation = old != null ? old.rotation : canvas.transform.rotation;
            if (old != null)
                old.gameObject.SetActive(false);

            // Espaco 1920x1080 (o mesmo da pausa). O canvas desses quadros tem escala -1 e nao esta alinhado com a
            // placa (o conteudo antigo se posicionava por conta propria), entao a ficha e colocada direto no mundo:
            // na face da placa voltada para a camera do quadro, com a rotacao que o conteudo antigo usava.
            GameObject go = new("NoirFichaOpcoes", typeof(RectTransform), typeof(CanvasGroup), typeof(NoirOwned));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);
            RectTransform root = (RectTransform)go.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(CardWidth, CardHeight);
            board.faceRotation = faceRotation;
            PlaceOnBoard(root, canvas.transform.parent, faceRotation);
            board.root = root;

            board.card = BuildCard(root);

            GameObject content = new("Conteudo", typeof(RectTransform));
            content.layer = go.layer;
            content.transform.SetParent(board.card, false);
            RectTransform contentRect = (RectTransform)content.transform;
            NoirUIKit.Stretch(contentRect);
            contentRect.anchoredPosition = new Vector2(0f, -18f);

            // Margem pequena no veu do dropdown: num quadro 3D ele nao pode virar uma placa escura na parede.
            board.options = new PauseOptions(contentRect, 40f, true);
            // Ja montada na aba certa: a ficha nao aparece vazia enquanto a camera passa pelo quadro.
            board.options.Show(board.tab);
            SetLayerRecursive(root, go.layer);
        }

        /// <summary>Ficha de dossie: cartao escuro preso por percevejos, carimbo e cabecalho datilografado.</summary>
        private static RectTransform BuildCard(RectTransform root)
        {
            Image shadow = NoirUIKit.NewImage("Sombra", root, NoirTheme.GetSprite("noir_glow"), new Color(0f, 0f, 0f, 0.75f));
            NoirUIKit.Center(shadow.rectTransform, new Vector2(18f, -22f), new Vector2(CardWidth + 260f, CardHeight + 220f));

            GameObject cardGo = new("Ficha", typeof(RectTransform));
            cardGo.transform.SetParent(root, false);
            RectTransform card = (RectTransform)cardGo.transform;
            NoirUIKit.Stretch(card);

            Image paper = NoirUIKit.NewImage("Papel", card, null, new Color(0.055f, 0.05f, 0.046f, 0.97f));
            NoirUIKit.Stretch(paper.rectTransform);

            Image frame = NoirUIKit.NewImage("Moldura", card, NoirTheme.GetSprite("noir_btn_normal", 14f), NoirTheme.WithAlpha(NoirTheme.Paper, 0.45f));
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 0.5f;
            NoirUIKit.Stretch(frame.rectTransform);

            // Fio interno, como a borda impressa de uma ficha policial.
            Image inner = NoirUIKit.NewImage("Fio", card, NoirTheme.GetSprite("noir_btn_normal", 14f), NoirTheme.WithAlpha(NoirTheme.Paper, 0.12f));
            inner.type = Image.Type.Sliced;
            inner.pixelsPerUnitMultiplier = 0.7f;
            NoirUIKit.Stretch(inner.rectTransform);
            inner.rectTransform.offsetMin = new Vector2(34f, 34f);
            inner.rectTransform.offsetMax = new Vector2(-34f, -34f);

            TMP_Text caseTag = NoirUIKit.NewText("Caso", card, Loc.T("PISCADA FATAL · FICHA DE AJUSTES"), 24f,
                NoirTheme.WithAlpha(NoirTheme.Ash, 0.7f), 6f);
            caseTag.alignment = TextAlignmentOptions.MidlineLeft;
            NoirUIKit.Center(caseTag.rectTransform, new Vector2(-420f, 470f), new Vector2(980f, 40f));

            TMP_Text caseNumber = NoirUIKit.NewText("Numero", card, "Nº 1958-07", 22f, NoirTheme.WithAlpha(NoirTheme.Ash, 0.5f), 5f);
            caseNumber.alignment = TextAlignmentOptions.MidlineLeft;
            NoirUIKit.Center(caseNumber.rectTransform, new Vector2(-420f, 436f), new Vector2(980f, 34f));

            // Carimbo vermelho no canto.
            GameObject stampGo = new("Carimbo", typeof(RectTransform));
            stampGo.transform.SetParent(card, false);
            RectTransform stamp = (RectTransform)stampGo.transform;
            NoirUIKit.Center(stamp, new Vector2(700f, 440f), new Vector2(360f, 70f));
            stamp.localRotation = Quaternion.Euler(0f, 0f, -9f);
            Image ink = NoirUIKit.NewImage("Tinta", stamp, null, NoirTheme.WithAlpha(NoirTheme.Blood, 0.12f));
            NoirUIKit.Stretch(ink.rectTransform);
            for (int i = 0; i < 2; i++)
            {
                Image rule = NoirUIKit.NewImage("Filete", stamp, null, NoirTheme.WithAlpha(NoirTheme.Blood, 0.75f));
                rule.rectTransform.anchorMin = new Vector2(0f, i);
                rule.rectTransform.anchorMax = new Vector2(1f, i);
                rule.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rule.rectTransform.sizeDelta = new Vector2(0f, 4f);
                rule.rectTransform.anchoredPosition = Vector2.zero;
            }
            TMP_Text stampText = NoirUIKit.NewText("Texto", stamp, Loc.T("CONFIDENCIAL"), 38f, NoirTheme.WithAlpha(NoirTheme.Blood, 0.85f), 10f);
            stampText.fontStyle = FontStyles.Bold;
            NoirUIKit.Stretch(stampText.rectTransform);

            // Percevejos prendendo a ficha no quadro.
            foreach (Vector2 p in new[] { new Vector2(-900f, 500f), new Vector2(900f, 500f) })
            {
                Image pinShadow = NoirUIKit.NewImage("SombraPercevejo", card, NoirSkin.Circle(), new Color(0f, 0f, 0f, 0.5f));
                NoirUIKit.Center(pinShadow.rectTransform, p + new Vector2(6f, -8f), new Vector2(46f, 46f));
                Image pin = NoirUIKit.NewImage("Percevejo", card, NoirSkin.Circle(), NoirTheme.Blood);
                NoirUIKit.Center(pin.rectTransform, p, new Vector2(42f, 42f));
                Image shine = NoirUIKit.NewImage("Brilho", pin.rectTransform, NoirSkin.Circle(), NoirTheme.WithAlpha(Color.white, 0.45f));
                NoirUIKit.Center(shine.rectTransform, new Vector2(-8f, 8f), new Vector2(12f, 12f));
            }

            return card;
        }

        /// <summary>Centra a ficha na face da placa voltada para a camera do quadro e a escala para caber nela.</summary>
        private static void PlaceOnBoard(RectTransform root, Transform board, Quaternion faceRotation)
        {
            Renderer plate = board != null ? board.GetComponent<Renderer>() : null;
            if (plate == null)
            {
                root.anchoredPosition = Vector2.zero;
                root.localScale = Vector3.one * 0.5f;
                return;
            }

            // A espessura da placa e o eixo z local dela; a frente e o lado em que esta a camera do quadro.
            Vector3 center = plate.bounds.center;
            Vector3 normal = board.forward;
            Unity.Cinemachine.CinemachineCamera cam = board.GetComponentInChildren<Unity.Cinemachine.CinemachineCamera>(true);
            if (cam != null && Vector3.Dot(cam.transform.position - center, normal) < 0f)
                normal = -normal;

            Vector3 size = board.lossyScale;
            float width = Mathf.Abs(size.x);
            float height = Mathf.Abs(size.y);
            float depth = Mathf.Abs(size.z);

            Vector3 face = center + normal * (depth * 0.5f + 0.03f);
            root.position = face;
            root.rotation = faceRotation;

            // Tamanho no mundo: no maximo 84% da placa (a cortica aparece em volta), sem distorcer o espaco 1920x1080.
            float metersPerPixel = Mathf.Min(width * 1f / CardWidth, height * 1f / CardHeight);

            // A camera do quadro enquadra so parte da placa (e de lado): centra a ficha onde ela olha e cresce ate os
            // quatro cantos encostarem numa margem da tela, para a ficha ocupar quase todo o enquadramento.
            if (cam != null)
            {
                // Pose sem a inclinacao do mouse: a ficha nao pode ficar torta por onde o cursor estava ao montar.
                CameraTilt tilt = cam.GetComponent<CameraTilt>();
                Quaternion camRotation = tilt != null ? tilt.BaseRotation : cam.transform.rotation;
                Ray ray = new(cam.transform.position, camRotation * Vector3.forward);
                if (new Plane(normal, face).Raycast(ray, out float distance) && distance > 0.01f)
                {
                    root.position = ray.GetPoint(distance);
                    float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
                    metersPerPixel = Mathf.Min(metersPerPixel,
                        FitToView(cam.transform.position, camRotation, cam.Lens.FieldOfView, aspect, root.position, root.rotation));
                }
            }
            Vector3 parentScale = root.parent.lossyScale;
            float parentMagnitude = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
            root.localScale = Vector3.one * (metersPerPixel / parentMagnitude);
        }

        /// <summary>
        /// Maior escala (metros por pixel da ficha) em que os quatro cantos da ficha cabem na visao da camera, com
        /// uma margem para o balanco do CameraTilt. Busca binaria projetando os cantos no espaco da camera.
        /// </summary>
        private static float FitToView(Vector3 camPosition, Quaternion camRotation, float verticalFov, float aspect,
            Vector3 center, Quaternion rotation)
        {
            const float marginX = 1f;
            const float marginY = 1f;
            float tanY = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * aspect;
            Quaternion toCamera = Quaternion.Inverse(camRotation);

            bool Fits(float metersPerPixel)
            {
                Vector3 right = rotation * Vector3.right * (CardWidth * 0.5f * metersPerPixel);
                Vector3 up = rotation * Vector3.up * (CardHeight * 0.5f * metersPerPixel);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = center + right * (i % 2 == 0 ? -1f : 1f) + up * (i < 2 ? 1f : -1f);
                    Vector3 local = toCamera * (corner - camPosition);
                    if (local.z <= 0.01f)
                        return false;
                    if (Mathf.Abs(local.x / local.z) > tanX * marginX || Mathf.Abs(local.y / local.z) > tanY * marginY)
                        return false;
                }

                return true;
            }

            float low = 0f;
            float high = 0.05f;
            for (int i = 0; i < 24; i++)
            {
                float mid = (low + high) * 0.5f;
                if (Fits(mid))
                    low = mid;
                else
                    high = mid;
            }

            return low > 0f ? low : 0.002f;
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t)
                SetLayerRecursive(child, layer);
        }

        // ------------------------------------------------------------ estado do menu e entrada

        private void Update()
        {
            if (rebuildPending)
                Rebuild();

            if (manager == null)
                return;

            IMenuState state = manager.CurrentState;
            if (state != lastState)
            {
                lastState = state;
                SetActive(BoardFor(state));
            }

            Keyboard kb = Keyboard.current;
            if (active != null && active.options != null)
            {
                active.options.Tick();
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    if (!active.options.HandleEscape())
                        GoBack();
                    return;
                }

                if (kb != null)
                    active.options.HandleInput(kb);
            }
        }

        private Board BoardFor(IMenuState state)
        {
            return state != null && state == manager.OptionsState ? Find(OptionsCanvasName) : null;
        }

        private Board Find(string canvasName)
        {
            foreach (Board b in boards)
                if (b.canvasName == canvasName && b.options != null)
                    return b;
            return null;
        }

        private void SetActive(Board board)
        {
            if (board == active)
                return;

            active = board;
            if (board == null)
                return;

            // A camera chega ao quadro: a ficha assenta e as linhas entram na ultima aba aberta.
            PlaceOnBoard(board.root, board.canvas.transform.parent, board.faceRotation);
            board.options.Show();
            RectTransform card = board.card;
            card.DOKill();
            card.localScale = Vector3.one * 1.03f;
            card.localRotation = Quaternion.Euler(0f, 0f, 1.6f);
            card.DOScale(1f, 0.6f).SetEase(Ease.OutCubic).SetUpdate(true);
            card.DOLocalRotate(Vector3.zero, 0.8f).SetEase(Ease.OutBack).SetUpdate(true);
            NoirAudio.Play(NoirSfx.Paper, 0.6f, 0.95f);
        }

        private void GoBack()
        {
            NoirAudio.Play(NoirSfx.Back, 0.6f);
            manager.OnBack();
        }

        // ------------------------------------------------------------ idioma e opcoes

        private void OnLanguageChanged(Language language)
        {
            // A troca vem de dentro do clique no dropdown: reconstroi no proximo frame.
            rebuildPending = true;
        }

        private void Rebuild()
        {
            rebuildPending = false;
            Board wasActive = active;
            int tab = wasActive != null ? wasActive.options.TabIndex : -1;
            int option = wasActive != null ? wasActive.options.OptionIndex : -1;

            foreach (Board b in boards)
            {
                if (b.root != null)
                    Destroy(b.root.gameObject);
                b.root = null;
                b.card = null;
                b.options = null;
            }

            active = null;
            BuildAll();

            if (wasActive != null && wasActive.options != null)
            {
                active = wasActive;
                wasActive.options.Show(tab, option);

                // A lampada falha e a ficha volta escrita na outra lingua.
                CanvasGroup cg = wasActive.root.GetComponent<CanvasGroup>();
                cg.alpha = 0f;
                DOTween.Sequence().SetUpdate(true).SetTarget(this)
                    .Append(cg.DOFade(1f, 0.06f))
                    .Append(cg.DOFade(0.2f, 0.06f))
                    .Append(cg.DOFade(1f, 0.08f))
                    .AppendInterval(0.06f)
                    .Append(cg.DOFade(0.45f, 0.05f))
                    .Append(cg.DOFade(1f, 0.15f));
                NoirAudio.Play(NoirSfx.LampFlicker, 0.5f, 1.1f);
            }
        }

        private static void OnSettingsChanged(string key)
        {
            // O contador de FPS do menu e do SettingsManager antigo: acompanha a opcao nova.
            if (key == GameSettings.ShowFpsKey && SettingsManager.Instance != null)
                SettingsManager.Instance.ShowFPS(GameSettings.ShowFps);
        }
    }
}
