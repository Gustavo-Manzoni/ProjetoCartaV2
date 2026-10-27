using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Keypad numerico (1-9 em grade 3x3; Apagar, 0 e Enter na quarta linha).
    /// Apagar: toque curto remove o ultimo digito, segurar limpa tudo.
    /// Tambem aceita teclado fisico (0-9, Backspace, Enter).
    /// </summary>
    public sealed class NumericKeypad : KeypadPanel
    {
        // No Input System, Digit1..Digit9 vem antes de Digit0: nao da para somar ao enum.
        private static readonly Key[] DigitKeys =
        {
            Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        private static readonly Key[] NumpadKeys =
        {
            Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
            Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
        };

        [Header("Codigo")]
        [Tooltip("Somente digitos. Cofre do Perdao (P3): 2358, do relogio parado as 23:58.")]
        [SerializeField] private string code = "2358";

        [Header("Botoes")]
        [Tooltip("Indice = digito (0 a 9).")]
        [SerializeField] private Button[] digitButtons = new Button[10];
        [SerializeField] private HoldButton eraseButton;
        [SerializeField] private Button enterButton;

        protected override int MaxLength => Mathf.Max(1, code.Length);

        protected override void BindButtons()
        {
            for (int digit = 0; digit < digitButtons.Length; digit++)
            {
                int value = digit;
                Bind(digitButtons[digit], () => Append(value));
            }

            if (eraseButton != null)
            {
                eraseButton.OnShortPress.RemoveAllListeners();
                eraseButton.OnLongPress.RemoveAllListeners();
                eraseButton.OnShortPress.AddListener(RemoveLast);
                eraseButton.OnLongPress.AddListener(ClearAll);
            }

            Bind(enterButton, Submit);
        }

        protected override bool Validate(IReadOnlyList<int> attempt)
        {
            if (attempt.Count != code.Length)
                return false;

            for (int i = 0; i < code.Length; i++)
            {
                if (attempt[i] != code[i] - '0')
                    return false;
            }

            return true;
        }

        protected override void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            for (int digit = 0; digit <= 9; digit++)
            {
                if (keyboard[DigitKeys[digit]].wasPressedThisFrame || keyboard[NumpadKeys[digit]].wasPressedThisFrame)
                {
                    Append(digit);
                    return;
                }
            }

            if (keyboard.backspaceKey.wasPressedThisFrame)
                RemoveLast();
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                Submit();
        }

        private void OnValidate()
        {
            if (digitButtons == null || digitButtons.Length != 10)
                Array.Resize(ref digitButtons, 10);

            string digitsOnly = string.Empty;
            foreach (char c in code ?? string.Empty)
            {
                if (char.IsDigit(c))
                    digitsOnly += c;
            }

            code = digitsOnly;
        }
    }
}
