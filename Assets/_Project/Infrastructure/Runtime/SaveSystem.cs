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
        public const string DefaultRunSaveFileName = "mandato_run.json";

        public static string GetDefaultProfilePath()
        {
            return Path.Combine(Application.persistentDataPath, DefaultProfileFileName);
        }

        public static string GetDefaultRunSavePath()
        {
            return Path.Combine(Application.persistentDataPath, DefaultRunSaveFileName);
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

        public static void MigrateProfile(ProfileState profile, int fromVersion, int toVersion)
        {
            if (profile == null) return;

            for (int v = fromVersion; v < toVersion; v++)
            {
                if (v == 1)
                {
                    MigrateV1ToV2(profile);
                }
            }

            profile.schemaVersion = toVersion;
        }

        public static void MigrateV1ToV2(ProfileState profile)
        {
            if (profile == null) return;

            profile.EnsureCollectionsInitialized();

            // v2 introduziu rastreio detalhado de decisões, popularidade máxima e listas de quests e ações
            if (profile.totalRunsPlayed < 0) profile.totalRunsPlayed = 0;
            if (profile.totalVictories < 0) profile.totalVictories = 0;
            if (profile.totalDecisionsMade < 0) profile.totalDecisionsMade = 0;
            if (profile.highestPopularityScore < 0) profile.highestPopularityScore = 0;

            profile.schemaVersion = 2;
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
                Debug.LogError($"[SaveSystem] Erro ao deletar save do perfil: {ex.Message}");
            }
            return false;
        }

        // ── Save e Carregamento de Run em Andamento ───────────────

        public static bool SaveRun(RunSaveData runData, string customPath = null)
        {
            if (runData == null) return false;

            try
            {
                string path = !string.IsNullOrEmpty(customPath) ? customPath : GetDefaultRunSavePath();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                runData.schemaVersion = CurrentSchemaVersion;
                var wrapper = new SaveWrapper<RunSaveData>(runData, CurrentSchemaVersion);
                string json = JsonUtility.ToJson(wrapper, prettyPrint: true);

                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Erro ao salvar Run: {ex.Message}");
                return false;
            }
        }

        public static RunSaveData LoadRun(string customPath = null)
        {
            string path = !string.IsNullOrEmpty(customPath) ? customPath : GetDefaultRunSavePath();

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                var wrapper = JsonUtility.FromJson<SaveWrapper<RunSaveData>>(json);
                return wrapper?.data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Erro ao carregar save da run em '{path}': {ex.Message}");
                return null;
            }
        }

        public static bool HasSavedRun(string customPath = null)
        {
            string path = !string.IsNullOrEmpty(customPath) ? customPath : GetDefaultRunSavePath();
            return File.Exists(path);
        }

        public static bool DeleteRunSave(string customPath = null)
        {
            try
            {
                string path = !string.IsNullOrEmpty(customPath) ? customPath : GetDefaultRunSavePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Erro ao deletar save da run: {ex.Message}");
            }
            return false;
        }
    }
}
