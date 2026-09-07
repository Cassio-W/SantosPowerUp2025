using UnityEngine;

[CreateAssetMenu(fileName = "AliancaEUA", menuName = "SO/New Perk/Alianca EUA")]
public class AliancaEUA : Perks
{
    public override void OnActivated(Attributes check, GameManager gm)
    {
        if (check.internationalRelations <= 0)
        {
            check.internationalRelations = 30;
            Debug.Log("Alianca com os EUA ativada! Corrupcao reduzida para 30.");
            UsePerk();
        }
    }

    public override void OnAquired(GameManager gm)
    {
        Debug.Log("Alianca com os EUA registrada");
    }

    public override void UsePerk()
    {
        base.UsePerk();
    }
}