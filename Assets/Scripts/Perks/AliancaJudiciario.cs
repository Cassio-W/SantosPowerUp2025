using UnityEngine;

[CreateAssetMenu(fileName = "AliancaJudiciario", menuName = "SO/New Perk/Alianca Judiciario")]
public class AliancaJudiciario : Perks
{
    public override void OnActivated(Attributes check, GameManager gm)
    {
        if (check.corruption >= 100)
        {
            check.corruption = 30;
            Debug.Log("Alianca com Judiciario ativada! Corrupcao reduzida para 30.");
            UsePerk();
        }
    }

    public override void OnAquired(GameManager gm)
    {
        Debug.Log("Alianca com Judiciario registrada");
    }

    public override void UsePerk()
    {
        base.UsePerk();
    }
}