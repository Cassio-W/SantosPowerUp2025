using System;
using Mandato.Run;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Contrato de caixa preta para qualquer evento interativo do jogo.
    /// O sistema de fluxo só conhece esta interface — o interior do evento
    /// (minigame, UI, cena, diálogo, etc.) é responsabilidade da implementação.
    /// </summary>
    public interface IInteractiveEvent
    {
        /// <summary>Identificador único do evento. Deve corresponder ao ID usado em ScheduleEvent.</summary>
        string EventId { get; }

        /// <summary>
        /// Inicia o evento. O evento assume o controle da apresentação.
        /// Quando terminar, deve chamar <paramref name="onCompleted"/> com o resultado.
        /// </summary>
        /// <param name="runState">Estado atual da run (somente leitura recomendado; mutações via resultado).</param>
        /// <param name="onCompleted">Callback obrigatório — devolve o controle ao jogo principal.</param>
        void Begin(RunState runState, Action<InteractiveEventResult> onCompleted);
    }
}
