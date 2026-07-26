using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Attributes
{

    [Header("Atributos")]
    public int climaticChanges;
    public int internationalRelations;
    public int populationalApproval;
    public int economy;

    public int corruption;

    public GameObject prop;

    public void ApplyChanges(Attributes impacts, Deal deal)
    {
        if (deal.hasCorruptionMods)
        {
            climaticChanges += impacts.climaticChanges;
            internationalRelations += impacts.internationalRelations + Mathf.RoundToInt(impacts.corruption * 0.2f);
            populationalApproval += impacts.populationalApproval + Mathf.RoundToInt(impacts.corruption * 0.2f);
            economy += impacts.economy;
            corruption += impacts.corruption;
        }
        else
        {
            climaticChanges += impacts.climaticChanges;
            internationalRelations += impacts.internationalRelations;
            populationalApproval += impacts.populationalApproval;
            economy += impacts.economy;
            corruption += impacts.corruption;
        }


        if (climaticChanges > 100) climaticChanges = 100;
        if (internationalRelations > 100) internationalRelations = 100;
        if (populationalApproval > 100) populationalApproval = 100;
        if (economy > 100) economy = 100;
        if (corruption > 100) corruption = 100;
    }
}
