using UnityEngine;

[CreateAssetMenu(fileName = "TratadoInternacional", menuName = "SO/New Perk/Tratado Internacional")]
public class TratadoInternacional : Perks
{
    public override void OnActivated(Attributes check, GameManager gm)
    {
        if(check.internationalRelations > 50)
        {
            check.populationalApproval += 1;
        }
    }

    public override void OnAquired(GameManager gm)
    {
        Debug.Log("Tratado Internacional registrado");
    }

    public override void UsePerk()
    {
        base.UsePerk();
    }
}