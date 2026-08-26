using Gameplay.Combat;
using UnityEngine;

/// <summary>
/// Rival local por planes cortos: no copia cada desplazamiento del jugador,
/// alterna aproximación, órbita, retirada y disputa de power-ups.
/// </summary>
[DisallowMultipleComponent]
public sealed class TrainingBotController : MonoBehaviour
{
    private enum BotPlan { Approach, Strafe, Retreat, SeekPickup }

    [SerializeField, Min(0.05f)] private float decisionInterval = 0.25f;
    [SerializeField, Min(0f)] private float reactionDelay = 0.6f;
    [SerializeField, Min(0f)] private float openingGracePeriod = 4f;
    [SerializeField, Range(0f, 25f)] private float aimErrorDegrees = 6f;
    [SerializeField, Min(1f)] private float personalSpaceDistance = 6f;
    [SerializeField, Range(0.5f, 1f)] private float movementSpeedMultiplier = 0.82f;
    [SerializeField, Range(0f, 1f)] private float pickupSeekChance = 0.28f;
    [SerializeField, Range(0f, 1f)] private float ultimateUseChance = 0.72f;

    private PlayerController bot;
    private PlayerController target;
    private BotPlan plan;
    private Vector3 planDestination;
    private float nextDecisionAt;
    private float nextCastAt;
    private float nextPlanAt;
    private float strafeSign = 1f;
    private bool retreatAfterCast;

    public void Configure(PlayerController owner, PlayerController opponent)
    {
        bot = owner;
        target = opponent;
        float start = Time.time + openingGracePeriod;
        nextDecisionAt = start;
        nextCastAt = start;
        nextPlanAt = start;
        plan = BotPlan.Strafe;
    }

    /// <summary>
    /// La torre aumenta lectura y ejecución, nunca el daño. El nivel se recibe
    /// en el rango 1..5 y también es útil desde herramientas de testing.
    /// </summary>
    public void ConfigureDifficulty(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        decisionInterval = Mathf.Lerp(0.38f, 0.22f, t);
        reactionDelay = Mathf.Lerp(0.86f, 0.36f, t);
        openingGracePeriod = Mathf.Lerp(4f, 2.8f, t);
        aimErrorDegrees = Mathf.Lerp(9f, 2f, t);
        movementSpeedMultiplier = Mathf.Lerp(0.78f, 0.94f, t);
        pickupSeekChance = Mathf.Lerp(0.16f, 0.48f, t);
        ultimateUseChance = Mathf.Lerp(0.52f, 0.96f, t);
    }

    private void Awake()
    {
        if (bot == null) bot = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (bot == null || target == null || bot.IsDefeated || target.IsDefeated)
        {
            bot?.StopBotMovement();
            return;
        }
        if (Time.time < nextDecisionAt) return;
        nextDecisionAt = Time.time + decisionInterval;

        Vector3 toTarget = Vector3.ProjectOnPlane(target.transform.position - bot.transform.position, Vector3.up);
        float distance = toTarget.magnitude;
        if (distance < 0.05f) return;
        Vector3 direction = toTarget / distance;

        if (Time.time >= nextPlanAt)
            ChoosePlan(direction, distance);
        ExecutePlan(direction);
        TryCast(direction, distance);
    }

    private void ChoosePlan(Vector3 direction, float distance)
    {
        nextPlanAt = Time.time + Random.Range(1.5f, 2.5f);
        strafeSign = Random.value < 0.5f ? -1f : 1f;

        if (ArenaPowerUpManager.Instance != null &&
            ArenaPowerUpManager.Instance.TryGetBestPickup(bot, out Vector3 pickup) &&
            (bot.CurrentHealth <= bot.HealthMaximum * 0.62f || Random.value < pickupSeekChance))
        {
            plan = BotPlan.SeekPickup;
            planDestination = pickup;
            return;
        }

        if (retreatAfterCast || distance < personalSpaceDistance)
        {
            retreatAfterCast = false;
            plan = BotPlan.Retreat;
            Vector3 tangent = Vector3.Cross(Vector3.up, direction) * strafeSign;
            planDestination = bot.transform.position - direction * Random.Range(4f, 6f) + tangent * 3f;
            return;
        }

        float preferred = Mathf.Max(6.5f, bot.BasicAbilityRange * 0.9f);
        if (distance > preferred * 1.25f)
        {
            plan = BotPlan.Approach;
            Vector3 tangent = Vector3.Cross(Vector3.up, direction) * strafeSign;
            planDestination = target.transform.position - direction * preferred + tangent * Random.Range(1f, 3f);
        }
        else
        {
            plan = BotPlan.Strafe;
            Vector3 tangent = Vector3.Cross(Vector3.up, direction) * strafeSign;
            planDestination = bot.transform.position + tangent * Random.Range(3.5f, 5.5f);
        }
    }

    private void ExecutePlan(Vector3 direction)
    {
        if (plan == BotPlan.Approach)
        {
            float preferred = Mathf.Max(6.5f, bot.BasicAbilityRange * 0.9f);
            planDestination = target.transform.position - direction * preferred;
        }
        bot.TrySetBotDestination(planDestination, movementSpeedMultiplier);
    }

    private void TryCast(Vector3 direction, float distance)
    {
        if (Time.time < nextCastAt || plan == BotPlan.Retreat) return;
        AbilitySlot? slot = null;
        if (bot.UltimateCooldownRemaining <= 0f && distance <= bot.UltimateAbilityRange * 1.05f &&
            Random.value <= ultimateUseChance)
            slot = AbilitySlot.Ultimate;
        else if (bot.BasicCooldownRemaining <= 0f && distance <= bot.BasicAbilityRange * 1.05f)
            slot = AbilitySlot.Basic;
        if (!slot.HasValue) return;

        Vector3 aim = Quaternion.AngleAxis(Random.Range(-aimErrorDegrees, aimErrorDegrees), Vector3.up) * direction;
        bool cast = bot.TryCastAbility(slot.Value, new AimData
        {
            Direction = aim,
            DistanceRatio = Mathf.Clamp01(distance / Mathf.Max(1f,
                slot.Value == AbilitySlot.Ultimate ? bot.UltimateAbilityRange : bot.BasicAbilityRange)),
            IsTap = false
        });
        if (!cast) return;

        nextCastAt = Time.time + reactionDelay + Random.Range(0.1f, 0.35f);
        retreatAfterCast = true;
        nextPlanAt = Mathf.Min(nextPlanAt, Time.time + Random.Range(0.35f, 0.7f));
    }

    private void OnDisable()
    {
        bot?.StopBotMovement();
    }
}
