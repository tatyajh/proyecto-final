using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private VirtualJoystick joystick;
    [SerializeField] private LayerMask groundLayer;

    [Header("Configuración de Movimiento")]
    [SerializeField] private float moveSpeed = 6.0f;
    [SerializeField] private float rotationSpeed = 10.0f;

    private bool isDirectControlActive = false;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
    }

    private void Update()
    {
        //// importantísimo para el multiplayer NO borrar nunca
        if (!HasStateAuthority) return;

        Vector2 inputDir = GetInputVector();

        // 1. CONTROL DIRECTO (WASD o Joystick Táctil)
        if (inputDir.magnitude > 0.1f)
        {
            isDirectControlActive = true;
            agent.ResetPath(); // Cancela la ruta actual de Click-to-Move

            // Convertir la entrada 2D a dirección 3D según la cámara
            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 cameraRight = Camera.main.transform.right;
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 moveDirection = (cameraForward * inputDir.y + cameraRight * inputDir.x).normalized;

            // Desplazar al personaje mediante NavMeshAgent.Move (respeta la malla y colisiones)
            agent.Move(moveDirection * moveSpeed * Time.deltaTime);

            // Rotar progresivamente hacia donde se mueve
            if (moveDirection != Vector3.zero)
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

            // 2. CLICK / TAP TO MOVE (Solo si no hay entrada de WASD/Joystick y no se hace click sobre UI)
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                ProcessClickToMove();
            }
        }
    }

    private Vector2 GetInputVector()
    {
        // Entrada por Teclado WASD / Flechas
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 keyboardInput = new Vector2(h, v).normalized;

        // Entrada del Joystick (Mobile)
        Vector2 joystickInput = joystick != null ? joystick.InputVector : Vector2.zero;

        // Priorizar el input que tenga mayor magnitud
        return keyboardInput.magnitude > joystickInput.magnitude ? keyboardInput : joystickInput;
    }

    private void ProcessClickToMove()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
        {
            agent.SetDestination(hit.point);
        }
    }

    private bool IsPointerOverUI()
    {
        // Evita activar Click-to-Move si el jugador toca la interfaz (botones, joystick, etc.)
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0)
        {
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }
        return EventSystem.current.IsPointerOverGameObject();
    }
}