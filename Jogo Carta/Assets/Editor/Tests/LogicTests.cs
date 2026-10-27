using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.Puzzles;
using UnityEditor;
using UnityEngine;

namespace ProjetoCarta.Tests
{
    /// <summary>Testes EditMode da logica pura (sem cena). Rodar em Window > General > Test Runner > EditMode.</summary>
    public sealed class GameSettingsTests
    {
        [Test]
        public void VolumeZeroViraMenos80DbSemInfinito()
        {
            float db = GameSettings.LinearToDecibels(0f);
            Assert.IsFalse(float.IsInfinity(db));
            Assert.AreEqual(-80f, db, 0.001f);
        }

        [Test]
        public void VolumeCheioViraZeroDb()
        {
            Assert.AreEqual(0f, GameSettings.LinearToDecibels(1f), 0.001f);
        }

        [Test]
        public void VolumeForaDaFaixaEhLimitado()
        {
            Assert.AreEqual(0f, GameSettings.LinearToDecibels(3f), 0.001f);
            Assert.AreEqual(-80f, GameSettings.LinearToDecibels(-1f), 0.001f);
        }

        [Test]
        public void MetadeDoVolumeEhCercaDeMenos6Db()
        {
            Assert.AreEqual(-6.02f, GameSettings.LinearToDecibels(0.5f), 0.01f);
        }
    }

    public sealed class GameplayLocksTests
    {
        private sealed class Owner
        {
        }

        [SetUp]
        public void SetUp()
        {
            GameplayLocks.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GameplayLocks.Clear();
        }

        [Test]
        public void SemTravasNadaFicaTravado()
        {
            Assert.AreEqual(GameplayLock.None, GameplayLocks.Current);
            Assert.IsFalse(GameplayLocks.MovementLocked);
            Assert.IsFalse(GameplayLocks.BlinkLocked);
        }

        [Test]
        public void EstadoEfetivoEhAUniaoDasTravas()
        {
            Owner a = new();
            Owner b = new();
            GameplayLocks.Acquire(a, GameplayLock.Movement);
            GameplayLocks.Acquire(b, GameplayLock.Blink);

            Assert.IsTrue(GameplayLocks.MovementLocked);
            Assert.IsTrue(GameplayLocks.BlinkLocked);
            Assert.IsFalse(GameplayLocks.InteractionLocked);

            GameplayLocks.Release(a);
            Assert.IsFalse(GameplayLocks.MovementLocked);
            Assert.IsTrue(GameplayLocks.BlinkLocked);

            GameplayLocks.Release(b);
            Assert.AreEqual(GameplayLock.None, GameplayLocks.Current);
        }

        [Test]
        public void ReadquirirSubstituiAsTravasDoMesmoDono()
        {
            Owner owner = new();
            GameplayLocks.Acquire(owner, GameplayLock.Modal);
            GameplayLocks.Acquire(owner, GameplayLock.Blink);

            Assert.IsTrue(GameplayLocks.BlinkLocked);
            Assert.IsFalse(GameplayLocks.MovementLocked);
            Assert.IsTrue(GameplayLocks.Holds(owner));
        }

        [Test]
        public void LiberarDonoDesconhecidoNaoMudaNada()
        {
            GameplayLocks.Acquire(new Owner(), GameplayLock.Pause);
            GameplayLocks.Release(new Owner());
            GameplayLocks.Release(null);

            Assert.IsTrue(GameplayLocks.PauseLocked);
        }

        [Test]
        public void ModalLiberaOCursorECutsceneNao()
        {
            Assert.IsTrue((GameplayLock.Modal & GameplayLock.Cursor) != 0);
            Assert.IsTrue((GameplayLock.Cutscene & GameplayLock.Cursor) == 0);
            Assert.IsTrue((GameplayLock.Cutscene & GameplayLock.Blink) != 0);
        }

        [Test]
        public void DonoDestruidoNaoSeguraTrava()
        {
            GameObject owner = new("DonoTemporario");
            GameplayLocks.Acquire(owner, GameplayLock.Movement);
            Object.DestroyImmediate(owner);

            // A limpeza acontece no proximo recalculo.
            Owner other = new();
            GameplayLocks.Acquire(other, GameplayLock.Blink);

            Assert.IsFalse(GameplayLocks.MovementLocked);
            Assert.IsTrue(GameplayLocks.BlinkLocked);
        }

        [Test]
        public void EscConsumidoValeSoNoFrameAtual()
        {
            GameplayLocks.ConsumeEscape();
            Assert.IsTrue(GameplayLocks.EscapeConsumedThisFrame);
        }
    }

    public sealed class TomasThoughtsTests
    {
        private readonly List<string> heard = new();

        [SetUp]
        public void SetUp()
        {
            heard.Clear();
            TomasThoughts.Raised += OnRaised;
        }

        [TearDown]
        public void TearDown()
        {
            TomasThoughts.Raised -= OnRaised;
        }

        private void OnRaised(string thought, InteractionOutcome outcome)
        {
            heard.Add(thought);
        }

        [Test]
        public void PensamentoChegaSemEspacosNasPontas()
        {
            TomasThoughts.Say("  Está trancado.  ");
            CollectionAssert.AreEqual(new[] { "Está trancado." }, heard);
        }

        [Test]
        public void PensamentoVazioNaoAparece()
        {
            TomasThoughts.Say(string.Empty);
            TomasThoughts.Say("   ");
            TomasThoughts.Say((string)null);
            TomasThoughts.Say(InteractionFeedback.None);

            Assert.IsEmpty(heard);
        }
    }

    public sealed class KeypadTests
    {
        private static bool Validate(KeypadPanel keypad, params int[] attempt)
        {
            MethodInfo validate = keypad.GetType().GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(validate, "Validate nao encontrado em " + keypad.GetType().Name);
            return (bool)validate.Invoke(keypad, new object[] { new List<int>(attempt) });
        }

        [Test]
        public void KeypadNumericoAceitaSo2358()
        {
            GameObject go = new("KeypadNumericoTeste");
            try
            {
                NumericKeypad keypad = go.AddComponent<NumericKeypad>();
                SerializedObject serialized = new(keypad);
                serialized.FindProperty("code").stringValue = "2358";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(Validate(keypad, 2, 3, 5, 8));
                Assert.IsFalse(Validate(keypad, 2, 3, 5));
                Assert.IsFalse(Validate(keypad, 8, 5, 3, 2));
                Assert.IsFalse(Validate(keypad, 2, 3, 5, 8, 0));
                Assert.IsFalse(Validate(keypad));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CodigoDeSimbolosExigeOrdemETamanhoExatos()
        {
            SymbolCodeDefinition code = ScriptableObject.CreateInstance<SymbolCodeDefinition>();
            try
            {
                SerializedObject serialized = new(code);
                SerializedProperty symbols = serialized.FindProperty("symbols");
                symbols.arraySize = 5;
                SetInts(serialized.FindProperty("solution"), 0, 1, 2, 3, 4);
                SetInts(serialized.FindProperty("keypadLayout"), 3, 0, 4, 2, 1);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(code.Matches(new List<int> { 0, 1, 2, 3, 4 }));
                Assert.IsFalse(code.Matches(new List<int> { 1, 0, 2, 3, 4 }));
                Assert.IsFalse(code.Matches(new List<int> { 0, 1, 2, 3 }));
                Assert.IsFalse(code.Matches(new List<int> { 0, 1, 2, 3, 4, 4 }));
                Assert.IsFalse(code.Matches(null));
            }
            finally
            {
                Object.DestroyImmediate(code);
            }
        }

        private static void SetInts(SerializedProperty array, params int[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).intValue = values[i];
        }
    }
}
