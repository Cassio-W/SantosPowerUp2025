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
            if (propPrefab == null) return null;

            ResolveCityTransform();

            Transform parentTransform = cityTransform != null ? cityTransform : transform;
            GameObject spawned = Instantiate(propPrefab, propPrefab.transform.position, propPrefab.transform.rotation);
            spawned.transform.SetParent(parentTransform, false);

            Vector3 startScale = spawned.transform.localScale;
            Vector3 targetScale = customTargetScale ?? Vector3.one;

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

            ResolveCityTransform();
            Transform parentTransform = cityTransform != null ? cityTransform : transform;

            // Calcula o centro e base exatos para que os andaimes de Construcao envolvam a estrutura real no chão
            Vector3 constOffset = GetConstructionBaseOffset(constructionPrefab);
            Vector3 targetWorldBase = GetTargetPlacementWorldBase(targetPropPrefab, parentTransform);
            Vector3 targetLocalBase = parentTransform != null
                ? parentTransform.InverseTransformPoint(targetWorldBase)
                : targetWorldBase;

            Vector3 spawnLocalPos = targetLocalBase - constOffset;

            GameObject spawnedConstruction = Instantiate(constructionPrefab, parentTransform);
            spawnedConstruction.transform.localPosition = spawnLocalPos;
            spawnedConstruction.transform.localRotation = Quaternion.identity;

            Vector3 targetScale = constructionPrefab.transform.localScale;
            if (targetScale == Vector3.zero) targetScale = Vector3.one;

            Vector3 startScale = Vector3.one * 0.001f;
            spawnedConstruction.transform.localScale = startScale;

            if (Application.isPlaying)
            {
                StartCoroutine(AnimatePropScale(spawnedConstruction, startScale, targetScale, duration));
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

            Debug.Log($"<color=#00ffaa>[EnvironmentPresentation]</color> 🏗️ Canteiro de obras instanciado para investimento '{investmentId}' no local {spawnLocalPos}");

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

            return SpawnCityProp(realPropPrefab, customTargetScale, duration);
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

        public static (Vector3 position, Quaternion rotation) GetEffectiveTargetPlacement(GameObject targetPrefab)
        {
            if (targetPrefab == null) return (Vector3.zero, Quaternion.identity);

            Vector3 pos = targetPrefab.transform.position;
            Quaternion rot = targetPrefab.transform.rotation;

            // Se o prefab raiz estiver na origem (ou muito próximo) mas possuir filhos posicionados no mapa,
            // calcula o centro dos filhos diretos para posicionar a obra no local real.
            if (pos.sqrMagnitude < 0.01f && targetPrefab.transform.childCount > 0)
            {
                Vector3 sum = Vector3.zero;
                int validCount = 0;
                for (int i = 0; i < targetPrefab.transform.childCount; i++)
                {
                    var child = targetPrefab.transform.GetChild(i);
                    if (child != null)
                    {
                        sum += child.localPosition;
                        validCount++;
                    }
                }
                if (validCount > 0)
                {
                    pos = sum / validCount;
                }
            }

            return (pos, rot);
        }

        private static Vector3 GetChildrenCenterOffset(GameObject prefab)
        {
            if (prefab == null || prefab.transform.childCount == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < prefab.transform.childCount; i++)
            {
                var child = prefab.transform.GetChild(i);
                if (child != null)
                {
                    sum += child.localPosition;
                    count++;
                }
            }

            return count > 0 ? (sum / count) : Vector3.zero;
        }

        private static Vector3 GetConstructionBaseOffset(GameObject constPrefab)
        {
            if (constPrefab == null) return Vector3.zero;

            Renderer[] rends = constPrefab.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
            {
                return GetChildrenCenterOffset(constPrefab);
            }

            GameObject temp = Instantiate(constPrefab);
            temp.transform.position = Vector3.zero;
            temp.transform.rotation = Quaternion.identity;
            temp.transform.localScale = Vector3.one;

            Renderer[] tempRends = temp.GetComponentsInChildren<Renderer>();
            Vector3 offset = Vector3.zero;
            if (tempRends != null && tempRends.Length > 0)
            {
                Bounds b = new Bounds(tempRends[0].bounds.center, Vector3.zero);
                for (int i = 0; i < tempRends.Length; i++)
                {
                    b.Encapsulate(tempRends[i].bounds);
                }
                offset = new Vector3(b.center.x, b.min.y, b.center.z);
            }

            SafeDestroy(temp);
            return offset;
        }

        private static Vector3 GetTargetPlacementWorldBase(GameObject targetPrefab, Transform parentTransform)
        {
            if (targetPrefab == null) return parentTransform != null ? parentTransform.position : Vector3.zero;

            Renderer[] rends = targetPrefab.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
            {
                Vector3 fallbackPos = targetPrefab.transform.position;
                return parentTransform != null ? parentTransform.TransformPoint(fallbackPos) : fallbackPos;
            }

            GameObject temp = Instantiate(targetPrefab, parentTransform);
            temp.transform.localPosition = targetPrefab.transform.localPosition;
            temp.transform.localRotation = targetPrefab.transform.localRotation;
            temp.transform.localScale = targetPrefab.transform.localScale;

            Renderer[] tempRends = temp.GetComponentsInChildren<Renderer>();
            Vector3 worldBase;
            if (tempRends != null && tempRends.Length > 0)
            {
                Bounds b = new Bounds(tempRends[0].bounds.center, Vector3.zero);
                for (int i = 0; i < tempRends.Length; i++)
                {
                    b.Encapsulate(tempRends[i].bounds);
                }
                worldBase = new Vector3(b.center.x, b.min.y, b.center.z);
            }
            else
            {
                worldBase = temp.transform.position;
            }

            SafeDestroy(temp);
            return worldBase;
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
                elapsed += Time.deltaTime;
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
