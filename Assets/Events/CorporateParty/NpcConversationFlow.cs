using System;
using System.Collections;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.UI;
using UnityEngine;

/// <summary>
/// Minigame de conversa de um único NPC na Festa Corporativa.
/// Exibe o diálogo de abertura do NPC, apresenta a roda de abordagens
/// (desabilitando as já usadas com esse NPC nesta instância da festa)
/// e calcula o delta de relação ao confirmar a escolha.
///
/// Protocolo de uso:
///   1. Chame Begin() passando o NPC alvo, o histórico de abordagens usadas e o callback.
///   2. Aguarde: o flow manipula a UI via PartyHudPresenter e chama onFinished ao concluir.
/// </summary>
public class NpcConversationFlow : MonoBehaviour
{
    [Header("Tempo de feedback visual")]
    [SerializeField] private float feedbackDisplayDuration = 1.2f;

    // Referência ao HUD injetada pelo PartyEventController
    private PartyHudPresenter hud;
    private Coroutine activeRoutine;
    private Action<ApproachStyle> activeApproachListener;

    // Diálogo de abertura por abordagem (configurável no Inspector ou via código).
    // Pode ser expandido futuramente com ScriptableObject de diálogos por NPC.
    [Header("Diálogos de Abertura (placeholder genérico)")]
    [SerializeField] private string defaultOpeningLine = "Que bom te ver por aqui, {nome}.";

    public void SetHud(PartyHudPresenter hudPresenter) => hud = hudPresenter;

    /// <summary>
    /// Inicia o fluxo de conversa com o NPC.
    /// </summary>
    /// <param name="npc">Definição do NPC com quem o jogador está conversando.</param>
    /// <param name="usedApproaches">Conjunto de abordagens já usadas com esse NPC nesta instância.</param>
    /// <param name="onFinished">Callback com (npcId, abordagemUsada, deltaRelação).</param>
    public void Begin(
        NpcDefinition npc,
        HashSet<ApproachStyle> usedApproaches,
        Action<string, ApproachStyle, int> onFinished)
    {
        Cancel();
        activeRoutine = StartCoroutine(ConversationRoutine(npc, usedApproaches, onFinished));
    }

    /// <summary>
    /// Cancela a conversa em andamento e fecha as UIs sem consumir ânimo nem aplicar efeitos.
    /// </summary>
    public void Cancel()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (hud != null && activeApproachListener != null)
        {
            hud.OnApproachSelected -= activeApproachListener;
            activeApproachListener = null;
        }

        hud?.HideWheel();
        hud?.HideBubble();
        hud?.HideBackButton();
    }

    private IEnumerator ConversationRoutine(
        NpcDefinition npc,
        HashSet<ApproachStyle> usedApproaches,
        Action<string, ApproachStyle, int> onFinished)
    {
        if (hud == null)
        {
            Debug.LogWarning("[NpcConversationFlow] PartyHudPresenter não atribuído. Abortando conversa.");
            onFinished?.Invoke(npc != null ? npc.id : string.Empty, ApproachStyle.Persuasivo, 0);
            yield break;
        }

        // Monta o array de abordagens já usadas (indexado por (int)ApproachStyle)
        bool[] used = new bool[4];
        foreach (var a in usedApproaches)
            used[(int)a] = true;

        // Exibe fala de abertura do NPC e botão de voltar
        string opening = BuildOpeningLine(npc);
        hud.ShowNpcSpeech(npc != null ? npc.displayName : "???", opening);
        hud.ShowWheel(used);
        hud.ShowBackButton();

        // Aguarda o jogador escolher uma abordagem via roda de interação
        ApproachStyle? chosenApproach = null;
        activeApproachListener = style => chosenApproach = style;
        hud.OnApproachSelected += activeApproachListener;

        while (chosenApproach == null)
        {
            // Atalhos de teclado para agilidade e acessibilidade
            if ((Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) && !used[0])
            {
                hud.HideWheel();
                chosenApproach = ApproachStyle.Arrogante;
            }
            else if ((Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) && !used[1])
            {
                hud.HideWheel();
                chosenApproach = ApproachStyle.Brincalhao;
            }
            else if ((Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) && !used[2])
            {
                hud.HideWheel();
                chosenApproach = ApproachStyle.Persuasivo;
            }
            else if ((Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) && !used[3])
            {
                hud.HideWheel();
                chosenApproach = ApproachStyle.Romantico;
            }

            yield return null;
        }

        hud.OnApproachSelected -= activeApproachListener;
        activeApproachListener = null;

        hud.HideBackButton();
        hud.HideBubble();

        // Calcula delta de relação
        int delta = npc != null ? npc.GetApproachDelta(chosenApproach.Value) : 0;

        // Exibe feedback visual momentâneo
        string feedbackText = FormatFeedback(delta);
        hud.ShowFeedback(feedbackText);
        yield return new WaitForSeconds(feedbackDisplayDuration);
        hud.HideFeedback();

        activeRoutine = null;
        string npcId = npc != null ? (!string.IsNullOrEmpty(npc.id) ? npc.id : npc.name) : string.Empty;
        onFinished?.Invoke(npcId, chosenApproach.Value, delta);
    }

    private string BuildOpeningLine(NpcDefinition npc)
    {
        string name = npc != null ? npc.displayName : "???";
        return defaultOpeningLine.Replace("{nome}", name);
    }

    private static string FormatFeedback(int delta)
    {
        if (delta > 0)  return $"+{delta} Relação";
        if (delta < 0)  return $"{delta} Relação";
        return "Nenhuma reação";
    }
}
