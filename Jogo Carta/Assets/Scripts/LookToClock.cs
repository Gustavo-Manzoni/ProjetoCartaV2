using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LookToClock : MonoBehaviour
{
    private static readonly int IsLookingToClock = Animator.StringToHash("isLookingToClock");

    [SerializeField] private Animator animator;
    [SerializeField] private KeyCode keyCode = KeyCode.E;

    [Header("Background Blur")]
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0f)] private float blurTransitionSpeed = 4f;
    [SerializeField, Min(0f)] private float blurStartDistance = 2f;
    [SerializeField, Min(0f)] private float blurEndDistance = 8f;
    [SerializeField, Range(0.5f, 1.5f)] private float maximumBlurRadius = 1f;

    private Volume blurVolume;
    private VolumeProfile runtimeProfile;

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        SetupBackgroundBlur();
    }

    private void Update()
    {
        bool isLooking = Input.GetKey(keyCode);

        if (animator != null)
            animator.SetBool(IsLookingToClock, isLooking);

        if (blurVolume != null)
        {
            float targetWeight = isLooking ? 1f : 0f;
            blurVolume.weight = Mathf.MoveTowards(
                blurVolume.weight,
                targetWeight,
                blurTransitionSpeed * Time.unscaledDeltaTime);
        }
    }

    private void OnDisable()
    {
        if (animator != null)
            animator.SetBool(IsLookingToClock, false);

        if (blurVolume != null)
            blurVolume.weight = 0f;
    }

    private void OnDestroy()
    {
        if (runtimeProfile != null)
            Destroy(runtimeProfile);
    }

    private void SetupBackgroundBlur()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null &&
            targetCamera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
        {
            cameraData.renderPostProcessing = true;
        }

        blurVolume = gameObject.AddComponent<Volume>();
        blurVolume.isGlobal = true;
        blurVolume.priority = 100f;
        blurVolume.weight = 0f;

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "Clock Background Blur (Runtime)";
        blurVolume.sharedProfile = runtimeProfile;

        DepthOfField depthOfField = runtimeProfile.Add<DepthOfField>(true);
        depthOfField.mode.value = DepthOfFieldMode.Gaussian;
        depthOfField.gaussianStart.value = blurStartDistance;
        depthOfField.gaussianEnd.value = Mathf.Max(blurEndDistance, blurStartDistance + 0.01f);
        depthOfField.gaussianMaxRadius.value = maximumBlurRadius;
        depthOfField.highQualitySampling.value = true;
    }
}
