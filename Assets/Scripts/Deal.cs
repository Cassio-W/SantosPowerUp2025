using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Deal", menuName = "SO/New Deal")]
public class Deal: ScriptableObject
{
    [TextArea] public string Description;
    public string leftAnswer;
    public string rightAnswer;

    public Attributes impactsLeft;
    public Attributes impactsRight;

    public List<Deal> newDealsIfLeft;
    public List<Deal> newDealsIfRight;

}
