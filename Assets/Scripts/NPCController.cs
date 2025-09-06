using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCController : MonoBehaviour
{
    [Header("Configurações")]
    public Vector3 targetPosition; // Posição final na mesa
    public Vector3 startPosition;

    [Header("Animações")]
    public Animator animator;
    public string walkAnimation = "Walk";
    public string idleAnimation = "Idle";
    public string deliverAnimation = "Deliver";

    private NavMeshAgent agent;
    public bool hasReachedTarget = false;

    public AudioSource audioPassos;
    public AudioClip audioPapel;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioPassos = GetComponent<AudioSource>();
    }

    public void MoveToTable()
    {
        agent.SetDestination(targetPosition);
        animator.Play(walkAnimation);
        audioPassos.Play();
    }
    public void MoveToExit()
    {
        audioPassos.Play();
        agent.SetDestination(startPosition);
        animator.Play(walkAnimation);
    }

    private void Update()
    {
        if(transform.position.z == targetPosition.z)
        {
            if(hasReachedTarget == false) PlayDeliveryAnimation();
            OnReachedTable();
        }

        if(hasReachedTarget && transform.position.x >= startPosition.x)
        {
            Destroy(gameObject);
        }
    }

    public void OnReachedTable()
    {
        hasReachedTarget = true;
    }

    public void PlayDeliveryAnimation()
    {
        audioPassos.Stop();
        audioPassos.PlayOneShot(audioPapel);
        animator.Play(deliverAnimation);
        transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        Invoke("OnDeliveryComplete", 0.75f);
    }

    public void OnDeliveryComplete()
    {
        animator.Play(idleAnimation);
    }
}
