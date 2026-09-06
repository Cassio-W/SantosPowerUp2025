using UnityEngine;

[CreateAssetMenu(fileName = "ReservaFlorestal", menuName = "SO/New Perk/Reserva Florestal")]
public class ReservaFlorestal: Perks
{
    public override void OnActivated(Attributes check, GameManager gm)
    {
        if (check.climaticChanges <= 0)
        {
            check.climaticChanges = 35;
            Debug.Log("Ativou");
            UsePerk();
        }
    }

    public override void OnAquired(GameManager gm)
    {
        GameManager.OnChangeAttributes += OnActivated;
        Debug.Log("Registrou");
    }

    public override void UsePerk()
    {
        GameManager.OnChangeAttributes -= OnActivated;
    }
}
