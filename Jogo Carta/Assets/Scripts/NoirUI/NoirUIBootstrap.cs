using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjetoCarta.NoirUI
{
    /// <summary>
    /// Liga a UI noir automaticamente em toda cena (nao precisa colocar nada na hierarquia):
    /// atmosfera, juice nos botoes existentes, titulo animado, menu de pausa e HUD de gameplay.
    /// </summary>
    public sealed class NoirUIBootstrap : MonoBehaviour
    {
        private static NoirUIBootstrap instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            // Troca de idioma ao vivo (botoes BR/EN do menu) nos textos de cena. -= antes de += para nunca acumular;
            // o Loc tambem zera o evento em SubsystemRegistration.
            ProjetoCarta.Core.Loc.Changed -= LocSceneTexts.Apply;
            ProjetoCarta.Core.Loc.Changed += LocSceneTexts.Apply;

            if (instance != null)
                return;

            GameObject go = new("NoirUIBootstrap");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NoirUIBootstrap>();
            SceneManager.sceneLoaded += instance.OnSceneLoaded;
            instance.StartCoroutine(instance.SetupScene(SceneManager.GetActiveScene()));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            // sceneLoaded vem antes do primeiro frame da cena: cobre de preto ja, porque o cenario e a UI montados em
            // runtime so ficam prontos depois de dois frames (sem isso a cena crua pisca ao voltar ao menu).
            NoirAtmosphere.Get().Cover();
            StartCoroutine(SetupScene(scene));
        }

        private IEnumerator SetupScene(Scene scene)
        {
            Time.timeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
            PauseState.IsPaused = false;

            NoirAtmosphere atmosphere = NoirAtmosphere.Get();
            NoirAudio.Get();

            // Aguarda os Awake/Start da cena (o menu cria os Buttons em tempo de execucao).
            yield return null;
            yield return null;

            // Traduz os textos que ja estao na cena antes de decorar titulo e botoes (o juice mede o rotulo final)
            // e antes de criar pausa e HUD, que se traduzem sozinhos com Loc.T.
            LocSceneTexts.Capture(scene);

            // Volumes e opcoes salvas valem em qualquer cena (mixer so aceita valores depois do primeiro frame).
            ProjetoCarta.Core.GameSettings.ApplyAll();

            bool gameplay = FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include) != null;
            atmosphere.Configure(gameplay);
            atmosphere.FadeIn(gameplay ? 1.4f : 1.0f, 0.1f);

            DecorateTitle();

            if (gameplay)
            {
                ProjetoCarta.NoirEnv.NoirEnvironment.Build();
                ProjetoCarta.NoirEnv.NoirEnvironment.FixPlayerSpawn();
                new GameObject("NoirPauseMenu").AddComponent<PauseMenu>();
                new GameObject("NoirGameplayHud").AddComponent<GameplayHud>();
                new GameObject("NoirAmbience").AddComponent<NoirAmbience>();
                new GameObject("NoirDread").AddComponent<NoirDread>();
            }
            else if (FindFirstObjectByType<MainMenuManager>() != null)
            {
                // Menu: escritorio do detetive, diorama da foto do caso e chuva na janela.
                ProjetoCarta.NoirEnv.NoirMenuOffice.Build();
                new GameObject("NoirAmbience").AddComponent<NoirAmbience>();
                new GameObject("NoirMenuOptions").AddComponent<MenuOptionsBoards>();
            }

            // Botoes criados depois (ex.: menus) tambem ganham juice.
            WaitForSecondsRealtime wait = new(0.5f);
            while (SceneManager.GetActiveScene() == scene)
            {
                DecorateButtons();
                if (!gameplay) NoirSkin.Apply();
                yield return wait;
            }
        }

        private static void DecorateButtons()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int introIndex = 0;
            System.Array.Sort(buttons, (a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            foreach (Button b in buttons)
            {
                if (b == null || b.GetComponent<NoirButtonJuice>() != null)
                    continue;
                // Botao-raiz criado pelo MainMenuManager: so envolve o botao real (prefab); nao decorar duas vezes.
                if (b.GetComponentsInChildren<Button>(true).Length > 1)
                    continue;
                if (b.GetComponentInParent<NoirOwned>() != null)
                    continue;
                if (b.gameObject.name.Contains("Titulo") || b.GetComponentInParent<Slider>() != null)
                    continue;

                NoirButtonJuice juice = b.gameObject.AddComponent<NoirButtonJuice>();
                string n = b.gameObject.name.ToLowerInvariant();
                juice.Danger = n.Contains("sair") || n.Contains("quit") || n.Contains("exit");
                if (juice.IsMenuStyle && Time.timeSinceLevelLoad < 4f)
                    juice.PlayMenuIntro(0.5f + 0.13f * introIndex++);
            }
        }

        private static void DecorateTitle()
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text t in texts)
            {
                if (t != null && t.gameObject.name == "Titulo" && t.GetComponent<NoirTitleJuice>() == null)
                    t.gameObject.AddComponent<NoirTitleJuice>();
            }
        }
    }

    public static class PauseState
    {
        public static bool IsPaused;
    }
}
