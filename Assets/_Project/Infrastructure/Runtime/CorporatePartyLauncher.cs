using System;
using System.Collections;
using Mandato.Content;
using Mandato.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Implementação de IInteractiveEvent para o evento Festa Corporativa.
    /// Fica na cena principal (gabinete) registrado no InteractiveEventRegistry.
    /// Responsável por carregar a cena da festa de forma aditiva, entregar o
    /// controle ao PartyEventController e descarregá-la ao terminar.
    /// </summary>
    public class CorporatePartyLauncher : MonoBehaviour, IInteractiveEvent
    {
        [SerializeField] private CorporatePartyEventDefinition definition;

        public string EventId => definition != null ? definition.eventId : string.Empty;

        private void Awake()
        {
            // Auto-registro no InteractiveEventRegistry da mesma cena, se presente.
            var registry = GetComponentInParent<InteractiveEventRegistry>()
                        ?? FindFirstObjectByType<InteractiveEventRegistry>();
            registry?.Register(this);
        }

        private void OnDestroy()
        {
            var registry = FindFirstObjectByType<InteractiveEventRegistry>();
            registry?.Unregister(EventId);
        }

        public void Begin(RunState runState, Action<InteractiveEventResult> onCompleted)
        {
            if (definition == null)
            {
                Debug.LogError("[CorporatePartyLauncher] Sem definição configurada. Abortando evento.", this);
                onCompleted?.Invoke(new InteractiveEventResult { wasCompleted = false });
                return;
            }

            StartCoroutine(RunPartyRoutine(runState, onCompleted));
        }

        private IEnumerator RunPartyRoutine(RunState runState, Action<InteractiveEventResult> onCompleted)
        {
            // 1. Carrega a cena da festa de forma aditiva (não destrói o gabinete)
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(definition.sceneName, LoadSceneMode.Additive);
            yield return loadOp;

            // 2. Localiza o controller da cena recém-carregada via interface
            //    (evita referência direta ao tipo concreto em Assembly-CSharp)
            var controller = FindFirstObjectByType<UnityEngine.MonoBehaviour>() is IPartyEventController c
                ? c
                : null;

            // Busca mais robusta: itera todos os MonoBehaviours até encontrar IPartyEventController
            if (controller == null)
            {
                foreach (var mb in FindObjectsByType<UnityEngine.MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb is IPartyEventController candidate)
                    {
                        controller = candidate;
                        break;
                    }
                }
            }
            if (controller == null)
            {
                Debug.LogError($"[CorporatePartyLauncher] PartyEventController não encontrado na cena '{definition.sceneName}'. Abortando.", this);
                yield return SceneManager.UnloadSceneAsync(definition.sceneName);
                onCompleted?.Invoke(new InteractiveEventResult { wasCompleted = false });
                yield break;
            }

            // 3. Entrega o controle ao controller
            InteractiveEventResult finalResult = null;
            bool isDone = false;

            controller.Initialize(runState, definition, result =>
            {
                finalResult = result;
                isDone = true;
            });

            // 4. Aguarda o evento terminar (o controller chama o callback acima)
            while (!isDone)
                yield return null;

            // 5. Descarrega a cena da festa
            yield return SceneManager.UnloadSceneAsync(definition.sceneName);

            // 6. Devolve o controle ao RunFlowCoordinator com o resultado
            onCompleted?.Invoke(finalResult ?? new InteractiveEventResult { wasCompleted = false });
        }
    }
}
