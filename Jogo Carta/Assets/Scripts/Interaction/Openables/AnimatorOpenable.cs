using UnityEngine;

namespace ProjetoCarta.Interaction
{
    /// <summary>
    /// Abre e fecha tocando o clip de abertura que ja existe no Animator Controller
    /// (para frente para abrir, de tras para frente para fechar).
    /// O estado de abertura precisa ter o multiplicador de velocidade ligado ao parametro float
    /// <see cref="directionParameter"/> (1 abre, -1 fecha).
    /// </summary>
    public sealed class AnimatorOpenable : OpenableInteractable
    {
        [Header("Animator")]
        [SerializeField] private Animator animator;
        [Tooltip("Nome do estado do Animator que contem o clip de abertura.")]
        [SerializeField] private string openStateName = "Open";
        [Tooltip("Parametro float usado como multiplicador de velocidade do estado (1 abre, -1 fecha).")]
        [SerializeField] private string directionParameter = "Sentido";
        [SerializeField] private int layer;

        private int stateHash;
        private int directionHash;
        private float clipLength = 0.5f;
        private float normalizedPosition;
        private float motionStart;
        private float motionTarget;

        protected override void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            stateHash = Animator.StringToHash(openStateName);
            directionHash = Animator.StringToHash(directionParameter);

            if (animator != null)
            {
                // Sem isso o Animator volta ao estado inicial quando o momento e desativado pela piscada.
                animator.keepAnimatorStateOnDisable = true;
                clipLength = FindClipLength();
            }

            base.Awake();
        }

        private void OnEnable()
        {
            // Reaplica a pose atual ao voltar de outro momento.
            if (animator != null && !IsBusy && animator.isActiveAndEnabled)
                PlayAt(normalizedPosition, 0f);
        }

        protected override void ApplyInitialState(bool open)
        {
            normalizedPosition = open ? 1f : 0f;
            if (animator != null && animator.isActiveAndEnabled && open)
                PlayAt(1f, 0f);
        }

        protected override float BeginMotion(bool open)
        {
            if (animator == null)
            {
                normalizedPosition = open ? 1f : 0f;
                return 0f;
            }

            motionStart = Mathf.Clamp01(normalizedPosition);
            motionTarget = open ? 1f : 0f;
            PlayAt(motionStart, open ? 1f : -1f);
            return Mathf.Abs(motionTarget - motionStart) * clipLength;
        }

        protected override void UpdateMotion(bool open, float normalizedTime)
        {
            normalizedPosition = Mathf.Lerp(motionStart, motionTarget, normalizedTime);
        }

        protected override void EndMotion(bool open)
        {
            normalizedPosition = open ? 1f : 0f;
            if (animator != null && animator.isActiveAndEnabled)
                PlayAt(normalizedPosition, 0f);
        }

        private void PlayAt(float normalizedTime, float direction)
        {
            if (HasParameter(directionHash))
                animator.SetFloat(directionHash, direction);

            animator.Play(stateHash, layer, Mathf.Clamp01(normalizedTime));
            animator.Update(0f);
        }

        private bool HasParameter(int hash)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash)
                    return true;
            }

            return false;
        }

        private float FindClipLength()
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
                return clipLength;

            // O estado de abertura usa um unico clip; o mais longo do controller e uma boa aproximacao.
            float longest = 0f;
            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null)
                    longest = Mathf.Max(longest, clip.length);
            }

            return longest > 0f ? longest : clipLength;
        }
    }
}
