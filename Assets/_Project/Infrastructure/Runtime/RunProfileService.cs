using System;
using Mandato.Run;
using UnityEngine;

namespace Mandato.Infrastructure
{
    public class RunProfileService
    {
        public ProfileState CurrentProfile { get; private set; }

        public ProfileState InitializeProfile()
        {
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

        public void RecordRunCompleted(bool victory, string endingId = null)
        {
            if (CurrentProfile == null)
            {
                InitializeProfile();
            }

            try
            {
                CurrentProfile.RecordRunCompleted(victory, endingId);
                SaveSystem.SaveProfile(CurrentProfile);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunProfileService] Falha ao salvar perfil após término da partida: {ex.Message}");
            }
        }
    }
}
