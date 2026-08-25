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
    // Visual scale only. Navigation, collider, movement and combat ranges stay unchanged.
    private const float DesiredCharacterHeight = 28f;
    private static readonly string[] CharacterNames =
    {
        "Heliandra", "Lunara", "Solmara", "Quietmor", "Acatheria", "Terramor"
    };
    private static readonly Color[] CharacterColors =
    {
        new Color(0.78f, 0.35f, 0.20f),
        new Color(0.34f, 0.48f, 0.76f),
        new Color(0.83f, 0.66f, 0.20f),
        new Color(0.34f, 0.25f, 0.48f),
        new Color(0.30f, 0.64f, 0.45f),
        new Color(0.43f, 0.30f, 0.20f)
    };

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
    private Transform prototypeVisual;
    private Animator prototypeAnimator;
    private int prototypeCharacterIndex = -1;
    private bool exitConfirmationVisible;
    private MatchOutcome currentOutcome;

    [Networked] public int NetworkHealth { get; private set; }
    [Networked] public NetworkBool CombatReady { get; private set; }
    [Networked, Capacity(24)] public NetworkString<_32> DisplayName { get; private set; }
    [Networked] public int TeamId { get; private set; }
    [Networked] public int TeamSlot { get; private set; }
    [Networked] public int CharacterIndex { get; private set; }

    public int CurrentHealth => Object == null || !CombatReady ? localHealth : NetworkHealth;
    public bool IsDefeated => (Object == null || CombatReady) && CurrentHealth <= 0;
    public bool HasLocalControl => CanControlPlayer();
    public string PlayerDisplayName => GetDisplayName();
    public float BasicCooldownRemaining => Mathf.Max(0f, basicReadyAt - Time.unscaledTime);
    public float UltimateCooldownRemaining => Mathf.Max(0f, ultimateReadyAt - Time.unscaledTime);
    public bool ExitConfirmationVisible => exitConfirmationVisible;
    public string NetworkStatusText => Object != null ? OnlineMatchState.Message : string.Empty;
    public bool IsOnlinePlayer => Object != null;
    public string TeamStatusText => Object == null
        ? string.Empty
        : $"{MatchContext.Mode.DisplayName} · {GameLocalization.Choose("Equipo", "Team")} {MatchTeams.NameOf(Team)}";
    public Color TeamDisplayColor => Object == null
        ? new Color(0.94f, 0.91f, 0.82f)
        : MatchTeams.ColorOf(Team);
    public string MatchResultText => currentOutcome == MatchOutcome.Victory
        ? GameLocalization.Choose("VICTORIA", "VICTORY")
        : currentOutcome == MatchOutcome.Defeat
            ? GameLocalization.Choose("DERROTA", "DEFEAT")
            : string.Empty;

    /// <summary>Equipo efectivo. Fuera de red (modo historia) todos son del mismo bando.</summary>
    public int Team => Object == null ? MatchTeams.Bloom : TeamId;

    public bool IsAllyOf(PlayerController other) => other != null && other.Team == Team;

    /// <summary>
    /// Sala aún incompleta. Se puede mover y lanzar ataques para ver los
    /// efectos, pero ningún golpe hace daño hasta que la partida arranca.
    /// </summary>
    private bool IsWarmingUp => Object != null && OnlineMatchState.Phase == OnlineMatchPhase.WaitingForOpponent;

    private enum MatchOutcome { Undecided, Victory, Defeat }

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        combatController = GetComponent<PlayerCombatController>();
    }

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // itch.io ejecuta el juego dentro de un iframe. Capturar el teclado
        // garantiza que WASD llegue al jugador después de enfocar el canvas.
        WebGLInput.captureAllKeyboardInput = true;
#endif

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

        ApplyPlayerColor();

        if (Object == null && PlayModeContext.Current == PlayMode.Training)
            CombatTrainingBootstrap.EnsureForLocalPlayer(this);

        if (CanControlPlayer())
            CombatHudController.EnsureFor(this);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (hasFocus) WebGLInput.captureAllKeyboardInput = true;
#endif
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetworkHealth = MaxHealth;
            CombatReady = true;
            DisplayName = PlayerPrefs.GetString("PlayerName", "Player");
            CharacterIndex = Mathf.Clamp(
                PlayerPrefs.GetInt("SelectedCharacterIndex", 3), 0, CharacterNames.Length - 1);

            // Equipo provisional para poder aparecer en el lado correcto de la
            // arena. PlayerSpawner.BalanceTeams lo confirma al llenarse la sala.
            TeamId = MatchTeams.TeamForPlayerId(Object.InputAuthority.PlayerId);
            TeamSlot = MatchTeams.SlotForPlayerId(Object.InputAuthority.PlayerId, MatchContext.TeamSize);
        }
        ApplyPlayerColor();
    }

    /// <summary>
    /// Lo invoca el master client de Shared Mode al completarse la sala. En
    /// Shared Mode solo el dueño del objeto puede escribir sus [Networked],
    /// por eso el reparto viaja como RPC hacia la autoridad de estado.
    /// </summary>
    public void RequestTeamAssignment(int team, int slot)
    {
        if (Object == null) return;
        if (TeamId == team && TeamSlot == slot) return;

        RPC_AssignTeam(team, slot);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_AssignTeam(int team, int slot)
    {
        TeamId = team;
        TeamSlot = slot;
        MoveToTeamSpawn();
    }

    private void MoveToTeamSpawn()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        Vector3 target = PlayerSpawner.ArenaCenter + MatchTeams.SpawnOffset(TeamId, TeamSlot, MatchContext.TeamSize);

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.Warp(target);
        }
        else
        {
            transform.position = target;
        }

        transform.rotation = MatchTeams.SpawnRotation(TeamId);
    }

    private void Update()
    {
        UpdateMatchOutcome();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Escape ya no abandona de golpe. En WebGL se pulsa por reflejo para
            // salir de pantalla completa, y en 2v2/3v3 eso dejaba al equipo en
            // inferioridad el resto de la partida sin poder deshacerlo.
            RequestExit();
            return;
        }

        // Con el diálogo abierto el juego no debe seguir respondiendo debajo.
        if (exitConfirmationVisible)
            return;

        //// importantísimo para el multiplayer NO borrar nunca
        if (!controlsEnabled || IsDefeated || !CanControlPlayer() || agent == null || mainCam == null)
            return;

        // Durante la espera se permite moverse y probar ataques: la arena vacía
        // se sentía congelada. Solo se bloquea si la partida aún no arrancó y
        // tampoco estamos esperando (conexión caída, resultado, etc.).
        if (Object != null && !OnlineMatchState.CanPlay && !IsWarmingUp)
            return;

        ProcessAttackInput();
        UpdateCharacterAnimation(agent.velocity.sqrMagnitude > 0.04f);

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

        // Mientras la sala se llena el ataque se ejecuta solo por su efecto
        // visual; el daño se descarta más abajo. Así la espera no se ve muerta
        // sin permitir que nadie reciba golpes antes de que empiece.
        if (Object != null && !OnlineMatchState.CanPlay && !IsWarmingUp)
            return false;

        float now = Time.unscaledTime;
        float readyAt = ultimate ? ultimateReadyAt : basicReadyAt;
        if (now < readyAt)
            return false;

        if (ultimate) ultimateReadyAt = now + 8f;
        else basicReadyAt = now + 1f;

        TriggerCharacterAnimation(ultimate ? "ultimate" : "attack");

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
        DestructiblePracticeTarget closestPracticeTarget = null;
        float closestDistance = float.MaxValue;
        float closestObstacleDistance = range;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;

            PlayerController candidate = hit.collider.GetComponentInParent<PlayerController>();
            if (candidate == this)
                continue;

            if (candidate == null)
            {
                DestructiblePracticeTarget practiceTarget = hit.collider.GetComponentInParent<DestructiblePracticeTarget>();
                if (practiceTarget != null && hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestPracticeTarget = practiceTarget;
                    closestTarget = null;
                    continue;
                }
                closestObstacleDistance = Mathf.Min(closestObstacleDistance, hit.distance);
                continue;
            }

            // Sin fuego amigo: un compañero no recibe daño ni bloquea el disparo.
            if (IsAllyOf(candidate))
                continue;

            if (!candidate.IsDefeated && hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestTarget = candidate;
                closestPracticeTarget = null;
            }
        }

        float feedbackRange = Mathf.Clamp(closestObstacleDistance, 0.25f, range);
        if (Object != null)
            RPC_ShowAttackFeedback(direction.normalized, ultimate, feedbackRange);
        else
            ShowAttackFeedback(direction.normalized, ultimate, feedbackRange);

        // En calentamiento el golpe se ve pero no resta vida a nadie.
        if (!IsWarmingUp && closestTarget != null && closestDistance <= closestObstacleDistance + 0.01f)
            closestTarget.ReceiveDamage(damage);
        else if (closestPracticeTarget != null && closestDistance <= closestObstacleDistance + 0.01f)
            closestPracticeTarget.ApplyDamage(damage);

        return true;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowAttackFeedback(Vector3 direction, bool ultimate, float feedbackRange)
    {
        ShowAttackFeedback(direction, ultimate, feedbackRange);
    }

    private void ShowAttackFeedback(Vector3 direction, bool ultimate, float feedbackRange)
    {
        GameObject feedback = new GameObject(ultimate ? "UltimateFeedback" : "AttackFeedback");
        LineRenderer line = feedback.AddComponent<LineRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        Material feedbackMaterial = shader != null ? new Material(shader) : null;
        if (feedbackMaterial != null) line.material = feedbackMaterial;

        Color color = ultimate ? new Color(0.95f, 0.75f, 0.1f, 0.95f) : new Color(0.85f, 0.18f, 0.18f, 0.95f);
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0.15f);
        // Anchos pensados para la cámara actual: más finos se perdían.
        line.startWidth = ultimate ? 0.70f : 0.42f;
        line.endWidth = ultimate ? 0.26f : 0.14f;
        line.positionCount = 2;
        line.useWorldSpace = true;

        // Salía a 0.8 de altura, de cuando el personaje medía menos. Con 4.5
        // unidades eso es a la altura de los tobillos y en vista en picado el
        // propio modelo lo tapaba. Ahora sale del pecho.
        // El modelo puede crecer para la cámara sin subir el origen del VFX
        // hasta la cara. De otro modo la esfera termina tapando al personaje.
        Vector3 origin = transform.position + Vector3.up * Mathf.Min(4.5f, DesiredCharacterHeight * 0.32f);
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + direction.normalized * feedbackRange);

        CharacterPowerVfx.Play(gameObject, transform.position, direction.normalized, ultimate,
            prototypeCharacterIndex, feedbackRange);

        // 0.2 s eran 12 fotogramas: un parpadeo que se perdía.
        Destroy(feedback, 0.4f);
        if (feedbackMaterial != null) Destroy(feedbackMaterial, 0.45f);
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

    /// <summary>
    /// Victoria cuando todo el equipo rival cae; derrota cuando cae el propio.
    /// En 1v1 equivale exactamente a la regla anterior de "cayó el rival".
    /// </summary>
    private MatchOutcome EvaluateOutcome()
    {
        if (Object == null)
            return IsDefeated ? MatchOutcome.Defeat : MatchOutcome.Undecided;

        // Si el equipo rival se fue, sus avatares ya no existen y el recuento de
        // abajo no puede resolver la partida. PlayerSpawner marca esta fase.
        if (OnlineMatchState.Phase == OnlineMatchPhase.OpponentDisconnected)
            return IsDefeated ? MatchOutcome.Defeat : MatchOutcome.Victory;

        int allies = 0;
        int alliesAlive = 0;
        int enemies = 0;
        int enemiesAlive = 0;

        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.Object == null || !player.CombatReady) continue;

            if (IsAllyOf(player))
            {
                allies++;
                if (!player.IsDefeated) alliesAlive++;
            }
            else
            {
                enemies++;
                if (!player.IsDefeated) enemiesAlive++;
            }
        }

        if (enemies > 0 && enemiesAlive == 0) return MatchOutcome.Victory;
        if (allies > 0 && alliesAlive == 0) return MatchOutcome.Defeat;
        return MatchOutcome.Undecided;
    }

    private void UpdateMatchOutcome()
    {
        if (!CanControlPlayer()) return;
        if (currentOutcome != MatchOutcome.Undecided) return;

        currentOutcome = EvaluateOutcome();
        if (currentOutcome == MatchOutcome.Undecided) return;

        controlsEnabled = false;
        if (Object != null)
            OnlineMatchState.Set(OnlineMatchPhase.Finished, MatchResultText);
    }

    private void ApplyPlayerColor()
    {
        EnsurePrototypeCharacterVisual();
    }

    private void EnsurePrototypeCharacterVisual()
    {
        int characterIndex = GetSelectedCharacterIndex();
        if (prototypeVisual != null && prototypeCharacterIndex == characterIndex) return;

        if (prototypeVisual != null)
            Destroy(prototypeVisual.gameObject);

        MeshRenderer capsuleRenderer = GetComponent<MeshRenderer>();
        if (capsuleRenderer != null)
            capsuleRenderer.enabled = false;

        prototypeCharacterIndex = characterIndex;
        Color primary = CharacterColors[characterIndex];
        Color secondary = Color.Lerp(primary, new Color(0.82f, 0.78f, 0.66f), 0.45f);

        // Cualquier personaje que ya tenga su prefab en Resources/Characters se
        // usa tal cual; el resto conserva la silueta provisional. Así integrar
        // un personaje nuevo es dejar su prefab en la carpeta, sin tocar código.
        if (TryCreateImportedCharacterVisual(characterIndex))
            return;

        prototypeVisual = new GameObject($"Prototype {CharacterNames[characterIndex]}").transform;
        prototypeVisual.SetParent(transform, false);
        prototypeVisual.localPosition = Vector3.zero;
        prototypeVisual.localScale = Vector3.one * (DesiredCharacterHeight / 2f);

        CreateVisualPart("Torso", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.68f, 0.82f, 0.42f), primary);
        CreateVisualPart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.88f, 0f), Vector3.one * 0.42f, secondary);
        CreateVisualPart("Left Arm", PrimitiveType.Capsule, new Vector3(-0.48f, 0.12f, 0f), new Vector3(0.18f, 0.48f, 0.18f), secondary);
        CreateVisualPart("Right Arm", PrimitiveType.Capsule, new Vector3(0.48f, 0.12f, 0f), new Vector3(0.18f, 0.48f, 0.18f), secondary);
        CreateVisualPart("Left Leg", PrimitiveType.Capsule, new Vector3(-0.20f, -0.55f, 0f), new Vector3(0.22f, 0.55f, 0.22f), primary * 0.72f);
        CreateVisualPart("Right Leg", PrimitiveType.Capsule, new Vector3(0.20f, -0.55f, 0f), new Vector3(0.22f, 0.55f, 0.22f), primary * 0.72f);

        // Una silueta distinta por personaje ayuda a identificar la elección
        // mientras llegan los modelos finales del equipo de arte.
        if (characterIndex == 1 || characterIndex == 3)
            CreateVisualPart("Hood", PrimitiveType.Sphere, new Vector3(0f, 0.98f, 0.05f), new Vector3(0.54f, 0.42f, 0.50f), primary * 0.7f);
        else if (characterIndex == 2 || characterIndex == 5)
            CreateVisualPart("Crown", PrimitiveType.Cylinder, new Vector3(0f, 1.18f, 0f), new Vector3(0.28f, 0.10f, 0.28f), secondary);
    }

    private bool TryCreateImportedCharacterVisual(int characterIndex)
    {
        GameObject characterPrefab = CharacterCatalog.LoadModel(characterIndex);
        if (characterPrefab == null) return false;

        GameObject instance = Instantiate(characterPrefab, transform);
        instance.name = $"Model {CharacterCatalog.NameOf(characterIndex)}";
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        foreach (Collider visualCollider in instance.GetComponentsInChildren<Collider>(true))
            Destroy(visualCollider);

        prototypeAnimator = EnsureCharacterAnimator(instance, characterIndex);
        prototypeVisual = instance.transform;
        FitImportedCharacterToCollider(instance);
        return true;
    }

    /// <summary>
    /// Red de seguridad: si el prefab llega sin Animator o sin controller, el
    /// personaje aparece completamente inmóvil. El controller vive junto al
    /// prefab en Resources, así que se puede reconectar en runtime.
    /// </summary>
    private static Animator EnsureCharacterAnimator(GameObject character, int characterIndex)
    {
        Animator animator = character.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = character.AddComponent<Animator>();

        if (animator.runtimeAnimatorController == null)
        {
            RuntimeAnimatorController controller = CharacterCatalog.LoadAnimatorController(characterIndex);
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            else
                Debug.LogWarning($"[PlayerController] '{CharacterCatalog.NameOf(characterIndex)}' no tiene Animator Controller: se verá inmóvil.");
        }

        animator.applyRootMotion = false;
        // Sin esto el personaje deja de animarse cuando la cámara no lo encuadra.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return animator;
    }

    private void UpdateCharacterAnimation(bool moving)
    {
        if (prototypeAnimator != null && HasAnimatorParameter(prototypeAnimator, "isMoving"))
            prototypeAnimator.SetBool("isMoving", moving);
    }

    private void TriggerCharacterAnimation(string parameter)
    {
        if (prototypeAnimator != null && HasAnimatorParameter(prototypeAnimator, parameter))
            prototypeAnimator.SetTrigger(parameter);
    }

    private static bool HasAnimatorParameter(Animator animator, string parameter)
    {
        foreach (AnimatorControllerParameter candidate in animator.parameters)
            if (candidate.name == parameter) return true;
        return false;
    }

    private void FitImportedCharacterToCollider(GameObject character)
    {
        if (character == null) return;
    character.transform.localPosition = Vector3.zero;
    character.transform.localRotation = Quaternion.identity;
    }

    private void CreateVisualPart(string partName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.layer = gameObject.layer;
        part.transform.SetParent(prototypeVisual, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null) Destroy(partCollider);

        Renderer renderer = part.GetComponent<Renderer>();
        Material material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 0.18f);
        renderer.sharedMaterial = material;
    }

    private int GetSelectedCharacterIndex()
    {
        // Fuera de red se lee de disco; en red se usa la propiedad replicada
        // para que cada jugador vea el personaje real que eligió el resto.
        if (Object == null)
            return Mathf.Clamp(PlayerPrefs.GetInt("SelectedCharacterIndex", 3), 0, CharacterNames.Length - 1);

        return Mathf.Clamp(CharacterIndex, 0, CharacterNames.Length - 1);
    }

    private string GetDisplayName()
    {
        if (Object == null) return PlayerPrefs.GetString("PlayerName", "Player");
        string networkName = DisplayName.ToString();
        return string.IsNullOrWhiteSpace(networkName) ? "Player" : networkName;
    }

    private bool CanControlPlayer()
    {
        return Object == null || HasStateAuthority;
    }

    /// <summary>
    /// Solo se confirma si abandonar perjudica de verdad: partida en curso y
    /// jugador vivo. Si ya terminó, o el rival se fue, salir es inmediato.
    /// </summary>
    private bool NeedsExitConfirmation()
    {
        return Object != null && OnlineMatchState.CanPlay && !IsDefeated;
    }

    public void RequestExit()
    {
        if (NeedsExitConfirmation())
            exitConfirmationVisible = true;
        else
            ReturnToMainMenu();
    }

    public string ExitWarningText => MatchContext.TeamSize > 1
        ? GameLocalization.Choose(
            $"Tu equipo se quedará con {MatchContext.TeamSize - 1} de {MatchContext.TeamSize} jugadores.",
            $"Your team will be left with {MatchContext.TeamSize - 1} of {MatchContext.TeamSize} players.")
        : GameLocalization.Choose("Se dará el duelo por perdido.", "The duel will count as a loss.");

    public void CancelExit() => exitConfirmationVisible = false;

    public void ConfirmExit()
    {
        exitConfirmationVisible = false;
        ReturnToMultiplayerLobby();
    }

    public void PlayAgain() => ReturnToMultiplayerLobby();

    public void ExitToMenu() => ReturnToMainMenu();

    private async void ReturnToMainMenu()
    {
        controlsEnabled = false;
        await ShutdownNetwork();

        PlayModeContext.UseLocalStory();
        BlightedIntroFlow.ReturnDirectlyToMenu = true;
        SceneManager.LoadScene(GameScenes.Intro);
    }

    private async void ReturnToMultiplayerLobby()
    {
        controlsEnabled = false;
        await ShutdownNetwork();

        OnlineMatchState.Reset();
        BlightedIntroFlow.ReturnDirectlyToMenu = true;
        SceneManager.LoadScene(GameScenes.Intro);
    }

    /// <summary>
    /// Shutdown no destruye el GameObject del runner, que sobrevive por
    /// DontDestroyOnLoad y deja su NetworkSceneManagerDefault suelto. Ese
    /// manager huérfano rompe la carga de escena de la siguiente partida.
    /// </summary>
    private async System.Threading.Tasks.Task ShutdownNetwork()
    {
        if (Object != null && Runner != null && Runner.IsRunning)
            await Runner.Shutdown();

        NetworkLauncher.DestroyStaleRunners();
    }

    private Vector2 GetInputVector()
    {
        // Leer las teclas directamente evita diferencias del Input Manager
        // entre el Editor y el reproductor WebGL embebido en itch.io.
        float h = 0f;
        float v = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;

        // Conservamos los ejes configurados por el proyecto como respaldo
        // para mandos y para versiones del Editor que los reporten primero.
        h += Input.GetAxisRaw("Horizontal");
        v += Input.GetAxisRaw("Vertical");
        Vector2 keyboardInput = Vector2.ClampMagnitude(new Vector2(h, v), 1f);

        if (keyboardInput.sqrMagnitude > 0.01f && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null)
        {
            // Un campo o botón seleccionado puede quedarse con el foco al
            // cambiar de menú. Al detectar movimiento devolvemos el foco al juego.
            EventSystem.current.SetSelectedGameObject(null);
        }

        Vector2 joystickInput = joystick != null ? joystick.InputVector : Vector2.zero;

        return keyboardInput.magnitude > joystickInput.magnitude ? keyboardInput : joystickInput;
    }

    /// <summary>
    /// Ataque con teclado y ratón. Las escenas de arena no tienen AttackJoystick,
    /// y PlayerCombatController solo dispara ataques desde esos joysticks, así
    /// que sin esto no había ninguna forma de atacar en PC.
    /// Q o clic derecho: ataque básico. E: ultimate. Siempre hacia el cursor.
    /// </summary>
    private void ProcessAttackInput()
    {
        bool basic = Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1);
        bool ultimate = Input.GetKeyDown(KeyCode.E);
        if (!basic && !ultimate) return;
        if (IsPointerOverUI()) return;

        Vector3 direction = GetAimDirectionFromCursor();
        if (direction == Vector3.zero) return;

        transform.rotation = Quaternion.LookRotation(direction);
        TryExecuteAttack(direction, ultimate);
    }

    /// <summary>
    /// Dirección desde el jugador al cursor sobre un plano horizontal a su
    /// altura. No usa raycast contra el suelo para no depender de que
    /// groundLayer esté bien configurado en cada escena.
    /// </summary>
    private Vector3 GetAimDirectionFromCursor()
    {
        if (mainCam == null) return transform.forward;

        Plane ground = new Plane(Vector3.up, transform.position);
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (!ground.Raycast(ray, out float distance)) return transform.forward;

        Vector3 planar = ray.GetPoint(distance) - transform.position;
        planar.y = 0f;
        return planar.sqrMagnitude < 0.01f ? transform.forward : planar.normalized;
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
