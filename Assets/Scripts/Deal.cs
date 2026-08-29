using System.Collections;
using System.Collections.Generic;
using System.Security;
using UnityEngine;

[CreateAssetMenu(fileName = "New Deal", menuName = "SO/New Deal")]
public class Deal: ScriptableObject
{
    [TextArea] public string Description;
    public string leftAnswer;
    public string rightAnswer;

    public bool hasCorruptionMods;

    public Attributes impactsLeft;
    public Attributes impactsRight;

    public List<Deal> newDealsIfLeft;
    public List<Deal> newDealsIfRight;

    public Perks perkIfLeft;
    public Perks perkIfRight;

    public GameObject NPC;
    public string tag;

}
