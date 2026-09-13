using System;
using Mandato.Content;
using Mandato.Core;
using Mandato.Infrastructure;
using Mandato.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class Phase5ArchitectureHardeningTests
    {
        [Test]
        public void Architecture_LegacyForbiddenTypes_DoNotExistInDomain()
        {
            string[] forbiddenTypes = new[]
            {
                "GameManager",
                "UIManager",
                "PhysicalPaperUI",
                "RetroMonitorUI",
                "LegacyDealAdapter",
                "LegacyCompatibilityBridge",
                "MenuUI"
            };

            foreach (var typeName in forbiddenTypes)
            {
                Type found = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    found = asm.GetType(typeName);
                    if (found != null) break;
                }

                Assert.IsNull(found, $"Tipo legado proibido '{typeName}' foi encontrado carregado no AppDomain!");
            }
        }

        [Test]
        public void ScenePresentationBindings_ExposesCameraEffectsAndFocus()
        {
            var bindings = new ScenePresentationBindings();

            // Deve instanciar propriedades sem exceções
            Assert.IsNull(bindings.PresentationCoordinator);
            Assert.IsNull(bindings.PaperPresenter);
            Assert.IsNull(bindings.RetroMonitorPresenter);
            Assert.IsNull(bindings.DecisionOverlayPresenter);
            Assert.IsNull(bindings.EndScreenPresenter);
            Assert.IsNull(bindings.FlipPhonePresenter);
        }

        [Test]
        public void AttributeCameraEffects_CanApplyStatsDirectly()
        {
            var go = new GameObject("TestCameraEffects");
            var effects = go.AddComponent<AttributeCameraEffects>();

            var stats = new StatBlock(climate: 40, relations: 50, approval: 60, eco: 70, corrupt: 30);

            Assert.DoesNotThrow(() => effects.ApplyAttributeEffects(stats, instant: true));
            Assert.DoesNotThrow(() => effects.UpdatePollutionEffect(30f, instant: true));
            Assert.DoesNotThrow(() => effects.UpdateCorruptionEffect(20f, instant: true));

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void RunCatalog_HasZeroLegacyRegistrationMethods()
        {
            var catalog = new RunCatalog();
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.id = "test_card_v2";

            catalog.RegisterCard(card);

            Assert.IsTrue(catalog.Cards.ContainsKey("test_card_v2"));
            Assert.AreEqual(1, catalog.Cards.Count);

            UnityEngine.Object.DestroyImmediate(card);
        }
    }
}
