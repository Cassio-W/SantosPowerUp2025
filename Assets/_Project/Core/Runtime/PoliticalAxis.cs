using System;
using UnityEngine;

namespace Mandato.Core
{
    [Serializable]
    public class PoliticalAxis
    {
        public const int MinValue = -10;
        public const int MaxValue = 10;
        public const int Center = 0;

        [Range(MinValue, MaxValue)]
        public int x = Center; // -10 (Esquerda) a +10 (Direita)

        [Range(MinValue, MaxValue)]
        public int y = Center; // -10 (Liberal) a +10 (Autoritário)

        public PoliticalAxis()
        {
            x = Center;
            y = Center;
        }

        public PoliticalAxis(int initialX, int initialY)
        {
            x = Clamp(initialX);
            y = Clamp(initialY);
        }

        public void ApplyDelta(int deltaX, int deltaY)
        {
            x = Clamp(x + deltaX);
            y = Clamp(y + deltaY);
        }

        public string Quadrant
        {
            get
            {
                if (Mathf.Abs(x) <= 2 && Mathf.Abs(y) <= 2)
                    return "Centro";

                if (x < -2)
                {
                    if (y > 2) return "Esquerda Autoritária";
                    if (y < -2) return "Esquerda Libertária";
                    return "Centro-Esquerda";
                }

                if (x > 2)
                {
                    if (y > 2) return "Direita Autoritária";
                    if (y < -2) return "Direita Liberal";
                    return "Centro-Direita";
                }

                return y > 2 ? "Governança Autoritária" : "Governança Liberal";
            }
        }

        public PoliticalAxis Clone()
        {
            return new PoliticalAxis(x, y);
        }

        public static int Clamp(int value)
        {
            if (value < MinValue) return MinValue;
            if (value > MaxValue) return MaxValue;
            return value;
        }
    }
}
