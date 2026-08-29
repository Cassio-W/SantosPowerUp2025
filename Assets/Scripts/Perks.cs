using UnityEngine;

public abstract class Perks: ScriptableObject
{
    public string perkName;
    public string description;
    public Sprite icon;
    public bool isActive;
    public bool isOneTimeUse;

    public int activationCount;
    public float cooldownTimer;

    public abstract void OnAquired();
    public abstract void OnActivated(Attributes check);
    public abstract void UsePerk();
}
