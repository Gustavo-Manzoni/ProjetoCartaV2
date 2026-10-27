using System.Collections;
using UnityEngine;

namespace ProjetoCarta.Core
{
    /// <summary>
    /// Objeto da cena atual que roda corrotinas que nao podem parar quando o objeto de origem e
    /// desativado (ex.: o momento some na piscada). Morre junto com a cena.
    /// </summary>
    public sealed class CoroutineHost : MonoBehaviour
    {
        private static CoroutineHost instance;

        public static Coroutine Run(IEnumerator routine)
        {
            if (instance == null)
                instance = new GameObject("CoroutineHost").AddComponent<CoroutineHost>();

            return instance.StartCoroutine(routine);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
