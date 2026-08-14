using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Fusion;
using Gameplay.Combat;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : NetworkBehaviour 
{
    public const int MaxHealth = 100;

    [Header("Referencias")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private VirtualJoystick joystick;
    [SerializeField] private PlayerCombatController combatController;
    [SerializeField] private LayerMask groundLayer;

    [Header("Configuración de Movimiento")]
    [SerializeField] private float moveSpeed = 6.0f;
    [SerializeField] private float rotationSpeed = 10.0f;

    private bool isDirectControlActive = false;
    private bool controlsEnabled = true;
    private Camera mainCam;
    private int localHealth = MaxHealth;
    private float basicReadyAt;
    private float ultimateReadyAt;

    [Networked] public int NetworkHealth { get; private set; }
    [Networked] public NetworkBool CombatReady { get; private set; }

    public int CurrentHealth => Object == null || !CombatReady ? localHealth : NetworkHealth;
    public bool IsDefeated => (Object == null || CombatReady) && CurrentHealth <= 0;
    public bool HasLocalControl => CanControlPlayer();

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        combatController = GetComponent<PlayerCombatController>();
    }

    private void Start()
    {
        if (Object == null && PlayModeContext.Current == PlayMode.Multiplayer)
        {
            controlsEnabled = false;
            gameObject.SetActive(false);
            return;
        }

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (combatController == null) combatController = GetComponent<PlayerCombatController>();
        if (combatController == null) combatController = gameObject.AddComponent<PlayerCombatController>();
        combatController.Bind(this);
        if (joystick == null) joystick = FindFirstObjectByType<VirtualJoystick>();
        
        mainCam = Camera.main;
        if (agent != null)
            agent.speed = moveSpeed;

        if (CanControlPlayer())
        {
            MobaCamera mobaCamera = FindFirstObjectByType<MobaCamera>();
            if (mobaCamera != null)
                mobaCamera.SetTarget(transform);
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetworkHealth = MaxHealth;
            CombatReady = true;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToMainMenu();
            return;
        }

        //// importantísimo para el multiplayer NO borrar nunca
        if (!controlsEnabled || IsDefeated || !CanControlPlayer() || agent == null || mainCam == null)
            return;

        bool isAiming = combatController != null && combatController.IsAiming;

        // 🎯 SI ESTÁ APUNTANDO: Apaga la rotación interna del NavMeshAgent incondicionalmente
        if (isAiming)
        {
            agent.updateRotation = false;
        }
        
        Vector2 inputDir = GetInputVector();

        // 1. CONTROL DIRECTO (WASD / Joystick Izquierdo)
        if (inputDir.magnitude > 0.1f)
        {
            isDirectControlActive = true;
            agent.ResetPath();
            agent.updateRotation = false;

            Vector3 cameraForward = mainCam.transform.forward;
            Vector3 cameraRight = mainCam.transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 moveDirection = (cameraForward * inputDir.y + cameraRight * inputDir.x).normalized;

            agent.Move(moveDirection * moveSpeed * Time.deltaTime);

            // Solo rotar con el movimiento si NO está apuntando
            if (!isAiming && moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            if (isDirectControlActive)
            {
                isDirectControlActive = false;
            }

            // Si no está apuntando, permite al agente rotar para el Click-To-Move
            if (!isAiming)
            {
                agent.updateRotation = true;
            }

            // 2. CLICK / TAP TO MOVE
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                ProcessClickToMove(isAiming);
            }
        }
    }

    public bool TryExecuteAttack(Vector3 direction, bool ultimate)
    {
        if (!controlsEnabled || IsDefeated || !CanControlPlayer() || direction == Vector3.zero)
            return false;

        if (Object != null && CountActivePlayers() < 2)
            return false;

        float now = Time.unscaledTime;
        float readyAt = ultimate ? ultimateReadyAt : basicReadyAt;
        if (now < readyAt)
            return false;

        if (ultimate) ultimateReadyAt = now + 8f;
        else basicReadyAt = now + 1f;

        float range = ultimate ? 8f : 5f;
        float radius = ultimate ? 1.25f : 0.65f;
        int damage = ultimate ? 40 : 20;
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position + Vector3.up,
            radius,
            direction.normalized,
            range,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        PlayerController closestTarget = null;
        float closestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            PlayerController candidate = hit.collider.GetComponentInParent<PlayerController>();
            if (candidate == null || candidate == this || candidate.IsDefeated)
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestTarget = candidate;
            }
        }

        if (closestTarget != null)
            closestTarget.ReceiveDamage(damage);

        return true;
    }

    private void ReceiveDamage(int damage)
    {
        if (Object == null)
        {
            localHealth = Mathf.Max(0, localHealth - damage);
            if (localHealth == 0) controlsEnabled = false;
            return;
        }

        RPC_ApplyDamage(damage);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyDamage(int damage)
    {
        if (NetworkHealth <= 0) return;
        NetworkHealth = Mathf.Max(0, NetworkHealth - Mathf.Clamp(damage, 0, MaxHealth));
        if (NetworkHealth == 0) controlsEnabled = false;
    }

    private int CountActivePlayers()
    {
        if (Runner == null) return 1;
        int count = 0;
        foreach (PlayerRef ignored in Runner.ActivePlayers) count++;
        return count;
    }

    private bool HasDefeatedOpponent()
    {
        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player != this && player.Object != null && player.CombatReady && player.IsDefeated)
                return true;
        }
        return false;
    }

    private void OnGUI()
    {
        if (!CanControlPlayer()) return;

        float scale = Mathf.Clamp(Screen.width / 960f, 0.8f, 1.6f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        GUIStyle label = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        label.normal.textColor = new Color(0.89f, 0.87f, 0.79f);

        GUI.Box(new Rect(width * 0.5f - 150f, 18f, 300f, 42f), $"VIDA  {CurrentHealth} / {MaxHealth}");
        float basicCooldown = Mathf.Max(0f, basicReadyAt - Time.unscaledTime);
        float ultimateCooldown = Mathf.Max(0f, ultimateReadyAt - Time.unscaledTime);
        GUI.Label(new Rect(width * 0.5f - 240f, 58f, 480f, 34f),
            $"Ataque: {(basicCooldown <= 0f ? "LISTO" : basicCooldown.ToString("0.0"))}    Ultimate: {(ultimateCooldown <= 0f ? "LISTA" : ultimateCooldown.ToString("0.0"))}",
            label);
        if (Object != null && CountActivePlayers() < 2)
            GUI.Label(new Rect(width * 0.5f - 240f, 92f, 480f, 36f), "Esperando al segundo jugador...", label);

        string result = IsDefeated ? "DERROTA" : HasDefeatedOpponent() ? "VICTORIA" : string.Empty;
        if (!string.IsNullOrEmpty(result))
        {
            controlsEnabled = false;
            GUI.Box(new Rect(width * 0.5f - 180f, 125f, 360f, 90f), string.Empty);
            GUI.Label(new Rect(width * 0.5f - 180f, 135f, 360f, 60f), result, label);
            if (GUI.Button(new Rect(width * 0.5f - 90f, 220f, 180f, 48f), "Volver al menú"))
                ReturnToMainMenu();
        }
    }

    private bool CanControlPlayer()
    {
        return Object == null || HasStateAuthority;
    }

    private async void ReturnToMainMenu()
    {
        controlsEnabled = false;

        if (Object != null && Runner != null && Runner.IsRunning)
            await Runner.Shutdown();

        PlayModeContext.UseLocalStory();
        SceneManager.LoadScene("Main Menu");
    }

    private Vector2 GetInputVector()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 keyboardInput = new Vector2(h, v).normalized;

        Vector2 joystickInput = joystick != null ? joystick.InputVector : Vector2.zero;

        return keyboardInput.magnitude > joystickInput.magnitude ? keyboardInput : joystickInput;
    }

    private void ProcessClickToMove(bool isAiming)
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
        {
            agent.updateRotation = !isAiming;
            agent.SetDestination(hit.point);
        }
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0)
        {
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }
        return EventSystem.current.IsPointerOverGameObject();
    }
}
