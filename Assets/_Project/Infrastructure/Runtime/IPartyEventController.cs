using System;
using Mandato.Content;
using Mandato.Run;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Contrato para o controller da cena de Festa Corporativa.
    /// Permite que o CorporatePartyLauncher (Infrastructure) entregue o controle
    /// ao PartyEventController (Assembly-CSharp) sem referência circular.
    /// </summary>
    public interface IPartyEventController
    {
        void Initialize(
            RunState runState,
            CorporatePartyEventDefinition definition,
            Action<InteractiveEventResult> onCompleted);
    }
}
