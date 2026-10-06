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
        }

        private void SafeDestroy(GameObject obj)
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
