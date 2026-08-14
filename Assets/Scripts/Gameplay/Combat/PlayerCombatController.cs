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
            IsAiming = true; // Activar estado de apuntado

            basicAimIndicator.ShowIndicators(attackRange);
            basicAimIndicator.UpdateAim(aimData.Direction);

            RotatePlayerTowards(aimData.Direction);
        }

        private void HandleBasicRelease(AimData aimData)
        {
            IsAiming = false; // Desactivar estado de apuntado

            basicAimIndicator.HideIndicators();

            Vector3 finalDirection = aimData.IsTap ? transform.forward : aimData.Direction;
            RotatePlayerTowards(finalDirection);

            ExecuteAttack(finalDirection);
        }

        private void ExecuteAttack(Vector3 direction)
        {
            Debug.Log($"Ataque Básico ejecutado hacia: {direction}");
        }
        #endregion

        #region Ultimate
        private void HandleUltimateAiming(AimData aimData)
        {
            IsAiming = true; // Activar estado de apuntado

            ultimateAimIndicator.ShowIndicators(ultimateRange);
            ultimateAimIndicator.UpdateAim(aimData.Direction);

            RotatePlayerTowards(aimData.Direction);
        }

        private void HandleUltimateRelease(AimData aimData)
        {
            IsAiming = false; // Desactivar estado de apuntado

            ultimateAimIndicator.HideIndicators();

            Vector3 finalDirection = aimData.IsTap ? transform.forward : aimData.Direction;
            RotatePlayerTowards(finalDirection);

            ExecuteUltimate(finalDirection);
        }

        private void ExecuteUltimate(Vector3 direction)
        {
            Debug.Log($"ULTIMATE ejecutada hacia: {direction}");
        }
        #endregion

        private void RotatePlayerTowards(Vector3 direction)
        {
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}