using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Fusion;
using Gameplay.Combat;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : NetworkBehaviour
{
    public const int MaxHealth = 250;
    // Julian's final gameplay setup scales the Player root itself. Character
    // meshes keep their authored scale so FBX proportions and pivot fixes are
    // not overwritten again from code.
    public const float GameplayPlayerScale = 4.5f;

    public static float DesiredCharacterHeight(int characterIndex) =>
        CharacterCatalog.ExpectedGameplayHeightOf(characterIndex);
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
    private float localRootedUntil;
    private float localSlowedUntil;
    private float localSilencedUntil;
    private float localHasteUntil;
    private float localStunnedUntil;
    private float localBlindUntil;
    private float localRevealUntil;
    private float localPickupHasteUntil;
    private float localPickupPowerUntil;
    private int localShield;
    private Vector2 pendingMoveInput;
    private bool pendingAimRotation;
    private int castSequence;
    private bool automatedInputEnabled;
    private Vector2 automatedMoveInput;
    private readonly HashSet<long> receivedCastTokens = new HashSet<long>();
    private Transform prototypeVisual;
    private Animator prototypeAnimator;
    private int prototypeCharacterIndex = -1;
    private bool exitConfirmationVisible;
    private MatchOutcome currentOutcome;
    private LocalCombatantConfig localCombatant;
    private bool localCombatantConfigured;
    private CombatTargetingController targetingController;
    private Coroutine presentationPauseRoutine;

    [Networked] public int NetworkHealth { get; private set; }
    [Networked] public NetworkBool CombatReady { get; private set; }
    [Networked, Capacity(24)] public NetworkString<_32> DisplayName { get; private set; }
    [Networked] public int TeamId { get; private set; }
    [Networked] public int TeamSlot { get; private set; }
    [Networked] public int CharacterIndex { get; private set; }
    [Networked] public NetworkBool MatchReady { get; private set; }
    [Networked] public NetworkBool NetworkMoving { get; private set; }
    [Networked] private TickTimer BasicCooldownTimer { get; set; }
    [Networked] private TickTimer UltimateCooldownTimer { get; set; }
    [Networked] private TickTimer RootTimer { get; set; }
    [Networked] private TickTimer SlowTimer { get; set; }
    [Networked] private TickTimer SilenceTimer { get; set; }
    [Networked] private TickTimer HasteTimer { get; set; }
    [Networked] private TickTimer StunTimer { get; set; }
    [Networked] private TickTimer BlindTimer { get; set; }
    [Networked] private TickTimer RevealTimer { get; set; }
    [Networked] private int NetworkShield { get; set; }
    [Networked] private TickTimer PickupHasteTimer { get; set; }
    [Networked] private TickTimer PickupPowerTimer { get; set; }
    [Networked] public NetworkBool ArenaSystemsInitialized { get; private set; }
    [Networked] public int ArenaPickupMask { get; private set; }
    [Networked] public int ArenaPickupTypes { get; private set; }
    [Networked] public int ArenaPickupGeneration { get; private set; }
    [Networked] public NetworkBool ArenaCorruptionActive { get; private set; }
    [Networked] public float ArenaCorruptionProgress { get; private set; }
    [Networked] private TickTimer ArenaNextPickupTimer { get; set; }
    [Networked] private TickTimer ArenaCorruptionTimer { get; set; }

    public int CurrentHealth => Object == null || !CombatReady ? localHealth : NetworkHealth;
    public bool IsDefeated => (Object == null || CombatReady) && CurrentHealth <= 0;
    public bool HasLocalControl => AcceptsHumanInput();
    public string PlayerDisplayName => GetDisplayName();
    public float BasicCooldownRemaining => RemainingCooldown(AbilitySlot.Basic);
    public float UltimateCooldownRemaining => RemainingCooldown(AbilitySlot.Ultimate);
    public float BasicCooldownDuration => GetAbility(AbilitySlot.Basic).cooldown;
    public float UltimateCooldownDuration => GetAbility(AbilitySlot.Ultimate).cooldown;
    public string BasicAbilityName => GetAbility(AbilitySlot.Basic).DisplayName;
    public string UltimateAbilityName => GetAbility(AbilitySlot.Ultimate).DisplayName;
    public float BasicAbilityRange => GetAbility(AbilitySlot.Basic).range;
    public float UltimateAbilityRange => GetAbility(AbilitySlot.Ultimate).range;
    public int CurrentShield => Object == null ? localShield : NetworkShield;
    public string CombatStatusText => BuildCombatStatusText();
    public bool IsBlinded => Object == null ? Time.unscaledTime < localBlindUntil : TimerActive(BlindTimer);
    public bool ExitConfirmationVisible => exitConfirmationVisible;
    public string NetworkStatusText => Object != null ? OnlineMatchState.Message : string.Empty;
    public bool IsOnlinePlayer => Object != null;
    public bool IsTrainingBot => Object == null && localCombatantConfigured &&
        localCombatant.Role == LocalCombatantRole.TrainingBot;
    public bool IsLocalTrainingParticipant => Object == null && localCombatantConfigured &&
        PlayModeContext.Current == PlayMode.Training;
    public bool IsCombatParticipant => IsNetworkMatchParticipant || IsLocalTrainingParticipant;
    public int SelectedCharacterIndex => GetSelectedCharacterIndex();
    public CombatTargetingController Targeting => targetingController;
    /// <summary>
    /// Los avatares jugables son objetos creados por PlayerSpawner. El Player
    /// serializado en OnlineArena es una referencia de escena del equipo y se
    /// conserva intacto, pero no debe ocupar un cupo ni recibir input.
    /// </summary>
    public bool IsNetworkMatchParticipant => Object != null &&
        !Object.NetworkTypeId.IsSceneObject && Object.InputAuthority.IsValid;
    public string TeamStatusText => Object == null
        ? (PlayModeContext.Current == PlayMode.Training
            ? GameLocalization.Choose("ENTRENAMIENTO", "TRAINING")
            : string.Empty)
        : $"{MatchContext.Mode.DisplayName} · {GameLocalization.Choose("Equipo", "Team")} {MatchTeams.NameOf(Team)}";
    public Color TeamDisplayColor => IsCombatParticipant
        ? MatchTeams.ColorOf(Team)
        : new Color(0.94f, 0.91f, 0.82f);
    public string MatchResultText => currentOutcome == MatchOutcome.Victory
        ? GameLocalization.Choose("VICTORIA", "VICTORY")
        : currentOutcome == MatchOutcome.Defeat
            ? GameLocalization.Choose("DERROTA", "DEFEAT")
            : string.Empty;
    public bool HasWon => currentOutcome == MatchOutcome.Victory;
    public bool HasLost => currentOutcome == MatchOutcome.Defeat;
    public bool HasPickupHaste => Object == null
        ? Time.unscaledTime < localPickupHasteUntil
        : TimerActive(PickupHasteTimer);
    public bool HasPowerPickup => Object == null
        ? Time.unscaledTime < localPickupPowerUntil
        : TimerActive(PickupPowerTimer);

    /// <summary>Equipo efectivo en red o en el duelo local de entrenamiento.</summary>
    public int Team => Object == null
        ? (localCombatantConfigured ? localCombatant.TeamId : MatchTeams.Bloom)
        : TeamId;

    public bool IsAllyOf(PlayerController other) => other != null && other.Team == Team;

    /// <summary>
    /// Sala aún incompleta. Se puede mover y lanzar ataques para ver los
    /// efectos, pero ningún golpe hace daño hasta que la partida arranca.
    /// </summary>
    private bool IsWarmingUp => Object != null && !MatchReady;

    private enum MatchOutcome { Undecided, Victory, Defeat }

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        combatController = GetComponent<PlayerCombatController>();
    }

    public void ConfigureLocalCombatant(LocalCombatantConfig config)
    {
        if (Object != null) return;

        config.CharacterIndex = CharacterCatalog.Clamp(config.CharacterIndex);
        config.TeamId = config.TeamId == MatchTeams.Blight ? MatchTeams.Blight : MatchTeams.Bloom;
        if (string.IsNullOrWhiteSpace(config.DisplayName))
            config.DisplayName = CharacterCatalog.NameOf(config.CharacterIndex);

        localCombatant = config;
        localCombatantConfigured = true;
    }

    /// <summary>Restaura por completo un combatiente local para una revancha.</summary>
    public void ResetLocalCombatState(Vector3 position, Quaternion rotation)
    {
        if (Object != null) return;

        StopAllCoroutines();
        localHealth = MaxHealth;
        localShield = 0;
        basicReadyAt = 0f;
        ultimateReadyAt = 0f;
        localRootedUntil = 0f;
        localSlowedUntil = 0f;
        localSilencedUntil = 0f;
        localHasteUntil = 0f;
        localStunnedUntil = 0f;
        localBlindUntil = 0f;
        localRevealUntil = 0f;
        localPickupHasteUntil = 0f;
        localPickupPowerUntil = 0f;
        castSequence = 0;
        receivedCastTokens.Clear();
        currentOutcome = MatchOutcome.Undecided;
        controlsEnabled = true;
        exitConfirmationVisible = false;

        transform.SetPositionAndRotation(position, rotation);
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 12f, agent.areaMask))
                agent.Warp(hit.position);
        }
    }

    public bool TrySetBotDestination(Vector3 destination, float speedMultiplier = 1f)
    {
        if (!IsTrainingBot || !controlsEnabled || IsDefeated || IsMovementBlocked()) return false;
        if (!EnsureAgentOnNavMesh()) return false;
        if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, agent.areaMask)) return false;

        agent.speed = moveSpeed * CurrentMovementMultiplier() * Mathf.Clamp(speedMultiplier, 0.25f, 1f);
        agent.stoppingDistance = 0.6f;
        agent.updateRotation = true;
        agent.SetDestination(hit.position);
        return true;
    }

    public void StopBotMovement()
    {
        if (!IsTrainingBot || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        agent.ResetPath();
    }

    private void Start()
    {
        if (Object != null && Object.NetworkTypeId.IsSceneObject)
        {
            DisableSceneReferencePlayer();
            return;
        }

        if (Object == null && PlayModeContext.Current == PlayMode.Multiplayer)
        {
            controlsEnabled = false;
            gameObject.SetActive(false);
            return;
        }

        if (Object == null && PlayModeContext.Current == PlayMode.Training && !localCombatantConfigured)
        {
            ConfigureLocalCombatant(LocalCombatantConfig.Human(
                PlayerPrefs.GetInt("SelectedCharacterIndex", 3),
                CharacterCatalog.NameOf(PlayerPrefs.GetInt("SelectedCharacterIndex", 3))));
        }

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (combatController == null) combatController = GetComponent<PlayerCombatController>();
        if (combatController == null) combatController = gameObject.AddComponent<PlayerCombatController>();
        combatController.Bind(this);
        if (joystick == null) joystick = FindFirstObjectByType<VirtualJoystick>();

        mainCam = Camera.main;
        if (agent != null)
            agent.speed = moveSpeed;

        if (HasLocalControl)
        {
            targetingController = CombatTargetingController.EnsureFor(this);
            MobaCamera mobaCamera = FindFirstObjectByType<MobaCamera>();
            if (mobaCamera != null) mobaCamera.BindTargeting(targetingController);
        }

        ApplyPlayerColor();

        if (Object == null && PlayModeContext.Current == PlayMode.Training && HasLocalControl)
            CombatTrainingBootstrap.EnsureForLocalPlayer(this);

        if (HasLocalControl)
        {
            CombatHudController.EnsureFor(this);
            CombatAlertAudioController.EnsureFor(this);
            ArenaPowerUpManager.EnsureFor(this);
        }

        ConfigureNetworkAuthorityComponents();
    }

    public override void Spawned()
    {
        if (Object.NetworkTypeId.IsSceneObject)
        {
            DisableSceneReferencePlayer();
            return;
        }

        if (HasStateAuthority)
        {
            NetworkHealth = MaxHealth;
            CombatReady = true;
            DisplayName = MultiplayerSmokeRuntime.Active
                ? MultiplayerSmokeRuntime.DesiredName
                : PlayerPrefs.GetString("PlayerName", "Player");
            CharacterIndex = Mathf.Clamp(
                MultiplayerSmokeRuntime.Active
                    ? MultiplayerSmokeRuntime.DesiredCharacterIndex
                    : PlayerPrefs.GetInt("SelectedCharacterIndex", 3),
                0, CharacterNames.Length - 1);

            // Equipo provisional para poder aparecer en el lado correcto de la
            // arena. PlayerSpawner.BalanceTeams lo confirma al llenarse la sala.
            TeamId = MatchTeams.TeamForPlayerId(Object.InputAuthority.PlayerId);
            TeamSlot = MatchTeams.SlotForPlayerId(Object.InputAuthority.PlayerId, MatchContext.TeamSize);
            MatchReady = false;
            NetworkMoving = false;
            NetworkShield = 0;
            PickupHasteTimer = default;
            PickupPowerTimer = default;
            ArenaSystemsInitialized = false;
            ArenaPickupMask = 0;
            ArenaPickupTypes = 0;
            ArenaPickupGeneration = 0;
            ArenaCorruptionActive = false;
            ArenaCorruptionProgress = 0f;
        }
        ApplyPlayerColor();
        ConfigureNetworkAuthorityComponents();

        if (HasLocalControl)
        {
            targetingController = CombatTargetingController.EnsureFor(this);
            MobaCamera mobaCamera = FindFirstObjectByType<MobaCamera>();
            if (mobaCamera != null) mobaCamera.BindTargeting(targetingController);
            CombatHudController.EnsureFor(this);
            CombatAlertAudioController.EnsureFor(this);
            ArenaPowerUpManager.EnsureFor(this);
        }
    }

    private void ConfigureNetworkAuthorityComponents()
    {
        if (Object == null) return;

        if (!IsNetworkMatchParticipant)
        {
            DisableSceneReferencePlayer();
            return;
        }

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = HasStateAuthority;
            if (HasStateAuthority) agent.speed = moveSpeed;
        }

        if (!HasStateAuthority)
        {
            if (combatController != null) combatController.enabled = false;
            if (joystick != null) joystick.gameObject.SetActive(false);
        }
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

    public void RequestMatchStart()
    {
        if (Object != null) RPC_SetMatchReady();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetMatchReady()
    {
        MatchReady = true;
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

        if (NavMesh.SamplePosition(target, out NavMeshHit spawnHit, 12f, NavMesh.AllAreas))
            target = spawnHit.position;

        if (agent != null && agent.isActiveAndEnabled)
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();

            if (!agent.Warp(target))
                transform.position = target;
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

        // Los bots comparten simulación y habilidades, pero jamás leen el
        // teclado/joysticks ni reaccionan al Escape del jugador humano.
        if (!HasLocalControl)
        {
            if (Object == null)
                UpdateCharacterAnimation(agent != null && agent.velocity.sqrMagnitude > 0.04f);
            return;
        }

        if (Object != null && HasStateAuthority && MatchReady && OnlineMatchState.Phase != OnlineMatchPhase.Playing &&
            OnlineMatchState.Phase != OnlineMatchPhase.Finished)
        {
            OnlineMatchState.Set(OnlineMatchPhase.Playing,
                $"{MatchContext.Mode.DisplayName} · {GameLocalization.Choose("Equipo", "Team")} {MatchTeams.NameOf(TeamId)}");
        }

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

        // El prefab puede activarse uno o dos frames antes de que Unity termine
        // de asociar el agente con la malla. Nunca invoques Move/ResetPath en
        // ese intervalo: además de ensuciar la consola, el personaje se queda
        // inmóvil. Si existe suelo navegable cercano, lo recuperamos aquí.
        if (!EnsureAgentOnNavMesh())
            return;

        // Durante la espera se permite moverse y probar ataques: la arena vacía
        // se sentía congelada. Solo se bloquea si la partida aún no arrancó y
        // tampoco estamos esperando (conexión caída, resultado, etc.).
        if (Object != null && OnlineMatchState.Phase is OnlineMatchPhase.ConnectionFailed or OnlineMatchPhase.Finished)
            return;

        ProcessAttackInput();
        if (Object == null)
            UpdateCharacterAnimation(agent.velocity.sqrMagnitude > 0.04f);

        bool isAiming = combatController != null && combatController.IsAiming;

        // 🎯 SI ESTÁ APUNTANDO: Apaga la rotación interna del NavMeshAgent incondicionalmente
        if (isAiming)
        {
            agent.updateRotation = false;
        }

        Vector2 inputDir = GetInputVector();

        if (IsMovementBlocked()) inputDir = Vector2.zero;

        if (Object != null)
        {
            pendingMoveInput = inputDir;
            pendingAimRotation = isAiming;

            if (inputDir.magnitude <= 0.1f && Input.GetMouseButtonDown(0) && !IsPointerOverUI())
                ProcessClickToMove(isAiming);
            return;
        }

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

            agent.Move(moveDirection * moveSpeed * CurrentMovementMultiplier() * Time.deltaTime);

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

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority) UpdateArenaCoordinator();
        if (!HasStateAuthority || agent == null || !agent.enabled || !controlsEnabled || IsDefeated)
            return;
        if (OnlineMatchState.Phase is OnlineMatchPhase.ConnectionFailed or OnlineMatchPhase.Finished)
            return;
        if (!EnsureAgentOnNavMesh())
        {
            NetworkMoving = false;
            return;
        }

        Vector2 movement = IsMovementBlocked() ? Vector2.zero : pendingMoveInput;
        SimulateDirectMovement(movement, pendingAimRotation, Runner.DeltaTime);
        NetworkMoving = agent.velocity.sqrMagnitude > 0.04f || movement.sqrMagnitude > 0.01f;
    }

    public override void Render()
    {
        if (Object != null && Object.NetworkTypeId.IsSceneObject) return;
        EnsurePrototypeCharacterVisual();
        UpdateCharacterAnimation(Object == null ? agent != null && agent.velocity.sqrMagnitude > 0.04f : NetworkMoving);
    }

    private void DisableSceneReferencePlayer()
    {
        controlsEnabled = false;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        if (combatController == null) combatController = GetComponent<PlayerCombatController>();
        if (combatController != null) combatController.enabled = false;
        if (joystick != null) joystick.gameObject.SetActive(false);

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
        foreach (Collider playerCollider in GetComponentsInChildren<Collider>(true))
            playerCollider.enabled = false;
    }

    private void SimulateDirectMovement(Vector2 inputDir, bool isAiming, float deltaTime)
    {
        if (!EnsureAgentOnNavMesh())
            return;

        if (inputDir.magnitude <= 0.1f)
        {
            if (!isAiming) agent.updateRotation = true;
            return;
        }

        isDirectControlActive = true;
        agent.ResetPath();
        agent.updateRotation = false;

        Vector3 cameraForward = mainCam != null ? mainCam.transform.forward : transform.forward;
        Vector3 cameraRight = mainCam != null ? mainCam.transform.right : transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();
        Vector3 moveDirection = (cameraForward * inputDir.y + cameraRight * inputDir.x).normalized;
        float speed = moveSpeed * CurrentMovementMultiplier();
        agent.Move(moveDirection * speed * deltaTime);

        if (!isAiming && moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
        }
    }

    public bool TryExecuteAttack(Vector3 direction, bool ultimate)
    {
        AimData aim = new AimData
        {
            Direction = direction,
            DistanceRatio = 1f,
            IsTap = true
        };
        return TryCastAbility(ultimate ? AbilitySlot.Ultimate : AbilitySlot.Basic, aim);
    }

    public Vector3 AssistAimDirection(Vector3 direction, AbilitySlot slot)
    {
        if (targetingController == null && HasLocalControl)
            targetingController = CombatTargetingController.EnsureFor(this);
        return targetingController != null
            ? targetingController.AssistDirection(direction, slot)
            : direction;
    }

    public bool TryCastAbility(AbilitySlot slot, AimData aim)
    {
        Vector3 direction = HasLocalControl ? AssistAimDirection(aim.Direction, slot) : aim.Direction;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
        direction.Normalize();

        if (!controlsEnabled || IsDefeated || !CanControlPlayer() || IsCastingBlocked())
            return false;
        if (Object != null && OnlineMatchState.Phase is OnlineMatchPhase.ConnectionFailed or OnlineMatchPhase.Finished)
            return false;
        if (RemainingCooldown(slot) > 0f)
            return false;

        AbilityDefinition ability = GetAbility(slot);
        StartCooldown(slot, ability.cooldown);
        castSequence = castSequence == int.MaxValue ? 1 : castSequence + 1;
        int sourcePlayer = Object != null
            ? Object.InputAuthority.PlayerId
            : (localCombatantConfigured ? localCombatant.CombatantId : -1);
        float ratio = aim.IsTap ? 1f : Mathf.Clamp(aim.DistanceRatio, 0.35f, 1f);
        float travel = ability.range * ratio;
        transform.rotation = Quaternion.LookRotation(direction);

        if (Object != null)
            RPC_PresentAbility(GetSelectedCharacterIndex(), (int)slot, direction, travel,
                ability.radius, (int)ability.shape);
        else
            PresentAbility(GetSelectedCharacterIndex(), slot, direction, travel, ability.radius, ability.shape);

        StartCoroutine(ResolveAbilityAfterDelay(ability, direction, travel, sourcePlayer, castSequence));
        return true;
    }

    private IEnumerator ResolveAbilityAfterDelay(AbilityDefinition ability, Vector3 direction, float travel,
        int sourcePlayer, int sequence)
    {
        if (ability.castDelay > 0f)
            yield return new WaitForSeconds(ability.castDelay);

        if (ability.shape is AbilityShape.Leap or AbilityShape.Dash)
            MoveForAbility(direction, travel);

        // El calentamiento comparte animación y VFX, pero jamás resolución de
        // daño o estados. Todos comienzan limpios en el mismo tick de inicio.
        if (IsWarmingUp)
            yield break;

        Vector3 areaCenter = ability.shape is AbilityShape.Area or AbilityShape.Leap or AbilityShape.Wall
            ? transform.position + direction * travel
            : transform.position;
        if (ability.shape == AbilityShape.Leap)
            areaCenter = transform.position;

        if (ability.shape == AbilityShape.Wall)
        {
            if (Object != null) RPC_CreateAbilityWall(areaCenter, direction, ability.radius, ability.hostileEffectDuration > 0f ? ability.hostileEffectDuration : 4f);
            else TemporaryAbilityWall.Spawn(areaCenter, direction, ability.radius, 4f, ability.vfxColor);
        }

        HashSet<PlayerController> players = new HashSet<PlayerController>();
        GatherAbilityTargets(ability, direction, travel, areaCenter, players);

        bool hitsEnemy = false;
        foreach (PlayerController candidate in players)
        {
            if (IsCombatParticipantForAbilities(candidate) && !candidate.IsDefeated && !IsAllyOf(candidate))
            {
                hitsEnemy = true;
                break;
            }
        }
        bool empowered = ability.damage > 0 && hitsEnemy && ConsumePowerPickup();
        int resolvedDamage = empowered ? Mathf.CeilToInt(ability.damage * 1.25f) : ability.damage;

        foreach (PlayerController target in players)
        {
            if (!IsCombatParticipantForAbilities(target) || target.IsDefeated) continue;
            if (IsAllyOf(target))
            {
                if (ability.alliedEffect != CombatEffectKind.None)
                    target.ReceiveFriendlyEffect(ability.alliedEffect, ability.alliedEffectDuration,
                        ability.alliedEffectStrength, Team, sourcePlayer, sequence);
                continue;
            }

            target.ReceiveAbilityImpact(resolvedDamage, ability.hostileEffect,
                ability.hostileEffectDuration, ability.hostileEffectStrength, direction,
                Team, sourcePlayer, sequence, GetSelectedCharacterIndex(), ability.slot);
        }

        // Aceleración de Acatheria se aplica a quien lanza la garra; la barrera
        // de Heliandra sí se reparte a aliados dentro de su flor.
        if (ability.alliedEffect == CombatEffectKind.Haste)
            ReceiveFriendlyEffect(ability.alliedEffect, ability.alliedEffectDuration,
                ability.alliedEffectStrength, Team, sourcePlayer, sequence);

    }

    private void GatherAbilityTargets(AbilityDefinition ability, Vector3 direction, float travel, Vector3 areaCenter,
        HashSet<PlayerController> players)
    {
        if (ability.shape is AbilityShape.Line or AbilityShape.Dash)
        {
            foreach (RaycastHit hit in Physics.SphereCastAll(transform.position + Vector3.up, ability.radius,
                         direction, travel, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                AddCombatTarget(hit.collider, players);
            return;
        }

        Vector3 center = ability.shape == AbilityShape.Cone ? transform.position : areaCenter;
        float radius = ability.shape == AbilityShape.Cone ? ability.range : ability.radius;
        foreach (Collider hit in Physics.OverlapSphere(center, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (ability.shape == AbilityShape.Cone)
            {
                Vector3 toTarget = hit.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(direction, toTarget) > ability.coneAngle * 0.5f)
                    continue;
            }
            AddCombatTarget(hit, players);
        }
    }

    private void AddCombatTarget(Collider hit, HashSet<PlayerController> players)
    {
        if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform)) return;
        PlayerController playerTarget = hit.GetComponentInParent<PlayerController>();
        if (playerTarget != null && playerTarget != this)
        {
            players.Add(playerTarget);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PresentAbility(int characterIndex, int slot, Vector3 direction, float travel, float radius, int shape)
    {
        PresentAbility(characterIndex, (AbilitySlot)slot, direction, travel, radius, (AbilityShape)shape);
    }

    private void PresentAbility(int characterIndex, AbilitySlot slot, Vector3 direction, float travel,
        float radius, AbilityShape shape)
    {
        bool ultimate = slot == AbilitySlot.Ultimate;
        TriggerCharacterAnimation(ultimate ? "ultimate" : "attack");
        ShowAbilityFeedback(direction, travel, radius, shape, ultimate, characterIndex);

        AbilityDefinition ability = CharacterCatalog.AbilityOf(characterIndex, slot);
        if (ability != null && ability.castSfx != null)
            AudioCatalog.PlayOneShot(ability.castSfx, transform.position);
    }

    private void ShowAbilityFeedback(Vector3 direction, float feedbackRange, float radius,
        AbilityShape shape, bool ultimate, int characterIndex)
    {
        GameObject feedback = new GameObject(ultimate ? "UltimateFeedback" : "AttackFeedback");
        LineRenderer line = feedback.AddComponent<LineRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        Material feedbackMaterial = shader != null ? new Material(shader) : null;
        if (feedbackMaterial != null) line.material = feedbackMaterial;

        Color color = CharacterCatalog.AbilityOf(characterIndex,
            ultimate ? AbilitySlot.Ultimate : AbilitySlot.Basic).vfxColor;
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
        Vector3 origin = transform.position + Vector3.up * GameplayPlayerScale;
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + direction.normalized * feedbackRange);

        CharacterPowerVfx.Play(gameObject, transform.position, direction.normalized, ultimate,
            characterIndex, Mathf.Max(feedbackRange, radius));

        // 0.2 s eran 12 fotogramas: un parpadeo que se perdía.
        Destroy(feedback, 0.4f);
        if (feedbackMaterial != null) Destroy(feedbackMaterial, 0.45f);
    }

    private void ReceiveAbilityImpact(int damage, CombatEffectKind effect, float duration, float strength,
        Vector3 direction, int sourceTeam, int sourcePlayer, int sequence, int sourceCharacter, AbilitySlot slot)
    {
        if (Object == null)
        {
            ApplyAbilityImpact(damage, effect, duration, strength, direction, sourceTeam,
                sourcePlayer, sequence, sourceCharacter, slot);
            return;
        }
        RPC_ApplyAbilityImpact(damage, (int)effect, duration, strength, direction, sourceTeam,
            sourcePlayer, sequence, sourceCharacter, (int)slot);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyAbilityImpact(int damage, int effect, float duration, float strength,
        Vector3 direction, int sourceTeam, int sourcePlayer, int sequence, int sourceCharacter, int slot)
    {
        ApplyAbilityImpact(damage, (CombatEffectKind)effect, duration, strength, direction,
            sourceTeam, sourcePlayer, sequence, sourceCharacter, (AbilitySlot)slot);
    }

    private void ApplyAbilityImpact(int damage, CombatEffectKind effect, float duration, float strength,
        Vector3 direction, int sourceTeam, int sourcePlayer, int sequence, int sourceCharacter, AbilitySlot slot)
    {
        if (Team == sourceTeam || IsDefeated || !RememberCast(sourcePlayer, sequence)) return;

        int remainingDamage = MultiplayerSmokeRuntime.Invulnerable ? 0 : Mathf.Max(0, damage);
        if (Object == null)
        {
            int absorbed = Mathf.Min(localShield, remainingDamage);
            localShield -= absorbed;
            remainingDamage -= absorbed;
            localHealth = Mathf.Max(0, localHealth - remainingDamage);
        }
        else
        {
            int absorbed = Mathf.Min(NetworkShield, remainingDamage);
            NetworkShield -= absorbed;
            remainingDamage -= absorbed;
            NetworkHealth = Mathf.Max(0, NetworkHealth - remainingDamage);
        }

        ApplyStatus(effect, duration, strength);

        // Dos habilidades combinan su estado principal con desplazamiento.
        if (effect == CombatEffectKind.Knockback ||
            (sourceCharacter == 0 && slot == AbilitySlot.Ultimate) ||
            (sourceCharacter == 5 && slot == AbilitySlot.Basic))
            ApplyDisplacement(direction, Mathf.Max(2f, strength));

        if (Object == null)
            CombatFeedbackController.PresentHit(this, remainingDamage, direction);
        else
            RPC_PresentImpact(remainingDamage, direction);

        if (CurrentHealth <= 0) controlsEnabled = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PresentImpact(int damage, Vector3 direction)
    {
        CombatFeedbackController.PresentHit(this, damage, direction);
    }

    public void PauseCharacterPresentation(float seconds)
    {
        if (prototypeAnimator == null || seconds <= 0f) return;
        if (presentationPauseRoutine != null) StopCoroutine(presentationPauseRoutine);
        presentationPauseRoutine = StartCoroutine(PauseAnimator(seconds));
    }

    private IEnumerator PauseAnimator(float seconds)
    {
        if (prototypeAnimator == null) yield break;
        float previousSpeed = prototypeAnimator.speed;
        prototypeAnimator.speed = 0f;
        yield return new WaitForSecondsRealtime(seconds);
        if (prototypeAnimator != null) prototypeAnimator.speed = previousSpeed;
        presentationPauseRoutine = null;
    }

    private void ReceiveFriendlyEffect(CombatEffectKind effect, float duration, float strength,
        int sourceTeam, int sourcePlayer, int sequence)
    {
        if (Object == null)
        {
            ApplyFriendlyEffect(effect, duration, strength, sourceTeam, sourcePlayer, sequence);
            return;
        }
        RPC_ApplyFriendlyEffect((int)effect, duration, strength, sourceTeam, sourcePlayer, sequence);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyFriendlyEffect(int effect, float duration, float strength,
        int sourceTeam, int sourcePlayer, int sequence)
    {
        ApplyFriendlyEffect((CombatEffectKind)effect, duration, strength, sourceTeam, sourcePlayer, sequence);
    }

    private void ApplyFriendlyEffect(CombatEffectKind effect, float duration, float strength,
        int sourceTeam, int sourcePlayer, int sequence)
    {
        if (Team != sourceTeam || !RememberCast(sourcePlayer, sequence + 1000000)) return;
        if (effect == CombatEffectKind.Shield)
        {
            if (Object == null) localShield = Mathf.Max(localShield, Mathf.RoundToInt(strength));
            else NetworkShield = Mathf.Max(NetworkShield, Mathf.RoundToInt(strength));
            return;
        }
        ApplyStatus(effect, duration, strength);
    }

    private bool RememberCast(int sourcePlayer, int sequence)
    {
        long token = ((long)(sourcePlayer + 2) << 32) | (uint)sequence;
        if (!receivedCastTokens.Add(token)) return false;
        if (receivedCastTokens.Count > 256) receivedCastTokens.Clear();
        return true;
    }

    private void ApplyStatus(CombatEffectKind effect, float duration, float strength)
    {
        if (effect == CombatEffectKind.None) return;
        float safeDuration = Mathf.Max(0.05f, duration);
        if (Object == null)
        {
            float expiry = Time.unscaledTime + safeDuration;
            switch (effect)
            {
                case CombatEffectKind.Root: localRootedUntil = Mathf.Max(localRootedUntil, expiry); break;
                case CombatEffectKind.Slow: localSlowedUntil = Mathf.Max(localSlowedUntil, expiry); break;
                case CombatEffectKind.Silence: localSilencedUntil = Mathf.Max(localSilencedUntil, expiry); break;
                case CombatEffectKind.Haste: localHasteUntil = Mathf.Max(localHasteUntil, expiry); break;
                case CombatEffectKind.Stun: localStunnedUntil = Mathf.Max(localStunnedUntil, expiry); break;
                case CombatEffectKind.Blind: localBlindUntil = Mathf.Max(localBlindUntil, expiry); break;
                case CombatEffectKind.Reveal: localRevealUntil = Mathf.Max(localRevealUntil, expiry); break;
            }
            return;
        }

        TickTimer timer = TickTimer.CreateFromSeconds(Runner, safeDuration);
        switch (effect)
        {
            case CombatEffectKind.Root: RootTimer = timer; break;
            case CombatEffectKind.Slow: SlowTimer = timer; break;
            case CombatEffectKind.Silence: SilenceTimer = timer; break;
            case CombatEffectKind.Haste: HasteTimer = timer; break;
            case CombatEffectKind.Stun: StunTimer = timer; break;
            case CombatEffectKind.Blind: BlindTimer = timer; break;
            case CombatEffectKind.Reveal: RevealTimer = timer; break;
        }
    }

    private void ApplyDisplacement(Vector3 direction, float distance)
    {
        Vector3 target = transform.position + direction.normalized * distance;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas)) target = hit.position;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(target);
        else transform.position = target;
    }

    private void MoveForAbility(Vector3 direction, float distance)
    {
        if (!CanControlPlayer()) return;
        ApplyDisplacement(direction, distance);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CreateAbilityWall(Vector3 center, Vector3 direction, float width, float duration)
    {
        AbilityDefinition ability = GetAbility(AbilitySlot.Ultimate);
        TemporaryAbilityWall.Spawn(center, direction, width, duration, ability.vfxColor);
    }

    /// <summary>
    /// Victoria cuando todo el equipo rival cae; derrota cuando cae el propio.
    /// En 1v1 equivale exactamente a la regla anterior de "cayó el rival".
    /// </summary>
    private MatchOutcome EvaluateOutcome()
    {
        // Si el equipo rival se fue, sus avatares ya no existen y el recuento de
        // abajo no puede resolver la partida. PlayerSpawner marca esta fase.
        if (Object != null && OnlineMatchState.Phase == OnlineMatchPhase.OpponentDisconnected)
            return IsDefeated ? MatchOutcome.Defeat : MatchOutcome.Victory;

        int allies = 0;
        int alliesAlive = 0;
        int enemies = 0;
        int enemiesAlive = 0;

        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!player.IsCombatParticipant) continue;
            if (player.Object != null && !player.CombatReady) continue;

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
        prototypeVisual.localScale = Vector3.one;

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
        Vector3 presentationOffset = CharacterCatalog.ModelLocalOffsetOf(characterIndex);
        instance.transform.localPosition = new Vector3(presentationOffset.x, 0f, presentationOffset.z);
        FitImportedCharacterToCollider(instance, presentationOffset.y);
        StartCoroutine(StabilizeImportedCharacterGrounding(instance, presentationOffset.y));
        return true;
    }

    private IEnumerator StabilizeImportedCharacterGrounding(GameObject character, float authoredGroundOffset)
    {
        // El Animator y el NavMeshAgent actualizan sus bounds/altura durante
        // los primeros frames. Repetir la alineación una vez evita que el arte
        // quede flotando después del primer tick de navegación.
        yield return null;
        yield return new WaitForEndOfFrame();
        if (character != null) FitImportedCharacterToCollider(character, authoredGroundOffset);
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

    private void FitImportedCharacterToCollider(GameObject character, float authoredGroundOffset)
    {
        if (character == null) return;
        character.transform.localRotation = Quaternion.identity;

        // Los seis FBX conservan su escala exportada. Solo compensamos la
        // diferencia de pivote para que el punto más bajo del arte coincida
        // con el suelo del Player; esto evita modelos volando o enterrados sin
        // volver a introducir el autoescalado que anulaba el ajuste de Julian.
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>(false);
        bool hasBounds = false;
        Bounds visualBounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer is not SkinnedMeshRenderer && renderer is not MeshRenderer)
                continue;
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (!hasBounds)
            {
                visualBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                visualBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds || float.IsNaN(visualBounds.min.y) || float.IsInfinity(visualBounds.min.y))
            return;

        float groundY = transform.position.y;
        int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit groundHit, 8f, areaMask))
            groundY = groundHit.position.y;
        float groundDelta = groundY - visualBounds.min.y;
        // modelLocalOffset.y es una corrección pequeña, calibrada por arte,
        // expresada en espacio local del Player. Se aplica después de apoyar
        // el bounds porque la escala 4.5 del rig también debe afectarla.
        character.transform.position += Vector3.up * groundDelta;
        character.transform.localPosition += Vector3.up * authoredGroundOffset;
    }

    private static bool IsCombatParticipantForAbilities(PlayerController player)
    {
        if (player == null) return false;
        if (player.IsLocalTrainingParticipant) return true;
        return player.IsNetworkMatchParticipant && player.MatchReady;
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
            return localCombatantConfigured
                ? CharacterCatalog.Clamp(localCombatant.CharacterIndex)
                : Mathf.Clamp(PlayerPrefs.GetInt("SelectedCharacterIndex", 3), 0, CharacterNames.Length - 1);

        return Mathf.Clamp(CharacterIndex, 0, CharacterNames.Length - 1);
    }

    private string GetDisplayName()
    {
        if (Object == null)
            return localCombatantConfigured
                ? localCombatant.DisplayName
                : PlayerPrefs.GetString("PlayerName", "Player");
        string networkName = DisplayName.ToString();
        return string.IsNullOrWhiteSpace(networkName) ? "Player" : networkName;
    }

    private bool CanControlPlayer()
    {
        return Object == null || (IsNetworkMatchParticipant && HasStateAuthority);
    }

    private bool AcceptsHumanInput()
    {
        if (Object != null) return IsNetworkMatchParticipant && HasStateAuthority;
        return !localCombatantConfigured || localCombatant.Role == LocalCombatantRole.Human;
    }

    private AbilityDefinition GetAbility(AbilitySlot slot)
    {
        return CharacterCatalog.AbilityOf(GetSelectedCharacterIndex(), slot);
    }

    private float RemainingCooldown(AbilitySlot slot)
    {
        if (Object == null)
        {
            float readyAt = slot == AbilitySlot.Ultimate ? ultimateReadyAt : basicReadyAt;
            return Mathf.Max(0f, readyAt - Time.unscaledTime);
        }

        if (Runner == null) return 0f;
        TickTimer timer = slot == AbilitySlot.Ultimate ? UltimateCooldownTimer : BasicCooldownTimer;
        return Mathf.Max(0f, timer.RemainingTime(Runner).GetValueOrDefault());
    }

    private void StartCooldown(AbilitySlot slot, float duration)
    {
        if (Object == null)
        {
            if (slot == AbilitySlot.Ultimate) ultimateReadyAt = Time.unscaledTime + duration;
            else basicReadyAt = Time.unscaledTime + duration;
            return;
        }

        TickTimer timer = TickTimer.CreateFromSeconds(Runner, duration);
        if (slot == AbilitySlot.Ultimate) UltimateCooldownTimer = timer;
        else BasicCooldownTimer = timer;
    }

    private bool IsMovementBlocked()
    {
        if (Object == null)
            return Time.unscaledTime < localRootedUntil || Time.unscaledTime < localStunnedUntil;
        return TimerActive(RootTimer) || TimerActive(StunTimer);
    }

    private bool IsCastingBlocked()
    {
        if (Object == null)
            return Time.unscaledTime < localSilencedUntil || Time.unscaledTime < localStunnedUntil;
        return TimerActive(SilenceTimer) || TimerActive(StunTimer);
    }

    private float CurrentMovementMultiplier()
    {
        if (Object == null)
        {
            if (Time.unscaledTime < localHasteUntil) return 1.3f;
            if (Time.unscaledTime < localPickupHasteUntil) return 1.25f;
            if (Time.unscaledTime < localSlowedUntil) return 0.55f;
            return 1f;
        }

        if (TimerActive(HasteTimer)) return 1.3f;
        if (TimerActive(PickupHasteTimer)) return 1.25f;
        if (TimerActive(SlowTimer)) return 0.55f;
        return 1f;
    }

    private bool TimerActive(TickTimer timer)
    {
        return Runner != null && !timer.ExpiredOrNotRunning(Runner);
    }

    private string BuildCombatStatusText()
    {
        List<string> states = new List<string>();
        if (CurrentShield > 0) states.Add($"{GameLocalization.Choose("Barrera", "Shield")} {CurrentShield}");

        bool rooted = Object == null ? Time.unscaledTime < localRootedUntil : TimerActive(RootTimer);
        bool slowed = Object == null ? Time.unscaledTime < localSlowedUntil : TimerActive(SlowTimer);
        bool silenced = Object == null ? Time.unscaledTime < localSilencedUntil : TimerActive(SilenceTimer);
        bool stunned = Object == null ? Time.unscaledTime < localStunnedUntil : TimerActive(StunTimer);
        bool blinded = Object == null ? Time.unscaledTime < localBlindUntil : TimerActive(BlindTimer);
        bool revealed = Object == null ? Time.unscaledTime < localRevealUntil : TimerActive(RevealTimer);
        if (rooted) states.Add(GameLocalization.Choose("Inmovilizado", "Rooted"));
        if (slowed) states.Add(GameLocalization.Choose("Ralentizado", "Slowed"));
        if (silenced) states.Add(GameLocalization.Choose("Silenciado", "Silenced"));
        if (stunned) states.Add(GameLocalization.Choose("Aturdido", "Stunned"));
        if (blinded) states.Add(GameLocalization.Choose("Cegado", "Blinded"));
        if (revealed) states.Add(GameLocalization.Choose("Revelado", "Revealed"));
        if (HasPickupHaste) states.Add(GameLocalization.Choose("Semilla veloz", "Swift seed"));
        if (HasPowerPickup) states.Add(GameLocalization.Choose("Chispa de poder", "Power spark"));
        return string.Join(" · ", states);
    }

    public void GrantArenaPickup(ArenaPickupType type)
    {
        if (Object == null || HasStateAuthority) ApplyArenaPickup(type);
        else RPC_GrantArenaPickup((int)type);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_GrantArenaPickup(int type)
    {
        ApplyArenaPickup((ArenaPickupType)type);
    }

    private void ApplyArenaPickup(ArenaPickupType type)
    {
        switch (type)
        {
            case ArenaPickupType.Vitality:
                int healing = Mathf.CeilToInt(MaxHealth * 0.15f);
                if (Object == null) localHealth = Mathf.Min(MaxHealth, localHealth + healing);
                else NetworkHealth = Mathf.Min(MaxHealth, NetworkHealth + healing);
                break;
            case ArenaPickupType.Haste:
                if (Object == null) localPickupHasteUntil = Time.unscaledTime + 6f;
                else PickupHasteTimer = TickTimer.CreateFromSeconds(Runner, 6f);
                break;
            case ArenaPickupType.Power:
                if (Object == null) localPickupPowerUntil = Time.unscaledTime + 10f;
                else PickupPowerTimer = TickTimer.CreateFromSeconds(Runner, 10f);
                break;
        }
    }

    private bool ConsumePowerPickup()
    {
        if (!HasPowerPickup) return false;
        if (Object == null) localPickupPowerUntil = 0f;
        else PickupPowerTimer = default;
        return true;
    }

    public void ApplyCorruptionDamage(int damage)
    {
        if (damage <= 0 || IsDefeated) return;
        if (Object != null && !HasStateAuthority) return;
        if (Object == null) localHealth = Mathf.Max(0, localHealth - damage);
        else NetworkHealth = Mathf.Max(0, NetworkHealth - damage);
        CombatFeedbackController.PresentHit(this, damage, Vector3.up);
        if (CurrentHealth <= 0) controlsEnabled = false;
    }

    public ArenaPickupType NetworkPickupTypeAt(int pedestal)
    {
        int shift = Mathf.Clamp(pedestal, 0, 3) * 2;
        return (ArenaPickupType)((ArenaPickupTypes >> shift) & 0x3);
    }

    public void RequestArenaPickupClaim(int pedestal, int generation, int claimantPlayerId)
    {
        if (Object == null) return;
        RPC_RequestArenaPickupClaim(pedestal, generation, claimantPlayerId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestArenaPickupClaim(int pedestal, int generation, int claimantPlayerId)
    {
        if (!IsArenaCoordinator() || generation != ArenaPickupGeneration ||
            pedestal < 0 || pedestal > 3 || (ArenaPickupMask & (1 << pedestal)) == 0) return;

        PlayerController claimant = null;
        foreach (PlayerController candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!candidate.IsNetworkMatchParticipant ||
                candidate.Object.InputAuthority.PlayerId != claimantPlayerId) continue;
            claimant = candidate;
            break;
        }
        if (claimant == null || claimant.IsDefeated) return;
        Vector3 pedestalPosition = ArenaPowerUpManager.PedestalPosition(PlayerSpawner.ArenaCenter, pedestal);
        if (Vector3.Distance(claimant.transform.position, pedestalPosition) > 3.2f) return;

        claimant.GrantArenaPickup(NetworkPickupTypeAt(pedestal));
        ArenaPickupMask &= ~(1 << pedestal);
        ArenaNextPickupTimer = TickTimer.CreateFromSeconds(Runner, 20f);
    }

    private void UpdateArenaCoordinator()
    {
        if (!IsArenaCoordinator() || !MatchReady) return;
        if (!ArenaSystemsInitialized)
        {
            ArenaSystemsInitialized = true;
            ArenaPickupMask = 0;
            ArenaPickupTypes = 0;
            ArenaPickupGeneration = 0;
            ArenaCorruptionActive = false;
            ArenaCorruptionProgress = 0f;
            ArenaNextPickupTimer = TickTimer.CreateFromSeconds(Runner, 15f);
            ArenaCorruptionTimer = TickTimer.CreateFromSeconds(Runner, 180f);
        }

        int desired = MatchContext.TeamSize <= 1 ? 1 : 2;
        if (CountBits(ArenaPickupMask) < desired && ArenaNextPickupTimer.ExpiredOrNotRunning(Runner))
            SpawnNetworkPickups(desired);

        if (!ArenaCorruptionActive && ArenaCorruptionTimer.ExpiredOrNotRunning(Runner))
            ArenaCorruptionActive = true;
        if (ArenaCorruptionActive)
            ArenaCorruptionProgress = Mathf.Clamp01(ArenaCorruptionProgress + Runner.DeltaTime / 45f);
    }

    private bool IsArenaCoordinator()
    {
        return IsNetworkMatchParticipant && HasStateAuthority && Runner != null &&
               Runner.IsSharedModeMasterClient;
    }

    private void SpawnNetworkPickups(int desired)
    {
        int mask = ArenaPickupMask;
        int types = ArenaPickupTypes;
        int seed = ArenaPickupGeneration + 1;
        for (int offset = 0; offset < 4 && CountBits(mask) < desired; offset++)
        {
            int pedestal = (seed + offset) % 4;
            if ((mask & (1 << pedestal)) != 0) continue;
            int type = (seed + pedestal) % 3;
            int shift = pedestal * 2;
            types = (types & ~(0x3 << shift)) | (type << shift);
            mask |= 1 << pedestal;
        }
        ArenaPickupMask = mask;
        ArenaPickupTypes = types;
        ArenaPickupGeneration = seed;
        ArenaNextPickupTimer = default;
    }

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0) { count += value & 1; value >>= 1; }
        return count;
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

    public void PlayAgain()
    {
        if (Object == null && PlayModeContext.Current == PlayMode.Training)
        {
            CombatTrainingBootstrap training = FindFirstObjectByType<CombatTrainingBootstrap>();
            if (training != null) training.StartNewDuel();
            return;
        }

        ReturnToMultiplayerLobby();
    }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (automatedInputEnabled) return automatedMoveInput;
#endif
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

    public void SetAutomatedTestInput(Vector2 input)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        automatedInputEnabled = true;
        automatedMoveInput = Vector2.ClampMagnitude(input, 1f);
#endif
    }

    /// <summary>
    /// Ataque con teclado y ratón. Las escenas de arena no tienen AttackJoystick,
    /// y PlayerCombatController solo dispara ataques desde esos joysticks, así
    /// que sin esto no había ninguna forma de atacar en PC.
    /// Q o clic izquierdo: ataque básico. E o clic derecho: definitiva.
    /// Siempre se resuelven hacia el cursor.
    /// </summary>
    private void ProcessAttackInput()
    {
        bool basic = Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(0);
        bool ultimate = Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(1);
        if (!basic && !ultimate) return;
        if (IsPointerOverUI()) return;

        AbilitySlot slot = ultimate ? AbilitySlot.Ultimate : AbilitySlot.Basic;
        Vector3 direction = AssistAimDirection(GetAimDirectionFromCursor(), slot);
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
        if (!EnsureAgentOnNavMesh())
            return;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
        {
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 3f, agent.areaMask))
            {
                agent.updateRotation = !isAiming;
                agent.SetDestination(navHit.position);
            }
        }
    }

    /// <summary>
    /// Garantiza que el agente activo esté asociado a una superficie navegable
    /// antes de usar cualquiera de sus operaciones de movimiento. Es necesario
    /// tanto al cargar Movement directamente como al crear jugadores de Fusion.
    /// </summary>
    private bool EnsureAgentOnNavMesh()
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return false;

        if (agent.isOnNavMesh)
            return true;

        float recoveryRadius = Mathf.Max(12f, agent.height);
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, recoveryRadius, agent.areaMask))
            return false;

        return agent.Warp(hit.position) && agent.isOnNavMesh;
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
