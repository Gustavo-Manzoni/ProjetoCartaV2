using System;
using System.Collections;
using System.Collections.Generic;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Trava de tres discos do compartimento de manutencao (P3). Com cada disco na posicao de
    /// <see cref="solution"/>, o compartimento (trancado por puzzle) destrava e abre, revelando a manivela.
    /// <see cref="solution"/> e a fonte unica: o esquema do relatorio de manutencao e gerado a partir dela
    /// (Projeto Carta > Piscada Fatal > Gerar arte dos documentos do caso).
    /// </summary>
    public sealed class DialLockMechanism : MonoBehaviour
    {
        [SerializeField] private RotaryDial[] dials = new RotaryDial[3];
        [Tooltip("Posicao certa de cada disco, da esquerda para a direita (0 = marca para cima; cada passo = um dente no sentido horario).")]
        [SerializeField] private int[] solution = { 2, 5, 7 };

        [Header("Recompensa")]
        [SerializeField] private OpenableInteractable reward;
        [SerializeField, Min(0f)] private float rewardDelay = 0.45f;

        [Header("Pensamentos")]
        [SerializeField, TextArea(1, 2)] private string solvedThought = "As três marcas alinharam. A trava soltou.";

        [SerializeField] private UnityEvent onSolved;

        private bool solved;

        public bool IsSolved => solved;
        public IReadOnlyList<int> Solution => solution;
        public IReadOnlyList<RotaryDial> Dials => dials;

        private void Awake()
        {
            foreach (RotaryDial dial in dials)
            {
                if (dial != null)
                    dial.Turned += OnDialTurned;
            }
        }

        private void OnDestroy()
        {
            foreach (RotaryDial dial in dials)
            {
                if (dial != null)
                    dial.Turned -= OnDialTurned;
            }
        }

        /// <summary>Todos os discos estao na posicao do esquema.</summary>
        public bool IsAligned()
        {
            if (dials == null || solution == null || dials.Length != solution.Length)
                return false;

            for (int i = 0; i < dials.Length; i++)
            {
                if (dials[i] == null || dials[i].Position != Mod(solution[i], dials[i].Positions))
                    return false;
            }

            return true;
        }

        private void OnDialTurned(RotaryDial dial)
        {
            if (solved || !IsAligned())
                return;

            solved = true;
            NoirAudio.Play(NoirSfx.Unlock, 0.85f, 0.9f);
            TomasThoughts.Say(solvedThought);
            onSolved?.Invoke();
            // No CoroutineHost: piscar durante a espera nao deixa o compartimento trancado.
            if (reward != null)
                CoroutineHost.Run(OpenRewardAfterDelay());
        }

        private IEnumerator OpenRewardAfterDelay()
        {
            yield return new WaitForSeconds(rewardDelay);
            if (reward != null)
                reward.UnlockAndOpen();
        }

        private static int Mod(int value, int modulus)
        {
            return modulus <= 0 ? value : ((value % modulus) + modulus) % modulus;
        }

        private void OnValidate()
        {
            if (solution == null || dials == null)
                return;

            if (solution.Length != dials.Length)
                Array.Resize(ref solution, dials.Length);
        }
    }
}
