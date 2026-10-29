using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mandato.Presentation
{
    public class EnvironmentPresentation : MonoBehaviour
    {
        [Header("Cenário da Cidade")]
        [Tooltip("Transform raiz da cidade onde as construções e props serão instanciados.")]
        public Transform cityTransform;

        [Header("Construções e Investimentos")]
        [Tooltip("Prefab do canteiro de obras instanciado enquanto um investimento estiver em andamento.")]
        public GameObject constructionPrefab;

        [Header("Posicionamento de Estruturas")]
        [Tooltip("Raiz opcional dos objetos de posicionamento (ex: 'posicaoEstruturas'). Se nulo, procura automaticamente.")]
        public Transform structurePositionsRoot;

        [Tooltip("Escala final do canteiro de obras. Se Vector3.zero, usa a escala gravada no prefab Construcao.")]
        public Vector3 constructionCustomScale = Vector3.one;

        [Tooltip("Duração da animação de escala do canteiro de obras em segundos.")]
        public float constructionAnimationDuration = 2.5f;

        private readonly Dictionary<string, GameObject> activeConstructions = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        [Header("Áudios e Efeitos")]
        public AudioSource sfxSource;
        public AudioClip applauseSound;
        public AudioClip protestSound;
        public AudioClip celebrationSound;
        public AudioClip scandalSound;

        [Header("Efeitos Visuais de Cena")]
        public GameObject applauseParticles;
        public Transform cameraTransform;

        private Vector3 originalCameraPosition;
        private Coroutine activeShakeRoutine;
        private readonly List<GameObject> spawnedProps = new List<GameObject>();

        private void Awake()
        {
            if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) originalCameraPosition = cameraTransform.localPosition;

            ResolveCityTransform();
            EnsureConstructionPrefabLoaded();
        }

        private void EnsureConstructionPrefabLoaded()
        {
            if (constructionPrefab != null) return;

#if UNITY_EDITOR
            constructionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Construcao.prefab");
#endif
        }

        private void ResolveCityTransform()
        {
            if (cityTransform == null)
            {
                var cityObj = GameObject.Find("Cidade") ?? GameObject.Find("cidade") ?? GameObject.Find("City");
                if (cityObj != null)
                {
                    cityTransform = cityObj.transform;
                }
            }
        }

        public void PlayPresentationCue(string cueName)
        {
            if (string.IsNullOrEmpty(cueName)) return;

            string normalized = cueName.Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "palmas":
                case "applause":
                    PlayApplause();
                    break;
                case "tremer_tela":
                case "shake":
                    ShakeCamera(0.3f, 0.15f);
                    break;
                case "protesto":
                    PlayProtest();
                    break;
                case "escandalo":
                    PlayScandal();
                    break;
                case "vitoria":
                    PlayCelebration();
                    break;
            }
        }

        public void PlayApplause()
        {
            if (applauseSound != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(applauseSound);
            }

            if (applauseParticles != null)
            {
                applauseParticles.SetActive(true);
                StartCoroutine(DeactivateAfterDelay(applauseParticles, 2.5f));
            }
        }

        public void PlayProtest()
        {
            if (protestSound != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(protestSound);
            }
            ShakeCamera(0.4f, 0.1f);
        }

        public void PlayScandal()
        {
            if (scandalSound != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(scandalSound);
            }
            ShakeCamera(0.5f, 0.2f);
        }

        public void PlayCelebration()
        {
            if (celebrationSound != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(celebrationSound);
            }
        }

        public void ShakeCamera(float duration = 0.3f, float intensity = 0.1f)
        {
            if (cameraTransform == null) return;

            if (activeShakeRoutine != null)
            {
                StopCoroutine(activeShakeRoutine);
                cameraTransform.localPosition = originalCameraPosition;
            }

            activeShakeRoutine = StartCoroutine(ShakeCameraRoutine(duration, intensity));
        }

        private IEnumerator ShakeCameraRoutine(float duration, float intensity)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (cameraTransform == null) yield break;

                cameraTransform.localPosition = originalCameraPosition + (Vector3)UnityEngine.Random.insideUnitCircle * intensity;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (cameraTransform != null)
            {
                cameraTransform.localPosition = originalCameraPosition;
            }
            activeShakeRoutine = null;
        }

        private IEnumerator DeactivateAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null) obj.SetActive(false);
        }

        public GameObject SpawnCityProp(GameObject propPrefab, Vector3? customTargetScale = null, float duration = 1.5f)
        {
            return SpawnCityProp(propPrefab, null, customTargetScale, duration);
        }

        public GameObject SpawnCityProp(GameObject propPrefab, Transform anchor, Vector3? customTargetScale = null, float duration = 1.5f)
        {
            if (propPrefab == null) return null;

            GameObject spawned;
            Vector3 targetScale;

            if (anchor != null)
            {
                spawned = Instantiate(propPrefab, anchor);
                spawned.transform.localPosition = Vector3.zero;
                spawned.transform.localRotation = Quaternion.identity;
                targetScale = customTargetScale ?? (propPrefab.transform.localScale != Vector3.zero ? propPrefab.transform.localScale : Vector3.one);
            }
            else
            {
                ResolveCityTransform();
                Transform parentTransform = cityTransform != null ? cityTransform : transform;
                spawned = Instantiate(propPrefab, propPrefab.transform.position, propPrefab.transform.rotation);
                spawned.transform.SetParent(parentTransform, false);
                targetScale = customTargetScale ?? Vector3.one;
            }

            Vector3 startScale = Vector3.zero;
            spawned.transform.localScale = startScale;

            if (Application.isPlaying)
            {
                StartCoroutine(AnimatePropScale(spawned, startScale, targetScale, duration));
            }
            else
            {
                spawned.transform.localScale = targetScale;
            }

            spawnedProps.Add(spawned);
            return spawned;
        }

        public void ClearSpawnedProps()
        {
            for (int i = 0; i < spawnedProps.Count; i++)
            {
                if (spawnedProps[i] != null)
                {
                    SafeDestroy(spawnedProps[i]);
                }
            }
            spawnedProps.Clear();
            activeConstructions.Clear();
        }

        public Transform GetAnchorForInvestment(string investmentId, GameObject targetPropPrefab = null)
        {
            if (structurePositionsRoot == null)
            {
                var found = GameObject.Find("posicaoEstruturas");
                if (found != null) structurePositionsRoot = found.transform;
            }

            if (structurePositionsRoot == null) return null;

            string id = investmentId?.ToLowerInvariant() ?? "";
            string prefabName = targetPropPrefab != null ? targetPropPrefab.name.ToLowerInvariant() : "";

            for (int i = 0; i < structurePositionsRoot.childCount; i++)
            {
                var child = structurePositionsRoot.GetChild(i);
                string childName = child.name.ToLowerInvariant();

                if (!string.IsNullOrEmpty(id) && (childName.Contains(id) || id.Contains(childName)))
                    return child;

                if ((id.Contains("fabrica") || prefabName.Contains("fabrica")) && childName.Contains("fabrica"))
                    return child;
                if ((id.Contains("eolica") || prefabName.Contains("turbina")) && (childName.Contains("turbina") || childName.Contains("eolica")))
                    return child;
                if ((id.Contains("solar") || prefabName.Contains("solar")) && childName.Contains("solar"))
                    return child;
                if ((id.Contains("nuclear") || id.Contains("usina") || prefabName.Contains("usina")) && (childName.Contains("usina") || childName.Contains("nuclear")))
                    return child;
            }

            return null;
        }

        /// <summary>
        /// Instancia o canteiro de obras pequeno e interpola até o tamanho normal no local onde a estrutura final do investimento será construída.
        /// </summary>
        public GameObject SpawnInvestmentConstruction(string investmentId, GameObject targetPropPrefab, float duration = 1.5f)
        {
            if (targetPropPrefab == null) return null;

            EnsureConstructionPrefabLoaded();
            if (constructionPrefab == null)
            {
                Debug.LogWarning("[EnvironmentPresentation] constructionPrefab não configurado para canteiro de obras de investimento.");
                return null;
            }

            // Se já existia uma obra em andamento para este mesmo ID, limpa a anterior
            if (!string.IsNullOrEmpty(investmentId) && activeConstructions.TryGetValue(investmentId, out var existing))
            {
                if (existing != null)
                {
                    spawnedProps.Remove(existing);
                    SafeDestroy(existing);
                }
                activeConstructions.Remove(investmentId);
            }

            Transform anchor = GetAnchorForInvestment(investmentId, targetPropPrefab);
            GameObject spawnedConstruction;

            if (anchor != null)
            {
                spawnedConstruction = Instantiate(constructionPrefab, anchor);
                spawnedConstruction.transform.localPosition = Vector3.zero;
                spawnedConstruction.transform.localRotation = Quaternion.identity;
            }
            else
            {
                ResolveCityTransform();
                Transform parentTransform = cityTransform != null ? cityTransform : transform;

                Vector3 spawnPos = targetPropPrefab.transform.position;
                Quaternion spawnRot = targetPropPrefab.transform.rotation;

                // Se a raiz do prop estiver em (0,0,0) e os filhos estiverem posicionados no mapa (ex: PaineisSolares ou Turbinas):
                if (spawnPos.sqrMagnitude < 0.01f && targetPropPrefab.transform.childCount > 0)
                {
                    Vector3 sum = Vector3.zero;
                    for (int i = 0; i < targetPropPrefab.transform.childCount; i++)
                    {
                        var child = targetPropPrefab.transform.GetChild(i);
                        if (child != null)
                        {
                            sum += child.localPosition;
                        }
                    }
                    spawnPos = sum / targetPropPrefab.transform.childCount;
                    spawnRot = Quaternion.identity;
                }

                spawnedConstruction = Instantiate(constructionPrefab, spawnPos, spawnRot);
                spawnedConstruction.transform.SetParent(parentTransform, false);
            }

            Vector3 targetScale = constructionCustomScale != Vector3.zero
                ? constructionCustomScale
                : (constructionPrefab.transform.localScale != Vector3.zero ? constructionPrefab.transform.localScale : Vector3.one);

            Vector3 startScale = Vector3.zero;
            spawnedConstruction.transform.localScale = startScale;

            float animDuration = duration > 0f ? duration : constructionAnimationDuration;

            if (Application.isPlaying)
            {
                StartCoroutine(AnimatePropScale(spawnedConstruction, startScale, targetScale, animDuration));
            }
            else
            {
                spawnedConstruction.transform.localScale = targetScale;
            }

            if (!string.IsNullOrEmpty(investmentId))
            {
                activeConstructions[investmentId] = spawnedConstruction;
            }
            spawnedProps.Add(spawnedConstruction);

            Debug.Log($"<color=#00ffaa>[EnvironmentPresentation]</color> 🏗️ Canteiro de obras instanciado para investimento '{investmentId}' no anchor '{(anchor != null ? anchor.name : "fallback")}'");

            return spawnedConstruction;
        }

        /// <summary>
        /// Ao concluir o investimento, troca o prefab de construção pela estrutura real do investimento.
        /// </summary>
        public GameObject CompleteInvestmentConstruction(string investmentId, GameObject realPropPrefab, Vector3? customTargetScale = null, float duration = 1.5f)
        {
            if (!string.IsNullOrEmpty(investmentId) && activeConstructions.TryGetValue(investmentId, out var constructionObj))
            {
                if (constructionObj != null)
                {
                    spawnedProps.Remove(constructionObj);
                    SafeDestroy(constructionObj);
                }
                activeConstructions.Remove(investmentId);
            }

            Transform anchor = GetAnchorForInvestment(investmentId, realPropPrefab);
            return SpawnCityProp(realPropPrefab, anchor, customTargetScale, duration);
        }

        /// <summary>
        /// Cancela a obra caso o investimento seja cancelado durante a run.
        /// </summary>
        public void CancelInvestmentConstruction(string investmentId)
        {
            if (string.IsNullOrEmpty(investmentId)) return;

            if (activeConstructions.TryGetValue(investmentId, out var constructionObj))
            {
                if (constructionObj != null)
                {
                    spawnedProps.Remove(constructionObj);
                    SafeDestroy(constructionObj);
                }
                activeConstructions.Remove(investmentId);
            }
        }

        public GameObject GetActiveConstruction(string investmentId)
        {
            if (string.IsNullOrEmpty(investmentId)) return null;
            activeConstructions.TryGetValue(investmentId, out var go);
            return go;
        }

        public bool HasActiveConstruction(string investmentId)
        {
            if (string.IsNullOrEmpty(investmentId)) return false;
            return activeConstructions.ContainsKey(investmentId) && activeConstructions[investmentId] != null;
        }

        private static void SafeDestroy(GameObject obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private IEnumerator AnimatePropScale(GameObject prop, Vector3 startScale, Vector3 targetScale, float duration = 1.5f)
        {
            if (prop == null) yield break;

            prop.transform.localScale = startScale;
            float elapsed = 0f;

            while (elapsed < duration && prop != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Back ease out: s = 1.70158
                float s = 1.70158f;
                float tNorm = t - 1f;
                float eased = (tNorm * tNorm * ((s + 1f) * tNorm + s) + 1f);
                prop.transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, Mathf.Max(0f, eased));
                yield return null;
            }

            if (prop != null)
            {
                prop.transform.localScale = targetScale;
            }
        }
    }
}
