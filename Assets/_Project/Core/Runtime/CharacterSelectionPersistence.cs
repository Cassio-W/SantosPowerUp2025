using UnityEngine;

namespace Mandato.Core
{
    /// <summary>
    /// Armazena e recupera o identificador do personagem escolhido pelo jogador via PlayerPrefs.
    /// Simples e sem infraestrutura adicional — o id persiste entre sessões até uma nova escolha ou limpeza.
    /// Mantido em Mandato.Core para ser acessível em todas as camadas superiores (Presentation, Infrastructure).
    /// </summary>
    public static class CharacterSelectionPersistence
    {
        private const string Key = "mandato.selected_character_id";

        /// <summary>Salva o id do personagem escolhido.</summary>
        public static void Save(string characterId)
        {
            PlayerPrefs.SetString(Key, characterId ?? string.Empty);
            PlayerPrefs.Save();
        }

        /// <summary>Retorna o id salvo, ou string.Empty se nenhum foi escolhido.</summary>
        public static string Load() => PlayerPrefs.GetString(Key, string.Empty);

        /// <summary>Remove a escolha persistida (útil ao iniciar um novo jogo sem personagem).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        /// <summary>Retorna true se há um personagem salvo.</summary>
        public static bool HasSavedSelection() => !string.IsNullOrEmpty(Load());
    }
}
