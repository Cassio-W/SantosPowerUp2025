using System.IO;
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

        [SetUp]
        public void Setup()
        {
            testSavePath = Path.Combine(Application.temporaryCachePath, "test_mandato_profile_" + System.Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void Teardown()
        {
            try
            {
                if (File.Exists(testSavePath)) File.Delete(testSavePath);
                if (File.Exists(testSavePath + ".bak")) File.Delete(testSavePath + ".bak");
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
    }
}
