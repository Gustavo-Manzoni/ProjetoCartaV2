using System.Collections;
using DG.Tweening;
using ProjetoCarta.NoirEnv;
using ProjetoCarta.NoirUI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Foto do caso Piscada Fatal no quadro do menu, o unico elemento clicavel do quadro. Ao clicar: usa o mesmo zoom de
/// camera das fotos de nivel, a lampada da foto pisca de forma irregular, queima, a tela escurece e a cena de
/// gameplay e carregada. Substitui o antigo PhotoBehaviour desta foto (que abria a mini-fase FirstRoom no menu).
/// A lampada pode ser uma luz real do diorama renderizado na foto (<see cref="SetBulb"/>) e/ou imagens de brilho.
/// </summary>
public sealed class CaseFileLauncher : MonoBehaviour
{
    [Header("Foto")]
    [SerializeField] private PhotoAnimation photoAnimation;
    [Tooltip("Imagem da foto (escurece quando a lampada queima).")]
    [SerializeField] private Graphic photoImage;
    [Tooltip("Brilho da lampada desenhado por cima da foto (opcional).")]
    [SerializeField] private Graphic bulbGlow;
    [Tooltip("Halo maior e suave em volta da lampada (opcional).")]
    [SerializeField] private Graphic bulbHalo;

    [Header("Lampada do diorama (opcional)")]
    [SerializeField] private Light[] bulbLights;
    [SerializeField] private Renderer bulbRenderer;
    [SerializeField, ColorUsage(false, true)] private Color bulbEmission = new(4f, 3f, 1.8f);

    [Header("Zoom (o mesmo das fotos de nivel)")]
    [SerializeField] private CinemachineCamera zoomCamera;
    [SerializeField] private AnimateCameraFov fovAnimator;

    [Header("Cena")]
    [SerializeField] private string gameplayScene = "1";
    [SerializeField, Min(0f)] private float flickerDelay = 0.9f;
    [SerializeField, Min(0.1f)] private float flickerDuration = 1.8f;

    private MainMenuManager mainMenuManager;
    private CameraFovManager cameraFovManager;
    private bool launching;
    private Color photoBaseColor = Color.white;
    private float glowBaseAlpha = 1f;
    private float haloBaseAlpha = 1f;
    private float[] lightBaseIntensity;
    private MaterialPropertyBlock block;

    public bool IsLaunching => launching;

    private void Start()
    {
        mainMenuManager = ServiceLocator.Get<MainMenuManager>();
        cameraFovManager = ServiceLocator.Get<CameraFovManager>();

        if (photoAnimation == null)
            photoAnimation = GetComponent<PhotoAnimation>();

        if (photoAnimation != null)
        {
            photoAnimation.onPointerEnter.AddListener(OnPointerEnter);
            photoAnimation.onPointerExit.AddListener(OnPointerExit);
            photoAnimation.onPointerClick.AddListener(Launch);
        }

        if (photoImage != null)
            photoBaseColor = photoImage.color;
        if (bulbGlow != null)
            glowBaseAlpha = bulbGlow.color.a;
        if (bulbHalo != null)
            haloBaseAlpha = bulbHalo.color.a;
        CaptureLightBase();
    }

    private void OnDestroy()
    {
        if (photoAnimation == null)
            return;

        photoAnimation.onPointerEnter.RemoveListener(OnPointerEnter);
        photoAnimation.onPointerExit.RemoveListener(OnPointerExit);
        photoAnimation.onPointerClick.RemoveListener(Launch);
    }

    /// <summary>Lampada real do diorama da foto (criado em tempo de execucao).</summary>
    public void SetBulb(Light[] lights, Renderer bulb, Color emission)
    {
        bulbLights = lights;
        bulbRenderer = bulb;
        bulbEmission = emission;
        CaptureLightBase();
    }

    private void CaptureLightBase()
    {
        if (bulbLights == null)
            return;

        lightBaseIntensity = new float[bulbLights.Length];
        for (int i = 0; i < bulbLights.Length; i++)
            lightBaseIntensity[i] = bulbLights[i] != null ? bulbLights[i].intensity : 0f;
    }

    private void Update()
    {
        if (launching || bulbGlow == null)
            return;

        // O brilho desenhado respira de leve enquanto espera o clique.
        float breathe = 0.85f + Mathf.PerlinNoise(Time.unscaledTime * 1.7f, 0.3f) * 0.3f;
        SetGlowGraphics(breathe);
    }

    private void OnPointerEnter()
    {
        if (!launching && CanInteract())
            cameraFovManager?.ChangeZoom(CameraZoomType.Photo);
    }

    private void OnPointerExit()
    {
        if (!launching)
            cameraFovManager?.ChangeZoom(CameraZoomType.Default);
    }

    /// <summary>Abre o caso: clique na foto ou botao do quadro.</summary>
    public void Launch()
    {
        if (launching || !CanInteract())
            return;

        launching = true;
        cameraFovManager?.ChangeZoom(CameraZoomType.Default);
        if (mainMenuManager != null)
        {
            mainMenuManager.HideBackButton();
            mainMenuManager.SetLastCamera(zoomCamera);
            mainMenuManager.ChangeCurrentState(mainMenuManager.LaunchState);
        }

        // Mesmo zoom de entrada das fotos de nivel.
        if (zoomCamera != null)
            zoomCamera.Prioritize();
        if (fovAnimator != null)
            fovAnimator.AnimateFov();

        // A lampada passa a ser controlada daqui (o tremeluzir ambiente para).
        foreach (Light l in bulbLights ?? new Light[0])
        {
            if (l == null) continue;
            NoirLightFlicker flicker = l.GetComponent<NoirLightFlicker>();
            if (flicker != null) flicker.enabled = false;
        }

        NoirAudio.Play(NoirSfx.Click, 0.8f);
        StartCoroutine(LaunchRoutine());
    }

    private bool CanInteract()
    {
        return mainMenuManager == null || mainMenuManager.CurrentState == null ||
               mainMenuManager.CurrentState.CanInteractWithPhotos();
    }

    private IEnumerator LaunchRoutine()
    {
        SetGlow(1f);
        yield return new WaitForSecondsRealtime(flickerDelay);

        // Falhas irregulares, cada vez mais longas e frequentes.
        float elapsed = 0f;
        while (elapsed < flickerDuration)
        {
            float t = elapsed / flickerDuration;
            bool off = Random.value < Mathf.Lerp(0.15f, 0.65f, t);
            float level = off ? Random.Range(0f, 0.15f) : Random.Range(0.7f, 1.2f);
            SetGlow(level);
            SetPhotoLight(off ? 0.55f : 1f);
            if (off && Random.value < 0.5f)
                NoirAudio.Play(NoirSfx.LampFlicker, 0.6f, Random.Range(0.85f, 1.2f));

            float step = Random.Range(0.04f, Mathf.Lerp(0.16f, 0.07f, t));
            elapsed += step;
            yield return new WaitForSecondsRealtime(step);
        }

        // Queima: clarao curto e apaga de vez.
        NoirAudio.Play(NoirSfx.LampBurst, 1f);
        SetGlow(1.8f);
        SetPhotoLight(1.15f);
        yield return new WaitForSecondsRealtime(0.07f);
        SetGlow(0f);
        SetPhotoLight(0.35f);
        NoirAmbience.FadeAll(0f, 1.4f);
        yield return new WaitForSecondsRealtime(0.6f);

        bool loaded = false;
        NoirAtmosphere.Get().FadeOut(1.1f, () =>
        {
            loaded = true;
            DOTween.KillAll();
            SceneManager.LoadScene(gameplayScene);
        });

        // Seguranca: se o fade nao concluir, carrega mesmo assim (nunca fica preso na tela preta).
        yield return new WaitForSecondsRealtime(2.5f);
        if (!loaded)
            SceneManager.LoadScene(gameplayScene);
    }

    private void SetGlow(float level)
    {
        SetGlowGraphics(level);

        if (bulbLights != null && lightBaseIntensity != null)
        {
            for (int i = 0; i < bulbLights.Length && i < lightBaseIntensity.Length; i++)
            {
                if (bulbLights[i] != null)
                    bulbLights[i].intensity = lightBaseIntensity[i] * level;
            }
        }

        if (bulbRenderer != null)
        {
            block ??= new MaterialPropertyBlock();
            bulbRenderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", bulbEmission * Mathf.Max(0.01f, level));
            bulbRenderer.SetPropertyBlock(block);
        }
    }

    private void SetGlowGraphics(float level)
    {
        if (bulbGlow != null)
        {
            Color c = bulbGlow.color;
            c.a = Mathf.Clamp01(glowBaseAlpha * level);
            bulbGlow.color = c;
        }

        if (bulbHalo != null)
        {
            Color c = bulbHalo.color;
            c.a = Mathf.Clamp01(haloBaseAlpha * level);
            bulbHalo.color = c;
        }
    }

    private void SetPhotoLight(float level)
    {
        if (photoImage == null)
            return;

        Color c = photoBaseColor * level;
        c.a = photoBaseColor.a;
        photoImage.color = c;
    }
}
