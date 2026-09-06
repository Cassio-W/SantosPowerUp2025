using UnityEngine;

[CreateAssetMenu(fileName = "InvestimentoCripto", menuName = "SO/New Perk/Investimento em Cripto")]
public class Cripto : Perks
{
    public override void OnActivated(Attributes check, GameManager gm)
    {
        Debug.Log("Cripto ativou! +1 economia.");
        check.economy += 1;
    }

    public override void OnAquired(GameManager gm)
    {
        Debug.Log($"Cripto registrou no mes {gm.month} e no ano {gm.year}");
    }

    public override void UsePerk()
    {
        base.UsePerk();
    }
}