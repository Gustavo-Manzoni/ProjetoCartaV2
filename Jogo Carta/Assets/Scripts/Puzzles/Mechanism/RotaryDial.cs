using System;
using System.Collections;
using ProjetoCarta.Interaction;
using ProjetoCarta.NoirUI;
using UnityEngine;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Disco dentado da trava de manutencao: cada clique gira um dente no sentido horario (visto de frente).
    /// A posicao 0 e a marca para cima; a posicao n fica n * (360 / posicoes) graus no sentido horario.
    /// Quem decide se o conjunto esta certo e o <see cref="DialLockMechanism"/>.
    /// </summary>
    public sealed class RotaryDial : RaycastInteractable
    {
        [Header("Disco")]
        [Tooltip("Parte que gira (o disco com a marca). Usa este objeto quando vazio.")]
        [SerializeField] private Transform rotor;
        [Tooltip("Eixo local do rotor que aponta para fora da face (para quem olha).")]
        [SerializeField] private Vector3 faceAxis = Vector3.forward;
        [SerializeField, Min(2)] private int positions = 8;
        [SerializeField, Min(0)] private int startPosition;
        [SerializeField, Min(0.01f)] private float turnDuration = 0.18f;

        private DialLockMechanism mechanism;
        private Quaternion restRotation;
        private int position;
        private Coroutine turnRoutine;

        public int Position => position;
        public int Positions => positions;
        public event Action<RotaryDial> Turned;

        private Transform Rotor => rotor != null ? rotor : transform;

        private void Awake()
        {
            mechanism = GetComponentInParent<DialLockMechanism>();
            // A pose da cena e a posicao inicial; a posicao 0 fica startPosition dentes antes.
            restRotation = Rotor.localRotation * Quaternion.Inverse(StepRotation(startPosition));
            position = Mathf.Clamp(startPosition, 0, positions - 1);
            ApplyPose();
        }

        private void OnDisable()
        {
            if (turnRoutine == null)
                return;

            StopCoroutine(turnRoutine);
            turnRoutine = null;
            ApplyPose();
        }

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && (mechanism == null || !mechanism.IsSolved);
        }

        public override bool CanInteract(InteractionContext context)
        {
            return IsFocusable(context) && turnRoutine == null;
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            return InteractionFeedback.None;
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            Quaternion from = Rotor.localRotation;
            position = (position + 1) % positions;
            NoirAudio.Play(NoirSfx.Tick, 0.75f, 0.8f);
            turnRoutine = StartCoroutine(TurnRoutine(from, TargetRotation()));
            // A logica muda na hora; a animacao e so visual (e conclui no OnDisable).
            Turned?.Invoke(this);
            context.Interactor?.RefreshPrompt(this);
        }

        private IEnumerator TurnRoutine(Quaternion from, Quaternion to)
        {
            float elapsed = 0f;
            while (elapsed < turnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / turnDuration);
                // Leve passagem do ponto: o dente "cai" no encaixe.
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Rotor.localRotation = Quaternion.SlerpUnclamped(from, to, eased);
                yield return null;
            }

            Rotor.localRotation = to;
            turnRoutine = null;
        }

        private void ApplyPose()
        {
            Rotor.localRotation = TargetRotation();
        }

        private Quaternion TargetRotation()
        {
            return restRotation * StepRotation(position);
        }

        /// <summary>
        /// O Unity e mao esquerda: angulo positivo em torno do eixo que sai da face (para quem olha) aparece
        /// no sentido horario para quem olha.
        /// </summary>
        private Quaternion StepRotation(int steps)
        {
            return Quaternion.AngleAxis(steps * 360f / positions, faceAxis.normalized);
        }

        private void OnValidate()
        {
            if (faceAxis == Vector3.zero)
                faceAxis = Vector3.forward;
            startPosition = Mathf.Clamp(startPosition, 0, Mathf.Max(0, positions - 1));
        }
    }
}
