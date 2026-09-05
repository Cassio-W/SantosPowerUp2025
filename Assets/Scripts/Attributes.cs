using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Attributes
{

    [Header("Atributos")]
    public int climaticChanges = 50;
    public int internationalRelations = 50;
    public int populationalApproval = 50;
    public int economy = 50;

    public int corruption = 0;

    public GameObject prop;

    public Attributes()
    {
        climaticChanges = 50;
        internationalRelations = 50;
        populationalApproval = 50;
        economy = 50;
        corruption = 0;
    }

    public Attributes(int climate, int relations, int approval, int eco, int corrupt)
    {
        climaticChanges = climate;
        internationalRelations = relations;
        populationalApproval = approval;
        economy = eco;
        corruption = corrupt;
    }

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
