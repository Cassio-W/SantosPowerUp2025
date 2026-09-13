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
        private INpcController activeNpcController;

        private void Awake()
        {
            enabled = true;
        }

        private void OnEnable()
        {
            enabled = true;
        }

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

            if (gameObject != null && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            enabled = true;

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
                    activeNpcController = null;
                }

                Vector3 spawnPos = npcSpawnPoint != null ? npcSpawnPoint.position : defaultSpawnPosition;
                Quaternion spawnRot = npcSpawnPoint != null ? npcSpawnPoint.rotation : Quaternion.identity;

                activeNpcGameObject = Instantiate(prefabToSpawn, spawnPos, spawnRot);

                // Garante que o NavMeshAgent esteja perfeitamente posicionado na malha
                var navAgent = activeNpcGameObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent != null)
                {
                    navAgent.Warp(spawnPos);
                }

                // Tenta obter INpcController (implementado pelo NPCController legado)
                activeNpcController = activeNpcGameObject.GetComponent<INpcController>();

                if (activeNpcController != null)
                {
                    Debug.Log($"<color=#00ffaa>[RunPresentationCoordinator]</color> 🧑 NPC com INpcController instanciado. Spawn: {spawnPos}, Mesa: {defaultTablePosition}");

                    // Define posições antes de iniciar o movimento
                    activeNpcController.SetPositions(spawnPos, defaultTablePosition);
                    activeNpcController.MoveToTable();

                    // Aguarda o NPC caminhar até a mesa e entregar o papel (com timeout de segurança)
                    float walkTimeout = 0f;
                    while (activeNpcGameObject != null && walkTimeout < 15f)
                    {
                        walkTimeout += Time.deltaTime;
                        if (activeNpcController.IsReadyForDismissal()) break;
                        yield return null;
                    }

                    if (activeNpcGameObject == null)
                    {
                        Debug.LogWarning("<color=#ff5566>[RunPresentationCoordinator]</color> ⚠️ NPC destruído antes de entregar o papel!");
                        yield break;
                    }

                    Debug.Log($"<color=#00ffaa>[RunPresentationCoordinator]</color> 📄 Papel entregue após {walkTimeout:F1}s. Liberando proposta.");
                    yield return new WaitForSeconds(0.05f);
                    OnProposalOnDesk?.Invoke(card);
                }
                else
                {
                    // Fallback: usa NpcPresentation (sem NPCController)
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

            if (gameObject != null && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            enabled = true;

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

            yield return StartCoroutine(DismissNpcRoutine(isPositive));

            // Aguarda 1 frame para garantir que esta coroutine retorne ao Unity antes de
            // qualquer listener de OnConsequencesFinished disparar uma nova proposta sincronamente
            yield return null;

            OnConsequencesFinished?.Invoke(report);
        }

        public void DismissCurrentProposal(bool isPositive = false, Action onDismissed = null)
        {
            StopActiveRoutine();

            // Fallback: se activeNpcController estiver nulo, busca qualquer INpcController ativo na cena
            if (activeNpcController == null && activeNpcGameObject == null)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb is INpcController ctrl)
                    {
                        activeNpcController = ctrl;
                        activeNpcGameObject = mb.gameObject;
                        break;
                    }
                }
            }

            if (gameObject != null && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            enabled = true;

            activePresentationRoutine = StartCoroutine(DismissProposalRoutine(isPositive, onDismissed));
        }

        private IEnumerator DismissProposalRoutine(bool isPositive, Action onDismissed)
        {
            // Fallback de busca dentro da coroutine
            if (activeNpcController == null && activeNpcGameObject == null)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb is INpcController ctrl)
                    {
                        activeNpcController = ctrl;
                        activeNpcGameObject = mb.gameObject;
                        Debug.Log($"<color=#ffaa00>[RunPresentationCoordinator]</color> 🔍 INpcController encontrado via fallback: {mb.gameObject.name}");
                        break;
                    }
                }
            }

            yield return StartCoroutine(DismissNpcRoutine(isPositive));

            // Aguarda 1 frame para garantir que a coroutine retorne ao Unity antes de disparar o callback
            yield return null;

            onDismissed?.Invoke();
        }

        /// <summary>
        /// Coroutine central de dismissal: comanda reação + saída do NPC e aguarda ele sair/se destruir.
        /// Usada tanto por DismissProposalRoutine quanto por PresentConsequencesRoutine.
        /// </summary>
        private IEnumerator DismissNpcRoutine(bool isPositive)
        {
            Debug.Log($"<color=#ffaa00>[RunPresentationCoordinator]</color> 🔎 DismissNpcRoutine: activeNpcController={(activeNpcController != null ? "OK" : "NULL")}, activeNpcGameObject={(activeNpcGameObject != null ? activeNpcGameObject.name : "NULL")}");

            if (activeNpcController != null && activeNpcGameObject != null)
            {
                Debug.Log($"<color=#00ffaa>[RunPresentationCoordinator]</color> 🚪 Comandando ReactAndExit(isPositive={isPositive}) no NPC '{activeNpcGameObject.name}'.");

                activeNpcController.ReactAndExit(isPositive);

                // Aguarda o NPC completar sua animação de reação, caminhar até a porta e se destruir (timeout de 12s)
                float exitTimer = 0f;
                while (activeNpcGameObject != null && exitTimer < 12f)
                {
                    exitTimer += Time.deltaTime;
                    yield return null;
                }

                Debug.Log($"<color=#00ffaa>[RunPresentationCoordinator]</color> ✅ NPC saiu após {exitTimer:F1}s.");

                if (activeNpcGameObject != null)
                {
                    Debug.LogWarning("<color=#ff5566>[RunPresentationCoordinator]</color> ⚠️ NPC não se auto-destruiu no tempo esperado. Forçando destruição.");
                    SafeDestroy(activeNpcGameObject);
                }

                activeNpcGameObject = null;
                activeNpcController = null;
            }
            else if (activeNpc != null)
            {
                bool exited = false;
                activeNpc.ReactAndExit(isPositive, string.Empty, () =>
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
                }

                activeNpcGameObject = null;
            }
            else
            {
                Debug.LogWarning("<color=#ff5566>[RunPresentationCoordinator]</color> ⚠️ Nenhum NPC ativo para dispensar.");
                yield return new WaitForSeconds(0.05f);
            }

            activeNpc = null;
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
