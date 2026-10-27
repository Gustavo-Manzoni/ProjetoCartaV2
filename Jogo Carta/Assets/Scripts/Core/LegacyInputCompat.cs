using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ProjetoCarta.Core
{
    /// <summary>
    /// Ponte entre o Input legado e o Input System, para migrar scripts antigos sem mudar a sensacao do jogo.
    /// Traduz KeyCode (ainda serializado nos componentes antigos) para o botao equivalente e reproduz as
    /// formulas do InputManager (ProjectSettings/InputManager.asset). Sem teclado, mouse ou controle, tudo
    /// le como solto ou zero, sem excecao. Leia sempre no Update (o delta do mouse e acumulado por frame).
    /// </summary>
    public static class LegacyInputCompat
    {
        /// <summary>"Mouse X"/"Mouse Y": sensitivity 0.1. GetAxis = delta do frame em pixels * 0.1 (sem deltaTime).</summary>
        public const float MouseAxisSensitivity = 0.1f;

        /// <summary>"Horizontal"/"Vertical" de teclado: sensitivity 3 u/s (0 a 1 em 0,333 s).</summary>
        public const float KeyAxisSensitivity = 3f;

        /// <summary>"Horizontal"/"Vertical" de teclado: gravity 3 u/s (1 a 0 em 0,333 s ao soltar).</summary>
        public const float KeyAxisGravity = 3f;

        /// <summary>"Horizontal"/"Vertical" de teclado: dead 0.001.</summary>
        public const float KeyAxisDeadZone = 0.001f;

        /// <summary>"Horizontal"/"Vertical" de joystick: dead 0.19 por eixo, sensitivity 1.</summary>
        public const float JoystickAxisDeadZone = 0.19f;

        /// <summary>Equivalente ao GetKey(code) do Input legado.</summary>
        public static bool IsHeld(KeyCode code)
        {
            ButtonControl button = Resolve(code);
            return button != null && button.isPressed;
        }

        /// <summary>Equivalente ao GetKeyDown(code) do Input legado.</summary>
        public static bool WasPressedThisFrame(KeyCode code)
        {
            ButtonControl button = Resolve(code);
            return button != null && button.wasPressedThisFrame;
        }

        /// <summary>Equivalente ao GetKeyUp(code) do Input legado.</summary>
        public static bool WasReleasedThisFrame(KeyCode code)
        {
            ButtonControl button = Resolve(code);
            return button != null && button.wasReleasedThisFrame;
        }

        /// <summary>Delta do mouse neste frame na escala do GetAxis("Mouse X"/"Mouse Y") legado. Nao multiplique por deltaTime.</summary>
        public static Vector2 MouseAxes()
        {
            Mouse mouse = Mouse.current;
            return mouse == null ? Vector2.zero : mouse.delta.ReadValue() * MouseAxisSensitivity;
        }

        /// <summary>GetAxisRaw("Horizontal"/"Vertical") de teclado: setas ou WASD, -1/0/1, os dois lados = 0.</summary>
        public static Vector2 KeyboardMoveRaw()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            return new Vector2(
                Digital(keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed,
                        keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed),
                Digital(keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed,
                        keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed));
        }

        /// <summary>
        /// Eixo de joystick "Horizontal"/"Vertical": analogico esquerdo cru, zona morta 0.19 por eixo, cima positivo.
        /// Como o legado (joyNum 0 = todos os joysticks), le o Gamepad e tambem o Joystick generico (HID sem layout
        /// de Gamepad) e fica, por eixo, com o de maior modulo.
        /// </summary>
        public static Vector2 GamepadMoveRaw()
        {
            Vector2 result = Vector2.zero;

            // Cru: o processador padrao do Input System (stickDeadzone 0.125..0.925, radial e reescalado) nao existe no legado.
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadUnprocessedValue();
                result = new Vector2(DeadZone(stick.x, JoystickAxisDeadZone), DeadZone(stick.y, JoystickAxisDeadZone));
            }

            Joystick joystick = Joystick.current;
            if (joystick != null && joystick.stick != null)
            {
                Vector2 stick = joystick.stick.ReadUnprocessedValue();
                result = new Vector2(
                    MaxAbs(result.x, DeadZone(stick.x, JoystickAxisDeadZone)),
                    MaxAbs(result.y, DeadZone(stick.y, JoystickAxisDeadZone)));
            }

            return result;
        }

        /// <summary>Eixo digital do legado: positivo = 1, negativo = -1, os dois ou nenhum = 0.</summary>
        public static float Digital(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        /// <summary>Varios eixos com o mesmo nome no InputManager: o legado devolve o de maior modulo (empate fica com 'a').</summary>
        public static float MaxAbs(float a, float b)
        {
            return Mathf.Abs(b) > Mathf.Abs(a) ? b : a;
        }

        /// <summary>Zona morta do legado, por eixo: abaixo de 'dead' em modulo vira zero; acima, o valor passa sem reescalar.</summary>
        public static float DeadZone(float value, float dead)
        {
            return Mathf.Abs(value) < dead ? 0f : value;
        }

        /// <summary>
        /// Um frame do GetAxis legado de teclas: anda ate o alvo a 'sensitivity' u/s, volta a zero a
        /// 'gravity' u/s e, com snap, zera ao inverter a direcao. Guarde o valor retornado para o proximo frame
        /// e aplique DeadZone(valor, KeyAxisDeadZone) na leitura.
        /// </summary>
        public static float StepKeyAxis(float value, float target, float deltaTime,
            float sensitivity = KeyAxisSensitivity, float gravity = KeyAxisGravity, bool snap = true)
        {
            if (snap && target != 0f && value != 0f && Mathf.Sign(target) != Mathf.Sign(value))
                value = 0f;

            float speed = target != 0f ? sensitivity : gravity;
            return Mathf.MoveTowards(value, target, speed * deltaTime);
        }

        /// <summary>
        /// Botao do Input System equivalente ao KeyCode (teclado ou mouse); null se nao houver dispositivo ou par.
        /// Recalculado a cada chamada, para nao guardar referencia a um dispositivo desconectado.
        /// </summary>
        public static ButtonControl Resolve(KeyCode code)
        {
            Mouse mouse = Mouse.current;
            switch (code)
            {
                case KeyCode.None: return null;
                case KeyCode.Mouse0: return mouse?.leftButton;
                case KeyCode.Mouse1: return mouse?.rightButton;
                case KeyCode.Mouse2: return mouse?.middleButton;
                case KeyCode.Mouse3: return mouse?.backButton;
                case KeyCode.Mouse4: return mouse?.forwardButton;
            }

            Keyboard keyboard = Keyboard.current;
            Key key = ToKey(code);
            // O indexador do Keyboard lanca excecao com Key.None.
            if (keyboard == null || key == Key.None)
                return null;
            return keyboard[key];
        }

        /// <summary>
        /// KeyCode de teclado -> Key (posicao fisica no layout US). Botoes de mouse e joystick, Command/Windows,
        /// F13 em diante e teclas sem par devolvem Key.None.
        /// </summary>
        public static Key ToKey(KeyCode code)
        {
            // Faixas contiguas nos dois enums.
            if (code >= KeyCode.A && code <= KeyCode.Z)
                return (Key)((int)Key.A + ((int)code - (int)KeyCode.A));
            if (code >= KeyCode.Alpha1 && code <= KeyCode.Alpha9)
                return (Key)((int)Key.Digit1 + ((int)code - (int)KeyCode.Alpha1));
            if (code >= KeyCode.Keypad0 && code <= KeyCode.Keypad9)
                return (Key)((int)Key.Numpad0 + ((int)code - (int)KeyCode.Keypad0));
            if (code >= KeyCode.F1 && code <= KeyCode.F12)
                return (Key)((int)Key.F1 + ((int)code - (int)KeyCode.F1));

            switch (code)
            {
                case KeyCode.Alpha0: return Key.Digit0; // no Input System o Digit0 vem depois do Digit9
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.Insert: return Key.Insert;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.PageDown: return Key.PageDown;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.CapsLock: return Key.CapsLock;
                case KeyCode.Numlock: return Key.NumLock;
                case KeyCode.ScrollLock: return Key.ScrollLock;
                case KeyCode.Print: return Key.PrintScreen;
                case KeyCode.Pause: return Key.Pause;
                case KeyCode.Menu: return Key.ContextMenu;
                case KeyCode.BackQuote: return Key.Backquote;
                case KeyCode.Quote: return Key.Quote;
                case KeyCode.Semicolon: return Key.Semicolon;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Slash: return Key.Slash;
                case KeyCode.Backslash: return Key.Backslash;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;
                case KeyCode.KeypadDivide: return Key.NumpadDivide;
                case KeyCode.KeypadMultiply: return Key.NumpadMultiply;
                case KeyCode.KeypadMinus: return Key.NumpadMinus;
                case KeyCode.KeypadPlus: return Key.NumpadPlus;
                case KeyCode.KeypadPeriod: return Key.NumpadPeriod;
                case KeyCode.KeypadEquals: return Key.NumpadEquals;
                default: return Key.None;
            }
        }
    }
}
