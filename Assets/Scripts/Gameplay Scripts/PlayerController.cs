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
    private int prototypeCharacterIndex = -1;

    [Networked] public int NetworkHealth { get; private set; }
    [Networked] public NetworkBool CombatReady { get; private set; }
    [Networked, Capacity(24)] public NetworkString<_32> DisplayName { get; private set; }

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
        }
        ApplyPlayerColor();
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

        if (Object != null && !OnlineMatchState.CanPlay)
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

        if (Object != null)
            RPC_ShowAttackFeedback(direction.normalized, ultimate);
        else
            ShowAttackFeedback(direction.normalized, ultimate);

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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowAttackFeedback(Vector3 direction, bool ultimate)
    {
        ShowAttackFeedback(direction, ultimate);
    }

    private void ShowAttackFeedback(Vector3 direction, bool ultimate)
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
        line.startWidth = ultimate ? 0.45f : 0.25f;
        line.endWidth = ultimate ? 0.16f : 0.08f;
        line.positionCount = 2;
        line.useWorldSpace = true;

        Vector3 origin = transform.position + Vector3.up * 0.8f;
        float range = ultimate ? 8f : 5f;
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + direction.normalized * range);

        Destroy(feedback, 0.2f);
        if (feedbackMaterial != null) Destroy(feedbackMaterial, 0.25f);
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

        // Por ahora solo existe un modelo de personaje real en el repositorio.
        // Se utiliza para Heliandra y se conserva la silueta provisional para
        // las demás selecciones hasta que arte entregue sus respectivos FBX.
        if (characterIndex == 0 && TryCreateImportedCharacterVisual())
            return;

        prototypeVisual = new GameObject($"Prototype {CharacterNames[characterIndex]}").transform;
        prototypeVisual.SetParent(transform, false);
        prototypeVisual.localPosition = Vector3.zero;

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

    private bool TryCreateImportedCharacterVisual()
    {
        GameObject characterPrefab = Resources.Load<GameObject>("Characters/CampanaPrototype");
        if (characterPrefab == null) return false;

        GameObject instance = Instantiate(characterPrefab, transform);
        instance.name = "Prototype Heliandra";
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        foreach (Collider visualCollider in instance.GetComponentsInChildren<Collider>(true))
            Destroy(visualCollider);

        prototypeVisual = instance.transform;
        FitImportedCharacterToCollider(instance);
        return true;
    }

    private void FitImportedCharacterToCollider(GameObject character)
    {
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        if (bounds.size.y <= 0.001f) return;

        const float desiredHeight = 2f;
        float scale = desiredHeight / bounds.size.y;
        character.transform.localScale *= scale;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float colliderBottom = transform.position.y - 1f;
        character.transform.position += Vector3.up * (colliderBottom - bounds.min.y);
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
        if (Object == null || Object.HasInputAuthority)
            return Mathf.Clamp(PlayerPrefs.GetInt("SelectedCharacterIndex", 0), 0, CharacterNames.Length - 1);

        return Mathf.Abs(Object.InputAuthority.PlayerId) % CharacterNames.Length;
    }

    private string GetDisplayName()
    {
        if (Object == null) return PlayerPrefs.GetString("PlayerName", "Player");
        string networkName = DisplayName.ToString();
        return string.IsNullOrWhiteSpace(networkName) ? "Player" : networkName;
    }

    private void DrawPlayerLabels(float scale, float width, GUIStyle label)
    {
        Camera cameraToUse = Camera.main;
        if (cameraToUse == null) return;

        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!player.gameObject.activeInHierarchy) continue;
            Vector3 screenPoint = cameraToUse.WorldToScreenPoint(player.transform.position + Vector3.up * 2.4f);
            if (screenPoint.z <= 0f) continue;

            float x = screenPoint.x / scale;
            float y = (Screen.height - screenPoint.y) / scale;
            float healthRatio = Mathf.Clamp01(player.CurrentHealth / (float)MaxHealth);
            GUI.Label(new Rect(x - 90f, y - 30f, 180f, 25f), player.GetDisplayName(), label);
            GUI.Box(new Rect(x - 65f, y, 130f, 14f), string.Empty);
            Color previous = GUI.color;
            GUI.color = healthRatio > 0.35f ? new Color(0.25f, 0.72f, 0.38f) : new Color(0.78f, 0.22f, 0.22f);
            GUI.DrawTexture(new Rect(x - 62f, y + 3f, 124f * healthRatio, 8f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }

    private void OnGUI()
    {
        if (!CanControlPlayer()) return;

        float scale = Mathf.Clamp(Screen.width / 960f, 0.8f, 1.6f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        GUIStyle playerLabel = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        playerLabel.normal.textColor = new Color(0.94f, 0.91f, 0.82f);

        GUIStyle hudLabel = new GUIStyle(playerLabel)
        {
            fontSize = 18
        };
        hudLabel.normal.textColor = new Color(0.86f, 0.67f, 0.24f);

        DrawPlayerLabels(scale, width, playerLabel);

        if (GUI.Button(new Rect(18f, 18f, 150f, 46f), "Salir al menú"))
        {
            ReturnToMainMenu();
            return;
        }

        GUI.Box(new Rect(width * 0.5f - 150f, 18f, 300f, 42f), $"VIDA  {CurrentHealth} / {MaxHealth}");
        float basicCooldown = Mathf.Max(0f, basicReadyAt - Time.unscaledTime);
        float ultimateCooldown = Mathf.Max(0f, ultimateReadyAt - Time.unscaledTime);
        GUI.Box(new Rect(width * 0.5f - 255f, 62f, 510f, 34f), string.Empty);
        GUI.Label(new Rect(width * 0.5f - 245f, 62f, 490f, 34f),
            $"Ataque: {(basicCooldown <= 0f ? "LISTO" : basicCooldown.ToString("0.0"))}    Ultimate: {(ultimateCooldown <= 0f ? "LISTA" : ultimateCooldown.ToString("0.0"))}",
            hudLabel);
        if (Object != null && !string.IsNullOrWhiteSpace(OnlineMatchState.Message))
        {
            GUIStyle statusLabel = new GUIStyle(hudLabel)
            {
                fontSize = 16,
                wordWrap = true
            };
            GUI.Box(new Rect(width * 0.5f - 300f, 102f, 600f, 58f), string.Empty);
            GUI.Label(new Rect(width * 0.5f - 290f, 104f, 580f, 54f), OnlineMatchState.Message, statusLabel);
        }

        string result = IsDefeated ? "DERROTA" : HasDefeatedOpponent() ? "VICTORIA" : string.Empty;
        if (!string.IsNullOrEmpty(result))
        {
            controlsEnabled = false;
            if (Object != null)
                OnlineMatchState.Set(OnlineMatchPhase.Finished, result);
            GUI.Box(new Rect(width * 0.5f - 180f, 125f, 360f, 90f), string.Empty);
            GUI.Label(new Rect(width * 0.5f - 180f, 135f, 360f, 60f), result, hudLabel);
            if (GUI.Button(new Rect(width * 0.5f - 195f, 220f, 185f, 48f), "Jugar otra vez"))
                ReturnToMultiplayerLobby();
            if (GUI.Button(new Rect(width * 0.5f + 10f, 220f, 185f, 48f), "Salir al menú"))
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

    private async void ReturnToMultiplayerLobby()
    {
        controlsEnabled = false;

        if (Object != null && Runner != null && Runner.IsRunning)
            await Runner.Shutdown();

        OnlineMatchState.Reset();
        SceneManager.LoadScene("MultiplayerMenu");
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
