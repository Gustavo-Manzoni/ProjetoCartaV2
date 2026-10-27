using System.Collections;
using System.Collections.Generic;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.NoirEnv;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Alavanca dentro da caixa de controle da cadeira eletrica (P2). Ao desligar: a alavanca desce,
    /// as luzes da cadeira morrem, o zumbido para e a porta da camara e destravada.
    /// So fica disponivel depois que a caixa de controle e aberta com a chave da camara.
    /// </summary>
    public sealed class ChairPowerSwitch : RaycastInteractable
    {
        [Header("Alavanca")]
        [SerializeField] private Transform lever;
        [SerializeField] private Vector3 offLocalEulerOffset = new(0f, 0f, -110f);
        [SerializeField, Min(0.05f)] private float leverDuration = 0.5f;

        [Header("Efeitos")]
        [Tooltip("Raiz do momento (P2) onde as luzes geradas em tempo de execucao sao procuradas.")]
        [SerializeField] private Transform momentRoot;
        [Tooltip("Luzes/objetos que apagam por completo (procurados pelo nome).")]
        [SerializeField] private string[] killByName = { "Holofote_Cadeira", "Feixe", "Halo_Chao" };
        [Tooltip("Luzes que ficam fracas e param de piscar.")]
        [SerializeField] private string[] dimByName = { "Luz_Teto_0", "Luz_Teto_1", "Luz_Teto_2", "Emergencia" };
        [SerializeField] private Renderer[] indicatorLamps;
        [SerializeField] private Color indicatorOffColor = new(0.05f, 0.02f, 0.02f);

        [Header("Consequência")]
        [SerializeField] private FinalDoor door;
        [SerializeField, TextArea(1, 2)] private string poweredOffThought = "O zumbido parou. A trava da porta estalou.";
        [SerializeField] private UnityEvent onPoweredOff;

        private bool poweredOff;

        public bool IsPoweredOff => poweredOff;

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && !poweredOff;
        }

        public override string GetPrompt(InteractionContext context)
        {
            string prompt = InteractionPrompt;
            return string.IsNullOrWhiteSpace(prompt) || prompt == "Interagir" ? "Desligar a cadeira" : prompt;
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context) || poweredOff)
                return;

            poweredOff = true;
            SetAvailable(false);
            StartCoroutine(PowerDownRoutine());
        }

        private IEnumerator PowerDownRoutine()
        {
            // A sequencia inteira acontece com a piscada travada: o estado nunca fica pela metade.
            GameplayLocks.Acquire(this, GameplayLock.Blink);
            NoirAudio.Play(NoirSfx.Lever, 1f);
            if (lever != null)
            {
                Quaternion start = lever.localRotation;
                Quaternion end = start * Quaternion.Euler(offLocalEulerOffset);
                float elapsed = 0f;
                while (elapsed < leverDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / leverDuration);
                    lever.localRotation = Quaternion.Slerp(start, end, 1f - Mathf.Pow(1f - t, 3f));
                    yield return null;
                }

                lever.localRotation = end;
            }

            NoirAudio.Play(NoirSfx.PowerDown, 1f);
            NoirAmbience.SetChairPowered(false);
            ApplyPowerOff(true);
            yield return new WaitForSeconds(0.9f);

            door?.Unlock();
            TomasThoughts.Say(poweredOffThought);
            onPoweredOff?.Invoke();
            GameplayLocks.Release(this);
        }

        private void OnDisable()
        {
            GameplayLocks.Release(this);
        }

        private void ApplyPowerOff(bool animate)
        {
            Transform root = momentRoot != null ? momentRoot : transform.root;
            List<Transform> found = new();

            foreach (string targetName in killByName)
                Collect(root, targetName, found);

            foreach (Transform target in found)
            {
                foreach (NoirLightFlicker flicker in target.GetComponentsInChildren<NoirLightFlicker>(true))
                    flicker.enabled = false;

                foreach (Light targetLight in target.GetComponentsInChildren<Light>(true))
                {
                    if (animate)
                        StartCoroutine(FadeLight(targetLight, 0f, 0.6f, true));
                    else
                        targetLight.enabled = false;
                }

                foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>(true))
                    targetRenderer.enabled = false;
            }

            found.Clear();
            foreach (string targetName in dimByName)
                Collect(root, targetName, found);

            foreach (Transform target in found)
            {
                foreach (NoirLightFlicker flicker in target.GetComponentsInChildren<NoirLightFlicker>(true))
                    flicker.enabled = false;

                foreach (Light targetLight in target.GetComponentsInChildren<Light>(true))
                {
                    float goal = targetLight.intensity * 0.45f;
                    if (animate)
                        StartCoroutine(FadeLight(targetLight, goal, 1.2f, false));
                    else
                        targetLight.intensity = goal;
                }
            }

            if (indicatorLamps != null)
            {
                MaterialPropertyBlock block = new();
                foreach (Renderer lamp in indicatorLamps)
                {
                    if (lamp == null)
                        continue;

                    lamp.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", Color.black);
                    block.SetColor("_BaseColor", indicatorOffColor);
                    lamp.SetPropertyBlock(block);
                }
            }
        }

        private static IEnumerator FadeLight(Light target, float goal, float duration, bool disableAtEnd)
        {
            if (target == null)
                yield break;

            float start = target.intensity;
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Agoniza piscando antes de apagar.
                float stutter = t < 0.7f && Random.value < 0.25f ? 0.2f : 1f;
                target.intensity = Mathf.Lerp(start, goal, t) * stutter;
                yield return null;
            }

            if (target == null)
                yield break;

            target.intensity = goal;
            if (disableAtEnd)
                target.enabled = false;
        }

        private static void Collect(Transform root, string targetName, List<Transform> results)
        {
            if (root == null)
                return;

            if (root.name == targetName)
                results.Add(root);

            foreach (Transform child in root)
                Collect(child, targetName, results);
        }
    }
}
