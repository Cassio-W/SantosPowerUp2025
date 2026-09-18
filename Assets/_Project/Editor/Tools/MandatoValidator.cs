using System;
using System.Collections.Generic;
using System.Reflection;
using Mandato.Content;
using Mandato.Infrastructure;
using Mandato.Presentation;
using Mandato.UI;
using UnityEditor;
using UnityEngine;

namespace Mandato.Editor
{
    public static class MandatoValidator
    {
        [MenuItem("Mandato/Validação/Validar Catálogo e Integridade")]
        public static int ValidateAllContent()
        {
            int totalChecked = 0;
            int errorCount = 0;

            var cardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var perkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var eventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var questIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var endingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Carrega catálogo de Perks
            string[] perkGuids = AssetDatabase.FindAssets("t:PerkDefinition");
            foreach (string guid in perkGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var perk = AssetDatabase.LoadAssetAtPath<PerkDefinition>(path);
                if (perk == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(perk.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ Perk sem ID em: {path}");
                    errorCount++;
                }
                else if (perkIds.Contains(perk.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ ID de Perk duplicado '{perk.id}' em: {path}");
                    errorCount++;
                }
                else
                {
                    perkIds.Add(perk.id);
                }
            }

            // 2. Carrega catálogo de Eventos
            string[] eventGuids = AssetDatabase.FindAssets("t:RunEventDefinition");
            foreach (string guid in eventGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ev = AssetDatabase.LoadAssetAtPath<RunEventDefinition>(path);
                if (ev == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(ev.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ Evento sem ID em: {path}");
                    errorCount++;
                }
                else if (eventIds.Contains(ev.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ ID de Evento duplicado '{ev.id}' em: {path}");
                    errorCount++;
                }
                else
                {
                    eventIds.Add(ev.id);
                }
            }

            // 3. Carrega catálogo de Quests
            string[] questGuids = AssetDatabase.FindAssets("t:QuestDefinition");
            foreach (string guid in questGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (quest == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(quest.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ Quest sem ID em: {path}");
                    errorCount++;
                }
                else if (questIds.Contains(quest.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ ID de Quest duplicado '{quest.id}' em: {path}");
                    errorCount++;
                }
                else
                {
                    questIds.Add(quest.id);
                }
            }

            // 4. Carrega catálogo de Finais
            string[] endingGuids = AssetDatabase.FindAssets("t:EndingDefinition");
            foreach (string guid in endingGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ending = AssetDatabase.LoadAssetAtPath<EndingDefinition>(path);
                if (ending == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(ending.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ Final sem ID em: {path}");
                    errorCount++;
                }
                else if (endingIds.Contains(ending.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ ID de Final duplicado '{ending.id}' em: {path}");
                    errorCount++;
                }
                else
                {
                    endingIds.Add(ending.id);
                }
            }

            // 5. Carrega catálogo de Ações do Flip-Phone
            string[] actionGuids = AssetDatabase.FindAssets("t:FlipPhoneActionDefinition");
            foreach (string guid in actionGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var act = AssetDatabase.LoadAssetAtPath<FlipPhoneActionDefinition>(path);
                if (act == null) continue;

                totalChecked++;
                if (string.IsNullOrEmpty(act.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ Ação de celular sem ID em: {path}");
                    errorCount++;
                }
                else if (actionIds.Contains(act.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ ID de Ação duplicado '{act.id}' em: {path}");
                    errorCount++;
                }
                else
                {
                    actionIds.Add(act.id);
                }
            }

            // 6. Valida CardDefinition e referências cruzadas
            var allCards = new List<(string path, CardDefinition card)>();
            string[] cardGuids = AssetDatabase.FindAssets("t:CardDefinition");
            foreach (string guid in cardGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
                if (card == null) continue;

                totalChecked++;
                allCards.Add((path, card));

                if (string.IsNullOrEmpty(card.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ Carta sem ID em: {path}");
                    errorCount++;
                }
                else if (cardIds.Contains(card.id))
                {
                    Debug.LogError($"[MandatoValidator] ❌ ID de Carta duplicado '{card.id}' em: {path}");
                    errorCount++;
                }
                else
                {
                    cardIds.Add(card.id);
                }

                if (string.IsNullOrEmpty(card.description))
                {
                    Debug.LogError($"[MandatoValidator] ❌ Carta '{card.name}' sem descrição em: {path}");
                    errorCount++;
                }
            }

            // 7. Validação de referências cruzadas (injectCardIds e grantPerkId)
            foreach (var (path, card) in allCards)
            {
                ValidateChoiceReferences(card.leftChoice, card.id, "Escolha Esquerda", path, cardIds, perkIds, ref errorCount);
                ValidateChoiceReferences(card.rightChoice, card.id, "Escolha Direita", path, cardIds, perkIds, ref errorCount);

                if (card.conditions != null)
                {
                    foreach (var cond in card.conditions)
                    {
                        if (cond == null) continue;
                        if (!string.IsNullOrEmpty(cond.requiredPerkId) && !perkIds.Contains(cond.requiredPerkId))
                        {
                            Debug.LogError($"[MandatoValidator] ❌ Carta '{card.id}' requer perk '{cond.requiredPerkId}' não encontrado no catálogo. Path: {path}");
                            errorCount++;
                        }
                        if (!string.IsNullOrEmpty(cond.requiredQuestId) && !questIds.Contains(cond.requiredQuestId))
                        {
                            Debug.LogError($"[MandatoValidator] ❌ Carta '{card.id}' requer quest '{cond.requiredQuestId}' não encontrada no catálogo. Path: {path}");
                            errorCount++;
                        }
                    }
                }
            }

            if (errorCount == 0)
            {
                Debug.Log($"<color=#00ffaa><b>[MandatoValidator] ✅ Catálogo Válido!</b></color> {totalChecked} assets verificados ({cardIds.Count} cartas, {perkIds.Count} perks, {eventIds.Count} eventos, {questIds.Count} quests, {endingIds.Count} finais, {actionIds.Count} ações). Nenhum erro encontrado.");
            }
            else
            {
                Debug.LogError($"<b>[MandatoValidator] ❌ Validação de catálogo falhou:</b> {totalChecked} assets verificados, {errorCount} erro(s) encontrado(s).");
            }

            return errorCount;
        }

        private static void ValidateChoiceReferences(
            ChoiceDefinition choice,
            string cardId,
            string choiceName,
            string path,
            HashSet<string> cardIds,
            HashSet<string> perkIds,
            ref int errorCount)
        {
            if (choice == null) return;

            if (choice.injectCardIds != null)
            {
                foreach (var injectId in choice.injectCardIds)
                {
                    if (!string.IsNullOrEmpty(injectId) && !cardIds.Contains(injectId))
                    {
                        Debug.LogError($"[MandatoValidator] ❌ Carta '{cardId}' ({choiceName}) injeta carta '{injectId}' não encontrada no catálogo. Path: {path}");
                        errorCount++;
                    }
                }
            }

            if (!string.IsNullOrEmpty(choice.grantPerkId) && !perkIds.Contains(choice.grantPerkId))
            {
                Debug.LogError($"[MandatoValidator] ❌ Carta '{cardId}' ({choiceName}) concede perk '{choice.grantPerkId}' não registrado no catálogo de Perks. Path: {path}");
                errorCount++;
            }
        }

        [MenuItem("Mandato/Validação/Validar Configuração de Cena (Bootstrap)")]
        public static int ValidateSceneConfiguration()
        {
            int errorCount = 0;

            // 1. Procura o MandatoBootstrap na cena aberta
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<MandatoBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[MandatoValidator] ⚠️ MandatoBootstrap não encontrado na cena aberta. Abra a cena JogoV2 antes de validar.");
                return 0; // Não bloqueia se outra cena estiver aberta, tratado separadamente no CI
            }

            var bootstrapType = typeof(MandatoBootstrap);

            // 2. Verifica campos de cartas via reflexão
            var startingField = bootstrapType.GetField("startingCards", BindingFlags.NonPublic | BindingFlags.Instance);
            var catalogField = bootstrapType.GetField("catalogCards", BindingFlags.NonPublic | BindingFlags.Instance);

            var startingCards = startingField?.GetValue(bootstrap) as System.Collections.IList;
            var catalogCards = catalogField?.GetValue(bootstrap) as System.Collections.IList;

            var managerTutCards = bootstrap.PresentationBindings?.TutorialManager?.GetConfiguredCards()
                ?? UnityEngine.Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include)?.GetConfiguredCards();
            int tutorialCount = managerTutCards?.Count ?? 0;
            int startingCount = startingCards?.Count ?? 0;
            int catalogCount = catalogCards?.Count ?? 0;

            if (startingCount == 0)
            {
                Debug.LogError("[MandatoValidator] ❌ startingCards está vazio no MandatoBootstrap! A run não terá cartas no baralho.");
                errorCount++;
            }
            else
            {
                Debug.Log($"[MandatoValidator] ✅ startingCards: {startingCount} carta(s) configurada(s).");
            }

            Debug.Log($"[MandatoValidator] 📝 tutorialCards (TutorialManager): {tutorialCount} carta(s) | catalogCards (injetáveis): {catalogCount} carta(s).");

            // 3. Constrói conjunto de IDs registrados (deck + catálogo)
            var allCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddCardsFromList(System.Collections.IList list)
            {
                if (list == null) return;
                foreach (var obj in list)
                {
                    if (obj is CardDefinition cd && !string.IsNullOrEmpty(cd.id))
                        allCardIds.Add(cd.id);
                }
            }

            AddCardsFromList(managerTutCards);
            AddCardsFromList(startingCards);
            AddCardsFromList(catalogCards);

            // 4. Valida injectCardIds de todas as cartas nos campos serialized
            void CheckCardInjects(System.Collections.IList cardList, string listName)
            {
                if (cardList == null) return;
                foreach (var obj in cardList)
                {
                    if (obj is not CardDefinition card) continue;

                    void CheckSide(ChoiceDefinition choice, string side)
                    {
                        if (choice?.injectCardIds == null) return;
                        foreach (var injectId in choice.injectCardIds)
                        {
                            if (!string.IsNullOrEmpty(injectId) && !allCardIds.Contains(injectId))
                            {
                                Debug.LogError($"[MandatoValidator] ❌ [{listName}] Carta '{card.id}' ({side}) injeta '{injectId}' que não está no catálogo da cena.");
                                errorCount++;
                            }
                        }
                    }

                    CheckSide(card.leftChoice, "Esquerda");
                    CheckSide(card.rightChoice, "Direita");
                }
            }

            CheckCardInjects(managerTutCards, "TutorialManager");
            CheckCardInjects(startingCards, "Starting");
            CheckCardInjects(catalogCards, "Catalog");

            // 5. Validação de ScenePresentationBindings
            if (bootstrap.PresentationBindings != null)
            {
                if (!bootstrap.PresentationBindings.Validate(out var missingList))
                {
                    foreach (var err in missingList)
                    {
                        Debug.LogError($"[MandatoValidator] ❌ ScenePresentationBindings: {err}");
                        errorCount++;
                    }
                }
                else
                {
                    Debug.Log("[MandatoValidator] ✅ ScenePresentationBindings: todos os apresentadores e efeitos estão configurados.");
                }
            }
            else
            {
                Debug.LogError("[MandatoValidator] ❌ presentationBindings é nulo no MandatoBootstrap!");
                errorCount++;
            }

            // 6. Resultado
            if (errorCount == 0)
            {
                Debug.Log($"<color=#00ffaa><b>[MandatoValidator] ✅ Configuração de cena válida!</b></color> {allCardIds.Count} cartas registradas, nenhum erro encontrado.");
            }
            else
            {
                Debug.LogError($"<b>[MandatoValidator] ❌ Configuração de cena com {errorCount} erro(s).</b> Resolva antes de entrar em Play Mode.");
            }

            return errorCount;
        }

        [MenuItem("Mandato/Configuração/Preencher e Salvar Bindings da Cena")]
        public static void AutoBindScenePresenters()
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<MandatoBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[MandatoValidator] ⚠️ MandatoBootstrap não encontrado na cena aberta. Abra a cena JogoV2.");
                return;
            }

            Undo.RecordObject(bootstrap, "Preencher Presentation Bindings");

            // 1. Presentation Coordinator
            var presCoord = UnityEngine.Object.FindFirstObjectByType<RunPresentationCoordinator>(FindObjectsInactive.Include);
            if (presCoord == null)
            {
                var go = GameObject.Find("PresentationCoordinator") ?? GameObject.Find("GameController") ?? bootstrap.gameObject;
                presCoord = Undo.AddComponent<RunPresentationCoordinator>(go);
            }

            // 2. Paper Presenter
            var paper = UnityEngine.Object.FindFirstObjectByType<PaperDocumentPresenter>(FindObjectsInactive.Include);
            if (paper == null)
            {
                var go = GameObject.Find("PaperPresenter") ?? GameObject.Find("Papel") ?? GameObject.Find("Paper") ?? GameObject.Find("Documento") ?? bootstrap.gameObject;
                paper = Undo.AddComponent<PaperDocumentPresenter>(go);
            }

            // 3. Retro Monitor Presenter
            var monitor = UnityEngine.Object.FindFirstObjectByType<RetroMonitorPresenter>(FindObjectsInactive.Include);
            if (monitor == null)
            {
                var go = GameObject.Find("RetroMonitorPresenter") ?? GameObject.Find("RetroMonitor") ?? GameObject.Find("Monitor") ?? GameObject.Find("Computador") ?? bootstrap.gameObject;
                monitor = Undo.AddComponent<RetroMonitorPresenter>(go);
            }

            // 4. Decision Overlay Presenter
            var overlay = UnityEngine.Object.FindFirstObjectByType<DecisionOverlayPresenter>(FindObjectsInactive.Include);
            if (overlay == null)
            {
                var go = GameObject.Find("DecisionOverlay") ?? GameObject.Find("HUD") ?? GameObject.Find("UI") ?? bootstrap.gameObject;
                overlay = Undo.AddComponent<DecisionOverlayPresenter>(go);
            }

            // 5. End Screen Presenter
            var endScreen = UnityEngine.Object.FindFirstObjectByType<EndScreenPresenter>(FindObjectsInactive.Include);
            if (endScreen == null)
            {
                var go = GameObject.Find("EndScreen") ?? GameObject.Find("GameOver") ?? GameObject.Find("UI") ?? bootstrap.gameObject;
                endScreen = Undo.AddComponent<EndScreenPresenter>(go);
            }

            // 6. Flip Phone Presenter
            var phone = UnityEngine.Object.FindFirstObjectByType<FlipPhonePresenter>(FindObjectsInactive.Include);
            if (phone == null)
            {
                var go = GameObject.Find("Celular") ?? GameObject.Find("FlipPhone") ?? GameObject.Find("Phone") ?? bootstrap.gameObject;
                phone = Undo.AddComponent<FlipPhonePresenter>(go);
            }

            // 6.1 Speech Bubble Presenter (Tutorial e Balão de Fala)
            var speechBubble = UnityEngine.Object.FindFirstObjectByType<NpcSpeechBubblePresenter>(FindObjectsInactive.Include);
            if (speechBubble == null)
            {
                var go = GameObject.Find("UI_Npc") ?? GameObject.Find("SpeechBubble") ?? GameObject.Find("BalaoFala") ?? bootstrap.gameObject;
                speechBubble = Undo.AddComponent<NpcSpeechBubblePresenter>(go);
            }

            // 6.2 Tutorial Manager
            var tutManager = UnityEngine.Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
            if (tutManager == null)
            {
                var go = GameObject.Find("TutorialManager") ?? GameObject.Find("Tutorial") ?? bootstrap.gameObject;
                tutManager = Undo.AddComponent<TutorialManager>(go);
            }

            // 7. Camera Effects & Focus
            var camEffects = UnityEngine.Object.FindFirstObjectByType<AttributeCameraEffects>(FindObjectsInactive.Include);
            var camFocus = UnityEngine.Object.FindFirstObjectByType<CameraFocusManager>(FindObjectsInactive.Include);

            // 8. Player Animator
            Animator playerAnim = null;
            var animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var a in animators)
            {
                if (a != null && (a.HasState(0, Animator.StringToHash("LevantaMao")) || a.gameObject.name.ToLower().Contains("player") || a.gameObject.name.ToLower().Contains("mao") || a.gameObject.name.ToLower().Contains("hand")))
                {
                    playerAnim = a;
                    break;
                }
            }

            // 9. Flip Phone GameObject
            var phoneObj = GameObject.Find("Celular") ?? GameObject.Find("FlipPhone") ?? GameObject.Find("Phone");

            // Atualiza via SerializedObject para garantir persistência robusta no arquivo .unity
            var serializedBootstrap = new SerializedObject(bootstrap);
            var bindingsProp = serializedBootstrap.FindProperty("presentationBindings");
            if (bindingsProp != null)
            {
                var pPresCoord = bindingsProp.FindPropertyRelative("presentationCoordinator");
                if (pPresCoord != null) pPresCoord.objectReferenceValue = presCoord;

                var pPaper = bindingsProp.FindPropertyRelative("paperPresenter");
                if (pPaper != null) pPaper.objectReferenceValue = paper;

                var pMonitor = bindingsProp.FindPropertyRelative("retroMonitorPresenter");
                if (pMonitor != null) pMonitor.objectReferenceValue = monitor;

                var pOverlay = bindingsProp.FindPropertyRelative("decisionOverlayPresenter");
                if (pOverlay != null) pOverlay.objectReferenceValue = overlay;

                var pEndScreen = bindingsProp.FindPropertyRelative("endScreenPresenter");
                if (pEndScreen != null) pEndScreen.objectReferenceValue = endScreen;

                var pPhone = bindingsProp.FindPropertyRelative("flipPhonePresenter");
                if (pPhone != null) pPhone.objectReferenceValue = phone;

                var pSpeechBubble = bindingsProp.FindPropertyRelative("speechBubblePresenter");
                if (pSpeechBubble != null && speechBubble != null) pSpeechBubble.objectReferenceValue = speechBubble;

                var pTutManager = bindingsProp.FindPropertyRelative("tutorialManager");
                if (pTutManager != null && tutManager != null) pTutManager.objectReferenceValue = tutManager;

                var pCamEffects = bindingsProp.FindPropertyRelative("cameraEffects");
                if (pCamEffects != null && camEffects != null) pCamEffects.objectReferenceValue = camEffects;

                var pCamFocus = bindingsProp.FindPropertyRelative("cameraFocus");
                if (pCamFocus != null && camFocus != null) pCamFocus.objectReferenceValue = camFocus;

                var pPlayerAnim = bindingsProp.FindPropertyRelative("playerAnimator");
                if (pPlayerAnim != null && playerAnim != null) pPlayerAnim.objectReferenceValue = playerAnim;

                var pPhoneObj = bindingsProp.FindPropertyRelative("flipPhoneObject");
                if (pPhoneObj != null && phoneObj != null) pPhoneObj.objectReferenceValue = phoneObj;

                serializedBootstrap.ApplyModifiedProperties();
            }

            // Fallback via reflection direto no objeto em memória
            var bindings = bootstrap.PresentationBindings;
            var bindingsType = typeof(ScenePresentationBindings);
            bindingsType.GetField("presentationCoordinator", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, presCoord);
            bindingsType.GetField("paperPresenter", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, paper);
            bindingsType.GetField("retroMonitorPresenter", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, monitor);
            bindingsType.GetField("decisionOverlayPresenter", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, overlay);
            bindingsType.GetField("endScreenPresenter", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, endScreen);
            bindingsType.GetField("flipPhonePresenter", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, phone);
            if (speechBubble != null) bindingsType.GetField("speechBubblePresenter", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, speechBubble);
            if (tutManager != null) bindingsType.GetField("tutorialManager", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, tutManager);
            if (camEffects != null) bindingsType.GetField("cameraEffects", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, camEffects);
            if (camFocus != null) bindingsType.GetField("cameraFocus", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, camFocus);
            if (playerAnim != null) bindingsType.GetField("playerAnimator", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, playerAnim);
            if (phoneObj != null) bindingsType.GetField("flipPhoneObject", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, phoneObj);

            EditorUtility.SetDirty(bootstrap);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);

            Debug.Log("<color=#00ffaa><b>[MandatoValidator] ✅ Bindings preenchidos e serializados na cena!</b></color> Salve a cena (Ctrl+S).");
            ValidateSceneConfiguration();
        }

        [MenuItem("Mandato/Validação/Validar Cena de Menu (MenuV2)")]
        public static int ValidateMenuSceneConfiguration()
        {
            int errorCount = 0;
            var menuPresenter = UnityEngine.Object.FindFirstObjectByType<MainMenuPresenter>();
            if (menuPresenter == null)
            {
                Debug.LogWarning("[MandatoValidator] ⚠️ MainMenuPresenter não encontrado na cena aberta. Se você está na cena MenuV2, adicione o componente MainMenuPresenter.");
                return 0;
            }

            var uiDoc = menuPresenter.GetComponent<UnityEngine.UIElements.UIDocument>();
            if (uiDoc == null)
            {
                Debug.LogError("[MandatoValidator] ❌ UIDocument não encontrado no MainMenuPresenter.");
                errorCount++;
            }
            else
            {
                Debug.Log("<color=#00ffaa><b>[MandatoValidator] ✅ Cena de Menu válida!</b></color> MainMenuPresenter configurado em UI Toolkit.");
            }

            return errorCount;
        }

        [MenuItem("Mandato/Validação/Executar Pipeline Completo (Local e CI)")]
        public static bool ValidatePipelineCI()
        {
            Debug.Log("<color=#00ccff><b>[MandatoValidator] 🚀 Iniciando Pipeline de Validação Completo...</b></color>");
            int totalErrors = 0;

            // 1. Validar Catálogo (falhas viram erros no total)
            int contentErrors = ValidateAllContent();
            totalErrors += contentErrors;

            // 2. Validar Ausência de Legado no AppDomain
            string[] legacyForbiddenTypes = new[]
            {
                "GameManager",
                "UIManager",
                "PhysicalPaperUI",
                "RetroMonitorUI",
                "LegacyDealAdapter",
                "LegacyCompatibilityBridge",
                "MenuUI"
            };

            foreach (var typeName in legacyForbiddenTypes)
            {
                Type foundType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foundType = asm.GetType(typeName);
                    if (foundType != null) break;
                }

                if (foundType != null)
                {
                    Debug.LogError($"[MandatoValidator] ❌ Tipo legado proibido encontrado no domínio C#: '{typeName}'");
                    totalErrors++;
                }
            }

            // 3. Validar Cena Atual
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.name.IndexOf("Jogo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                totalErrors += ValidateSceneConfiguration();
            }
            else if (activeScene.name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                totalErrors += ValidateMenuSceneConfiguration();
            }

            if (totalErrors == 0)
            {
                Debug.Log("<color=#00ffaa><b>[MandatoValidator] 🏆 PIPELINE APROVADO! Todos os critérios da arquitetura V2 foram atendidos com 0 erros.</b></color>");
                return true;
            }
            else
            {
                Debug.LogError($"<color=#ff4444><b>[MandatoValidator] ❌ PIPELINE FALHOU com {totalErrors} erro(s).</b></color>");
                return false;
            }
        }
    }
}
