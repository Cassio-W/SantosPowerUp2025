using UnityEngine;

[CreateAssetMenu(fileName = "AliancaJudiciario", menuName = "Scriptable Objects/New Perk")]
public class AliancaJudiciario : Perks
{
    public override void OnActivated(Attributes check)
    {
        if (check.corruption >= 100)
        {
            check.corruption = 30;
            Debug.Log("Ativou");
            UsePerk();
        }
    }

    public override void OnAquired()
    {
        GameManager.OnChangeAttributes += OnActivated;
        Debug.Log("Registrou");
    }

    public override void UsePerk()
    {
        GameManager.OnChangeAttributes -= OnActivated;
    }
}
