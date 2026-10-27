using System;
using UnityEngine;

namespace ProjetoCarta.NoirEnv
{
    /// <summary>
    /// Modelos 3D usados como mobilia de fundo pelas tres salas (<see cref="NoirEnvironment"/>). Fica em
    /// Resources/NoirEnv para o cenario, que e montado por codigo, alcancar os modelos sem objeto na cena.
    /// Cada entrada pode pegar so um filho de um kit (ex.: uma estante do FBX de estantes) e trocar o material.
    /// </summary>
    [CreateAssetMenu(menuName = "Projeto Carta/Cenario/Biblioteca de mobilia", fileName = "NoirDecorLibrary")]
    public sealed class NoirDecorLibrary : ScriptableObject
    {
        public const string ResourcePath = "NoirEnv/NoirDecorLibrary";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Nome usado pelo codigo do cenario.")]
            public string id;
            public GameObject model;
            [Tooltip("Opcional: so este filho do modelo (kits com varios moveis num FBX).")]
            public string child;
            [Tooltip("Opcional: material aplicado a todas as malhas (modelos importados sem textura).")]
            public Material material;
            [Tooltip("Giro em Y para a frente do movel apontar para +Z.")]
            public float frontYaw;
            [Tooltip("Alturas das prateleiras, de 0 (base) a 1 (topo), para encher de caixas e livros.")]
            public float[] shelves = Array.Empty<float>();
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private static NoirDecorLibrary cached;

        public static NoirDecorLibrary Instance
        {
            get
            {
                if (cached == null)
                    cached = Resources.Load<NoirDecorLibrary>(ResourcePath);
                return cached;
            }
        }

        public bool TryGet(string id, out Entry entry)
        {
            foreach (Entry e in entries)
            {
                if (e != null && e.model != null && e.id == id)
                {
                    entry = e;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }
}
