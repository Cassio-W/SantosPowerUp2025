using UnityEngine;

namespace Mandato.Presentation
{
    /// <summary>
    /// Contrato de controle de NPC de cena.
    /// Implementado pelo NPCController legado no Assembly-CSharp.
    /// Permite que o Mandato.Presentation comande NPCs sem reflection.
    /// </summary>
    public interface INpcController
    {
        /// <summary>Define as posições de spawn e mesa antes de iniciar o movimento.</summary>
        void SetPositions(Vector3 spawnPosition, Vector3 tablePosition);

        /// <summary>True quando a entrega do papel foi concluída.</summary>
        bool IsReadyForDismissal();

        /// <summary>Inicia o NPC caminhando do spawn até a mesa.</summary>
        void MoveToTable();

        /// <summary>Toca animação de reação e, após, comanda a saída do NPC.</summary>
        void ReactAndExit(bool isPositive = true);

        /// <summary>Move o NPC diretamente para a saída (sem reação).</summary>
        void MoveToExit();
    }
}
