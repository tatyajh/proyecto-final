using System.Collections.Generic;
using UnityEngine;

public enum TrainingRunPhase
{
    Preview,
    Fighting,
    RoundWon,
    RoundLost,
    Complete
}

/// <summary>
/// Progresión de la Escalera de la Podredumbre. No guarda estadísticas ni
/// toca Fusion: cada ejecución local enfrenta una vez a los cinco personajes
/// distintos del elegido y delega el duelo real al bootstrap existente.
/// </summary>
[DisallowMultipleComponent]
public sealed class TrainingRunController : MonoBehaviour
{
    private static readonly float[] HealthMultipliers = { 1f, 1.04f, 1.08f, 1.12f, 1.15f };

    private readonly List<int> opponentOrder = new List<int>(5);
    private CombatTrainingBootstrap bootstrap;
    private PlayerController player;
    private PlayerController opponent;
    private int currentIndex;
    private int configuredPlayerIndex = -1;

    public TrainingRunPhase Phase { get; private set; } = TrainingRunPhase.Preview;
    public IReadOnlyList<int> OpponentOrder => opponentOrder;
    public int CurrentIndex => currentIndex;
    public int CurrentRivalNumber => Mathf.Clamp(currentIndex + 1, 1, Mathf.Max(1, opponentOrder.Count));
    public int TotalRivals => opponentOrder.Count;
    public int CurrentOpponentIndex => opponentOrder.Count == 0
        ? -1
        : opponentOrder[Mathf.Clamp(currentIndex, 0, opponentOrder.Count - 1)];
    public int NextOpponentIndex => currentIndex + 1 < opponentOrder.Count
        ? opponentOrder[currentIndex + 1]
        : -1;
    public PlayerController Opponent => opponent;
    public bool OverlayVisible => Phase != TrainingRunPhase.Fighting;

    public void Initialize(CombatTrainingBootstrap owner, PlayerController human)
    {
        if (owner == null || human == null) return;
        bootstrap = owner;
        player = human;
        int playerIndex = human.SelectedCharacterIndex;
        if (configuredPlayerIndex == playerIndex && opponentOrder.Count == CharacterCatalog.Count - 1)
            return;

        configuredPlayerIndex = playerIndex;
        BuildOrder(playerIndex);
        currentIndex = 0;
        opponent = null;
        Phase = TrainingRunPhase.Preview;
        player.SetLocalControlsEnabled(false);
    }

    private void BuildOrder(int playerIndex)
    {
        opponentOrder.Clear();
        for (int i = 0; i < CharacterCatalog.Count; i++)
            if (i != playerIndex) opponentOrder.Add(i);

        for (int i = opponentOrder.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (opponentOrder[i], opponentOrder[swap]) = (opponentOrder[swap], opponentOrder[i]);
        }
    }

    public void BeginOrContinue()
    {
        if (bootstrap == null || player == null || opponentOrder.Count == 0) return;
        if (Phase == TrainingRunPhase.RoundWon)
        {
            currentIndex++;
            if (currentIndex >= opponentOrder.Count)
            {
                Phase = TrainingRunPhase.Complete;
                return;
            }
        }
        if (Phase != TrainingRunPhase.Preview && Phase != TrainingRunPhase.RoundWon) return;
        BeginCurrentRival();
    }

    public void RetryCurrent()
    {
        if (Phase != TrainingRunPhase.RoundLost && Phase != TrainingRunPhase.Preview) return;
        BeginCurrentRival();
    }

    public void RestartRun()
    {
        if (player == null) return;
        BuildOrder(player.SelectedCharacterIndex);
        currentIndex = 0;
        opponent = null;
        Phase = TrainingRunPhase.Preview;
        player.SetLocalControlsEnabled(false);
        bootstrap?.ClearOpponent();
    }

    public void RetryOrStartCurrent()
    {
        if (Phase == TrainingRunPhase.RoundLost) RetryCurrent();
        else if (Phase == TrainingRunPhase.Preview) BeginCurrentRival();
        else if (Phase == TrainingRunPhase.RoundWon) BeginOrContinue();
        else if (Phase == TrainingRunPhase.Complete) RestartRun();
    }

    private void BeginCurrentRival()
    {
        int rival = CurrentOpponentIndex;
        if (rival < 0) return;
        Phase = TrainingRunPhase.Fighting;
        opponent = null;
        float health = HealthMultipliers[Mathf.Clamp(currentIndex, 0, HealthMultipliers.Length - 1)];
        bootstrap.BeginTowerDuel(rival, currentIndex + 1, health);
    }

    public void BindOpponent(PlayerController value)
    {
        opponent = value;
    }

    private void Update()
    {
        if (Phase != TrainingRunPhase.Fighting || player == null || opponent == null) return;
        if (player.IsDefeated)
        {
            Phase = TrainingRunPhase.RoundLost;
            bootstrap?.PauseDuel();
            return;
        }
        if (!opponent.IsDefeated) return;

        Phase = currentIndex >= opponentOrder.Count - 1
            ? TrainingRunPhase.Complete
            : TrainingRunPhase.RoundWon;
        bootstrap?.PauseDuel();
    }
}
