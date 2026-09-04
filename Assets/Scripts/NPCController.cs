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

    [Header("Rotacao para Camera")]
    [Tooltip("Velocidade de rotacao suave para olhar para a camera.")]
    public float lookAtCameraSpeed = 5f;

    private NavMeshAgent agent;
    public bool hasReachedTarget = false;
    private bool isDelivered = false;
    private bool isExiting = false;

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
        if (animator != null && !string.IsNullOrEmpty(walkAnimation)) animator.Play(walkAnimation);
        if (audioPassos != null) audioPassos.Play();
    }

    public void MoveToExit()
    {
        isExiting = true;
        if (audioPassos != null) audioPassos.Play();
        if (agent != null) agent.SetDestination(startPosition);
        if (animator != null && !string.IsNullOrEmpty(walkAnimation)) animator.Play(walkAnimation);
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
        if (animator != null && !string.IsNullOrEmpty(deliverAnimation))
        {
            animator.Play(deliverAnimation);
        }
        transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        Invoke("OnDeliveryComplete", 0.75f);
    }

    public void OnDeliveryComplete()
    {
        isDelivered = true;
        if (animator != null && !string.IsNullOrEmpty(idleAnimation))
        {
            animator.Play(idleAnimation);
        }
    }
}
