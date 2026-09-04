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
    [SerializeField]List<string> animations = new List<string>();
    [SerializeField] Animator anim;
    [SerializeField] GameObject palmas;
    [SerializeField] bool playRandomReaction = false;

    [Header("Time")]
    public int month;
    public int year;
    public bool onTutorial;

    [Header("Props")]
    public GameObject city;

    private void Awake()
    {
        if(instance == null) instance = this; //Singleton
        gameAttributes = new Attributes();

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
                yield return new WaitWhile(() => !actualNPC.hasReachedTarget);
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

    public IEnumerator ApplyDecision(Deal deal, Attributes impacts)
    {
        gameAttributes.ApplyChanges(impacts, deal);
        OnChangeAttributes?.Invoke(gameAttributes);
        if (!onTutorial)
        {
            DisplayProp(impacts.prop);
            CheckGameOver();
            actualDeck.Remove(actualDeck[0]);
            ShuffleDeck();
            PassTime();
            actualNPC.MoveToExit();
            yield return new WaitForSeconds(0.5f);
            if (playRandomReaction)
            {
                StartCoroutine(RandomizeAnimation());
            }
            yield return new WaitForSeconds(5);
            StartCoroutine(GetDeal());
        }
        else
        {
            tutorialDeals.Remove(tutorialDeals[0]);
            if (!tutorialDeals.Any())
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

    public void CheckGameOver()
    {
        if (gameAttributes.climaticChanges <= 0 ||
        gameAttributes.internationalRelations <= 0 ||
        gameAttributes.populationalApproval <= 0 ||
        gameAttributes.economy <= 0 ||
        gameAttributes.corruption >= 100)
        {
            OnGameOver.Invoke("Olha o que você fez! Estragou tudo e agora vamos ter que te tirar da presidência. Boa sorte explicando seus erros para o povo.");
            actualDeck.Clear();
            StopAllCoroutines();
        }
    }

    public void PassTime()
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

        if (year == 2030)
        {
            Debug.Log("Cabo o jogo");
            OnGameWin.Invoke("Parabéns, chegamos em 2030 e seu mandato foi incrível, você tirou o país do lixo e impediu que o pior ocorresse. Obrigado.");
            Instantiate(palmas, new Vector3(0, 0, 2), Quaternion.identity);
            actualDeck.Clear();
            StopAllCoroutines();
        }
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
        int choice = UnityEngine.Random.Range(0, animations.Count());
        anim.Play(animations[choice]);
        yield return new WaitForSeconds(1);
        anim.Play("None");
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
