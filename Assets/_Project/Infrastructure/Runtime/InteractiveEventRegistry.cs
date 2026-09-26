using System.Collections.Generic;
using UnityEngine;

namespace Mandato.Infrastructure
{
    /// <summary>
    /// Registro central de implementações de eventos interativos na cena.
    /// Mapeia eventId → IInteractiveEvent, desacoplando o agendamento da implementação.
    ///
    /// Para registrar um evento: arraste o componente de evento para a lista no Inspector,
    /// ou chame Register() via código no Awake do próprio evento.
    /// </summary>
    public class InteractiveEventRegistry : MonoBehaviour
    {
        [Tooltip("Componentes de evento registrados manualmente pelo Inspector. " +
                 "Cada componente deve implementar IInteractiveEvent.")]
        [SerializeField] private List<MonoBehaviour> registeredEvents = new List<MonoBehaviour>();

        private readonly Dictionary<string, IInteractiveEvent> registry =
            new Dictionary<string, IInteractiveEvent>(System.StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            foreach (var mb in registeredEvents)
            {
                if (mb is IInteractiveEvent ev)
                    Register(ev);
                else if (mb != null)
                    Debug.LogWarning($"[InteractiveEventRegistry] '{mb.name}' não implementa IInteractiveEvent e foi ignorado.", mb);
            }
        }

        /// <summary>Registra um evento em runtime (ex: chamado pelo próprio evento no Awake).</summary>
        public void Register(IInteractiveEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.EventId)) return;
            if (registry.ContainsKey(ev.EventId))
                Debug.LogWarning($"[InteractiveEventRegistry] Evento '{ev.EventId}' registrado mais de uma vez. Sobrescrevendo.");
            registry[ev.EventId] = ev;
        }

        public void Unregister(string eventId)
        {
            if (!string.IsNullOrEmpty(eventId))
                registry.Remove(eventId);
        }

        public bool TryGet(string eventId, out IInteractiveEvent ev)
        {
            ev = null;
            return !string.IsNullOrEmpty(eventId) && registry.TryGetValue(eventId, out ev);
        }

        public bool IsRegistered(string eventId) =>
            !string.IsNullOrEmpty(eventId) && registry.ContainsKey(eventId);
    }
}
