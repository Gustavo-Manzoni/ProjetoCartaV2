using NUnit.Framework;
using ProjetoCarta.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjetoCarta.Tests
{
    /// <summary>
    /// Testes EditMode da ponte LegacyInputCompat (logica pura, sem dispositivo): KeyCode -> Key e as formulas de eixo
    /// do Input legado. A sensacao do jogo depende destes numeros; os valores vem de ProjectSettings/InputManager.asset.
    /// </summary>
    public sealed class InputCompatTests
    {
        private const float Tolerance = 0.0001f;

        // ------------------------------------------------------------ KeyCode -> Key

        [TestCase(KeyCode.LeftShift, Key.LeftShift)]
        [TestCase(KeyCode.Space, Key.Space)]
        [TestCase(KeyCode.LeftControl, Key.LeftCtrl)]
        [TestCase(KeyCode.Alpha0, Key.Digit0)]
        [TestCase(KeyCode.Alpha1, Key.Digit1)]
        [TestCase(KeyCode.A, Key.A)]
        [TestCase(KeyCode.Z, Key.Z)]
        [TestCase(KeyCode.F12, Key.F12)]
        [TestCase(KeyCode.Keypad9, Key.Numpad9)]
        [TestCase(KeyCode.Mouse1, Key.None)]
        [TestCase(KeyCode.JoystickButton0, Key.None)]
        [TestCase(KeyCode.None, Key.None)]
        public void ToKeyConverteKeyCode(KeyCode code, Key expected)
        {
            Assert.AreEqual(expected, LegacyInputCompat.ToKey(code));
        }

        [Test]
        public void FaixasContiguasBatemPeloNome()
        {
            for (KeyCode code = KeyCode.A; code <= KeyCode.Z; code++)
                Assert.AreEqual(KeyNamed(code.ToString()), LegacyInputCompat.ToKey(code), code.ToString());

            for (int i = 0; i <= 9; i++)
            {
                Assert.AreEqual(KeyNamed("Digit" + i), LegacyInputCompat.ToKey(KeyCode.Alpha0 + i), "Alpha" + i);
                Assert.AreEqual(KeyNamed("Numpad" + i), LegacyInputCompat.ToKey(KeyCode.Keypad0 + i), "Keypad" + i);
            }

            for (int i = 1; i <= 12; i++)
                Assert.AreEqual(KeyNamed("F" + i), LegacyInputCompat.ToKey(KeyCode.F1 + (i - 1)), "F" + i);
        }

        [Test]
        public void BotoesDeMouseEJoystickNaoViramTecla()
        {
            for (KeyCode code = KeyCode.Mouse0; code <= KeyCode.Mouse6; code++)
                Assert.AreEqual(Key.None, LegacyInputCompat.ToKey(code), code.ToString());

            for (KeyCode code = KeyCode.JoystickButton0; code <= KeyCode.Joystick8Button19; code++)
                Assert.AreEqual(Key.None, LegacyInputCompat.ToKey(code), code.ToString());
        }

        [Test]
        public void TeclaSemParNaoResolveNemFicaApertada()
        {
            // Independe de haver teclado ou mouse conectados: nao ha botao equivalente.
            Assert.IsNull(LegacyInputCompat.Resolve(KeyCode.None));
            Assert.IsNull(LegacyInputCompat.Resolve(KeyCode.JoystickButton0));
            Assert.IsFalse(LegacyInputCompat.IsHeld(KeyCode.None));
            Assert.IsFalse(LegacyInputCompat.WasPressedThisFrame(KeyCode.JoystickButton0));
            Assert.IsFalse(LegacyInputCompat.WasReleasedThisFrame(KeyCode.None));
        }

        private static Key KeyNamed(string name)
        {
            return (Key)System.Enum.Parse(typeof(Key), name);
        }

        // ------------------------------------------------------------ StepKeyAxis (GetAxis de teclas)

        [Test]
        public void EixoDeTeclaSobe3PorSegundo()
        {
            Assert.AreEqual(0.3f, LegacyInputCompat.StepKeyAxis(0f, 1f, 0.1f), Tolerance);
        }

        [Test]
        public void EixoDeTeclaChegaEm1ENaoPassa()
        {
            float value = 0f;
            for (int i = 0; i < 3; i++)
                value = LegacyInputCompat.StepKeyAxis(value, 1f, 0.1f);
            Assert.AreEqual(0.9f, value, Tolerance);

            value = LegacyInputCompat.StepKeyAxis(value, 1f, 0.1f);
            Assert.AreEqual(1f, value, Tolerance);
        }

        [Test]
        public void SoltarAVoltaAZeroPelaGravidade()
        {
            Assert.AreEqual(0.7f, LegacyInputCompat.StepKeyAxis(1f, 0f, 0.1f), Tolerance);
            Assert.AreEqual(0f, LegacyInputCompat.StepKeyAxis(0.1f, 0f, 0.1f), Tolerance);
            Assert.AreEqual(0f, LegacyInputCompat.StepKeyAxis(0f, 0f, 0.1f), Tolerance);
        }

        [Test]
        public void InverterComSnapZeraAntesDeAndar()
        {
            Assert.AreEqual(-0.3f, LegacyInputCompat.StepKeyAxis(0.5f, -1f, 0.1f), Tolerance);
        }

        [Test]
        public void InverterSemSnapAndaAPartirDoValorAtual()
        {
            Assert.AreEqual(0.2f, LegacyInputCompat.StepKeyAxis(0.5f, -1f, 0.1f, snap: false), Tolerance);
        }

        [Test]
        public void RampaDuraUmTercoDeSegundoEmQualquerFps()
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float dt = 1f / fps;
                float value = 0f;
                int frames = Mathf.CeilToInt(fps / 3f) - 1;
                for (int i = 0; i < frames; i++)
                    value = LegacyInputCompat.StepKeyAxis(value, 1f, dt);
                Assert.Less(value, 1f, fps + " fps: chegou em 1 antes de 0,333 s");

                value = LegacyInputCompat.StepKeyAxis(value, 1f, dt);
                value = LegacyInputCompat.StepKeyAxis(value, 1f, dt);
                Assert.AreEqual(1f, value, Tolerance, fps + " fps: nao chegou em 1 em 0,333 s");
            }
        }

        // ------------------------------------------------------------ Digital, MaxAbs, DeadZone

        [Test]
        public void DigitalSomaOsDoisLados()
        {
            Assert.AreEqual(1f, LegacyInputCompat.Digital(true, false));
            Assert.AreEqual(-1f, LegacyInputCompat.Digital(false, true));
            Assert.AreEqual(0f, LegacyInputCompat.Digital(true, true));
            Assert.AreEqual(0f, LegacyInputCompat.Digital(false, false));
        }

        [Test]
        public void MaxAbsFicaComOMaiorModulo()
        {
            Assert.AreEqual(1f, LegacyInputCompat.MaxAbs(1f, -0.5f));
            Assert.AreEqual(-0.5f, LegacyInputCompat.MaxAbs(0.2f, -0.5f));
            Assert.AreEqual(0f, LegacyInputCompat.MaxAbs(0f, 0f));
        }

        [Test]
        public void ZonaMortaZeraSoAbaixoDoLimite()
        {
            Assert.AreEqual(0f, LegacyInputCompat.DeadZone(0.18f, 0.19f));
            Assert.AreEqual(0f, LegacyInputCompat.DeadZone(-0.18f, 0.19f));
            Assert.AreEqual(0.5f, LegacyInputCompat.DeadZone(0.5f, 0.19f));
            Assert.AreEqual(-0.5f, LegacyInputCompat.DeadZone(-0.5f, 0.19f));
            Assert.AreEqual(0f, LegacyInputCompat.DeadZone(0.0005f, LegacyInputCompat.KeyAxisDeadZone));
        }

        // ------------------------------------------------------------ constantes x InputManager.asset

        [Test]
        public void ConstantesBatemComOInputManager()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            Assert.IsNotEmpty(assets, "ProjectSettings/InputManager.asset nao carregou");

            SerializedProperty axes = new SerializedObject(assets[0]).FindProperty("m_Axes");
            Assert.IsNotNull(axes, "InputManager sem m_Axes");

            int mouseAxes = 0;
            int keyAxes = 0;
            int joystickAxes = 0;
            for (int i = 0; i < axes.arraySize; i++)
            {
                SerializedProperty axis = axes.GetArrayElementAtIndex(i);
                string name = axis.FindPropertyRelative("m_Name").stringValue;
                int type = axis.FindPropertyRelative("type").intValue; // 0 tecla/botao, 1 movimento do mouse, 2 eixo de joystick
                float sensitivity = axis.FindPropertyRelative("sensitivity").floatValue;
                float gravity = axis.FindPropertyRelative("gravity").floatValue;
                float dead = axis.FindPropertyRelative("dead").floatValue;
                string label = name + " (type " + type + ")";

                if ((name == "Mouse X" || name == "Mouse Y") && type == 1)
                {
                    Assert.AreEqual(LegacyInputCompat.MouseAxisSensitivity, sensitivity, Tolerance, label);
                    mouseAxes++;
                }
                else if ((name == "Horizontal" || name == "Vertical") && type == 0)
                {
                    Assert.AreEqual(LegacyInputCompat.KeyAxisSensitivity, sensitivity, Tolerance, label);
                    Assert.AreEqual(LegacyInputCompat.KeyAxisGravity, gravity, Tolerance, label);
                    Assert.AreEqual(LegacyInputCompat.KeyAxisDeadZone, dead, Tolerance, label);
                    Assert.IsTrue(axis.FindPropertyRelative("snap").boolValue, label + ": snap desligado");
                    keyAxes++;
                }
                else if ((name == "Horizontal" || name == "Vertical") && type == 2)
                {
                    Assert.AreEqual(LegacyInputCompat.JoystickAxisDeadZone, dead, Tolerance, label);
                    joystickAxes++;
                }
            }

            Assert.AreEqual(2, mouseAxes, "Eixos Mouse X/Y");
            Assert.AreEqual(2, keyAxes, "Eixos Horizontal/Vertical de teclado");
            Assert.AreEqual(2, joystickAxes, "Eixos Horizontal/Vertical de joystick");
        }
    }
}
