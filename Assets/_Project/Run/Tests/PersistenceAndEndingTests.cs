using System.Collections.Generic;
using NUnit.Framework;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using UnityEngine;

namespace Mandato.Run.Tests
{
    public class PersistenceAndEndingTests
    {
        [Test]
        public void ProfileState_JsonSerialization_RoundTripsAccurately()
        {
            var profile = new ProfileState();
            profile.RecordRunCompleted(isVictory: true, endingId: "ending_milagre_economico");
            profile.UnlockCharacter("npc_ministro_marcio");
            profile.UnlockCard("card_reforma_tributaria");

            string json = JsonUtility.ToJson(profile);
            var loaded = JsonUtility.FromJson<ProfileState>(json);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.totalRunsPlayed);
            Assert.AreEqual(1, loaded.totalVictories);
            Assert.IsTrue(loaded.discoveredEndingIds.Contains("ending_milagre_economico"));
            Assert.IsTrue(loaded.unlockedCharacterIds.Contains("npc_ministro_marcio"));
            Assert.IsTrue(loaded.unlockedCardIds.Contains("card_reforma_tributaria"));
        }

        [Test]
        public void EndingEvaluator_SelectsSpecificEnding_BasedOnQuadrantAndStats()
        {
            var run = new RunState();
            // Simula mandato completo de 4 anos com Direita Liberal e Economia forte
            run.ForceVictory();
            run.politicalAxis.x = 6;
            run.politicalAxis.y = -5; // Direita Liberal
            run.stats.economy = 85;

            var specificEnding = EndingDefinition.CreateRuntimeInstance(
                id: "ending_liberal_boom",
                title: "Livre Mercado Triunfante",
                epilogue: "O país virou polo de atração de capital.",
                victory: true,
                quadrant: "Direita Liberal",
                priority: 10
            );
            specificEnding.minEconomy = 80;

            var genericEnding = EndingDefinition.CreateRuntimeInstance(
                id: "ending_generic",
                title: "Governo Terminado",
                epilogue: "Fim normal.",
                victory: true,
                quadrant: "",
                priority: 0
            );

            var catalog = new List<EndingDefinition> { genericEnding, specificEnding };

            EndingDefinition result = EndingEvaluator.EvaluateEnding(run, catalog);

            Assert.IsNotNull(result);
            Assert.AreEqual("ending_liberal_boom", result.id);
            Assert.AreEqual("Livre Mercado Triunfante", result.title);
        }

        [Test]
        public void EndingEvaluator_DefeatRun_FallsBackToDefeatEpilogue()
        {
            var run = new RunState();
            run.ForceDefeat("Colapso Ecológico");

            var catalog = new List<EndingDefinition>(); // Catálogo vazio para testar fallback

            EndingDefinition result = EndingEvaluator.EvaluateEnding(run, catalog);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.requiredVictory);
            StringAssert.Contains("Colapso Ecológico", result.epilogueText);
        }
    }
}
