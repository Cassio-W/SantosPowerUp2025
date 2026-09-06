using UnityEngine;

public abstract class Perks: ScriptableObject
{
    public string perkName;
    [TextArea] public string description;
    public Sprite icon;
    public bool isActive;
    public bool isOneTimeUse;

    public int activationCount;
    public float cooldownTimer;

    public abstract void OnAquired(GameManager gm);
    public abstract void OnActivated(Attributes check, GameManager gm);
    public abstract void UsePerk();
}
