using UnityEngine;

[CreateAssetMenu(fileName = "ReservaFlorestal", menuName = "SO/New Perk/Reserva Florestal")]
public class ReservaFlorestal : Perks
{
    public override void OnActivated(Attributes check, GameManager gm)
    {
        if (check.climaticChanges <= 0)
        {
            check.climaticChanges = 35;
            Debug.Log("Reserva Florestal ativada! Meio-ambiente restaurado para 35.");
            UsePerk();
        }
    }

    public override void OnAquired(GameManager gm)
    {
        Debug.Log("Reserva Florestal registrada");
    }

    public override void UsePerk()
    {
        base.UsePerk();
    }
}