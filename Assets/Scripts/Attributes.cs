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

    public void ApplyChanges(Attributes impacts)
    {
        climaticChanges += impacts.climaticChanges;
        internationalRelations += impacts.internationalRelations;
        populationalApproval += impacts.populationalApproval;
        economy += impacts.economy;

        if(climaticChanges > 100) climaticChanges = 100;
        if (internationalRelations > 100) internationalRelations = 100;
        if (populationalApproval > 100) populationalApproval = 100;
        if (economy > 100) economy = 100;
    }
}
