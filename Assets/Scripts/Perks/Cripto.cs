using UnityEngine;

[CreateAssetMenu(fileName = "InvestimentoALongoPrazo", menuName = "SO/New Perk/Investimento em Cripto")]
public class Cripto : Perks
{
    public override void OnActivated(Attributes check, GameManager gm)
    {
        Debug.Log("Ativou!");
        check.economy += 1;

    }

    public override void OnAquired(GameManager gm)
    {
        GameManager.BeforeChangeAttributes += OnActivated;
        Debug.Log($"Registrou no mês {gm.month} e no ano {gm.year}");
    }

    public override void UsePerk()
    {
        GameManager.BeforeChangeAttributes -= OnActivated;
    }
}
