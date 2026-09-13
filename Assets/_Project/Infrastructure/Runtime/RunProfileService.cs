using System;
using Mandato.Run;
using UnityEngine;

namespace Mandato.Infrastructure
{
    public class RunProfileService
    {
        public ProfileState CurrentProfile { get; private set; }

        public ProfileState InitializeProfile(ProfileState explicitProfile = null)
        {
            if (explicitProfile != null)
            {
                CurrentProfile = explicitProfile;
                return CurrentProfile;
            }

            try
            {
                CurrentProfile = SaveSystem.LoadProfile() ?? new ProfileState();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunProfileService] Não foi possível carregar o perfil persistido: {ex.Message}. Criando perfil padrão.");
                CurrentProfile = new ProfileState();
            }

            return CurrentProfile;
        }

        public void RecordRunCompleted(bool victory, string endingId = null, int decisionsCount = 0, int finalPopularity = 0)
        {
            if (CurrentProfile == null)
            {
                InitializeProfile();
            }

            try
            {
                CurrentProfile.RecordRunCompleted(victory, endingId, decisionsCount, finalPopularity);
                SaveSystem.SaveProfile(CurrentProfile);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunProfileService] Falha ao salvar perfil após término da partida: {ex.Message}");
            }
        }
    }
}
