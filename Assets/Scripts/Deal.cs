using System;
using System.Collections;
using System.Collections.Generic;
using System.Security;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Retorna a descrição formatada garantindo que cada frase termine com quebra de linha após o ponto.
    /// </summary>
    public string FormattedDescription => FormatSentenceBreaks(Description);

    private static readonly HashSet<string> Abbreviations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sr", "sra", "dr", "dra", "etc", "ex"
    };

    private static readonly Regex SentenceBreakRegex = new Regex(@"(?<!\.)\.(?![\.\d])([""''”’]?)\s*(?=\S)", RegexOptions.Compiled);

    /// <summary>
    /// Formata textos de propostas (deals) para que quebrem a linha após o ponto (.),
    /// garantindo que cada frase fique em uma linha própria, preservando reticências (...),
    /// números decimais (ex: 1.5, 10.000) e abreviações (Sr., Dr., etc).
    /// </summary>
    public static string FormatSentenceBreaks(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

        return SentenceBreakRegex.Replace(text, match =>
        {
            int start = match.Index;
            string prefix = text.Substring(0, start);
            var wordMatch = Regex.Match(prefix, @"([a-zA-ZáéíóúÁÉÍÓÚãõÃÕâêôÂÊÔçÇ]+)$");
            if (wordMatch.Success && Abbreviations.Contains(wordMatch.Value))
            {
                return match.Value; // Preserva abreviações como "Sr."
            }

            string quote = match.Groups[1].Value;
            return "." + quote + "\n";
        }).TrimEnd('\r', '\n');
    }
}

