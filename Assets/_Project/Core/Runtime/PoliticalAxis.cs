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

        public const int DefaultProposalStep = 2;
        public const int DefaultRelationStep = 1;

        [Range(MinValue, MaxValue)]
        public int x = Center; // -10 (Esquerda) a +10 (Direita)

        [Range(MinValue, MaxValue)]
        public int y = Center; // -10 (Liberal) a +10 (Autoritário)

        public bool isLocked = false;

        public PoliticalAxis()
        {
            x = Center;
            y = Center;
            isLocked = false;
        }

        public PoliticalAxis(int initialX, int initialY, bool locked = false)
        {
            x = Clamp(initialX);
            y = Clamp(initialY);
            isLocked = locked;
        }

        public void Lock() => isLocked = true;
        public void Unlock() => isLocked = false;

        public void ApplyDelta(int deltaX, int deltaY)
        {
            if (isLocked) return;

            x = Clamp(x + deltaX);
            y = Clamp(y + deltaY);
        }

        /// <summary>
        /// Desloca as coordenadas do eixo na direção de um ponto alvo (targetX, targetY)
        /// por um valor fixo de passo em cada coordenada.
        /// Não ultrapassa o valor alvo e respeita os limites [-10, 10] e isLocked.
        /// Retorna o delta efetivamente aplicado (deltaX, deltaY).
        /// </summary>
        public (int deltaX, int deltaY) MoveTowards(int targetX, int targetY, int step)
        {
            if (isLocked || step <= 0) return (0, 0);

            targetX = Clamp(targetX);
            targetY = Clamp(targetY);

            int deltaX = 0;
            if (x < targetX)
            {
                deltaX = Math.Min(step, targetX - x);
            }
            else if (x > targetX)
            {
                deltaX = -Math.Min(step, x - targetX);
            }

            int deltaY = 0;
            if (y < targetY)
            {
                deltaY = Math.Min(step, targetY - y);
            }
            else if (y > targetY)
            {
                deltaY = -Math.Min(step, y - targetY);
            }

            x = Clamp(x + deltaX);
            y = Clamp(y + deltaY);

            return (deltaX, deltaY);
        }

        public bool IsInRange(int minX, int maxX, int minY, int maxY)
        {
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
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
            return new PoliticalAxis(x, y, isLocked);
        }

        public static int Clamp(int value)
        {
            if (value < MinValue) return MinValue;
            if (value > MaxValue) return MaxValue;
            return value;
        }
    }
}
