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

        public void Bind(PlayerController owner)
        {
            player = owner;
            ResolveSceneReferences();
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
        }

        private void OnEnable()
        {
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
        }

        private void OnDisable()
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
