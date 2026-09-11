using System;

namespace Mandato.Core
{
    [Serializable]
    public class RunCalendar
    {
        public const int DefaultTotalMonths = 48; // 4 anos de mandato
        public const int DefaultStartYear = 2026;

        public static readonly string[] MonthNames = new[]
        {
            "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
            "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
        };

        public int currentMonthIndex = 1; // 1 a 48
        public int startYear = DefaultStartYear;
        public int totalMonths = DefaultTotalMonths;

        public RunCalendar(int initialMonthIndex = 1, int year = DefaultStartYear, int durationMonths = DefaultTotalMonths)
        {
            currentMonthIndex = Math.Max(1, initialMonthIndex);
            startYear = year;
            totalMonths = durationMonths;
        }

        public int MonthInYear => ((currentMonthIndex - 1) % 12) + 1; // 1 a 12

        public int Year => startYear + ((currentMonthIndex - 1) / 12);

        public string MonthName => MonthNames[MonthInYear - 1];

        public string DisplayDate => $"{MonthInYear:D2}/{Year}";

        public bool IsLastMonth => currentMonthIndex == totalMonths;

        public bool IsTermCompleted => currentMonthIndex > totalMonths;

        public void Advance()
        {
            currentMonthIndex++;
        }

        public void Reset()
        {
            currentMonthIndex = 1;
        }

        public RunCalendar Clone()
        {
            return new RunCalendar(currentMonthIndex, startYear, totalMonths);
        }
    }
}
