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
            registry[ev.EventId] = ev;
        }

        /// <summary>Registra um alias alternativo para o evento.</summary>
        public void RegisterAlias(string alias, IInteractiveEvent ev)
        {
            if (string.IsNullOrEmpty(alias) || ev == null) return;
            registry[alias] = ev;
        }

        public void Unregister(string eventId)
        {
            if (!string.IsNullOrEmpty(eventId))
                registry.Remove(eventId);
        }

        public bool TryGet(string eventId, out IInteractiveEvent ev)
        {
            ev = null;
            if (string.IsNullOrEmpty(eventId)) return false;

            if (registry.TryGetValue(eventId, out ev)) return true;

            // Busca tolerante a variações comuns (ex: FestaEvent, FestaCorporativa, JantarEvento)
            foreach (var kvp in registry)
            {
                if (string.Equals(kvp.Key, eventId, System.StringComparison.OrdinalIgnoreCase))
                {
                    ev = kvp.Value;
                    return true;
                }
            }

            return false;
        }

        public bool IsRegistered(string eventId) => TryGet(eventId, out _);
    }
}
