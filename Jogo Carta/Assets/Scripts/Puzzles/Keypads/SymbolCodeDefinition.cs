using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Fonte unica do cofre de simbolos: as imagens dos simbolos, a ordem em que aparecem no keypad
    /// e a sequencia correta. O papel revelado pelo painel do ritual mostra so as cores (sprite); o keypad
    /// mostra o objeto do painel que tem aquela cor (keypadSprite), e o jogador associa um ao outro.
    /// </summary>
    [CreateAssetMenu(fileName = "SymbolCode", menuName = "Projeto Carta/Puzzles/Symbol Code")]
    public sealed class SymbolCodeDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Symbol
        {
            public string id;
            public string displayName;
            public Color color = Color.white;
            [Tooltip("Marca desta cor no papel da senha.")]
            public Sprite sprite;
            [Tooltip("Icone do objeto do painel com esta cor, mostrado no botao do cofre. Vazio = usa o sprite.")]
            public Sprite keypadSprite;

            public Sprite KeypadSprite => keypadSprite != null ? keypadSprite : sprite;
        }

        [Tooltip("Simbolos disponiveis (indice = valor usado na validacao).")]
        [SerializeField] private Symbol[] symbols = Array.Empty<Symbol>();
        [Tooltip("Sequencia correta, em indices de 'symbols'.")]
        [SerializeField] private int[] solution = Array.Empty<int>();
        [Tooltip("Qual simbolo fica em cada botao do keypad (da esquerda para a direita, de cima para baixo).")]
        [SerializeField] private int[] keypadLayout = Array.Empty<int>();

        public IReadOnlyList<Symbol> Symbols => symbols;
        public IReadOnlyList<int> Solution => solution;
        public IReadOnlyList<int> KeypadLayout => keypadLayout;
        public int Length => solution.Length;

        public Symbol GetSymbol(int index)
        {
            return index >= 0 && index < symbols.Length ? symbols[index] : null;
        }

        public bool Matches(IReadOnlyList<int> entry)
        {
            if (entry == null || entry.Count != solution.Length)
                return false;

            for (int i = 0; i < solution.Length; i++)
            {
                if (entry[i] != solution[i])
                    return false;
            }

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Editor: avisa a ferramenta que reassa o papel da senha quando o codigo muda.</summary>
        public static event Action<SymbolCodeDefinition> EditorValidated;
#endif

        private void OnValidate()
        {
#if UNITY_EDITOR
            EditorValidated?.Invoke(this);
#endif
            if (symbols == null)
                return;

            for (int i = 0; i < solution.Length; i++)
                solution[i] = Mathf.Clamp(solution[i], 0, Mathf.Max(0, symbols.Length - 1));

            for (int i = 0; i < keypadLayout.Length; i++)
                keypadLayout[i] = Mathf.Clamp(keypadLayout[i], 0, Mathf.Max(0, symbols.Length - 1));
        }
    }
}
