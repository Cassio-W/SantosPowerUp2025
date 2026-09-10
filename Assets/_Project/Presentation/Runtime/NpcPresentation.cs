using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Mandato.Presentation
{
    public class NpcPresentation : MonoBehaviour
    {
        [Header("Posicionamento")]
        public Vector3 tablePosition = new Vector3(0f, 0f, 2f);
        public Vector3 exitPosition = new Vector3(-8f, 0f, 2f);

        [Header("Animador e Estados")]
        public Animator animator;
        public string walkAnimation = "Walk";
        public string idleAnimation = "Idle";
        public string deliverAnimation = "Deliver";

        [Header("Reações")]
        public List<string> positiveReactions = new List<string> { "SuperJoia", "SuperSalto", "Yeah" };
        public List<string> negativeReactions = new List<string> { "Bravo", "Decepcao", "Espanto", "Triste", "OlhandoLado" };
        public List<string> neutralReactions = new List<string> { "TantoFaz" };

        [Header("Tempos")]
        public float defaultReactionDuration = 1.5f;
        public float defaultDeliveryDuration = 0.8f;
        [Range(0.1f, 1f)] public float deliveryHandoverNormalizedTime = 0.5f;
        public float lookAtCameraSpeed = 5f;

        [Header("Áudio")]
        public AudioSource audioSource;
        public AudioClip paperSound;

        private NavMeshAgent agent;
        private Camera targetCamera;
        private Coroutine activeRoutine;
        private bool isDelivered = false;
        private bool isExiting = false;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponent<Animator>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            targetCamera = Camera.main;
        }

        public void EnterAndDeliver(Action onDelivered)
        {
            StopActiveRoutine();
            activeRoutine = StartCoroutine(EnterAndDeliverRoutine(onDelivered));
        }

        private IEnumerator EnterAndDeliverRoutine(Action onDelivered)
        {
            isExiting = false;
            isDelivered = false;

            // 1. Caminha até a mesa
            if (agent != null) agent.SetDestination(tablePosition);
            PlayAnimation(walkAnimation);
            if (audioSource != null) audioSource.Play();

            while (true)
            {
                if (this == null || gameObject == null) yield break;

                bool reachedByZ = Mathf.Abs(transform.position.z - tablePosition.z) <= 0.1f;
                bool reachedByNav = agent != null && !agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.2f);

                if (reachedByZ || reachedByNav) break;
                yield return null;
            }

            if (audioSource != null) audioSource.Stop();

            // 2. Toca animação de entrega
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            if (paperSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(paperSound);
            }

            float deliveryDuration = defaultDeliveryDuration;
            if (animator != null && HasAnimationState(deliverAnimation))
            {
                animator.Play(deliverAnimation, 0, 0f);
                yield return null;

                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.length > 0.1f) deliveryDuration = info.length;
            }

            // Aguarda ponto de entrega
            yield return new WaitForSeconds(deliveryDuration * deliveryHandoverNormalizedTime);

            isDelivered = true;
            onDelivered?.Invoke();

            // Aguarda resto da animação de entrega
            yield return new WaitForSeconds(deliveryDuration * (1f - deliveryHandoverNormalizedTime));

            PlayAnimation(idleAnimation);
        }

        public void ReactAndExit(bool isPositive, string specificCue, Action onExited)
        {
            StopActiveRoutine();
            activeRoutine = StartCoroutine(ReactAndExitRoutine(isPositive, specificCue, onExited));
        }

        private IEnumerator ReactAndExitRoutine(bool isPositive, string specificCue, Action onExited)
        {
            isDelivered = false;

            // 1. Escolhe e toca a reação
            string reactionState = ChooseReaction(isPositive, specificCue);
            if (!string.IsNullOrEmpty(reactionState) && HasAnimationState(reactionState))
            {
                animator.Play(reactionState, 0, 0f);
                yield return null;

                float duration = defaultReactionDuration;
                if (animator != null)
                {
                    AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                    if (info.length > 0.1f) duration = info.length;
                }
                yield return new WaitForSeconds(duration);
            }
            else
            {
                yield return new WaitForSeconds(0.4f);
            }

            // 2. Caminha para a saída
            isExiting = true;
            if (agent != null) agent.SetDestination(exitPosition);
            PlayAnimation(walkAnimation);
            if (audioSource != null) audioSource.Play();

            while (true)
            {
                if (this == null || gameObject == null) yield break;

                bool reachedExit = Vector3.Distance(transform.position, exitPosition) <= 0.5f;
                if (reachedExit) break;
                yield return null;
            }

            if (audioSource != null) audioSource.Stop();
            onExited?.Invoke();
        }

        private string ChooseReaction(bool isPositive, string specificCue)
        {
            if (!string.IsNullOrEmpty(specificCue) && HasAnimationState(specificCue))
                return specificCue;

            var candidates = isPositive ? positiveReactions : negativeReactions;
            var valid = GetValidStates(candidates);
            if (valid.Count > 0)
            {
                return valid[UnityEngine.Random.Range(0, valid.Count)];
            }

            var neutralValid = GetValidStates(neutralReactions);
            return neutralValid.Count > 0 ? neutralValid[0] : string.Empty;
        }

        private void Update()
        {
            if (isDelivered && !isExiting)
            {
                LookAtCamera();
            }
        }

        private void LookAtCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            Vector3 dir = targetCamera.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * lookAtCameraSpeed);
            }
        }

        public void PlayAnimation(string stateName)
        {
            if (animator != null && !string.IsNullOrEmpty(stateName) && HasAnimationState(stateName))
            {
                animator.Play(stateName);
            }
        }

        public bool HasAnimationState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return false;

            int hash = Animator.StringToHash(stateName);
            int baseHash = Animator.StringToHash("Base Layer." + stateName);

            for (int i = 0; i < animator.layerCount; i++)
            {
                if (animator.HasState(i, hash) || animator.HasState(i, baseHash))
                    return true;
            }
            return false;
        }

        private List<string> GetValidStates(List<string> candidates)
        {
            var valid = new List<string>();
            if (candidates == null) return valid;

            foreach (var c in candidates)
            {
                if (!string.IsNullOrEmpty(c) && HasAnimationState(c)) valid.Add(c);
            }
            return valid;
        }

        private void StopActiveRoutine()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
        }

        private void OnDisable()
        {
            StopActiveRoutine();
        }
    }
}
