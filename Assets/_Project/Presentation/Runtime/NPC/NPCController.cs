using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Mandato.Presentation
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp", null)]
    public class NPCController : MonoBehaviour, INpcController
    {
        [Header("Configuracoes")]
        public Vector3 targetPosition; // Posicao final na mesa
        public Vector3 startPosition;
        [SerializeField] private float walkSpeed = 2.5f;

        [Header("Animacoes")]
        public Animator animator;
        public string walkAnimation = "Walk";
        public string idleAnimation = "Idle";
        public string deliverAnimation = "Deliver";

        [Header("Reações Positivas")]
        [Tooltip("Animações tocadas quando a proposta é aprovada/aceita.")]
        public List<string> positiveReactions = new List<string>()
        {
            "SuperJoia",
            "SuperSalto",
            "Yeah"
        };

        [Header("Reações Negativas")]
        [Tooltip("Animações tocadas quando a proposta é rejeitada/recusada.")]
        public List<string> negativeReactions = new List<string>()
        {
            "Bravo",
            "Decepcao",
            "Espanto",
            "Morte",
            "Triste",
            "OlhandoLado"
        };

        [Header("Reações Neutras")]
        [Tooltip("Animações neutras que podem rodar independentemente da decisão tomada (aprovação ou rejeição).")]
        public List<string> neutralReactions = new List<string>()
        {
            "TantoFaz"
        };

        [Header("Configuração de Reação")]
        [Tooltip("Duração padrão de espera da reação antes de virar para sair.")]
        public float defaultReactionDuration = 2.0f;

        [Header("Configuração de Entrega")]
        [Tooltip("Duração padrão de espera da animação de entrega de papel caso o tempo do clipe não seja detectado.")]
        public float defaultDeliveryDuration = 0.8f;
        [Range(0.1f, 1f)]
        [Tooltip("Porcentagem/ponto da animação de entrega em que o papel já está na mesa e o player é liberado para puxar (ex: 0.5 = na metade do clipe).")]
        public float deliveryHandoverNormalizedTime = 0.5f;
        [Tooltip("Tempo adicional de espera após o ponto de entrega antes de liberar o player.")]
        public float deliveryPostDelay = 0f;

        [Header("Rotacao para Camera")]
        [Tooltip("Velocidade de rotacao suave para olhar para a camera.")]
        public float lookAtCameraSpeed = 5f;

        private NavMeshAgent agent;
        public bool hasReachedTarget = false;
        public bool isDelivered = false;
        private bool isExiting = false;
        private int exitStartFrame = -1;
        private Coroutine deliveryCoroutine;

        public AudioSource audioPassos;
        public AudioClip audioPapel;

        private Camera targetCamera;

        private void Awake()
        {
            EnsureComponentReferences();
        }

        private void Start()
        {
            EnsureComponentReferences();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void EnsureComponentReferences()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>() ?? GetComponentInChildren<NavMeshAgent>();
            }
            if (animator == null)
            {
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            }
            if (audioPassos == null)
            {
                audioPassos = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>();
            }
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        /// <summary>Define posições de spawn e mesa. Deve ser chamado antes de MoveToTable.</summary>
        public void SetPositions(Vector3 spawnPosition, Vector3 tablePosition)
        {
            startPosition = spawnPosition;
            targetPosition = tablePosition;
        }

        /// <summary>Retorna true quando o papel foi entregue e o NPC está aguardando decisão.</summary>
        public bool IsReadyForDismissal() => isDelivered;

        public void MoveToTable()
        {
            EnsureComponentReferences();
            isExiting = false;
            hasReachedTarget = false;
            isDelivered = false;

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(targetPosition);
            }

            PlayWalkAnimation();

            if (audioPassos != null && !audioPassos.isPlaying)
            {
                audioPassos.Play();
            }
        }

        /// <summary>
        /// Faz o NPC tocar uma animação de reação (positiva ou negativa, com chance de neutra) e, após a animação, mover-se para a saída.
        /// </summary>
        /// <param name="isPositive">True para reação positiva (aprovação), False para reação negativa (rejeição).</param>
        public void ReactAndExit(bool isPositive = true)
        {
            StartCoroutine(ReactAndExitRoutine(isPositive));
        }

        /// <summary>
        /// Corrotina que escolhe e executa a reação combinando a decisão tomada com as reações neutras e depois aciona a saída do NPC.
        /// Valida se o estado existe no Animator antes de tocar, fazendo fallback/re-roll entre estados válidos.
        /// </summary>
        public IEnumerator ReactAndExitRoutine(bool isPositive)
        {
            EnsureComponentReferences();

            List<string> candidateList = new List<string>();

            if (isPositive && positiveReactions != null)
            {
                candidateList.AddRange(positiveReactions);
            }
            else if (!isPositive && negativeReactions != null)
            {
                candidateList.AddRange(negativeReactions);
            }

            if (neutralReactions != null)
            {
                candidateList.AddRange(neutralReactions);
            }

            List<string> validCandidates = GetValidAnimationStates(candidateList);

            if (validCandidates.Count == 0)
            {
                List<string> allConfigured = new List<string>();
                if (positiveReactions != null) allConfigured.AddRange(positiveReactions);
                if (negativeReactions != null) allConfigured.AddRange(negativeReactions);
                if (neutralReactions != null) allConfigured.AddRange(neutralReactions);

                validCandidates = GetValidAnimationStates(allConfigured);
            }

            if (animator != null && validCandidates.Count > 0)
            {
                string chosenReaction = validCandidates[Random.Range(0, validCandidates.Count)];
                TryPlayAnimation(chosenReaction);

                yield return null;

                float duration = defaultReactionDuration;
                if (animator != null)
                {
                    AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.length > 0.1f)
                    {
                        duration = stateInfo.length;
                    }
                    else
                    {
                        float clipLen = GetClipDuration(animator, chosenReaction);
                        if (clipLen > 0.1f) duration = clipLen;
                    }
                }

                yield return new WaitForSeconds(duration);
            }
            else
            {
                yield return new WaitForSeconds(0.3f);
            }

            MoveToExit();
        }

        public void MoveToExit()
        {
            EnsureComponentReferences();
            isExiting = true;
            exitStartFrame = Time.frameCount;

            if (audioPassos != null && !audioPassos.isPlaying)
            {
                audioPassos.Play();
            }

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(startPosition);
            }

            PlayWalkAnimation();
        }

        private void Update()
        {
            EnsureComponentReferences();

            // 1. Caminhada até a mesa
            if (!hasReachedTarget && !isExiting)
            {
                bool arrivedByNavMesh = (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
                                         !agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.25f));

                float distToTable = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                     new Vector3(targetPosition.x, 0, targetPosition.z));
                bool arrivedByDistance = distToTable <= 0.35f;

                if (arrivedByNavMesh || arrivedByDistance)
                {
                    PlayDeliveryAnimation();
                    OnReachedTable();
                }
                else if (agent == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled)
                {
                    // Fallback manual de movimento
                    Vector3 moveDir = (targetPosition - transform.position);
                    moveDir.y = 0f;
                    if (moveDir.sqrMagnitude > 0.001f)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, targetPosition, walkSpeed * Time.deltaTime);
                        Quaternion targetRot = Quaternion.LookRotation(moveDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
                    }
                }
            }

            // 2. Olhar para a câmera após entregar e enquanto aguarda decisão
            if (isDelivered && !isExiting)
            {
                LookAtCameraY();
            }

            // 3. Caminhada até a saída
            if (isExiting && exitStartFrame >= 0 && (Time.frameCount - exitStartFrame) >= 5)
            {
                bool arrivedViaNavMesh = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
                                         !agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.35f);

                float distToExit = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                    new Vector3(startPosition.x, 0, startPosition.z));
                bool arrivedByDistance = distToExit <= 0.5f;

                if (arrivedViaNavMesh || arrivedByDistance)
                {
#if UNITY_EDITOR
                    if (UnityEditor.Selection.activeGameObject == gameObject || 
                        (UnityEditor.Selection.activeGameObject != null && UnityEditor.Selection.activeGameObject.transform.IsChildOf(transform)) ||
                        (UnityEditor.Selection.objects != null && System.Array.Exists(UnityEditor.Selection.objects, o => o is GameObject go && go != null && (go == gameObject || go.transform.IsChildOf(transform)))))
                    {
                        UnityEditor.Selection.objects = new UnityEngine.Object[0];
                        UnityEditor.Selection.activeGameObject = null;
                    }
#endif
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                }
                else if (agent == null || !agent.isOnNavMesh || !agent.isActiveAndEnabled)
                {
                    // Fallback manual para saída
                    Vector3 exitDir = (startPosition - transform.position);
                    exitDir.y = 0f;
                    if (exitDir.sqrMagnitude > 0.001f)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, startPosition, walkSpeed * Time.deltaTime);
                        Quaternion targetRot = Quaternion.LookRotation(exitDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
                    }
                }
            }
        }

        private void LookAtCameraY()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            Vector3 direction = targetCamera.transform.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookAtCameraSpeed);
            }
        }

        public void OnReachedTable()
        {
            hasReachedTarget = true;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
        }

        public void PlayDeliveryAnimation()
        {
            if (audioPassos != null)
            {
                audioPassos.Stop();
                if (audioPapel != null)
                {
                    audioPassos.PlayOneShot(audioPapel);
                }
            }
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            if (deliveryCoroutine != null)
            {
                StopCoroutine(deliveryCoroutine);
            }
            deliveryCoroutine = StartCoroutine(DeliveryRoutine());
        }

        private IEnumerator DeliveryRoutine()
        {
            float totalDuration = defaultDeliveryDuration;

            if (animator != null)
            {
                bool played = TryPlayAnimation(deliverAnimation, "deliver", "Delivery", "entrega");
                if (played)
                {
                    yield return null;

                    AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.length > 0.1f)
                    {
                        totalDuration = stateInfo.length;
                    }
                    else
                    {
                        float clipLen = GetClipDuration(animator, deliverAnimation);
                        if (clipLen > 0.1f) totalDuration = clipLen;
                    }
                }
            }

            float handoverTime = totalDuration * Mathf.Clamp01(deliveryHandoverNormalizedTime);
            if (handoverTime > 0f)
            {
                yield return new WaitForSeconds(handoverTime);
            }

            if (deliveryPostDelay > 0f)
            {
                yield return new WaitForSeconds(deliveryPostDelay);
            }

            isDelivered = true;

            float remainingTime = totalDuration - handoverTime;
            if (remainingTime > 0.05f)
            {
                yield return new WaitForSeconds(remainingTime);
            }

            PlayIdleAnimation();
        }

        public void PlayWalkAnimation()
        {
            TryPlayAnimation(walkAnimation, "walk", "Walking", "walking", "Walk_Fwd", "andar");
        }

        public void PlayIdleAnimation()
        {
            TryPlayAnimation(idleAnimation, "idle", "Idle_Neutral", "idle_neutral", "Idle1", "parado");
        }

        private bool TryPlayAnimation(string primaryName, params string[] fallbackNames)
        {
            if (animator == null) return false;

            if (!string.IsNullOrEmpty(primaryName) && HasAnimationState(animator, primaryName))
            {
                animator.Play(primaryName, 0, 0f);
                return true;
            }

            if (fallbackNames != null)
            {
                foreach (var fb in fallbackNames)
                {
                    if (!string.IsNullOrEmpty(fb) && HasAnimationState(animator, fb))
                    {
                        animator.Play(fb, 0, 0f);
                        return true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(primaryName))
            {
                try
                {
                    animator.Play(primaryName, 0, 0f);
                    return true;
                }
                catch { }
            }

            return false;
        }

        private float GetClipDuration(Animator anim, string clipOrStateName)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return 0f;
            foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
            {
                if (clip != null && (clip.name.Equals(clipOrStateName, System.StringComparison.OrdinalIgnoreCase) ||
                                     clipOrStateName.IndexOf(clip.name, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     clip.name.IndexOf(clipOrStateName, System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return clip.length;
                }
            }
            return 0f;
        }

        private List<string> GetValidAnimationStates(List<string> candidates)
        {
            List<string> valid = new List<string>();
            if (animator == null || candidates == null) return valid;

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && HasAnimationState(animator, candidate) && !valid.Contains(candidate))
                {
                    valid.Add(candidate);
                }
            }
            return valid;
        }

        public bool HasAnimationState(Animator anim, string stateName)
        {
            if (anim == null || anim.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName))
                return false;

            int hash = Animator.StringToHash(stateName);
            int baseLayerHash = Animator.StringToHash("Base Layer." + stateName);

            for (int i = 0; i < anim.layerCount; i++)
            {
                if (anim.HasState(i, hash) || anim.HasState(i, baseLayerHash))
                {
                    return true;
                }
            }

            // Checa se existe algum AnimationClip com o mesmo nome no controller
            foreach (var clip in anim.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name.Equals(stateName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void OnDeliveryComplete()
        {
            isDelivered = true;
            PlayIdleAnimation();
        }
    }
}

