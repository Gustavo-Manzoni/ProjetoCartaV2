using System;
using System.Collections;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Bandeja de protocolo da mesa de P3: tres documentos do caso em tres espacos (I, II, III).
    /// Cada documento e um <see cref="ReadableDocument"/> comum (clique le em tela cheia); as alavancas entre os
    /// espacos (<see cref="ProtocolSwapHandle"/>) trocam os vizinhos de lugar. Com o mais antigo em I e o mais
    /// recente em III, a gaveta do arquivo destrava e abre. O estado vive aqui e sobrevive a piscada.
    /// </summary>
    public sealed class ProtocolTray : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public ReadableDocument document;
            [Tooltip("Ordem cronologica do documento: 0 = o mais antigo.")]
            [Min(0)] public int chronologicalRank;
        }

        [Header("Espacos (I, II, III)")]
        [Tooltip("Pose de cada espaco. O documento fica exatamente nela.")]
        [SerializeField] private Transform[] slots = new Transform[3];
        [Tooltip("Documento em cada espaco no comeco (I, II, III). Nao deve comecar na ordem certa.")]
        [SerializeField] private Entry[] startingOrder = new Entry[3];

        [Header("Troca")]
        [SerializeField, Min(0.05f)] private float swapDuration = 0.5f;
        [Tooltip("Quanto os papeis sobem (metros) para passar um por cima do outro.")]
        [SerializeField, Min(0f)] private float swapLift = 0.04f;

        [Header("Recompensa")]
        [Tooltip("Gaveta trancada por puzzle (lockedByPuzzle) que guarda a pasta.")]
        [SerializeField] private OpenableInteractable reward;
        [SerializeField, Min(0f)] private float rewardDelay = 0.6f;

        [Header("Pensamentos")]
        [SerializeField, TextArea(1, 2)] private string solvedThought = "A culpa, a súplica, a graça. A gaveta do arquivo destravou.";

        [SerializeField] private UnityEvent onSolved;

        private Entry[] current;
        private bool solved;
        private Coroutine swapRoutine;
        private int swapA = -1, swapB = -1;

        public bool IsSolved => solved;
        public bool IsBusy => swapRoutine != null;
        public int SlotCount => slots.Length;
        public event Action<ProtocolTray> Changed;

        private void Awake()
        {
            current = new Entry[startingOrder.Length];
            Array.Copy(startingOrder, current, startingOrder.Length);
            for (int i = 0; i < current.Length; i++)
                SnapToSlot(i);
        }

        private void OnDisable()
        {
            // Piscada no meio da troca: os papeis terminam no lugar novo, sem animacao.
            if (swapRoutine == null)
                return;

            StopCoroutine(swapRoutine);
            swapRoutine = null;
            SnapToSlot(swapA);
            SnapToSlot(swapB);
            Evaluate(false);
        }

        /// <summary>Documento no espaco (0 = I). Null fora do intervalo.</summary>
        public ReadableDocument DocumentAt(int slot)
        {
            return current != null && slot >= 0 && slot < current.Length ? current[slot]?.document : null;
        }

        /// <summary>Os tres documentos estao do mais antigo (I) ao mais recente (III).</summary>
        public bool IsInChronologicalOrder()
        {
            if (current == null)
                return false;

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] == null || current[i].chronologicalRank != i)
                    return false;
            }

            return true;
        }

        public bool CanSwap(int a, int b)
        {
            return !solved && !IsBusy && isActiveAndEnabled && IsValid(a) && IsValid(b) && a != b;
        }

        public void Swap(int a, int b)
        {
            if (!CanSwap(a, b))
                return;

            (current[a], current[b]) = (current[b], current[a]);
            swapA = a;
            swapB = b;
            NoirAudio.Play(NoirSfx.Paper, 0.7f, 1.1f);
            swapRoutine = StartCoroutine(SwapRoutine(a, b));
        }

        private IEnumerator SwapRoutine(int a, int b)
        {
            Transform first = current[a].document.transform;
            Transform second = current[b].document.transform;
            Vector3 firstFrom = first.position, secondFrom = second.position;
            Quaternion firstRot = first.rotation, secondRot = second.rotation;
            Vector3 up = transform.up;

            float elapsed = 0f;
            while (elapsed < swapDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swapDuration);
                float eased = t * t * (3f - 2f * t);
                float arc = Mathf.Sin(t * Mathf.PI);
                // Um passa por cima do outro, para nao atravessarem.
                first.SetPositionAndRotation(Vector3.Lerp(firstFrom, slots[a].position, eased) + up * (arc * swapLift * 2f),
                    Quaternion.Slerp(firstRot, slots[a].rotation, eased));
                second.SetPositionAndRotation(Vector3.Lerp(secondFrom, slots[b].position, eased) + up * (arc * swapLift),
                    Quaternion.Slerp(secondRot, slots[b].rotation, eased));
                yield return null;
            }

            SnapToSlot(a);
            SnapToSlot(b);
            swapRoutine = null;
            Evaluate(true);
        }

        private void Evaluate(bool withFeedback)
        {
            Changed?.Invoke(this);
            if (solved)
                return;

            if (!IsInChronologicalOrder())
            {
                // Feedback discreto: o trinco da gaveta estala, mas nao solta.
                if (withFeedback)
                    NoirAudio.Play(NoirSfx.Locked, 0.35f, 1.15f);
                return;
            }

            solved = true;
            if (withFeedback)
            {
                NoirAudio.Play(NoirSfx.Unlock, 0.9f);
                TomasThoughts.Say(solvedThought);
            }
            onSolved?.Invoke();
            // No CoroutineHost: uma piscada durante a espera nao deixa a gaveta trancada para sempre.
            if (reward != null)
                CoroutineHost.Run(OpenRewardAfterDelay());
        }

        private IEnumerator OpenRewardAfterDelay()
        {
            yield return new WaitForSeconds(rewardDelay);
            if (reward != null)
                reward.UnlockAndOpen();
        }

        private void SnapToSlot(int slot)
        {
            if (!IsValid(slot) || current[slot]?.document == null || slots[slot] == null)
                return;

            current[slot].document.transform.SetPositionAndRotation(slots[slot].position, slots[slot].rotation);
        }

        private bool IsValid(int slot)
        {
            return current != null && slot >= 0 && slot < current.Length && slot < slots.Length;
        }
    }
}
