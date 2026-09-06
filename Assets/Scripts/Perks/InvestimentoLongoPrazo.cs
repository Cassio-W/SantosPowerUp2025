using UnityEngine;

[CreateAssetMenu(fileName = "InvestimentoALongoPrazo", menuName = "SO/New Perk/Investimento Longo Prazo")]
public class InvestimentoLongoPrazo : Perks
{
    int monthWhenAquired;
    int yearWhenAquired;
    public override void OnActivated(Attributes check, GameManager gm)
    {
        Debug.Log($"Mês atual: {gm.month} \nAno atual: {gm.year}");
        if (gm.month == monthWhenAquired && gm.year == yearWhenAquired + 1)
        {
            Debug.Log("Ativou!");
            check.economy += 20;
            UsePerk();
        }
    }

    public override void OnAquired(GameManager gm)
    {
        GameManager.BeforeChangeAttributes += OnActivated;
        monthWhenAquired = gm.month;
        yearWhenAquired = gm.year;
        Debug.Log($"Registrou no mês {gm.month} e no ano {gm.year}");
    }

    public override void UsePerk()
    {
        GameManager.BeforeChangeAttributes -= OnActivated;
    }
}
