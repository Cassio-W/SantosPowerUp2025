using System;
using System.IO;
using Mandato.Run;
using UnityEngine;

namespace Mandato.Infrastructure
{
    [Serializable]
    public class SaveWrapper<T>
    {
        public int schemaVersion = 2;
        public string saveTimestamp = string.Empty;
        public T data;

        public SaveWrapper() { }

        public SaveWrapper(T data, int version = 2)
        {
            this.schemaVersion = version;
            this.saveTimestamp = DateTime.UtcNow.ToString("o");
            this.data = data;
        }
    }

    public static class SaveSystem
    {
        public const int CurrentSchemaVersion = 2;
        public const string DefaultProfileFileName = "mandato_profile.json";

        public static string GetDefaultProfilePath()
        {
            return Path.Combine(Application.persistentDataPath, DefaultProfileFileName);
        }

        public static bool SaveProfile(ProfileState profile, string customPath = null)
        {
            if (profile == null) return false;

            try
            {
                string path = !string.IsNullOrEmpty(customPath) ? customPath : GetDefaultProfilePath();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                profile.EnsureCollectionsInitialized();
                profile.schemaVersion = CurrentSchemaVersion;

                var wrapper = new SaveWrapper<ProfileState>(profile, CurrentSchemaVersion);
                string json = JsonUtility.ToJson(wrapper, prettyPrint: true);

                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Erro ao salvar ProfileState: {ex.Message}");
                return false;
            }
        }

        public static ProfileState LoadProfile(string customPath = null)
        {
            string path = !string.IsNullOrEmpty(customPath) ? customPath : GetDefaultProfilePath();

            if (!File.Exists(path))
            {
                return new ProfileState();
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new ProfileState();
                }

                var wrapper = JsonUtility.FromJson<SaveWrapper<ProfileState>>(json);
                if (wrapper != null && wrapper.data != null)
                {
                    var profile = wrapper.data;
                    profile.EnsureCollectionsInitialized();

                    if (wrapper.schemaVersion < CurrentSchemaVersion)
                    {
                        MigrateProfile(profile, wrapper.schemaVersion, CurrentSchemaVersion);
                        // Salva o perfil já migrado
                        SaveProfile(profile, path);
                    }

                    return profile;
                }

                return new ProfileState();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Arquivo de save em '{path}' inválido ou corrompido: {ex.Message}. Criando backup '{path}.bak' e gerando novo perfil limpo.");
                try
                {
                    string backupPath = path + ".bak";
                    File.Copy(path, backupPath, overwrite: true);
                }
                catch { }

                return new ProfileState();
            }
        }

        private static void MigrateProfile(ProfileState profile, int fromVersion, int toVersion)
        {
            if (profile == null) return;
            profile.EnsureCollectionsInitialized();
            profile.schemaVersion = toVersion;
        }

        public static bool DeleteProfile(string customPath = null)
        {
            try
            {
                string path = !string.IsNullOrEmpty(customPath) ? customPath : GetDefaultProfilePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Erro ao deletar save: {ex.Message}");
            }
            return false;
        }
    }
}

