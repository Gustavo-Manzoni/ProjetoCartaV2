using System.Collections;
using ProjetoCarta.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace ProjetoCarta.Blink
{
    public sealed class BlinkSceneController : MonoBehaviour
    {
        [Header("Moments in fixed order")]
        [Tooltip("Sentenca (P1), Ultima Noite (P2), Perdao (P3).")]
        [SerializeField] private GameObject[] moments = new GameObject[3];
        [SerializeField, Min(0)] private int startingMoment;

        [Header("Visual transition")]
        [SerializeField] private CanvasGroup blackOverlay;
        [SerializeField, Min(0f)] private float closeDuration = 0.12f;
        [SerializeField, Min(0f)] private float closedHoldDuration = 0.06f;
        [SerializeField, Min(0f)] private float openDuration = 0.16f;
        [SerializeField] private AnimationCurve eyelidCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Player")]
        [SerializeField] private FirstPersonController playerController;
        [Tooltip("Caminho (dentro de cada momento) do piso que define a area caminhavel.")]
        [SerializeField] private string floorPath = "Sala_12x12/Piso";

        [Header("Events")]
        [SerializeField] private UnityEvent onBlinkStarted;
        [SerializeField] private UnityEvent<int> onMomentChanged;
        [SerializeField] private UnityEvent onBlinkFinished;

        private static Sprite lidSprite;

        private Coroutine blinkRoutine;
        private RectTransform upperLid;
        private RectTransform lowerLid;

        public int CurrentMoment { get; private set; }
        public bool IsBlinking => blinkRoutine != null;
        public int MomentCount => moments?.Length ?? 0;

        private void Awake()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<FirstPersonController>();

            // O jogo sempre comeca no momento inicial configurado (P1 - A Sentenca), mesmo que outro
            // momento tenha ficado ativo na cena salva durante a edicao.
            CurrentMoment = NormalizeIndex(startingMoment);
            if (moments == null || moments.Length == 0 || moments[CurrentMoment] == null)
                CurrentMoment = Mathf.Max(0, FindActiveMoment());

            ApplyMoment(CurrentMoment);
            CreateEyelids();
            SetOverlayAlpha(0f);
        }

        /// <summary>Piscar e o botao direito do mouse (o esquerdo fica para interagir).</summary>
        private void Update()
        {
            if (IsBlinking || Mouse.current == null ||
                ProjetoCarta.NoirUI.PauseState.IsPaused || GameplayLocks.BlinkLocked)
                return;

            if (Mouse.current.rightButton.wasPressedThisFrame)
                Blink();
        }

        public bool Blink()
        {
            if (IsBlinking || !HasUsableMoments() || GameplayLocks.BlinkLocked)
                return false;

            blinkRoutine = StartCoroutine(BlinkRoutine());
            return true;
        }

        public void SetMomentImmediate(int momentIndex)
        {
            if (IsBlinking || !HasUsableMoments())
                return;

            int normalizedIndex = NormalizeIndex(momentIndex);
            if (moments[normalizedIndex] == null)
                return;

            CurrentMoment = normalizedIndex;
            ApplyMoment(CurrentMoment);
            onMomentChanged?.Invoke(CurrentMoment);
        }

        private IEnumerator BlinkRoutine()
        {
            LockPlayer();
            onBlinkStarted?.Invoke();

            yield return FadeOverlay(0f, 1f, closeDuration);
            AdvanceMoment();
            ResolvePlayerPosition();

            if (closedHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(closedHoldDuration);

            yield return FadeOverlay(1f, 0f, openDuration);

            UnlockPlayer();
            blinkRoutine = null;
            onBlinkFinished?.Invoke();
        }

        private void AdvanceMoment()
        {
            int nextMoment = CurrentMoment;
            for (int i = 0; i < moments.Length; i++)
            {
                nextMoment = NormalizeIndex(nextMoment + 1);
                if (moments[nextMoment] != null)
                    break;
            }

            CurrentMoment = nextMoment;
            ApplyMoment(CurrentMoment);
            onMomentChanged?.Invoke(CurrentMoment);
        }

        private void ApplyMoment(int activeIndex)
        {
            if (moments == null)
                return;

            for (int i = 0; i < moments.Length; i++)
            {
                if (moments[i] != null)
                    moments[i].SetActive(i == activeIndex);
            }
        }

        private IEnumerator FadeOverlay(float from, float to, float duration)
        {
            if (blackOverlay == null)
                yield break;

            blackOverlay.blocksRaycasts = true;

            if (duration <= 0f)
            {
                SetOverlayAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float amount = eyelidCurve != null
                    ? eyelidCurve.Evaluate(normalizedTime)
                    : normalizedTime;
                SetOverlayAlpha(Mathf.LerpUnclamped(from, to, amount));
                yield return null;
            }

            SetOverlayAlpha(to);
            blackOverlay.blocksRaycasts = to > 0f;
        }

        private void LockPlayer()
        {
            // Durante a piscada nada responde: o controle so volta com o jogador ja em posicao segura.
            GameplayLocks.Acquire(this, GameplayLock.Cutscene);
        }

        private void UnlockPlayer()
        {
            GameplayLocks.Release(this);
        }

        private void ResolvePlayerPosition()
        {
            if (playerController == null)
                return;

            CharacterController controller = playerController.GetComponent<CharacterController>();
            PlayerSafePlacement.Resolve(controller, GetWalkableArea(CurrentMoment), GetBlockedAreas(CurrentMoment));
        }

        private Bounds[] GetBlockedAreas(int momentIndex)
        {
            if (moments == null || momentIndex < 0 || momentIndex >= moments.Length || moments[momentIndex] == null)
                return System.Array.Empty<Bounds>();

            BlinkBlockedArea[] areas = moments[momentIndex].GetComponentsInChildren<BlinkBlockedArea>();
            Bounds[] bounds = new Bounds[areas.Length];
            for (int i = 0; i < areas.Length; i++)
                bounds[i] = areas[i].WorldBounds;
            return bounds;
        }

        private Bounds GetWalkableArea(int momentIndex)
        {
            if (moments == null || momentIndex < 0 || momentIndex >= moments.Length || moments[momentIndex] == null)
                return default;

            Transform floor = moments[momentIndex].transform.Find(floorPath);
            Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
            return floorRenderer != null ? floorRenderer.bounds : default;
        }

        /// <summary>
        /// 0 = olhos abertos, 1 = fechados. As palpebras descem/sobem primeiro e o preto total so entra
        /// no fim do fechamento: a piscada fica legivel sem virar um simples fade.
        /// </summary>
        private void SetOverlayAlpha(float closedAmount)
        {
            closedAmount = Mathf.Clamp01(closedAmount);
            UpdateEyelids(closedAmount);

            if (blackOverlay == null)
                return;

            blackOverlay.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, closedAmount));
            if (blackOverlay.alpha <= 0f)
                blackOverlay.blocksRaycasts = false;
        }

        private void CreateEyelids()
        {
            if (blackOverlay == null || upperLid != null)
                return;

            Transform parent = blackOverlay.transform.parent;
            if (parent == null)
                return;

            Sprite sprite = GetLidSprite();
            upperLid = CreateLid("Palpebra_Superior", parent, sprite, true);
            lowerLid = CreateLid("Palpebra_Inferior", parent, sprite, false);
            UpdateEyelids(0f);
        }

        private RectTransform CreateLid(string lidName, Transform parent, Sprite sprite, bool top)
        {
            GameObject go = new(lidName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(blackOverlay.transform.GetSiblingIndex());
            UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = Color.black;
            image.raycastTarget = false;
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(-0.05f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1.05f, top ? 1f : 0f);
            // Pivo na borda da tela; a de cima e espelhada para crescer para baixo.
            rect.pivot = new Vector2(0.5f, 0f);
            rect.localScale = new Vector3(1f, top ? -1f : 1f, 1f);
            return rect;
        }

        private void UpdateEyelids(float closedAmount)
        {
            if (upperLid == null || lowerLid == null)
                return;

            RectTransform canvasRect = upperLid.parent as RectTransform;
            float height = canvasRect != null ? canvasRect.rect.height : Screen.height;
            // Cada palpebra cobre pouco mais da metade da tela quando fechada (a borda e suave).
            float lidHeight = height * 0.62f * Mathf.SmoothStep(0f, 1f, closedAmount);
            bool visible = closedAmount > 0.001f;
            upperLid.gameObject.SetActive(visible);
            lowerLid.gameObject.SetActive(visible);
            upperLid.sizeDelta = new Vector2(0f, lidHeight);
            lowerLid.sizeDelta = new Vector2(0f, lidHeight);
        }

        private static Sprite GetLidSprite()
        {
            if (lidSprite != null)
                return lidSprite;

            const int height = 128;
            Texture2D texture = new(4, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "BlinkLid"
            };

            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);
                // Base (t=0) opaca, borda (t=1) transparente e suave.
                float alpha = 1f - Mathf.SmoothStep(0.62f, 1f, t);
                for (int x = 0; x < 4; x++)
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }

            texture.Apply();
            lidSprite = Sprite.Create(texture, new Rect(0, 0, 4, height), new Vector2(0.5f, 0f), 100f);
            return lidSprite;
        }

        private int FindActiveMoment()
        {
            if (moments == null)
                return -1;

            for (int i = 0; i < moments.Length; i++)
            {
                if (moments[i] != null && moments[i].activeSelf)
                    return i;
            }

            return -1;
        }

        private int NormalizeIndex(int index)
        {
            if (moments == null || moments.Length == 0)
                return 0;

            return (index % moments.Length + moments.Length) % moments.Length;
        }

        private bool HasUsableMoments()
        {
            if (moments == null || moments.Length < 2)
                return false;

            int validMoments = 0;
            foreach (GameObject moment in moments)
            {
                if (moment != null)
                    validMoments++;
            }

            return validMoments >= 2;
        }

        private void OnDisable()
        {
            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
                blinkRoutine = null;
            }

            SetOverlayAlpha(0f);
            UnlockPlayer();
        }
    }
}
