using System;
using System.Collections.Generic;
using Mandato.Core;

namespace Mandato.Run
{
    [Serializable]
    public struct NpcStateEntry
    {
        public string key;
        public NpcRunState value;

        public NpcStateEntry(string key, NpcRunState value)
        {
            this.key = key;
            this.value = value;
        }
    }

    [Serializable]
    public struct QuestStateEntry
    {
        public string key;
        public QuestRunState value;

        public QuestStateEntry(string key, QuestRunState value)
        {
            this.key = key;
            this.value = value;
        }
    }

    [Serializable]
    public struct ActionCooldownEntry
    {
        public string key;
        public int value;

        public ActionCooldownEntry(string key, int value)
        {
            this.key = key;
            this.value = value;
        }
    }

    [Serializable]
    public class DeckSaveData
    {
        public List<string> priorityDrawPile = new List<string>();
        public List<string> drawPile = new List<string>();
        public List<string> discardPile = new List<string>();
        public List<string> removedCardIds = new List<string>();

        public DeckSaveData() { }

        public DeckSaveData(DeckState deckState)
        {
            if (deckState == null) return;
            priorityDrawPile = new List<string>(deckState.priorityDrawPile);
            drawPile = new List<string>(deckState.drawPile);
            discardPile = new List<string>(deckState.discardPile);
            removedCardIds = new List<string>(deckState.removedCardIds);
        }

        public void ApplyTo(DeckState deckState)
        {
            if (deckState == null) return;
            deckState.priorityDrawPile = new List<string>(priorityDrawPile ?? new List<string>());
            deckState.drawPile = new List<string>(drawPile ?? new List<string>());
            deckState.discardPile = new List<string>(discardPile ?? new List<string>());
            deckState.removedCardIds = new List<string>(removedCardIds ?? new List<string>());
        }
    }

    [Serializable]
    public class RunSaveData
    {
        public int schemaVersion = 2;
        public int seed;
        public StatBlock stats = new StatBlock();
        public PoliticalAxis politicalAxis = new PoliticalAxis();
        public RunCalendar calendar = new RunCalendar();
        public RunTermination termination = RunTermination.Ongoing;

        public List<string> activePerkIds = new List<string>();
        public List<string> decisionHistory = new List<string>();
        public List<ActiveEventState> activeEvents = new List<ActiveEventState>();
        public List<ActivePerkState> activePerks = new List<ActivePerkState>();
        public List<string> unlockedActionIds = new List<string>();
        public List<string> consumedSingleUseActions = new List<string>();
        public string activeCharacterId = string.Empty;

        public List<NpcStateEntry> npcStates = new List<NpcStateEntry>();
        public List<QuestStateEntry> questStates = new List<QuestStateEntry>();
        public List<ActionCooldownEntry> actionCooldowns = new List<ActionCooldownEntry>();

        public DeckSaveData deck = new DeckSaveData();

        /// <summary>Evento interativo agendado para o próximo turno. Vazio = nenhum.</summary>
        public string scheduledEventId = string.Empty;

        public RunSaveData() { }

        public static RunSaveData FromRuntime(RunState runState, DeckState deckState)
        {
            if (runState == null) return null;

            var save = new RunSaveData
            {
                schemaVersion = 2,
                seed = runState.seed,
                stats = new StatBlock(
                    runState.stats.climaticChanges,
                    runState.stats.internationalRelations,
                    runState.stats.popularApproval,
                    runState.stats.economy,
                    runState.stats.corruption
                ),
                politicalAxis = runState.politicalAxis != null
                    ? new PoliticalAxis(runState.politicalAxis.x, runState.politicalAxis.y, runState.politicalAxis.isLocked)
                    : new PoliticalAxis(),
                calendar = runState.calendar != null
                    ? new RunCalendar(runState.calendar.currentMonthIndex, runState.calendar.startYear, runState.calendar.totalMonths)
                    : new RunCalendar(),
                termination = runState.termination,
                activePerkIds = new List<string>(runState.activePerkIds ?? new List<string>()),
                decisionHistory = new List<string>(runState.decisionHistory ?? new List<string>()),
                activeEvents = new List<ActiveEventState>(runState.activeEvents ?? new List<ActiveEventState>()),
                activePerks = new List<ActivePerkState>(runState.activePerks ?? new List<ActivePerkState>()),
                unlockedActionIds = new List<string>(runState.unlockedActionIds ?? new List<string>()),
                consumedSingleUseActions = new List<string>(runState.consumedSingleUseActions ?? new List<string>()),
                activeCharacterId = runState.activeCharacterId ?? string.Empty,
                scheduledEventId = runState.scheduledEventId ?? string.Empty,
                deck = new DeckSaveData(deckState)
            };

            if (runState.npcStates != null)
            {
                foreach (var kvp in runState.npcStates)
                {
                    save.npcStates.Add(new NpcStateEntry(kvp.Key, kvp.Value));
                }
            }

            if (runState.questStates != null)
            {
                foreach (var kvp in runState.questStates)
                {
                    save.questStates.Add(new QuestStateEntry(kvp.Key, kvp.Value));
                }
            }

            if (runState.actionCooldowns != null)
            {
                foreach (var kvp in runState.actionCooldowns)
                {
                    save.actionCooldowns.Add(new ActionCooldownEntry(kvp.Key, kvp.Value));
                }
            }

            return save;
        }

        public void ApplyToRuntime(RunState runState, DeckState deckState)
        {
            if (runState == null) return;

            runState.seed = seed;
            runState.stats = new StatBlock(
                stats.climaticChanges,
                stats.internationalRelations,
                stats.popularApproval,
                stats.economy,
                stats.corruption
            );
            runState.politicalAxis = politicalAxis != null
                ? new PoliticalAxis(politicalAxis.x, politicalAxis.y, politicalAxis.isLocked)
                : new PoliticalAxis();
            runState.calendar = calendar != null
                ? new RunCalendar(calendar.currentMonthIndex, calendar.startYear, calendar.totalMonths)
                : new RunCalendar();
            runState.termination = termination;

            runState.activePerkIds = new List<string>(activePerkIds ?? new List<string>());
            runState.decisionHistory = new List<string>(decisionHistory ?? new List<string>());
            runState.activeEvents = new List<ActiveEventState>(activeEvents ?? new List<ActiveEventState>());
            runState.activePerks = new List<ActivePerkState>(activePerks ?? new List<ActivePerkState>());
            runState.unlockedActionIds = new List<string>(unlockedActionIds ?? new List<string>());
            runState.consumedSingleUseActions = new List<string>(consumedSingleUseActions ?? new List<string>());
            runState.activeCharacterId = activeCharacterId ?? string.Empty;
            runState.scheduledEventId = scheduledEventId ?? string.Empty;

            runState.npcStates.Clear();
            if (npcStates != null)
            {
                foreach (var entry in npcStates)
                {
                    if (!string.IsNullOrEmpty(entry.key) && entry.value != null)
                    {
                        runState.npcStates[entry.key] = entry.value;
                    }
                }
            }

            runState.questStates.Clear();
            if (questStates != null)
            {
                foreach (var entry in questStates)
                {
                    if (!string.IsNullOrEmpty(entry.key) && entry.value != null)
                    {
                        runState.questStates[entry.key] = entry.value;
                    }
                }
            }

            runState.actionCooldowns.Clear();
            if (actionCooldowns != null)
            {
                foreach (var entry in actionCooldowns)
                {
                    if (!string.IsNullOrEmpty(entry.key))
                    {
                        runState.actionCooldowns[entry.key] = entry.value;
                    }
                }
            }

            deck?.ApplyTo(deckState);
        }
    }
}
