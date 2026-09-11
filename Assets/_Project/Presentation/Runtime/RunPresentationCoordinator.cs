using System;
using System.Collections;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Core;
using Mandato.Run;
using UnityEngine;

namespace Mandato.Presentation
{
    public class RunPresentationCoordinator : MonoBehaviour
    {
        [Header("Apresentadores")]
        public EnvironmentPresentation environment;
        public NpcPresentation activeNpc;

        [Header("Spawn de NPCs")]
        public Transform npcSpawnPoint;
        public Vector3 defaultSpawnPosition = new Vector3(5.85f, 0f, 3.65f);
        public Vector3 defaultTablePosition = new Vector3(0f, 0f, -1.5f);
        public Vector3 defaultExitPosition = new Vector3(5.85f, 0f, 3.65f);
        public List<GameObject> npcPrefabs = new List<GameObject>();

        public event Action<CardDefinition> OnProposalOnDesk;
        public event Action<ResolutionReport> OnConsequencesFinished;

        private RunStateMachine stateMachine;
        private IReadOnlyDictionary<string, CardDefinition> cardCatalog;
        private Coroutine activePresentationRoutine;

        private GameObject activeNpcGameObject;

        public void Bind(RunStateMachine runStateMachine, IReadOnlyDictionary<string, CardDefinition> catalog)
        {
            stateMachine = runStateMachine;
            cardCatalog = catalog;

            // Auto-preenche a lista de prefabs a partir do catálogo caso esteja vazia
            if ((npcPrefabs == null || npcPrefabs.Count == 0) && catalog != null)
            {
                if (npcPrefabs == null) npcPrefabs = new List<GameObject>();
                foreach (var kvp in catalog)
                {
                    if (kvp.Value != null && kvp.Value.npcPrefab != null && !npcPrefabs.Contains(kvp.Value.npcPrefab))
                    {
                        npcPrefabs.Add(kvp.Value.npcPrefab);
                    }
                }
            }

            if (stateMachine != null)
            {
                stateMachine.OnProposalReady += PresentProposal;
                stateMachine.OnConsequencesReady += PresentConsequences;
            }
        }

        public void PresentProposal(CardDefinition card)
        {
            if (card == null) return;

            StopActiveRoutine();
            activePresentationRoutine = StartCoroutine(PresentProposalRoutine(card));
        }

        private IEnumerator PresentProposalRoutine(CardDefinition card)
        {
            // Propostas de tutorial não usam NPC caminhando na porta
            bool isTutorialCard = card.id.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  card.categoryTag.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  card.title.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isTutorialCard)
            {
                yield return new WaitForSeconds(0.05f);
                OnProposalOnDesk?.Invoke(card);
                yield break;
            }

            GameObject prefabToSpawn = card.npcPrefab;
            if (prefabToSpawn == null && npcPrefabs != null && npcPrefabs.Count > 0)
            {
                prefabToSpawn = npcPrefabs[UnityEngine.Random.Range(0, npcPrefabs.Count)];
            }

            // Se for proposta com NPC válido:
            if (prefabToSpawn != null)
            {
                // Limpa NPC anterior se ainda existir
                if (activeNpcGameObject != null)
                {
                    SafeDestroy(activeNpcGameObject);
                    activeNpcGameObject = null;
                }

                Vector3 spawnPos = npcSpawnPoint != null ? npcSpawnPoint.position : defaultSpawnPosition;
                Quaternion spawnRot = npcSpawnPoint != null ? npcSpawnPoint.rotation : Quaternion.identity;

                activeNpcGameObject = Instantiate(prefabToSpawn, spawnPos, spawnRot);
                activeNpcGameObject.transform.position = spawnPos;

                // Garante que o NavMeshAgent esteja perfeitamente posicionado na malha
                var navAgent = activeNpcGameObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent != null)
                {
                    navAgent.Warp(spawnPos);
                }

                // 1. Verifica se tem NPCController no prefab
                Type controllerType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    controllerType = asm.GetType("NPCController");
                    if (controllerType != null) break;
                }

                Component npcController = controllerType != null ? activeNpcGameObject.GetComponent(controllerType) : null;

                if (npcController != null)
                {
                    // Garante posições de início e destino se o spawnPoint for customizado
                    if (npcSpawnPoint != null)
                    {
                        var startPosField = controllerType.GetField("startPosition");
                        var targetPosField = controllerType.GetField("targetPosition");
                        startPosField?.SetValue(npcController, spawnPos);
                        targetPosField?.SetValue(npcController, defaultTablePosition);
                    }

                    // Usa a rotina nativa do NPCController
                    var moveMethod = controllerType.GetMethod("MoveToTable");
                    var isDeliveredField = controllerType.GetField("isDelivered");

                    moveMethod?.Invoke(npcController, null);

                    // Aguarda o NPC caminhar até a mesa e entregar o papel (com timeout de segurança)
                    float walkTimeout = 0f;
                    while (activeNpcGameObject != null && walkTimeout < 10f)
                    {
                        walkTimeout += Time.deltaTime;
                        bool delivered = (bool)(isDeliveredField?.GetValue(npcController) ?? false);
                        if (delivered) break;
                        yield return null;
                    }

                    yield return new WaitForSeconds(0.05f);
                    OnProposalOnDesk?.Invoke(card);
                }
                else
                {
                    // Usa NpcPresentation
                    activeNpc = activeNpcGameObject.GetComponent<NpcPresentation>();
                    if (activeNpc == null)
                    {
                        activeNpc = activeNpcGameObject.AddComponent<NpcPresentation>();
                    }

                    activeNpc.tablePosition = defaultTablePosition;
                    activeNpc.exitPosition = defaultExitPosition;

                    bool delivered = false;
                    activeNpc.EnterAndDeliver(() =>
                    {
                        delivered = true;
                    });

                    while (!delivered && activeNpcGameObject != null)
                    {
                        yield return null;
                    }

                    OnProposalOnDesk?.Invoke(card);
                }
            }
            else
            {
                // Fallback de apresentação sem NPC 3D
                yield return new WaitForSeconds(0.05f);
                OnProposalOnDesk?.Invoke(card);
            }
        }

        public void PresentConsequences(ResolutionReport report)
        {
            if (report == null) return;

            StopActiveRoutine();
            activePresentationRoutine = StartCoroutine(PresentConsequencesRoutine(report));
        }

        private IEnumerator PresentConsequencesRoutine(ResolutionReport report)
        {
            // 1. Toca efeitos de ambiente e sons de cue
            if (environment != null)
            {
                environment.PlayPresentationCue(report.presentationCue);
            }

            // 2. Comanda reação do NPC e saída
            bool isPositive = report.choiceIndex == 0; // 0 = Aceitar / Aprovar, 1 = Recusar / Rejeitar

            if (activeNpcGameObject != null)
            {
                Type controllerType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    controllerType = asm.GetType("NPCController");
                    if (controllerType != null) break;
                }

                Component npcController = controllerType != null ? activeNpcGameObject.GetComponent(controllerType) : null;

                if (npcController != null)
                {
                    var reactMethod = controllerType.GetMethod("ReactAndExit");
                    if (reactMethod != null)
                    {
                        reactMethod.Invoke(npcController, new object[] { isPositive });
                    }

                    // Aguarda o NPC completar a saída pela porta ou o objeto ser destruído (timeout de 7s)
                    float exitTimer = 0f;
                    while (activeNpcGameObject != null && exitTimer < 7f)
                    {
                        exitTimer += Time.deltaTime;
                        if (activeNpcGameObject != null && activeNpcGameObject.transform.position.x >= defaultSpawnPosition.x - 0.3f)
                        {
                            SafeDestroy(activeNpcGameObject);
                            activeNpcGameObject = null;
                            break;
                        }
                        yield return null;
                    }

                    if (activeNpcGameObject != null)
                    {
                        SafeDestroy(activeNpcGameObject);
                        activeNpcGameObject = null;
                    }
                }
                else if (activeNpc != null)
                {
                    bool exited = false;
                    activeNpc.ReactAndExit(isPositive, report.presentationCue, () =>
                    {
                        exited = true;
                    });

                    while (!exited && activeNpcGameObject != null)
                    {
                        yield return null;
                    }

                    if (activeNpcGameObject != null)
                    {
                        SafeDestroy(activeNpcGameObject);
                        activeNpcGameObject = null;
                    }
                }

                activeNpc = null;
                activeNpcGameObject = null;
            }
            else
            {
                yield return new WaitForSeconds(0.05f);
            }

            OnConsequencesFinished?.Invoke(report);
            stateMachine?.CompleteTurnAndAdvance();
        }

        private void SafeDestroy(GameObject obj)
        {
            if (obj == null) return;
            obj.SetActive(false);
#if UNITY_EDITOR
            if (UnityEditor.Selection.activeGameObject == obj || 
                (UnityEditor.Selection.activeGameObject != null && UnityEditor.Selection.activeGameObject.transform.IsChildOf(obj.transform)) ||
                (UnityEditor.Selection.objects != null && System.Array.Exists(UnityEditor.Selection.objects, o => o is GameObject go && go != null && (go == obj || go.transform.IsChildOf(obj.transform)))))
            {
                UnityEditor.Selection.objects = new UnityEngine.Object[0];
                UnityEditor.Selection.activeGameObject = null;
            }
#endif
            Destroy(obj);
        }

        private void StopActiveRoutine()
        {
            if (activePresentationRoutine != null)
            {
                StopCoroutine(activePresentationRoutine);
                activePresentationRoutine = null;
            }
        }

        private void OnDestroy()
        {
            StopActiveRoutine();

            if (stateMachine != null)
            {
                stateMachine.OnProposalReady -= PresentProposal;
                stateMachine.OnConsequencesReady -= PresentConsequences;
            }
        }
    }
}
