using System;
using System.Collections;
using System.Collections.Generic;
using Mandato.Content;
using Mandato.Run;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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

        public const string DefaultEventId = "FestaCorporativa";

        public string EventId
        {
            get
            {
                if (definition != null && !string.IsNullOrEmpty(definition.eventId))
                    return definition.eventId;
                return DefaultEventId;
            }
        }

        private void Awake()
        {
            EnsureDefinition();

            // Auto-registro no InteractiveEventRegistry da mesma cena, se presente.
            var registry = GetComponentInParent<InteractiveEventRegistry>()
                        ?? FindFirstObjectByType<InteractiveEventRegistry>();
            if (registry != null)
            {
                registry.Register(this);
                registry.RegisterAlias("FestaEvent", this);
                registry.RegisterAlias("JantarEvento", this);
            }
        }

        private void OnDestroy()
        {
            var registry = FindFirstObjectByType<InteractiveEventRegistry>();
            registry?.Unregister(EventId);
            registry?.Unregister("FestaEvent");
            registry?.Unregister("JantarEvento");
        }

        private void EnsureDefinition()
        {
            if (definition != null) return;

            definition = Resources.Load<CorporatePartyEventDefinition>("FestaEvent")
                      ?? Resources.Load<CorporatePartyEventDefinition>("CorporateParty");

#if UNITY_EDITOR
            if (definition == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CorporatePartyEventDefinition");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CorporatePartyEventDefinition>(path);
                }
            }
#endif
        }

        public void Begin(RunState runState, Action<InteractiveEventResult> onCompleted)
        {
            EnsureDefinition();

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
            Scene cabinetScene = SceneManager.GetActiveScene();
            string targetScene = !string.IsNullOrEmpty(definition?.sceneName) ? definition.sceneName : "JantarEvento";
            if (string.Equals(targetScene, "Event_CorporateParty", StringComparison.OrdinalIgnoreCase))
            {
                targetScene = "JantarEvento";
            }

            // Desativa temporariamente a câmera principal e áudio do gabinete
            var mainCam = Camera.main;
            var listener = mainCam != null ? mainCam.GetComponent<AudioListener>() : null;
            bool camWasActive = mainCam != null && mainCam.gameObject.activeSelf;
            bool listenerWasEnabled = listener != null && listener.enabled;

            if (listener != null) listener.enabled = false;
            if (mainCam != null) mainCam.gameObject.SetActive(false);

            // 1. Carrega a cena da festa de forma aditiva
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
            yield return loadOp;

            Scene partyScene = SceneManager.GetSceneByName(targetScene);

            // 2. Define a cena da festa como Ativa para que Unity aplique seus RenderSettings
            // (Iluminação ambiente, skybox, reflexos e lightmaps específicos do jantar)
            if (partyScene.IsValid() && partyScene.isLoaded)
            {
                SceneManager.SetActiveScene(partyScene);
            }

            // 3. Desativa temporariamente luzes direcionais, volumes de pós-processamento,
            // EventSystems e UIDocuments do gabinete para não poluir ou bloquear a UI/luz da festa.
            var disabledLights = new List<Light>();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l != null && l.gameObject.scene != partyScene && l.type == LightType.Directional && l.enabled)
                {
                    disabledLights.Add(l);
                    l.enabled = false;
                }
            }

            var disabledVolumes = new List<Volume>();
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v != null && v.gameObject.scene != partyScene && v.enabled)
                {
                    disabledVolumes.Add(v);
                    v.enabled = false;
                }
            }

            var disabledEventSystems = new List<UnityEngine.EventSystems.EventSystem>();
            foreach (var es in FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            {
                if (es != null && es.gameObject.scene != partyScene && es.enabled)
                {
                    disabledEventSystems.Add(es);
                    es.enabled = false;
                }
            }

            var disabledUiDocs = new List<UIDocument>();
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (doc != null && doc.gameObject.scene != partyScene && doc.enabled)
                {
                    disabledUiDocs.Add(doc);
                    doc.enabled = false;
                }
            }

            // 4. Localiza o controller da cena recém-carregada via interface
            IPartyEventController controller = null;
            foreach (var mb in FindObjectsByType<UnityEngine.MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb is IPartyEventController candidate && mb.gameObject.scene == partyScene)
                {
                    controller = candidate;
                    break;
                }
            }

            if (controller == null)
            {
                Debug.LogError($"[CorporatePartyLauncher] PartyEventController não encontrado na cena '{targetScene}'. Abortando.", this);
                if (cabinetScene.IsValid() && cabinetScene.isLoaded)
                    SceneManager.SetActiveScene(cabinetScene);

                yield return SceneManager.UnloadSceneAsync(targetScene);
                RestoreCabinetObjects(disabledLights, disabledVolumes, disabledEventSystems, disabledUiDocs, mainCam, camWasActive, listener, listenerWasEnabled);
                onCompleted?.Invoke(new InteractiveEventResult { wasCompleted = false });
                yield break;
            }

            // 5. Entrega o controle ao controller com o RunState da partida
            InteractiveEventResult finalResult = null;
            bool isDone = false;

            controller.Initialize(runState, definition, result =>
            {
                finalResult = result;
                isDone = true;
            });

            // 6. Aguarda o evento terminar
            while (!isDone)
                yield return null;

            // 7. Restaura a cena ativa para o gabinete antes de descarregar a festa
            if (cabinetScene.IsValid() && cabinetScene.isLoaded)
            {
                SceneManager.SetActiveScene(cabinetScene);
            }

            // 8. Descarrega a cena da festa
            yield return SceneManager.UnloadSceneAsync(targetScene);

            // 9. Restaura os componentes do gabinete
            RestoreCabinetObjects(disabledLights, disabledVolumes, disabledEventSystems, disabledUiDocs, mainCam, camWasActive, listener, listenerWasEnabled);

            // 10. Devolve o controle com o resultado
            onCompleted?.Invoke(finalResult ?? new InteractiveEventResult { wasCompleted = false });
        }

        private static void RestoreCabinetObjects(
            List<Light> lights,
            List<Volume> volumes,
            List<UnityEngine.EventSystems.EventSystem> eventSystems,
            List<UIDocument> uiDocs,
            Camera cam,
            bool camWasActive,
            AudioListener listener,
            bool listenerWasEnabled)
        {
            if (lights != null)
            {
                foreach (var l in lights)
                    if (l != null) l.enabled = true;
            }

            if (volumes != null)
            {
                foreach (var v in volumes)
                    if (v != null) v.enabled = true;
            }

            if (eventSystems != null)
            {
                foreach (var es in eventSystems)
                    if (es != null) es.enabled = true;
            }

            if (uiDocs != null)
            {
                foreach (var doc in uiDocs)
                    if (doc != null) doc.enabled = true;
            }

            if (cam != null) cam.gameObject.SetActive(camWasActive);
            if (listener != null) listener.enabled = listenerWasEnabled;
        }
    }
}
