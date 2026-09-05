using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCController : MonoBehaviour
{
    [Header("Configuracoes")]
    public Vector3 targetPosition; // Posicao final na mesa
    public Vector3 startPosition;

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
    private Coroutine deliveryCoroutine;

    public AudioSource audioPassos;
    public AudioClip audioPapel;

    private Camera targetCamera;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioPassos = GetComponent<AudioSource>();
    }

    private void Start()
    {
        targetCamera = Camera.main;
    }

    public void MoveToTable()
    {
        isExiting = false;
        isDelivered = false;
        if (agent != null) agent.SetDestination(targetPosition);
        if (animator != null && !string.IsNullOrEmpty(walkAnimation) && HasAnimationState(animator, walkAnimation))
        {
            animator.Play(walkAnimation);
        }
        if (audioPassos != null) audioPassos.Play();
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
        List<string> candidateList = new List<string>();

        // Adiciona as reações específicas da decisão
        if (isPositive && positiveReactions != null)
        {
            candidateList.AddRange(positiveReactions);
        }
        else if (!isPositive && negativeReactions != null)
        {
            candidateList.AddRange(negativeReactions);
        }

        // Adiciona as reações neutras independentes
        if (neutralReactions != null)
        {
            candidateList.AddRange(neutralReactions);
        }

        // Filtra apenas as animações candidatas que realmente existem no Animator do NPC
        List<string> validCandidates = GetValidAnimationStates(candidateList);

        // Fallback: caso nenhuma das candidatas específicas/neutras exista no Animator deste NPC,
        // busca qualquer outra reação configurada (positiva, negativa ou neutra) que exista no Animator
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
            // Sorteia entre os estados confirmados no Animator
            string chosenReaction = validCandidates[Random.Range(0, validCandidates.Count)];
            animator.Play(chosenReaction, 0, 0f);

            // Aguarda um frame para o Animator atualizar o estado atual
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
            // Se nenhuma animação de reação existir no Animator, aguarda brevemente antes de sair
            yield return new WaitForSeconds(0.3f);
        }

        MoveToExit();
    }

    public void MoveToExit()
    {
        isExiting = true;
        if (audioPassos != null) audioPassos.Play();
        if (agent != null) agent.SetDestination(startPosition);
        if (animator != null && !string.IsNullOrEmpty(walkAnimation) && HasAnimationState(animator, walkAnimation))
        {
            animator.Play(walkAnimation);
        }
    }

    private void Update()
    {
        if (!hasReachedTarget)
        {
            bool reachedByZ = Mathf.Abs(transform.position.z - targetPosition.z) <= 0.08f;
            bool reachedByNavMesh = (agent != null && !agent.pathPending && agent.hasPath && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.15f));

            if (reachedByZ || reachedByNavMesh)
            {
                PlayDeliveryAnimation();
                OnReachedTable();
            }
        }

        // Apos entregar e enquanto nao estiver saindo, sempre olha em direcao a camera (apenas rotacionando no eixo Y)
        if (isDelivered && !isExiting)
        {
            LookAtCameraY();
        }

        if (hasReachedTarget && transform.position.x >= startPosition.x - 0.2f)
        {
            Destroy(gameObject);
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
        direction.y = 0f; // Manter apenas rotacao horizontal (eixo Y)

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookAtCameraSpeed);
        }
    }

    public void OnReachedTable()
    {
        hasReachedTarget = true;
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

        if (animator != null && !string.IsNullOrEmpty(deliverAnimation) && HasAnimationState(animator, deliverAnimation))
        {
            animator.Play(deliverAnimation, 0, 0f);
            yield return null; // Aguarda 1 frame para o Animator atualizar o estado

            if (animator != null)
            {
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

        // Ponto onde a mão do NPC posiciona o papel na mesa (fração da duração total)
        float handoverTime = totalDuration * Mathf.Clamp01(deliveryHandoverNormalizedTime);
        if (handoverTime > 0f)
        {
            yield return new WaitForSeconds(handoverTime);
        }

        if (deliveryPostDelay > 0f)
        {
            yield return new WaitForSeconds(deliveryPostDelay);
        }

        // Libera imediatamente o GameManager e o Player para puxar a proposta
        isDelivered = true;

        // Aguarda o restante da animação do NPC terminar antes de alternar para Idle
        float remainingTime = totalDuration - handoverTime;
        if (remainingTime > 0.05f)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        if (animator != null && !string.IsNullOrEmpty(idleAnimation) && HasAnimationState(animator, idleAnimation))
        {
            animator.Play(idleAnimation);
        }
    }

    private float GetClipDuration(Animator anim, string clipOrStateName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return 0f;
        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip != null && (clip.name == clipOrStateName || clipOrStateName.Contains(clip.name) || clip.name.Contains(clipOrStateName)))
            {
                return clip.length;
            }
        }
        return 0f;
    }

    /// <summary>
    /// Filtra uma lista de nomes de estados retornando apenas os que existem no Animator do NPC.
    /// </summary>
    private List<string> GetValidAnimationStates(List<string> candidates)
    {
        List<string> valid = new List<string>();
        if (animator == null || animator.runtimeAnimatorController == null || candidates == null)
            return valid;

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) && HasAnimationState(animator, candidate) && !valid.Contains(candidate))
            {
                valid.Add(candidate);
            }
        }
        return valid;
    }

    /// <summary>
    /// Verifica se o Animator possui o estado especificado em alguma de suas camadas.
    /// </summary>
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

        return false;
    }

    public void OnDeliveryComplete()
    {
        isDelivered = true;
        if (animator != null && !string.IsNullOrEmpty(idleAnimation) && HasAnimationState(animator, idleAnimation))
        {
            animator.Play(idleAnimation);
        }
    }
}
