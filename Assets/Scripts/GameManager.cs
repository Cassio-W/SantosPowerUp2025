using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public class GameManager : MonoBehaviour
{
    //eventos principalmente para a UI depois
    public static event Action<Deal> OnNewDeal; // Disparado quando uma nova proposta é puxada
    public static event Action<Attributes> OnChangeAttributes; // Disparado quando qualquer decisão é tomada
    public static event Action<string> OnGameOver; // Disparado quando um atributo zera. O string pode ser a causa.
    public static event Action<string> OnGameWin;

    public static GameManager instance;

    [Header("Player")]
    public Attributes gameAttributes;
    public List<Perks> activePerks = new List<Perks>(3);

    [Header("Decks")]
    public List<Deal> allDeals = new List<Deal>();
    public List<Deal> actualDeck = new List<Deal>();
    public List<Deal> tutorialDeals = new List<Deal>();
    
    [Header("NPCs")]
    public NPCController actualNPC;

    [Header("Animations")]
    public List<string> animations = new List<string>();
    [SerializeField] Animator anim;
    [SerializeField] GameObject palmas;
    [SerializeField] bool playRandomReaction = false;

    [Header("Timers & Delays")]
    [Tooltip("Tempo de espera (em segundos) após o término da entrega do papel pelo NPC antes de disparar o evento da nova proposta e o jogador levantar a mão.")]
    public float delayAfterDelivery = 0.0f;

    [Header("Time")]
    public int month;
    public int year;
    public bool onTutorial;

    [Header("Props")]
    public GameObject city;

    private void Awake()
    {
        if (instance == null) instance = this; //Singleton
        if (gameAttributes == null)
        {
            gameAttributes = new Attributes();
        }
        else
        {
            gameAttributes.climaticChanges = 50;
            gameAttributes.internationalRelations = 50;
            gameAttributes.populationalApproval = 50;
            gameAttributes.economy = 50;
            gameAttributes.corruption = 0;
        }

        month = 1;
        year = 2026;
        onTutorial = true;
    }

    void Start()
    {
        gameAttributes.climaticChanges = 50;
        gameAttributes.internationalRelations = 50;
        gameAttributes.populationalApproval = 50;
        gameAttributes.economy = 50;
        gameAttributes.corruption = 0;

        // Notifica a UI e o Monitor Retrô com os valores iniciais de mandato
        OnChangeAttributes?.Invoke(gameAttributes);

        foreach (Deal deal in allDeals)
        {
            actualDeck.Add(deal);
        }
        ShuffleDeck();
        StartCoroutine(GetDeal());
    }

    void Update()
    {

    }

    public void ShuffleDeck()
    {
        if (!onTutorial)
        {
            actualDeck.Shuffle();
        }
    }

    public IEnumerator GetDeal()
    {
        yield return new WaitForSeconds(0.1f);
        if(actualDeck.Count >= 1)
        {

            if (onTutorial)
            {
                OnNewDeal.Invoke(tutorialDeals[0]);
            }
            else
            {
                GameObject npc = Instantiate(actualDeck[0].NPC, new Vector3(5.8f, 0, 3.65f), transform.rotation);
                actualNPC = npc.GetComponent<NPCController>();
                npc.transform.position = actualNPC.startPosition;
                actualNPC.MoveToTable();

                // 1. Aguarda o NPC caminhar e chegar até a mesa
                yield return new WaitWhile(() => actualNPC != null && !actualNPC.hasReachedTarget);

                // 2. Aguarda o NPC finalizar a animação de entrega do papel
                yield return new WaitWhile(() => actualNPC != null && !actualNPC.isDelivered);

                // 3. Delay suave pós-entrega para transição natural antes do jogador levantar a mão
                if (delayAfterDelivery > 0f)
                {
                    yield return new WaitForSeconds(delayAfterDelivery);
                }

                OnNewDeal.Invoke(actualDeck[0]);
            }
        }
    }

    public void ChooseLeft(Deal deal)
    {
        foreach (Deal newDeal in deal.newDealsIfLeft) actualDeck.Add(newDeal);
        if (deal.perkIfLeft != null)
        {
            deal.perkIfLeft.OnAquired();
            activePerks.Add(deal.perkIfLeft);
        }

    }

    public void ChooseRight(Deal deal)
    {
        foreach (Deal newDeal in deal.newDealsIfRight) actualDeck.Add(newDeal);
        if (deal.perkIfRight != null)
        {
            deal.perkIfRight.OnAquired();
            activePerks.Add(deal.perkIfRight);
        }
    }

    public IEnumerator ApplyDecision(Deal deal, Attributes impacts, bool isApproved = true)
    {
        gameAttributes.ApplyChanges(impacts, deal);
        OnChangeAttributes?.Invoke(gameAttributes);
        if (!onTutorial)
        {
            DisplayProp(impacts.prop);

            if (actualDeck != null && actualDeck.Count > 0)
            {
                actualDeck.RemoveAt(0);
            }

            if (CheckGameOver())
            {
                if (actualNPC != null)
                {
                    actualNPC.ReactAndExit(isApproved);
                }
                yield break;
            }

            ShuffleDeck();

            if (PassTime())
            {
                if (actualNPC != null)
                {
                    actualNPC.ReactAndExit(isApproved);
                }
                yield break;
            }

            if (actualNPC != null)
            {
                actualNPC.ReactAndExit(isApproved);
            }

            yield return new WaitForSeconds(0.5f);
            if (playRandomReaction)
            {
                StartCoroutine(RandomizeAnimation());
            }
            yield return new WaitForSeconds(5.5f);
            StartCoroutine(GetDeal());
        }
        else
        {
            if (tutorialDeals != null && tutorialDeals.Count > 0)
            {
                tutorialDeals.RemoveAt(0);
            }

            if (tutorialDeals == null || !tutorialDeals.Any())
            {
                onTutorial = false;
                StartCoroutine(GetDeal());
            }
            else
            {
                StartCoroutine(GetDeal());
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    public bool CheckGameOver()
    {
        if (gameAttributes.climaticChanges <= 0 ||
        gameAttributes.internationalRelations <= 0 ||
        gameAttributes.populationalApproval <= 0 ||
        gameAttributes.economy <= 0 ||
        gameAttributes.corruption >= 100)
        {
            OnGameOver?.Invoke("Olha o que você fez! Estragou tudo e agora vamos ter que te tirar da presidência. Boa sorte explicando seus erros para o povo.");
            actualDeck.Clear();
            return true;
        }
        return false;
    }

    public bool PassTime()
    {
        if(month == 12)
        {
            year++;
            month = 1;
        }
        else
        {
            month++;
        }

        if (year >= 2028)
        {
            Debug.Log("Cabo o jogo");
            //OnGameWin?.Invoke("Parabéns, chegamos em 2030 e seu mandato foi incrível, você tirou o país do lixo e impediu que o pior ocorresse. Obrigado.");
            OnGameWin?.Invoke("Parabéns, chegamos em 2028 e seu mandato têm sido incrível até agora mas, a Demo chegou ao fim. Obrigado por jogar! Siga @studiomicrowave e fique por dentro das novidades!");
            if (palmas != null)
            {
                Instantiate(palmas, new Vector3(0, 0, 2), Quaternion.identity);
            }
            actualDeck.Clear();
            return true;
        }
        return false;
    }

    public void PPFocus()
    {
        if (CameraFocusManager.Instance != null)
        {
            CameraFocusManager.Instance.SetFocusCameraEffect(true, 1.5f);
            return;
        }

        Volume pp = GetFocusVolume();
        LeanTween.value(0f, 1f, 1.5f).setOnUpdate((float weight) => {
            if (pp != null) pp.weight = weight;
            Shader.SetGlobalFloat("_EdgeBlurIntensity", weight);
        }).setEase(LeanTweenType.easeInOutQuad);
    }

    public void PPUnfocus()
    {
        if (CameraFocusManager.Instance != null)
        {
            CameraFocusManager.Instance.SetFocusCameraEffect(false, 1.5f);
            return;
        }

        Volume pp = GetFocusVolume();
        float currentWeight = pp != null ? pp.weight : Shader.GetGlobalFloat("_EdgeBlurIntensity");
        if (currentWeight <= 0.001f) currentWeight = 1f;

        LeanTween.value(currentWeight, 0f, 1.5f).setOnUpdate((float weight) => {
            if (pp != null) pp.weight = weight;
            Shader.SetGlobalFloat("_EdgeBlurIntensity", weight);
        }).setEase(LeanTweenType.easeInOutQuad);
    }

    private Volume GetFocusVolume()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        Volume[] volumes = cam.GetComponents<Volume>();
        foreach (var v in volumes)
        {
            if (v != null && v.sharedProfile != null && v.sharedProfile.name.Contains("1"))
            {
                return v;
            }
        }
        return cam.GetComponent<Volume>();
    }

    IEnumerator RandomizeAnimation()
    {
        if (anim == null && UIManager.instance != null) anim = UIManager.instance.GetPlayerAnimator();
        if (anim == null || anim.runtimeAnimatorController == null) yield break;
        if (animations == null || animations.Count == 0) yield break;

        List<string> valid = new List<string>();
        foreach (var a in animations)
        {
            if (!string.IsNullOrEmpty(a) && (anim.HasState(0, Animator.StringToHash(a)) || anim.HasState(0, Animator.StringToHash("Base Layer." + a))))
            {
                valid.Add(a);
            }
        }

        if (valid.Count > 0)
        {
            int choice = UnityEngine.Random.Range(0, valid.Count);
            anim.speed = 1f;
            anim.Play(valid[choice], 0, 0f);
            yield return new WaitForSeconds(1);

            int noneHash = Animator.StringToHash("None");
            if (anim.HasState(0, noneHash) || anim.HasState(0, Animator.StringToHash("Base Layer.None")))
            {
                anim.Play("None", 0, 0f);
            }
        }
    }

    void DisplayProp(GameObject prop)
    {
        if(prop != null)
        {
            GameObject p = Instantiate(prop, prop.transform.position, prop.transform.rotation);
            p.transform.SetParent(city.transform, false);
            LeanTween.scale(p, transform.localScale, 1.5f);
        }
    }
}
