using UnityEngine;
using UIScripts;

namespace Gameplay.Combat
{
    public class PlayerCombatController : MonoBehaviour
    {
        [Header("Joysticks (UI)")]
        [SerializeField] private AttackJoystick basicAttackJoystick;
        [SerializeField] private AttackJoystick ultimateJoystick;

        [Header("Aim Indicators (In-Game Floor)")]
        [SerializeField] private SkillAimIndicator basicAimIndicator;
        [SerializeField] private SkillAimIndicator ultimateAimIndicator;

        [Header("Attack Settings")]
        [SerializeField] private float attackRange = 5f;
        [SerializeField] private float ultimateRange = 8f;

        // 🎯 Flag para saber si el jugador está apuntando
        public bool IsAiming { get; private set; }
        private PlayerController player;
        private bool isSubscribed;

        public void Bind(PlayerController owner)
        {
            UnsubscribeFromJoysticks();
            player = owner;
            ResolveSceneReferences();

            if (isActiveAndEnabled)
            {
                SubscribeToJoysticks();
            }
        }

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            ResolveSceneReferences();
        }

        private void ResolveSceneReferences()
        {
            if (basicAttackJoystick == null || ultimateJoystick == null)
            {
                foreach (AttackJoystick candidate in FindObjectsByType<AttackJoystick>(FindObjectsSortMode.None))
                {
                    if (candidate.name.ToLowerInvariant().Contains("ultimate")) ultimateJoystick = candidate;
                    else basicAttackJoystick = candidate;
                }
            }

            if (basicAimIndicator == null || ultimateAimIndicator == null)
            {
                foreach (SkillAimIndicator candidate in FindObjectsByType<SkillAimIndicator>(FindObjectsSortMode.None))
                {
                    if (candidate.name.ToLowerInvariant().Contains("ultimate")) ultimateAimIndicator = candidate;
                    else basicAimIndicator = candidate;
                }
            }

            if (basicAimIndicator == null || !basicAimIndicator.IsConfigured)
                basicAimIndicator = CreateRuntimeAimIndicator("Basic Aim Indicator", new Color(0.83f, 0.64f, 0.20f, 0.9f));
            if (ultimateAimIndicator == null || !ultimateAimIndicator.IsConfigured)
                ultimateAimIndicator = CreateRuntimeAimIndicator("Ultimate Aim Indicator", new Color(0.62f, 0.22f, 0.48f, 0.92f));
        }

        private SkillAimIndicator CreateRuntimeAimIndicator(string indicatorName, Color color)
        {
            GameObject root = new GameObject(indicatorName);
            root.transform.SetParent(transform, false);

            GameObject range = new GameObject("Range");
            range.transform.SetParent(root.transform, false);
            LineRenderer rangeLine = range.AddComponent<LineRenderer>();
            ConfigureLine(rangeLine, color, true);

            GameObject direction = new GameObject("Direction");
            direction.transform.SetParent(root.transform, false);
            LineRenderer directionLine = direction.AddComponent<LineRenderer>();
            ConfigureLine(directionLine, color, false);

            SkillAimIndicator indicator = root.AddComponent<SkillAimIndicator>();
            indicator.Configure(range, direction);
            return indicator;
        }

        private static void ConfigureLine(LineRenderer line, Color color, bool circle)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            line.sharedMaterial = new Material(shader) { color = color };
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = circle ? 0.08f : 0.13f;
            line.useWorldSpace = false;
            line.loop = circle;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;

            if (!circle)
            {
                line.positionCount = 2;
                line.SetPosition(0, Vector3.zero);
                line.SetPosition(1, Vector3.forward);
                return;
            }

            const int segments = 64;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }
        }

        private void OnEnable()
        {
            ResolveSceneReferences();
            SubscribeToJoysticks();
        }

        private void SubscribeToJoysticks()
        {
            if (isSubscribed) return;

            if (basicAttackJoystick != null)
            {
                basicAttackJoystick.OnAiming += HandleBasicAiming;
                basicAttackJoystick.OnReleased += HandleBasicRelease;
            }

            if (ultimateJoystick != null)
            {
                ultimateJoystick.OnAiming += HandleUltimateAiming;
                ultimateJoystick.OnReleased += HandleUltimateRelease;
            }

            isSubscribed = basicAttackJoystick != null || ultimateJoystick != null;
        }

        private void OnDisable()
        {
            UnsubscribeFromJoysticks();
        }

        private void UnsubscribeFromJoysticks()
        {
            if (basicAttackJoystick != null)
            {
                basicAttackJoystick.OnAiming -= HandleBasicAiming;
                basicAttackJoystick.OnReleased -= HandleBasicRelease;
            }

            if (ultimateJoystick != null)
            {
                ultimateJoystick.OnAiming -= HandleUltimateAiming;
                ultimateJoystick.OnReleased -= HandleUltimateRelease;
            }

            isSubscribed = false;
        }

        #region Basic Attack
        private void HandleBasicAiming(AimData aimData)
        {
            if (!CanUseControls()) return;
            IsAiming = true; // Activar estado de apuntado

            if (basicAimIndicator != null)
            {
                basicAimIndicator.ShowIndicators(attackRange);
                basicAimIndicator.UpdateAim(aimData.Direction);
            }

            RotatePlayerTowards(aimData.Direction);
        }

        private void HandleBasicRelease(AimData aimData)
        {
            if (!CanUseControls()) return;
            IsAiming = false; // Desactivar estado de apuntado

            if (basicAimIndicator != null) basicAimIndicator.HideIndicators();

            Vector3 finalDirection = aimData.IsTap ? transform.forward : aimData.Direction;
            RotatePlayerTowards(finalDirection);

            ExecuteAttack(finalDirection);
        }

        private void ExecuteAttack(Vector3 direction)
        {
            player?.TryExecuteAttack(direction, false);
        }
        #endregion

        #region Ultimate
        private void HandleUltimateAiming(AimData aimData)
        {
            if (!CanUseControls()) return;
            IsAiming = true; // Activar estado de apuntado

            if (ultimateAimIndicator != null)
            {
                ultimateAimIndicator.ShowIndicators(ultimateRange);
                ultimateAimIndicator.UpdateAim(aimData.Direction);
            }

            RotatePlayerTowards(aimData.Direction);
        }

        private void HandleUltimateRelease(AimData aimData)
        {
            if (!CanUseControls()) return;
            IsAiming = false; // Desactivar estado de apuntado

            if (ultimateAimIndicator != null) ultimateAimIndicator.HideIndicators();

            Vector3 finalDirection = aimData.IsTap ? transform.forward : aimData.Direction;
            RotatePlayerTowards(finalDirection);

            ExecuteUltimate(finalDirection);
        }

        private void ExecuteUltimate(Vector3 direction)
        {
            player?.TryExecuteAttack(direction, true);
        }
        #endregion

        private void RotatePlayerTowards(Vector3 direction)
        {
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private bool CanUseControls()
        {
            return player != null && player.HasLocalControl && !player.IsDefeated;
        }
    }
}
