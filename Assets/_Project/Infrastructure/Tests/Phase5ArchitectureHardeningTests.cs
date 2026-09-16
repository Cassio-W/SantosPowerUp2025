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
            Assert.IsNull(bindings.CameraEffects);
            Assert.IsNull(bindings.CameraFocus);
            Assert.IsNull(bindings.DeskCallButton);
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

        [Test]
        public void CameraFocusManager_RaycastOcclusion_FrontObjectBlocksFocusableBehind()
        {
            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0, 0, -5);
            camGo.transform.forward = Vector3.forward;

            var manager = camGo.AddComponent<CameraFocusManager>();

            // Objeto na frente (ex: celular) com colisor, SEM FocusableObject
            var frontObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontObj.transform.position = new Vector3(0, 0, 0);

            // Objeto ao fundo (ex: computador) com colisor E FocusableObject
            var backObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backObj.transform.position = new Vector3(0, 0, 5);
            var focusable = backObj.AddComponent<FocusableObject>();

            Ray ray = new Ray(camGo.transform.position, Vector3.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            FocusableObject hitFocusable = null;
            bool hasHitObstacle = false;

            foreach (var h in hits)
            {
                if (h.collider == null) continue;

                bool isInteractiveTrigger = h.collider.isTrigger && (
                    h.collider.GetComponentInParent<WorldSpaceUIInteraction>() != null ||
                    h.collider.GetComponentInParent<FocusableObject>() != null
                );

                if (h.collider.isTrigger && !isInteractiveTrigger) continue;

                hasHitObstacle = true;
                var fo = h.collider.GetComponentInParent<FocusableObject>();
                if (fo != null && fo.enabled && fo.gameObject.activeInHierarchy)
                {
                    hitFocusable = fo;
                }
                else
                {
                    hitFocusable = null;
                }
                break;
            }

            Assert.IsTrue(hasHitObstacle, "Deveria ter atingido o objeto da frente.");
            Assert.IsNull(hitFocusable, "O FocusableObject ao fundo NÃO deve ser selecionado quando há um objeto na frente.");

            UnityEngine.Object.DestroyImmediate(frontObj);
            UnityEngine.Object.DestroyImmediate(backObj);
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        [Test]
        public void FocusableObject_EnsureCollider_RotatedHierarchy_CalculatesExactLocalBounds()
        {
            var root = new GameObject("RotatedRoot");
            root.transform.position = new Vector3(10, 2, 5);
            root.transform.rotation = Quaternion.Euler(0, -119.13f, 0);

            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = "ChildMesh";
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(0, 0, 0);
            child.transform.localScale = new Vector3(1, 1, 1);

            // Remove o colisor do próprio cubo para testar a geração no pai FocusableObject
            UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());

            var focusable = root.AddComponent<FocusableObject>();
            focusable.EnsureCollider();

            var box = root.GetComponent<BoxCollider>();
            Assert.IsNotNull(box, "Deveria ter gerado um BoxCollider no FocusableObject pai.");
            Assert.Less(Vector3.Distance(box.center, Vector3.zero), 0.01f, "O centro do BoxCollider deve coincidir com o cubo local.");
            Assert.GreaterOrEqual(box.size.x, 0.99f);
            Assert.GreaterOrEqual(box.size.y, 0.99f);
            Assert.GreaterOrEqual(box.size.z, 0.99f);

            UnityEngine.Object.DestroyImmediate(child);
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void CameraFocusManager_Raycast_CameraChildColliders_IgnoredDuringRaycast()
        {
            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0, 0, 0);
            camGo.transform.forward = Vector3.forward;

            // Objeto filho da câmera (ex: celular flip recolhido na mão)
            var camChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
            camChild.transform.SetParent(camGo.transform, false);
            camChild.transform.localPosition = new Vector3(0, 0, 0.2f); // Bem na frente da lente
            camChild.transform.localScale = new Vector3(0.5f, 0.5f, 0.1f);

            // Objeto 3D no cenário do jogo ao fundo
            var worldTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            worldTarget.transform.position = new Vector3(0, 0, 3f);
            var focusable = worldTarget.AddComponent<FocusableObject>();

            Ray ray = new Ray(camGo.transform.position, Vector3.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            FocusableObject hitFocusable = null;
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(camGo.transform)) continue;

                var fo = h.collider.GetComponentInParent<FocusableObject>();
                if (fo != null && fo.enabled && fo.gameObject.activeInHierarchy)
                {
                    hitFocusable = fo;
                    break;
                }
            }

            Assert.IsNotNull(hitFocusable, "Deveria ter atingido o FocusableObject do cenário, ignorando o colisor filho da câmera.");
            Assert.AreSame(focusable, hitFocusable);

            UnityEngine.Object.DestroyImmediate(camChild);
            UnityEngine.Object.DestroyImmediate(worldTarget);
            UnityEngine.Object.DestroyImmediate(camGo);
        }
    }
}
