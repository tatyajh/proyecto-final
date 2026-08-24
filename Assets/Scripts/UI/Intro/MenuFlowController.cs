using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Máquina de estados de las fases del menú (spec 01/02 del paquete de menú),
/// extraída de BlightedIntroFlow para que la vista no sea también el árbitro
/// de las transiciones.
///
/// Concentra las tres defensas que se fueron ganando a pulso contra la
/// condición de carrera de los fundidos:
/// 1. transitionInProgress: un GoTo durante una transición se ignora.
/// 2. Kill(true): interrumpir una transición la completa primero, así ningún
///    CanvasGroup queda a media opacidad.
/// 3. ForcePhaseState: antes de cada cruce, toda fase que no sea la actual ni
///    la destino se fuerza a oculta, sin confiar en la cadena de fundidos.
///
/// La vista se entera por eventos: TransitionStarted (ocultar botones de
/// esquina), PhaseChanged (punto medio, cambiar atmósfera) y PhaseShown
/// (fundido completo, reactivar lo que corresponda).
/// </summary>
public sealed class MenuFlowController<TPhase> where TPhase : struct, Enum
{
    private readonly Dictionary<TPhase, CanvasGroup> phases = new Dictionary<TPhase, CanvasGroup>();
    private readonly float crossFadeSeconds;
    private Sequence transition;

    public TPhase Current { get; private set; }
    public bool TransitionInProgress { get; private set; }

    public event Action<TPhase> TransitionStarted;
    public event Action<TPhase> PhaseChanged;
    public event Action<TPhase> PhaseShown;

    public MenuFlowController(float crossFadeSeconds)
    {
        this.crossFadeSeconds = crossFadeSeconds;
    }

    public void Register(TPhase phase, CanvasGroup group)
    {
        phases[phase] = group;
    }

    public CanvasGroup GroupOf(TPhase phase) => phases[phase];

    public void HideAll()
    {
        foreach (CanvasGroup group in phases.Values) UITween.SnapHidden(group);
    }

    /// <summary>Muestra una fase al instante, sin fundido (p. ej. al volver de partida).</summary>
    public void SnapTo(TPhase phase)
    {
        Current = phase;
        ForcePhaseState(phase, phase);
        CanvasGroup group = phases[phase];
        group.alpha = 1f;
        UITween.SetInteractive(group, true);
        PhaseShown?.Invoke(phase);
    }

    public void GoTo(TPhase next)
    {
        if (TransitionInProgress) return;
        TransitionInProgress = true;

        if (transition != null && transition.IsActive()) transition.Kill(true);

        ForcePhaseState(Current, next);

        CanvasGroup from = phases[Current];
        CanvasGroup to = phases[next];

        TransitionStarted?.Invoke(next);

        transition = UITween.Sequence();
        transition.Append(UITween.Fade(from, 0f, crossFadeSeconds * 0.5f));
        transition.AppendCallback(() =>
        {
            Current = next;
            PhaseChanged?.Invoke(next);
        });
        transition.Append(UITween.Fade(to, 1f, crossFadeSeconds));
        transition.OnComplete(() =>
        {
            transition = null;
            TransitionInProgress = false;
            PhaseShown?.Invoke(next);
        });
    }

    private void ForcePhaseState(TPhase current, TPhase next)
    {
        foreach (KeyValuePair<TPhase, CanvasGroup> entry in phases)
        {
            if (entry.Key.Equals(current) || entry.Key.Equals(next)) continue;
            entry.Value.alpha = 0f;
            UITween.SetInteractive(entry.Value, false);
        }
    }

    /// <summary>Corta la transición viva; llamar desde OnDestroy de la vista.</summary>
    public void Kill()
    {
        if (transition != null && transition.IsActive()) transition.Kill();
        transition = null;
        TransitionInProgress = false;
    }
}
