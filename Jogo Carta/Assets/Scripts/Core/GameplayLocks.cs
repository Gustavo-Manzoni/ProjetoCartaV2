using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjetoCarta.Core
{
    [Flags]
    public enum GameplayLock
    {
        None = 0,
        Movement = 1 << 0,
        Interaction = 1 << 1,
        Inventory = 1 << 2,
        Blink = 1 << 3,
        Pause = 1 << 4,
        Cursor = 1 << 5,

        /// <summary>Tela que toma o controle e precisa do cursor (keypad, leitura de documento, final).</summary>
        Modal = Movement | Interaction | Inventory | Blink | Pause | Cursor,
        /// <summary>Sequencia curta sem cursor (piscada, animacao do poco com camera, etc.).</summary>
        Cutscene = Movement | Interaction | Inventory | Blink | Pause
    }

    /// <summary>
    /// Central de travas do jogador. Cada sistema (piscada, keypad, pausa, roda de inventario, final)
    /// pede travas com um dono e as libera depois; o estado efetivo e a uniao de todas as travas ativas.
    /// Assim ninguem precisa guardar e restaurar o "estado anterior" de movimento, cursor e interacao.
    /// </summary>
    public static class GameplayLocks
    {
        private static readonly Dictionary<object, GameplayLock> owners = new();
        private static readonly List<object> deadOwners = new();
        private static GameplayLock current;
        private static int escapeConsumedFrame = -1;

        public static event Action Changed;

        /// <summary>Quando verdadeiro, o cursor e controlado pelas travas (cena de gameplay em primeira pessoa).</summary>
        public static bool ManageCursor { get; private set; }

        public static GameplayLock Current => current;
        public static bool MovementLocked => IsLocked(GameplayLock.Movement);
        public static bool InteractionLocked => IsLocked(GameplayLock.Interaction);
        public static bool InventoryLocked => IsLocked(GameplayLock.Inventory);
        public static bool BlinkLocked => IsLocked(GameplayLock.Blink);
        public static bool PauseLocked => IsLocked(GameplayLock.Pause);

        /// <summary>Verdadeiro no frame em que algum sistema ja usou o Esc (ex.: sair do keypad).</summary>
        public static bool EscapeConsumedThisFrame => escapeConsumedFrame == Time.frameCount;

        public static bool IsLocked(GameplayLock flags)
        {
            return (current & flags) != 0;
        }

        public static bool Holds(object owner)
        {
            return owner != null && owners.ContainsKey(owner);
        }

        public static void Acquire(object owner, GameplayLock flags)
        {
            if (owner == null)
                return;

            owners[owner] = flags;
            Recompute();
        }

        public static void Release(object owner)
        {
            if (owner != null && owners.Remove(owner))
                Recompute();
        }

        public static void ConsumeEscape()
        {
            escapeConsumedFrame = Time.frameCount;
        }

        /// <summary>Liga o controle de cursor pelas travas (chamado pelo jogador em primeira pessoa).</summary>
        public static void EnableCursorManagement()
        {
            ManageCursor = true;
            ApplyCursor();
        }

        /// <summary>Solta o cursor e desliga o controle (ex.: ao voltar para o menu).</summary>
        public static void DisableCursorManagement()
        {
            ManageCursor = false;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public static void Clear()
        {
            owners.Clear();
            current = GameplayLock.None;
            ManageCursor = false;
            Changed?.Invoke();
        }

        private static void Recompute()
        {
            GameplayLock combined = GameplayLock.None;
            deadOwners.Clear();

            foreach (KeyValuePair<object, GameplayLock> pair in owners)
            {
                // Donos destruidos (ex.: objeto de cena descarregado) nao seguram travas.
                if (pair.Key is UnityEngine.Object unityObject && unityObject == null)
                {
                    deadOwners.Add(pair.Key);
                    continue;
                }

                combined |= pair.Value;
            }

            foreach (object dead in deadOwners)
                owners.Remove(dead);

            current = combined;
            ApplyCursor();
            Changed?.Invoke();
        }

        private static void ApplyCursor()
        {
            if (!ManageCursor)
                return;

            bool free = IsLocked(GameplayLock.Cursor);
            UnityEngine.Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = free;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            owners.Clear();
            current = GameplayLock.None;
            ManageCursor = false;
            escapeConsumedFrame = -1;
            Changed = null;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            // Travas nunca sobrevivem a troca de cena.
            owners.Clear();
            current = GameplayLock.None;
            ManageCursor = false;
        }
    }
}
