using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Keypad de simbolos (grade 3x2: os cinco objetos do painel, cada um na sua cor, + Enter no canto inferior
    /// direito, sem Apagar).
    /// Cada botao mostra a ordem em que foi escolhido; a telinha mostra uma bolinha por simbolo.
    /// Simbolos, posicoes e sequencia correta vem de <see cref="SymbolCodeDefinition"/>.
    /// </summary>
    public sealed class SymbolKeypad : KeypadPanel
    {
        [Header("Codigo")]
        [SerializeField] private SymbolCodeDefinition code;

        [Header("Botoes (na ordem do layout do codigo)")]
        [SerializeField] private Button[] symbolButtons = Array.Empty<Button>();
        [SerializeField] private Image[] symbolImages = Array.Empty<Image>();
        [Tooltip("Pequeno numero no canto de cada botao com a ordem escolhida.")]
        [SerializeField] private TMP_Text[] orderBadges = Array.Empty<TMP_Text>();
        [SerializeField] private Button enterButton;

        private readonly StringBuilder badgeBuilder = new();

        public SymbolCodeDefinition Code => code;
        protected override int MaxLength => code != null ? Mathf.Max(1, code.Length) : 5;

        private static readonly Color BadgeColor = new(0.97f, 0.94f, 0.86f);

        protected override void Awake()
        {
            ApplySymbolSprites();
            StyleBadges();
            base.Awake();
        }

        /// <summary>O numero da ordem fica sobre o icone colorido: claro, com contorno escuro, legivel em qualquer cor.</summary>
        private void StyleBadges()
        {
            foreach (TMP_Text badge in orderBadges)
            {
                if (badge == null)
                    continue;

                badge.color = BadgeColor;
                badge.fontStyle = FontStyles.Bold;
                badge.outlineWidth = 0.3f;
                badge.outlineColor = new Color32(12, 10, 8, 255);
            }
        }

        protected override void BindButtons()
        {
            for (int slot = 0; slot < symbolButtons.Length; slot++)
            {
                int symbolIndex = SymbolAtSlot(slot);
                Bind(symbolButtons[slot], () => Append(symbolIndex));
            }

            Bind(enterButton, Submit);
        }

        protected override bool Validate(IReadOnlyList<int> attempt)
        {
            return code != null && code.Matches(attempt);
        }

        protected override void OnEntryChanged()
        {
            for (int slot = 0; slot < orderBadges.Length; slot++)
            {
                if (orderBadges[slot] == null)
                    continue;

                int symbolIndex = SymbolAtSlot(slot);
                badgeBuilder.Clear();
                for (int i = 0; i < Entry.Count; i++)
                {
                    if (Entry[i] != symbolIndex)
                        continue;

                    if (badgeBuilder.Length > 0)
                        badgeBuilder.Append('·');
                    badgeBuilder.Append(i + 1);
                }

                orderBadges[slot].text = badgeBuilder.ToString();
            }
        }

        protected override void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                Submit();
        }

        /// <summary>Atualiza as imagens dos botoes a partir do SymbolCodeDefinition.</summary>
        public void ApplySymbolSprites()
        {
            if (code == null)
                return;

            for (int slot = 0; slot < symbolImages.Length; slot++)
            {
                SymbolCodeDefinition.Symbol symbol = code.GetSymbol(SymbolAtSlot(slot));
                if (symbolImages[slot] == null || symbol == null)
                    continue;

                symbolImages[slot].sprite = symbol.KeypadSprite;
                symbolImages[slot].preserveAspect = true;
            }
        }

        private int SymbolAtSlot(int slot)
        {
            if (code == null)
                return slot;

            IReadOnlyList<int> layout = code.KeypadLayout;
            return slot < layout.Count ? layout[slot] : slot;
        }
    }
}
