// Assembly-CSharp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// FirstPersonController
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using ProjetoCarta.Core;
public class FirstPersonController : MonoBehaviour
{
	[Serializable]
	public class GroundType
	{
		public string name;

		public PhysicsMaterial phyMat;

		public AudioClip[] footstepSounds;

		public bool changeWalkSpped;

		public bool changeHeadBobSpeed;

		public bool overrideVolume;

		[Range(0f, 1f)]
		public float volume;

		public float walkSpeed = 1.5f;

		public float sprintSpeed = 2f;

		public float sprintBobSpeed;

		public float sprintBobAmount;
	}

	private AudioSource m_AudioSource;

	[Header("Functional Options")]
	[SerializeField]
	private bool canSprint = true;

	[SerializeField]
	private bool canJump = true;

	[SerializeField]
	private bool canCrouch = true;

	[SerializeField]
	private bool canUseHeadBop = true;

	[SerializeField]
	private bool willSlideOnSlopes = true;

	[SerializeField]
	public bool canZoomIn = true;

	[SerializeField]
	private bool canCameraSway = true;

	[Header("Controls")]
	[SerializeField]
	private KeyCode sprintKey = KeyCode.LeftShift;

	[SerializeField]
	private KeyCode jumpKey = KeyCode.Space;

	[SerializeField]
	private KeyCode crouchKey = KeyCode.LeftControl;

	[SerializeField]
	private KeyCode zoomKey = KeyCode.Mouse2;

	[Header("Movement Parameters")]
	[SerializeField]
	public float walkSpeed = 3f;

	public float sprintSpeed = 6f;

	[SerializeField]
	private float crouchSpeed = 1.5f;

	[SerializeField]
	private float slopeSpeed = 8f;

	[Header("Look Parameters")]
	[SerializeField]
	[Range(1f, 10f)]
	public float lookSpeedX = 2f;

	[SerializeField]
	[Range(1f, 10f)]
	public float lookSpeedY = 2f;

	[SerializeField]
	[Range(1f, 180f)]
	public float upperLookLimit = 90f;

	[SerializeField]
	[Range(1f, 180f)]
	public float lowerLookLimit = 90f;

	[Header("Camera Leaning Parameters")]
	[SerializeField]
	private bool isInversed;

	[SerializeField]
	private float leanAmount;

	[SerializeField]
	private float leanSpeed;

	private float rotationZ;

	[Header("Jumping Parameters")]
	[SerializeField]
	private float jumpForce = 8f;

	[SerializeField]
	private float gravity = 30f;

	[Header("Crouch Parameters")]
	[SerializeField]
	public float crouchHeight = 0.5f;

	[SerializeField]
	private float standingHeight = 2f;

	[SerializeField]
	private float timeToCrouch = 0.25f;

	[SerializeField]
	public Vector3 crouchingCenter = new Vector3(0f, 0.5f, 0f);

	[SerializeField]
	private Vector3 standingCenter = new Vector3(0f, 0f, 0f);

	private bool isCrouching;

	private bool duringCrouchAnimation;

	[SerializeField]
	private float crouchStandCheckRadius = 1f;

	[SerializeField]
	private float crouchStandCheckHeight = 1f;

	[Header("Headbob Parameters")]
	[SerializeField]
	private float walkBobSpeed = 14f;

	[SerializeField]
	private float walkBobAmount = 0.05f;

	[SerializeField]
	public float sprintBobSpeed = 18f;

	[SerializeField]
	public float sprintBobAmount = 0.1f;

	[SerializeField]
	private float crouchBobSpeed = 8f;

	[SerializeField]
	private float crouchBobAmount = 0.025f;

	private float defaultYPos;

	private float timer;

	[SerializeField]
	public float iWalkspeed;

	[SerializeField]
	public float iSprintSpeed;

	[SerializeField]
	public float iVolume;

	private float m_StepCycle;

	private float m_NextStep;

	private Vector3 hitPointNormal;

	private AudioClip[] m_FootstepSounds;

	[Header("Footstep Settings")]
	[Range(0f, 1f)]
	public float volume;

	[SerializeField]
	private float m_StepInterval;

	[SerializeField]
	private string currentGround;

	public float iSprintBobSpeed;

	public float iSprintBobAmount;

	[Header("Dynamic Footsteps")]
	public List<GroundType> GroundTypes = new List<GroundType>();

	[Header("Passos sem GroundTypes (sons do NoirAudio)")]
	[Tooltip("Usado quando nenhum GroundType tem sons: madeira em P1, concreto em P2 e P3.")]
	[SerializeField, Range(0f, 1f)]
	private float fallbackFootstepVolume = 0.35f;

	[Tooltip("Intervalo entre passos quando m_StepInterval esta zerado (andando ~0,5 s, correndo ~0,25 s).")]
	[SerializeField, Min(0.1f)]
	private float fallbackStepInterval = 3f;

	private ProjetoCarta.Blink.BlinkSceneController blinkController;

	[Header("Zoom in Parameters")]
	[SerializeField]
	private float zoomFovAmount;

	[SerializeField]
	private float timeToZoom;

	private float startFov;

	private float currentFov;

	public CinemachineCamera playerCamera;

	private CharacterController characterController;

	private Vector3 moveDirection;

	private Vector2 currentInput;
	[SerializeField] bool lockMouseOnStart;

	// Eixos do Input legado reproduzidos com o Input System (ver LegacyInputCompat), lidos uma vez por frame no Update.
	private Vector2 moveAxisRaw;          // (GetAxisRaw("Horizontal"), GetAxisRaw("Vertical")): teclado ou analogico, o de maior modulo
	private float keyHorizontalValue;     // estado interno do GetAxis("Horizontal") de teclado (suavizado)
	private float horizontalAxis;         // GetAxis("Horizontal"): teclado suavizado ou analogico, o de maior modulo

	[HideInInspector]
	public float rotationX;

	private int moveAbs = 1;

	public bool skipEsc;

	public bool CanMove { get; set; } = true;

	private const float MaxFallSpeed = 30f;

	public void SetControlEnabled(bool enabled)
	{
		CanMove = enabled;

		if (!enabled)
		{
			currentInput = Vector2.zero;
			moveDirection.x = 0f;
			moveDirection.z = 0f;
		}
	}

	private bool IsSprinting
	{
		get
		{
			if (canSprint && LegacyInputCompat.IsHeld(sprintKey))
			{
				return moveAxisRaw.y > 0f;
			}
			return false;
		}
	}

	private bool ShouldJump
	{
		get
		{
			if (LegacyInputCompat.WasPressedThisFrame(jumpKey))
			{
				return characterController.isGrounded;
			}
			return false;
		}
	}

	private bool ShouldCrouch
	{
		get
		{
			if (LegacyInputCompat.WasPressedThisFrame(crouchKey) && !duringCrouchAnimation)
			{
				return characterController.isGrounded;
			}
			return false;
		}
	}

	private bool isSliding
	{
		get
		{
			// Ignora gatilhos (pontos de interacao): so o chao de verdade conta como rampa.
			if (characterController.isGrounded && Physics.Raycast(base.transform.position, Vector3.down, out var hitInfo, 2f, ~0, QueryTriggerInteraction.Ignore))
			{
				hitPointNormal = hitInfo.normal;
				return Vector3.Angle(hitPointNormal, Vector3.up) > characterController.slopeLimit;
			}
			return false;
		}
	}

	private void OnControllerColliderHit(ControllerColliderHit hit)
	{
		if (GroundTypes == null || GroundTypes.Count == 0)
			return;

		foreach (GroundType groundType in GroundTypes)
		{
			if (hit.collider.sharedMaterial == groundType.phyMat)
			{
				SetGroundType(groundType);
				return;
			}
		}

		SetGroundType(GroundTypes[0]);
	}

	private void SetGroundType(GroundType ground)
	{
		if (currentGround != ground.name)
		{
			m_FootstepSounds = ground.footstepSounds;
			if (ground.changeWalkSpped)
			{
				walkSpeed = ground.walkSpeed;
				sprintSpeed = ground.sprintSpeed;
			}
			else
			{
				walkSpeed = iWalkspeed;
				sprintSpeed = iSprintSpeed;
			}
			if (ground.overrideVolume)
			{
				volume = ground.volume;
			}
			else
			{
				volume = iVolume;
			}
			if (ground.changeHeadBobSpeed)
			{
				sprintBobSpeed = ground.sprintBobSpeed;
				sprintBobAmount = ground.sprintBobAmount;
			}
			else
			{
				sprintBobSpeed = iSprintBobSpeed;
				sprintBobAmount = iSprintBobAmount;
			}
			currentGround = ground.name;
		}
	}

	private void Awake()
	{
		ServiceLocator.Register(this);
		playerCamera = GetComponentInChildren<CinemachineCamera>();
		characterController = GetComponent<CharacterController>();
		defaultYPos = playerCamera.transform.localPosition.y;
		startFov = playerCamera.Lens.FieldOfView;
		currentFov = startFov;
		iWalkspeed = walkSpeed;
		iSprintSpeed = sprintSpeed;
		iVolume = volume;
		m_AudioSource = GetComponent<AudioSource>();
		iSprintBobAmount = sprintBobAmount;
		iSprintBobSpeed = sprintBobSpeed;
		sceneFov = startFov;
		ApplySettings(null);
		GameSettings.Changed += ApplySettings;
	}

	private void OnDestroy()
	{
		GameSettings.Changed -= ApplySettings;
	}

	// Opcoes da pausa (sensibilidade, eixo Y, balanco e campo de visao), lidas uma vez por mudanca.
	private float settingsSensitivity = 1f;
	private bool settingsInvertY;
	private bool settingsHeadBob = true;
	private float sceneFov;

	/// <summary>Campo de visao definido na cena (o padrao da opcao).</summary>
	public float SceneFov => sceneFov;

	/// <summary>Campo de visao em uso fora do zoom.</summary>
	public float BaseFov => startFov;

	private void ApplySettings(string key)
	{
		settingsSensitivity = GameSettings.MouseSensitivity;
		settingsInvertY = GameSettings.InvertY;
		settingsHeadBob = GameSettings.HeadBob;

		float fov = GameSettings.Fov;
		startFov = fov > 0f ? fov : sceneFov;
		if (!LegacyInputCompat.IsHeld(zoomKey))
		{
			currentFov = startFov;
			playerCamera.Lens.FieldOfView = startFov;
		}

		if (!settingsHeadBob)
		{
			Vector3 p = playerCamera.transform.localPosition;
			playerCamera.transform.localPosition = new Vector3(p.x, defaultYPos, p.z);
			rotationZ = 0f;
		}
	}

	private void Start()
	{
		// O cursor passa a ser controlado pelas travas de gameplay (keypad, pausa, inventario...).
		if (lockMouseOnStart)
			ProjetoCarta.Core.GameplayLocks.EnableCursorManagement();
	}

	public void ShowCursor(){Cursor.lockState = CursorLockMode.None; Cursor.visible = true;}
	public void HideCursor(){Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;}
	private void Update()
	{
		// Le os eixos antes da trava: o GetAxis legado evoluia todo frame, mesmo com o jogador travado.
		ReadLegacyAxes();
		if (CanMove && !ProjetoCarta.Core.GameplayLocks.MovementLocked)
		{
			HandleMovementInput();
			HandleMouseLook();
			if (canJump)
			{
				HandleJump();
			}
			if (canCrouch)
			{
				HandleCrouch();
			}
			if (canUseHeadBop && settingsHeadBob)
			{
				HandleHeadbob();
			}
			if (canZoomIn)
			{
				HandleZoomIn();
			}
			if (canCameraSway && settingsHeadBob)
			{
				HandleSway();
			}
			ApplyFinalMovement();
		}
	}

	/// <summary>Reproduz Horizontal/Vertical do InputManager: teclas (setas/WASD) e analogico esquerdo, o de maior modulo.</summary>
	private void ReadLegacyAxes()
	{
		Vector2 keys = LegacyInputCompat.KeyboardMoveRaw();
		Vector2 stick = LegacyInputCompat.GamepadMoveRaw();

		moveAxisRaw = new Vector2(LegacyInputCompat.MaxAbs(keys.x, stick.x), LegacyInputCompat.MaxAbs(keys.y, stick.y));

		keyHorizontalValue = LegacyInputCompat.StepKeyAxis(keyHorizontalValue, keys.x, Time.deltaTime);
		horizontalAxis = LegacyInputCompat.MaxAbs(
			LegacyInputCompat.DeadZone(keyHorizontalValue, LegacyInputCompat.KeyAxisDeadZone), stick.x);
	}

	private void FixedUpdate()
	{
		if (CanMove && !ProjetoCarta.Core.GameplayLocks.MovementLocked)
		{
			ProgressStepCycle();
		}
		
	}

	private void ProgressStepCycle()
	{
		if (characterController.velocity.sqrMagnitude > 0f && (currentInput.x != 0f || currentInput.y != 0f))
		{
			m_StepCycle += (characterController.velocity.magnitude + currentInput.magnitude) * Time.fixedDeltaTime;
		}
		if (m_StepCycle > m_NextStep)
		{
			m_NextStep = m_StepCycle + (m_StepInterval > 0f ? m_StepInterval : fallbackStepInterval);
			PlayFootStepAudio();
		}
	}

	private void PlayFootStepAudio()
	{
		if ((m_FootstepSounds == null || m_FootstepSounds.Length == 0) && characterController.isGrounded && characterController.velocity.magnitude > 0.5f)
		{
			PlayFallbackFootstep();
			return;
		}
		if (characterController.isGrounded && m_FootstepSounds != null && m_FootstepSounds.Length != 0 && characterController.velocity.magnitude > 0.5f)
		{
			if (m_AudioSource == null)
			{
				return;
			}
			if (m_FootstepSounds.Length == 1)
			{
				m_AudioSource.PlayOneShot(m_FootstepSounds[0], volume);
				return;
			}
			int num = UnityEngine.Random.Range(1, m_FootstepSounds.Length);
			m_AudioSource.clip = m_FootstepSounds[num];
			m_AudioSource.PlayOneShot(m_AudioSource.clip, volume);
			m_FootstepSounds[num] = m_FootstepSounds[0];
			m_FootstepSounds[0] = m_AudioSource.clip;
		}
	}

	// Piso de madeira no escritorio (P1) e concreto na camara e na sala administrativa (P2 e P3).
	private void PlayFallbackFootstep()
	{
		if (blinkController == null)
			blinkController = FindFirstObjectByType<ProjetoCarta.Blink.BlinkSceneController>(FindObjectsInactive.Include);

		int moment = blinkController != null ? blinkController.CurrentMoment : 0;
		ProjetoCarta.NoirUI.NoirSfx step = moment == 0 ? ProjetoCarta.NoirUI.NoirSfx.FootWood : ProjetoCarta.NoirUI.NoirSfx.FootConcrete;
		float crouchFactor = isCrouching ? 0.5f : 1f;
		ProjetoCarta.NoirUI.NoirAudio.Play(step, fallbackFootstepVolume * crouchFactor, UnityEngine.Random.Range(0.92f, 1.06f));
	}

	private void HandleMovementInput()
	{
		// Sem normalizar: a diagonal continua mais rapida, como no Input legado.
		currentInput = new Vector2((isCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed)) * moveAxisRaw.y, (isCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed)) * moveAxisRaw.x);
		float y = moveDirection.y;
		moveDirection = base.transform.TransformDirection(Vector3.forward) * currentInput.x * moveAbs + base.transform.TransformDirection(Vector3.right) * currentInput.y * moveAbs;
		moveDirection.y = y;
	}

	private void HandleMouseLook()
	{
		// Mesma escala do GetAxis("Mouse X"/"Mouse Y") legado: delta do frame em pixels * 0.1, sem deltaTime.
		Vector2 look = LegacyInputCompat.MouseAxes() * settingsSensitivity;
		if (settingsInvertY)
			look.y = -look.y;
		rotationX -= look.y * lookSpeedY;
		rotationX = Mathf.Clamp(rotationX, 0f - lowerLookLimit, upperLookLimit);
		playerCamera.transform.localRotation = Quaternion.Euler(rotationX * (float)moveAbs, 0f, rotationZ);
		base.transform.rotation *= Quaternion.Euler(0f, look.x * lookSpeedX * (float)moveAbs, 0f);
	}

	private void HandleSway()
	{
		float lean = horizontalAxis * leanAmount;
		rotationZ = Mathf.Lerp(rotationZ, isInversed ? -lean : lean, leanSpeed * Time.deltaTime);
	}

	private void HandleJump()
	{
		if (ShouldJump)
		{
			moveDirection.y = jumpForce;
		}
	}

	private void HandleCrouch()
	{
		if (ShouldCrouch)
		{
			StartCoroutine(CrouchStand());
		}
	}

	private void HandleHeadbob()
	{
		if (characterController.isGrounded && characterController.velocity.magnitude > 0.5f && (Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z) > 0.1f))
		{
			timer += Time.deltaTime * (isCrouching ? crouchBobSpeed : (IsSprinting ? sprintBobSpeed : walkBobSpeed));
			playerCamera.transform.localPosition = new Vector3(playerCamera.transform.localPosition.x, defaultYPos + Mathf.Sin(timer) * (isCrouching ? crouchBobAmount : (IsSprinting ? sprintBobAmount : walkBobAmount)), playerCamera.transform.localPosition.z);
		}
	}

	private void HandleZoomIn()
	{
		currentFov = Mathf.Lerp(currentFov, LegacyInputCompat.IsHeld(zoomKey) ? zoomFovAmount : startFov, timeToZoom * Time.deltaTime);
		playerCamera.Lens.FieldOfView = currentFov;
	}

	private void ApplyFinalMovement()
	{
		if (!characterController.isGrounded)
		{
			moveDirection.y -= gravity * Time.deltaTime;
		}
		else if (moveDirection.y < -2f)
		{
			// No chao, a velocidade de queda nao se acumula.
			moveDirection.y = -2f;
		}
		if (willSlideOnSlopes && isSliding)
		{
			// Desliza na rampa sem somar velocidade frame a frame (antes acumulava e arremessava o jogador).
			Vector3 slide = new Vector3(hitPointNormal.x, 0f - hitPointNormal.y, hitPointNormal.z) * slopeSpeed;
			moveDirection.x += slide.x;
			moveDirection.z += slide.z;
			moveDirection.y = Mathf.Min(moveDirection.y, slide.y);
		}
		moveDirection.y = Mathf.Max(moveDirection.y, -MaxFallSpeed);
		characterController.Move(moveDirection * Time.deltaTime);
	}

	/// <summary>Zera a velocidade acumulada (usado ao teleportar o jogador, ex.: posicao segura na piscada).</summary>
	public void ResetMotion()
	{
		moveDirection = Vector3.zero;
		currentInput = Vector2.zero;
	}

	private bool HasCeilingAbove()
	{
		Vector3 checkPosition = playerCamera.transform.position + Vector3.up * crouchStandCheckHeight;
		foreach (Collider hit in Physics.OverlapSphere(checkPosition, crouchStandCheckRadius, ~0, QueryTriggerInteraction.Ignore))
		{
			// O proprio corpo do jogador nao conta como teto.
			if (hit != characterController && !hit.transform.IsChildOf(transform))
			{
				return true;
			}
		}
		return false;
	}

	private IEnumerator CrouchStand()
	{
		// Nao levanta se houver algo logo acima da cabeca.
		if (isCrouching && HasCeilingAbove())
		{
			yield break;
		}
		duringCrouchAnimation = true;
		float timeElapsed = 0f;
		float targetHeight = (isCrouching ? standingHeight : crouchHeight);
		float currentHeight = characterController.height;
		Vector3 targetCenter = (isCrouching ? standingCenter : crouchingCenter);
		Vector3 currentCenter = characterController.center;
		while (timeElapsed < timeToCrouch)
		{
			characterController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / timeToCrouch);
			characterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / timeToCrouch);
			timeElapsed += Time.deltaTime;
			yield return null;
		}
		characterController.height = targetHeight;
		characterController.center = targetCenter;
		isCrouching = !isCrouching;
		duringCrouchAnimation = false;
	}
}
