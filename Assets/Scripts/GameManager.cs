using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    //eventos principalmente para a UI depois
    public static event Action<Deal> OnNewDeal; // Disparado quando uma nova proposta é puxada
    public static event Action<Attributes> OnChangeAttributes; // Disparado quando qualquer decisão é tomada
    public static event Action<string> OnGameOver; // Disparado quando um atributo zera. O string pode ser a causa.

    public static GameManager instance;
    public Attributes gameAttributes;

    public List<Deal> allDeals = new List<Deal>();
    public List<Deal> actualDeck = new List<Deal>();
    public List<Deal> tutorialDeals = new List<Deal>();

    public int month;
    public int year;
    public bool onTutorial;
    int tutorialStep;

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
        ShuffleDeck();
        Invoke("GetDeal", 5);
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

    public void GetDeal()
    {
        if (onTutorial)
        {
            OnNewDeal.Invoke(tutorialDeals[0]);
        }
        else
        {
            OnNewDeal.Invoke(actualDeck[0]);
        }
    }

    public IEnumerator ApplyDecision(Deal deal, Attributes impacts)
    {
        gameAttributes.ApplyChanges(impacts);
        OnChangeAttributes?.Invoke(gameAttributes);
        if (!onTutorial)
        {
            CheckGameOver();
            actualDeck.Remove(actualDeck[0]);
            ShuffleDeck();
            PassTime();
            yield return new WaitForSeconds(10);
            GetDeal();
        }
        else
        {
            tutorialDeals.Remove(tutorialDeals[0]);
            if (!tutorialDeals.Any())
            {
                onTutorial = false;
                Invoke("GetDeal", 5);
            }
            else
            {
                GetDeal();
            }
        }
        

        foreach (Deal newDeal in deal.newDealsIfLeft)
        {
            actualDeck.Add(newDeal);
        }
        foreach (Deal newDeal in deal.newDealsIfRight)
        {
            actualDeck.Add(newDeal);
        }
    }

    public void CheckGameOver()
    {
        if (gameAttributes.climaticChanges <= 0 ||
        gameAttributes.internationalRelations <= 0 ||
        gameAttributes.populationalApproval <= 0 ||
        gameAttributes.economy <= 0)
        {
            OnGameOver.Invoke("Perdeu Mané");
            Time.timeScale = 0;
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
            Time.timeScale = 0;
        }
    }
}
