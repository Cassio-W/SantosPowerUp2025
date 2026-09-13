using System.IO;
using Mandato.Core;
using Mandato.Infrastructure;
using Mandato.Run;
using NUnit.Framework;
using UnityEngine;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class SaveSystemTests
    {
        private string testSavePath;
        private string testRunSavePath;

        [SetUp]
        public void Setup()
        {
            testSavePath = Path.Combine(Application.temporaryCachePath, "test_mandato_profile_" + System.Guid.NewGuid().ToString("N") + ".json");
            testRunSavePath = Path.Combine(Application.temporaryCachePath, "test_mandato_run_" + System.Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void Teardown()
        {
            try
            {
                if (File.Exists(testSavePath)) File.Delete(testSavePath);
                if (File.Exists(testSavePath + ".bak")) File.Delete(testSavePath + ".bak");
                if (File.Exists(testRunSavePath)) File.Delete(testRunSavePath);
            }
            catch { }
        }

        [Test]
        public void SaveAndLoad_MaintainsDataIntegrity()
        {
            var profile = new ProfileState();
            profile.RecordRunCompleted(true, "Ending_Good", 42, 85);
            profile.UnlockCharacter("npc_senador");
            profile.UnlockCard("card_reforma");
            profile.UnlockAction("action_ligar_conselheiro");
            profile.RecordQuestCompletion("quest_cop30");
            profile.UnlockAchievement("ach_first_win");

            bool saved = SaveSystem.SaveProfile(profile, testSavePath);
            Assert.IsTrue(saved);
            Assert.IsTrue(File.Exists(testSavePath));

            var loaded = SaveSystem.LoadProfile(testSavePath);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.schemaVersion);
            Assert.AreEqual(1, loaded.totalRunsPlayed);
            Assert.AreEqual(1, loaded.totalVictories);
            Assert.AreEqual(42, loaded.totalDecisionsMade);
            Assert.AreEqual(85, loaded.highestPopularityScore);
            Assert.IsTrue(loaded.discoveredEndingIds.Contains("Ending_Good"));
            Assert.IsTrue(loaded.unlockedCharacterIds.Contains("npc_senador"));
            Assert.IsTrue(loaded.unlockedCardIds.Contains("card_reforma"));
            Assert.IsTrue(loaded.unlockedActionIds.Contains("action_ligar_conselheiro"));
            Assert.IsTrue(loaded.completedQuestIds.Contains("quest_cop30"));
            Assert.IsTrue(loaded.unlockedAchievementIds.Contains("ach_first_win"));
        }

        [Test]
        public void LoadProfile_WhenCorrupted_CreatesBackupFileAndReturnsFreshProfile()
        {
            File.WriteAllText(testSavePath, "{ invalid json corrupt content !!! }");

            var loaded = SaveSystem.LoadProfile(testSavePath);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.totalRunsPlayed);
            Assert.IsTrue(File.Exists(testSavePath + ".bak"), "Deveria ter criado um arquivo .bak de backup.");
        }

        [Test]
        public void LoadProfile_WhenSchemaVersionIsOld_MigratesToCurrentVersion()
        {
            string oldJson = "{\n  \"schemaVersion\": 1,\n  \"saveTimestamp\": \"2026-01-01T00:00:00.000Z\",\n  \"data\": {\n    \"totalRunsPlayed\": 5,\n    \"totalVictories\": 2,\n    \"unlockedCharacterIds\": [\"npc_1\"],\n    \"discoveredEndingIds\": [\"end_1\"],\n    \"unlockedCardIds\": [\"c_1\"]\n  }\n}";
            File.WriteAllText(testSavePath, oldJson);

            var loaded = SaveSystem.LoadProfile(testSavePath);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.schemaVersion);
            Assert.AreEqual(5, loaded.totalRunsPlayed);
            Assert.AreEqual(2, loaded.totalVictories);
            Assert.IsTrue(loaded.unlockedCharacterIds.Contains("npc_1"));
            Assert.IsNotNull(loaded.unlockedActionIds);
            Assert.IsNotNull(loaded.completedQuestIds);
            Assert.IsNotNull(loaded.unlockedAchievementIds);
        }

        [Test]
        public void ProfileState_MetaProgressionMethods_AreIdempotent()
        {
            var profile = new ProfileState();

            Assert.IsTrue(profile.UnlockAction("action_1"));
            Assert.IsFalse(profile.UnlockAction("action_1"));
            Assert.AreEqual(1, profile.unlockedActionIds.Count);

            Assert.IsTrue(profile.RecordQuestCompletion("quest_1"));
            Assert.IsFalse(profile.RecordQuestCompletion("quest_1"));
            Assert.AreEqual(1, profile.completedQuestIds.Count);

            Assert.IsTrue(profile.UnlockAchievement("ach_1"));
            Assert.IsFalse(profile.UnlockAchievement("ach_1"));
            Assert.AreEqual(1, profile.unlockedAchievementIds.Count);
        }

        [Test]
        public void RunSave_SaveAndLoadRoundtrip_PreservesAllStateAndDeck()
        {
            var runState = new RunState(seed: 12345);
            runState.stats.ApplyImpacts(new StatBlock(climate: 10, relations: -5, approval: 20, eco: 15, corrupt: -10));
            runState.politicalAxis.ApplyDelta(3, 4);
            runState.calendar.Advance();
            runState.decisionHistory.Add("card_1:choice_0");
            runState.decisionHistory.Add("card_2:choice_1");

            var npc = runState.GetOrCreateNpcState("npc_ministro");
            npc.relationScore = 70;

            var quest = runState.GetOrCreateQuestState("quest_amazonia");
            quest.currentStepIndex = 2;

            runState.actionCooldowns["action_imprensa"] = 3;
            runState.activeEvents.Add(new ActiveEventState("event_seca", 2));
            runState.activePerks.Add(new ActivePerkState("perk_imunidade", 4));

            var deckState = new DeckState();
            deckState.drawPile.Add("card_a");
            deckState.drawPile.Add("card_b");
            deckState.priorityDrawPile.Add("card_prio");
            deckState.discardPile.Add("card_disc");
            deckState.removedCardIds.Add("card_rem");

            var saveData = RunSaveData.FromRuntime(runState, deckState);
            bool saved = SaveSystem.SaveRun(saveData, testRunSavePath);

            Assert.IsTrue(saved);
            Assert.IsTrue(SaveSystem.HasSavedRun(testRunSavePath));

            var loadedSave = SaveSystem.LoadRun(testRunSavePath);
            Assert.IsNotNull(loadedSave);

            var restoredRun = new RunState();
            var restoredDeck = new DeckState();
            loadedSave.ApplyToRuntime(restoredRun, restoredDeck);

            Assert.AreEqual(12345, restoredRun.seed);
            Assert.AreEqual(runState.stats.popularApproval, restoredRun.stats.popularApproval);
            Assert.AreEqual(runState.stats.economy, restoredRun.stats.economy);
            Assert.AreEqual(3, restoredRun.politicalAxis.x);
            Assert.AreEqual(4, restoredRun.politicalAxis.y);
            Assert.AreEqual(2, restoredRun.calendar.currentMonthIndex);
            Assert.AreEqual(2, restoredRun.decisionHistory.Count);
            Assert.AreEqual(70, restoredRun.GetNpcRelation("npc_ministro"));
            Assert.AreEqual(2, restoredRun.GetOrCreateQuestState("quest_amazonia").currentStepIndex);
            Assert.AreEqual(3, restoredRun.actionCooldowns["action_imprensa"]);
            Assert.AreEqual(1, restoredRun.activeEvents.Count);
            Assert.AreEqual("event_seca", restoredRun.activeEvents[0].eventId);
            Assert.AreEqual(1, restoredRun.activePerks.Count);
            Assert.AreEqual("perk_imunidade", restoredRun.activePerks[0].perkId);

            Assert.AreEqual(2, restoredDeck.drawPile.Count);
            Assert.AreEqual(1, restoredDeck.priorityDrawPile.Count);
            Assert.AreEqual("card_prio", restoredDeck.priorityDrawPile[0]);
            Assert.AreEqual(1, restoredDeck.discardPile.Count);
            Assert.AreEqual(1, restoredDeck.removedCardIds.Count);

            bool deleted = SaveSystem.DeleteRunSave(testRunSavePath);
            Assert.IsTrue(deleted);
            Assert.IsFalse(SaveSystem.HasSavedRun(testRunSavePath));
        }
    }
}
