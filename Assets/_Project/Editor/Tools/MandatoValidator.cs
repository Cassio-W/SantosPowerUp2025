using System;
using System.Collections.Generic;
using System.Reflection;
using Mandato.Content;
using Mandato.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Mandato.Editor
{
    public static class MandatoValidator
    {
        [MenuItem("Mandato/Validação/Validar Catálogo e Integridade")]
        public static void ValidateAllContent()
        {
            int totalChecked = 0;
            int warningCount = 0;

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
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Perk sem ID em: {path}");
                    warningCount++;
                }
                else if (perkIds.Contains(perk.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Perk duplicado '{perk.id}' em: {path}");
                    warningCount++;
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
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Evento sem ID em: {path}");
                    warningCount++;
                }
                else if (eventIds.Contains(ev.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Evento duplicado '{ev.id}' em: {path}");
                    warningCount++;
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
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Quest sem ID em: {path}");
                    warningCount++;
                }
                else if (questIds.Contains(quest.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Quest duplicado '{quest.id}' em: {path}");
                    warningCount++;
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
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Final sem ID em: {path}");
                    warningCount++;
                }
                else if (endingIds.Contains(ending.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Final duplicado '{ending.id}' em: {path}");
                    warningCount++;
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
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Ação de celular sem ID em: {path}");
                    warningCount++;
                }
                else if (actionIds.Contains(act.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Ação duplicado '{act.id}' em: {path}");
                    warningCount++;
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
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Carta sem ID em: {path}");
                    warningCount++;
                }
                else if (cardIds.Contains(card.id))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ ID de Carta duplicado '{card.id}' em: {path}");
                    warningCount++;
                }
                else
                {
                    cardIds.Add(card.id);
                }

                if (string.IsNullOrEmpty(card.description))
                {
                    Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{card.name}' sem descrição em: {path}");
                    warningCount++;
                }
            }

            // 7. Validação de referências cruzadas (injectCardIds e grantPerkId)
            foreach (var (path, card) in allCards)
            {
                ValidateChoiceReferences(card.leftChoice, card.id, "Escolha Esquerda", path, cardIds, perkIds, ref warningCount);
                ValidateChoiceReferences(card.rightChoice, card.id, "Escolha Direita", path, cardIds, perkIds, ref warningCount);

                if (card.conditions != null)
                {
                    foreach (var cond in card.conditions)
                    {
                        if (cond == null) continue;
                        if (!string.IsNullOrEmpty(cond.requiredPerkId) && !perkIds.Contains(cond.requiredPerkId))
                        {
                            Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{card.id}' requer perk '{cond.requiredPerkId}' não encontrado no catálogo. Path: {path}");
                            warningCount++;
                        }
                        if (!string.IsNullOrEmpty(cond.requiredQuestId) && !questIds.Contains(cond.requiredQuestId))
                        {
                            Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{card.id}' requer quest '{cond.requiredQuestId}' não encontrada no catálogo. Path: {path}");
                            warningCount++;
                        }
                    }
                }
            }

            if (warningCount == 0)
            {
                Debug.Log($"<color=#00ffaa><b>[MandatoValidator] ✅ Sucesso!</b></color> {totalChecked} assets validados ({cardIds.Count} cartas, {perkIds.Count} perks, {eventIds.Count} eventos, {questIds.Count} quests, {endingIds.Count} finais, {actionIds.Count} ações). Nenhum erro encontrado.");
            }
            else
            {
                Debug.LogWarning($"<b>[MandatoValidator] ⚠️ Concluído com avisos:</b> {totalChecked} assets verificados, {warningCount} problemas encontrados.");
            }
        }

        private static void ValidateChoiceReferences(
            ChoiceDefinition choice,
            string cardId,
            string choiceName,
            string path,
            HashSet<string> cardIds,
            HashSet<string> perkIds,
            ref int warningCount)
        {
            if (choice == null) return;

            if (choice.injectCardIds != null)
            {
                foreach (var injectId in choice.injectCardIds)
                {
                    if (!string.IsNullOrEmpty(injectId) && !cardIds.Contains(injectId))
                    {
                        Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{cardId}' ({choiceName}) injeta carta '{injectId}' não encontrada no catálogo. Path: {path}");
                        warningCount++;
                    }
                }
            }

            if (!string.IsNullOrEmpty(choice.grantPerkId) && !perkIds.Contains(choice.grantPerkId))
            {
                // Apenas aviso informativo, pois o perk pode ser dinâmico ou adicionado via SO
                Debug.LogWarning($"[MandatoValidator] ⚠️ Carta '{cardId}' ({choiceName}) concede perk '{choice.grantPerkId}' não registrado no catálogo de Perks. Path: {path}");
                warningCount++;
            }
        }

        [MenuItem("Mandato/Validação/Validar Configuração de Cena (Bootstrap)")]
        public static void ValidateSceneConfiguration()
        {
            int warningCount = 0;

            // 1. Procura o MandatoBootstrap na cena aberta
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<MandatoBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[MandatoValidator] ⚠️ MandatoBootstrap não encontrado na cena aberta. Abra a cena JogoV2 antes de validar.");
                return;
            }

            var bootstrapType = typeof(MandatoBootstrap);

            // 2. Verifica campos de cartas via reflexión (campos são serialized private)
            var startingField = bootstrapType.GetField("startingCards", BindingFlags.NonPublic | BindingFlags.Instance);
            var tutorialField = bootstrapType.GetField("tutorialCards", BindingFlags.NonPublic | BindingFlags.Instance);
            var catalogField = bootstrapType.GetField("catalogCards", BindingFlags.NonPublic | BindingFlags.Instance);

            var startingCards = startingField?.GetValue(bootstrap) as System.Collections.IList;
            var tutorialCards = tutorialField?.GetValue(bootstrap) as System.Collections.IList;
            var catalogCards = catalogField?.GetValue(bootstrap) as System.Collections.IList;

            int startingCount = startingCards?.Count ?? 0;
            int tutorialCount = tutorialCards?.Count ?? 0;
            int catalogCount = catalogCards?.Count ?? 0;

            if (startingCount == 0)
            {
                Debug.LogError("[MandatoValidator] ❌ startingCards está vazio no MandatoBootstrap! A run não terá cartas no baralho.");
                warningCount++;
            }
            else
            {
                Debug.Log($"[MandatoValidator] ✅ startingCards: {startingCount} carta(s) configurada(s).");
            }

            Debug.Log($"[MandatoValidator] 📝 tutorialCards: {tutorialCount} carta(s) | catalogCards (injetáveis): {catalogCount} carta(s).");

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

            AddCardsFromList(tutorialCards);
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
                                Debug.LogWarning($"[MandatoValidator] ⚠️ [{listName}] Carta '{card.id}' ({side}) injeta '{injectId}' que não está no catálogo da cena.");
                                warningCount++;
                            }
                        }
                    }

                    CheckSide(card.leftChoice, "Esquerda");
                    CheckSide(card.rightChoice, "Direita");
                }
            }

            CheckCardInjects(tutorialCards, "Tutorial");
            CheckCardInjects(startingCards, "Starting");
            CheckCardInjects(catalogCards, "Catalog");

            // 5. Validação de ScenePresentationBindings
            if (bootstrap.PresentationBindings != null)
            {
                if (!bootstrap.PresentationBindings.Validate(out var missingList))
                {
                    foreach (var err in missingList)
                    {
                        Debug.LogWarning($"[MandatoValidator] ⚠️ ScenePresentationBindings: {err}");
                        warningCount++;
                    }
                }
                else
                {
                    Debug.Log("[MandatoValidator] ✅ ScenePresentationBindings: todos os apresentadores estão configurados.");
                }
            }
            else
            {
                Debug.LogError("[MandatoValidator] ❌ presentationBindings é nulo no MandatoBootstrap!");
                warningCount++;
            }

            // 6. Resultado
            if (warningCount == 0)
            {
                Debug.Log($"<color=#00ffaa><b>[MandatoValidator] ✅ Configuração de cena válida!</b></color> {allCardIds.Count} cartas registradas, nenhum problema encontrado.");
            }
            else
            {
                Debug.LogWarning($"<b>[MandatoValidator] ⚠️ Configuração de cena com {warningCount} problema(s).</b> Resolva antes de entrar em Play Mode.");
            }
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
            var presCoord = UnityEngine.Object.FindFirstObjectByType<Mandato.Presentation.RunPresentationCoordinator>(FindObjectsInactive.Include);
            if (presCoord == null)
            {
                var go = GameObject.Find("PresentationCoordinator") ?? GameObject.Find("GameController") ?? bootstrap.gameObject;
                presCoord = Undo.AddComponent<Mandato.Presentation.RunPresentationCoordinator>(go);
            }

            // 2. Paper Presenter
            var paper = UnityEngine.Object.FindFirstObjectByType<Mandato.UI.PaperDocumentPresenter>(FindObjectsInactive.Include);
            if (paper == null)
            {
                var go = GameObject.Find("PhysicalPaperUI") ?? GameObject.Find("Papel") ?? GameObject.Find("Paper") ?? GameObject.Find("Documento") ?? bootstrap.gameObject;
                paper = Undo.AddComponent<Mandato.UI.PaperDocumentPresenter>(go);
            }

            // 3. Retro Monitor Presenter
            var monitor = UnityEngine.Object.FindFirstObjectByType<Mandato.UI.RetroMonitorPresenter>(FindObjectsInactive.Include);
            if (monitor == null)
            {
                var go = GameObject.Find("RetroMonitorUI") ?? GameObject.Find("RetroMonitor") ?? GameObject.Find("Monitor") ?? GameObject.Find("Computador") ?? bootstrap.gameObject;
                monitor = Undo.AddComponent<Mandato.UI.RetroMonitorPresenter>(go);
            }

            // 4. Decision Overlay Presenter
            var overlay = UnityEngine.Object.FindFirstObjectByType<Mandato.UI.DecisionOverlayPresenter>(FindObjectsInactive.Include);
            if (overlay == null)
            {
                var go = GameObject.Find("DecisionOverlay") ?? GameObject.Find("HUD") ?? GameObject.Find("UI") ?? bootstrap.gameObject;
                overlay = Undo.AddComponent<Mandato.UI.DecisionOverlayPresenter>(go);
            }

            // 5. End Screen Presenter
            var endScreen = UnityEngine.Object.FindFirstObjectByType<Mandato.UI.EndScreenPresenter>(FindObjectsInactive.Include);
            if (endScreen == null)
            {
                var go = GameObject.Find("EndScreen") ?? GameObject.Find("GameOver") ?? GameObject.Find("UI") ?? bootstrap.gameObject;
                endScreen = Undo.AddComponent<Mandato.UI.EndScreenPresenter>(go);
            }

            // 6. Flip Phone Presenter
            var phone = UnityEngine.Object.FindFirstObjectByType<Mandato.UI.FlipPhonePresenter>(FindObjectsInactive.Include);
            if (phone == null)
            {
                var go = GameObject.Find("Celular") ?? GameObject.Find("FlipPhone") ?? GameObject.Find("Phone") ?? bootstrap.gameObject;
                phone = Undo.AddComponent<Mandato.UI.FlipPhonePresenter>(go);
            }

            // 7. Player Animator
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

            // 8. Flip Phone GameObject
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
            if (playerAnim != null) bindingsType.GetField("playerAnimator", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, playerAnim);
            if (phoneObj != null) bindingsType.GetField("flipPhoneObject", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(bindings, phoneObj);

            EditorUtility.SetDirty(bootstrap);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);

            Debug.Log("<color=#00ffaa><b>[MandatoValidator] ✅ Bindings preenchidos e serializados na cena!</b></color> Salve a cena (Ctrl+S).");
            ValidateSceneConfiguration();
        }
    }
}
