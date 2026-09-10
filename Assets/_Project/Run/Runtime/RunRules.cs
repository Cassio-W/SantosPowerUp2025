using System;
using Mandato.Core;

namespace Mandato.Run
{
    public enum RunStatus
    {
        Ongoing,
        Defeat,
        Victory
    }

    [Serializable]
    public struct RunTermination
    {
        public RunStatus status;
        public string reason;

        public bool IsOngoing => status == RunStatus.Ongoing;
        public bool IsDefeat => status == RunStatus.Defeat;
        public bool IsVictory => status == RunStatus.Victory;

        public RunTermination(RunStatus status, string reason = "")
        {
            this.status = status;
            this.reason = reason ?? string.Empty;
        }

        public static RunTermination Ongoing => new RunTermination(RunStatus.Ongoing);
        public static RunTermination CreateDefeat(string reason) => new RunTermination(RunStatus.Defeat, reason);
        public static RunTermination CreateVictory(string reason = "Mandato Concluído com Sucesso!") => new RunTermination(RunStatus.Victory, reason);
    }

    public static class RunRules
    {
        public static RunTermination Evaluate(StatBlock stats, RunCalendar calendar)
        {
            if (stats == null) return RunTermination.Ongoing;

            // Condições de Derrota por Atributos
            if (stats.economy <= StatBlock.MinValue)
                return RunTermination.CreateDefeat("Colapso Econômico: O país entrou em falência fiscal.");

            if (stats.popularApproval <= StatBlock.MinValue)
                return RunTermination.CreateDefeat("Revolta Popular: O descontentamento generalizado levou ao impeachment.");

            if (stats.climaticChanges <= StatBlock.MinValue)
                return RunTermination.CreateDefeat("Colapso Climático: A degradação ambiental atingiu um ponto irreversível.");

            if (stats.internationalRelations <= StatBlock.MinValue)
                return RunTermination.CreateDefeat("Isolamento Internacional: O país sofreu sanções e perdeu apoio externo.");

            if (stats.corruption >= StatBlock.MaxValue)
                return RunTermination.CreateDefeat("Escândalo de Corrupção: O governo ruiu diante de escândalos sistemáticos.");

            // Condição de Vitória (Conclusão dos 48 meses de mandato)
            if (calendar != null && calendar.IsTermCompleted)
                return RunTermination.CreateVictory("Mandato Concluído: Você governou o país durante os 4 anos completos!");

            return RunTermination.Ongoing;
        }
    }
}
