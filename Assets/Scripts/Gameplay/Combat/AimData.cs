using UnityEngine;

namespace Gameplay.Combat
{
    public struct AimData
    {
        public Vector3 Direction;   // Dirección en plano 3D (X, Z)
        public float DistanceRatio; // Fuerza del drag (0.0 a 1.0)
        public bool IsTap;         // True si fue un toque rápido sin arrastrar
    }

    public enum AbilityType
    {
        BasicAttack,
        Ultimate
    }
}