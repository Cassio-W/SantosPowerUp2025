using System.Collections;
using UnityEngine;

namespace Mandato.Presentation
{
    public class EnvironmentPresentation : MonoBehaviour
    {
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

        private void Awake()
        {
            if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) originalCameraPosition = cameraTransform.localPosition;
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
    }
}
