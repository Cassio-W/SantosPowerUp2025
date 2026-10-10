using System.Collections.Generic;
using NUnit.Framework;
using Mandato.Content;
using UnityEngine;

namespace Mandato.Run.Tests
{
    [TestFixture]
    public class NpcApproachReactionTests
    {
        [Test]
        public void GetApproachDelta_ReturnsConfiguredDeltasClampedToLimits()
        {
            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            npc.id = "npc_teste";
            npc.displayName = "NPC Teste";
            npc.SetApproachDeltas(arrogante: 5, brincalhao: -10, persuasivo: 0, romantico: 2);

            Assert.AreEqual(2, npc.GetApproachDelta(ApproachStyle.Arrogante), "Deveria clampar 5 para +2");
            Assert.AreEqual(-2, npc.GetApproachDelta(ApproachStyle.Brincalhao), "Deveria clampar -10 para -2");
            Assert.AreEqual(0, npc.GetApproachDelta(ApproachStyle.Persuasivo));
            Assert.AreEqual(2, npc.GetApproachDelta(ApproachStyle.Romantico));
        }

        [Test]
        public void DifferentNpcs_HaveDifferentReactionProfiles()
        {
            var militar = ScriptableObject.CreateInstance<NpcDefinition>();
            militar.SetApproachDeltas(arrogante: 2, brincalhao: -2, persuasivo: 0, romantico: -1);

            var populista = ScriptableObject.CreateInstance<NpcDefinition>();
            populista.SetApproachDeltas(arrogante: -2, brincalhao: 2, persuasivo: 1, romantico: 0);

            Assert.AreNotEqual(
                militar.GetApproachDelta(ApproachStyle.Arrogante),
                populista.GetApproachDelta(ApproachStyle.Arrogante)
            );

            Assert.AreNotEqual(
                militar.GetApproachDelta(ApproachStyle.Brincalhao),
                populista.GetApproachDelta(ApproachStyle.Brincalhao)
            );
        }
    }
}
