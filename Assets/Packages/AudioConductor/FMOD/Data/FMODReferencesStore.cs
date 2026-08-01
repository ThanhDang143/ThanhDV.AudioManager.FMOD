using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    public enum FMODReferencesLoadStatus
    {
        Success,
        Missing,
        Invalid
    }

    public enum FMODReferencesSaveStatus
    {
        Success,
        InvalidSource,
        Failed
    }

    /// <summary>
    /// Editor-time persistence for the FMOD references authoring data.
    /// Stored as a JSON file (see <see cref="Common.FMOD_JSON_PATH"/>); never loaded at runtime.
    /// </summary>
    public static class FMODReferencesStore
    {
        private const string BackupFolder = "Library/AudioConductor/Backups";

        public static FMODReferencesLoadStatus Load(out FMODReferencesData data, out string error)
        {
            data = null;
            error = null;

            if (!File.Exists(Common.FMOD_JSON_PATH))
            {
                data = new FMODReferencesData();
                return FMODReferencesLoadStatus.Missing;
            }

            try
            {
                string json = File.ReadAllText(Common.FMOD_JSON_PATH);
                FMODReferencesData loadedData = JsonUtility.FromJson<FMODReferencesData>(json);
                if (loadedData == null)
                {
                    error = $"FMOD references JSON at '{Common.FMOD_JSON_PATH}' does not contain a valid data object.";
                    return FMODReferencesLoadStatus.Invalid;
                }

                Normalize(loadedData);
                data = loadedData;
                return FMODReferencesLoadStatus.Success;
            }
            catch (Exception e)
            {
                error = $"Failed to read FMOD references JSON at '{Common.FMOD_JSON_PATH}': {e.Message}";
                return FMODReferencesLoadStatus.Invalid;
            }
        }

        public static FMODReferencesSaveStatus SaveBuses(List<BusEntry> buses, out string error)
        {
            return SaveChanges(data => data.Buses = buses ?? new List<BusEntry>(), out error);
        }

        public static FMODReferencesSaveStatus SaveEventReferences(List<EventReferenceEntry> eventReferences, out string error)
        {
            return SaveChanges(data => data.EventReferences = eventReferences ?? new List<EventReferenceEntry>(), out error);
        }

        public static bool TryRestoreLatestBackup(out string error)
        {
            error = null;
            if (!Directory.Exists(BackupFolder))
            {
                error = "No FMOD references backup directory exists yet.";
                return false;
            }

            try
            {
                string[] backupPaths = Directory.GetFiles(BackupFolder, "FMODReferences-*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(backupPaths, (left, right) =>
                    File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)));

                for (int i = 0; i < backupPaths.Length; i++)
                {
                    string json = File.ReadAllText(backupPaths[i]);
                    FMODReferencesData backupData;
                    try
                    {
                        backupData = JsonUtility.FromJson<FMODReferencesData>(json);
                    }
                    catch
                    {
                        continue;
                    }

                    if (backupData == null) continue;

                    Normalize(backupData);
                    string restoredJson = JsonUtility.ToJson(backupData, true);
                    if (JsonUtility.FromJson<FMODReferencesData>(restoredJson) == null) continue;

                    WriteAtomically(Common.FMOD_JSON_PATH, restoredJson, createBackup: false);
                    AssetDatabase.ImportAsset(Common.FMOD_JSON_PATH);
                    return true;
                }

                error = "No valid FMOD references backup was found.";
                return false;
            }
            catch (Exception e)
            {
                error = $"Failed to restore the latest FMOD references backup: {e.Message}";
                return false;
            }
        }

        private static FMODReferencesSaveStatus SaveChanges(Action<FMODReferencesData> applyChanges, out string error)
        {
            FMODReferencesLoadStatus loadStatus = Load(out FMODReferencesData data, out error);
            if (loadStatus == FMODReferencesLoadStatus.Invalid)
                return FMODReferencesSaveStatus.InvalidSource;

            applyChanges(data);

            try
            {
                Save(data);
                error = null;
                return FMODReferencesSaveStatus.Success;
            }
            catch (Exception e)
            {
                error = $"Failed to save FMOD references JSON at '{Common.FMOD_JSON_PATH}': {e.Message}";
                return FMODReferencesSaveStatus.Failed;
            }
        }

        private static void Save(FMODReferencesData data)
        {
            Normalize(data);

            string json = JsonUtility.ToJson(data, true);
            if (JsonUtility.FromJson<FMODReferencesData>(json) == null)
                throw new InvalidDataException("Serialized FMOD references JSON failed validation.");

            WriteAtomically(Common.FMOD_JSON_PATH, json);
            AssetDatabase.ImportAsset(Common.FMOD_JSON_PATH);
        }

        private static void WriteAtomically(string targetPath, string contents, bool createBackup = true)
        {
            string targetDirectory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(targetDirectory))
                throw new InvalidOperationException($"Cannot resolve the directory for '{targetPath}'.");

            Directory.CreateDirectory(targetDirectory);
            string tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                File.WriteAllText(tempPath, contents, new UTF8Encoding(false));

                if (File.Exists(targetPath) && createBackup)
                {
                    Directory.CreateDirectory(BackupFolder);
                    string backupPath = BuildBackupPath();

                    try
                    {
                        File.Copy(targetPath, backupPath, false);
                    }
                    catch
                    {
                        TryDelete(backupPath);
                        throw;
                    }

                    File.Replace(tempPath, targetPath, null);
                }
                else if (File.Exists(targetPath))
                {
                    File.Replace(tempPath, targetPath, null);
                }
                else
                {
                    File.Move(tempPath, targetPath);
                }
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        private static string BuildBackupPath()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            return Path.Combine(BackupFolder, $"FMODReferences-{timestamp}-{uniqueSuffix}.json");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AudioConductor - FMOD] Failed to remove temporary file '{path}': {e.Message}");
            }
        }

        private static void Normalize(FMODReferencesData data)
        {
            data.Buses ??= new List<BusEntry>();
            data.EventReferences ??= new List<EventReferenceEntry>();

            int removedBuses = data.Buses.RemoveAll(entry => entry == null);
            if (removedBuses > 0)
            {
                Debug.LogWarning($"[AudioConductor - FMOD] Ignored {removedBuses} null bus entry from FMODReferences JSON.");
            }

            int removedEventReferences = data.EventReferences.RemoveAll(entry => entry == null);
            if (removedEventReferences > 0)
            {
                Debug.LogWarning($"[AudioConductor - FMOD] Ignored {removedEventReferences} null event reference entry from FMODReferences JSON.");
            }
        }
    }
}