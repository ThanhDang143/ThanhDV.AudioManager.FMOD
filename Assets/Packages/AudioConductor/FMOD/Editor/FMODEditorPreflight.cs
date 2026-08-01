using System;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using FMODStudio = global::FMOD.Studio;

namespace ThanhDV.AudioConductor.FMOD
{
    internal static class FMODEditorPreflight
    {
        public static bool TryGetSettings(out Settings settings, out string error)
        {
            settings = Settings.Instance;
            error = null;

            if (settings == null)
            {
                error = "FMOD settings asset is not available.";
                return false;
            }

            if (settings.HasSourceProject)
            {
                if (string.IsNullOrWhiteSpace(settings.SourceProjectPath))
                {
                    error = "FMOD Studio project path has not been configured.";
                    return false;
                }

                if (!File.Exists(settings.SourceProjectPath))
                {
                    error = $"FMOD Studio project was not found at '{settings.SourceProjectPath}'.";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(settings.SourceBankPath))
            {
                error = "FMOD source bank path has not been configured.";
                return false;
            }

            if (!Directory.Exists(settings.SourceBankPath))
            {
                error = $"FMOD source bank directory was not found at '{settings.SourceBankPath}'.";
                return false;
            }

            return true;
        }

        public static bool TryResolveBankDirectory(Settings settings, out string bankDirectory, out string error)
        {
            bankDirectory = null;
            error = null;

            if (settings == null)
            {
                error = "FMOD settings asset is not available.";
                return false;
            }

            try
            {
                bankDirectory = settings.SourceBankPath;
                if (settings.HasPlatforms)
                {
                    Platform editorPlatform = settings.PlayInEditorPlatform;
                    if (editorPlatform == null)
                    {
                        error = "FMOD Play In Editor platform is not available.";
                        return false;
                    }

                    string buildDirectory = editorPlatform.BuildDirectory;
                    if (string.IsNullOrWhiteSpace(buildDirectory))
                    {
                        error = "FMOD Play In Editor build directory has not been configured.";
                        return false;
                    }

                    bankDirectory = Path.Combine(bankDirectory, buildDirectory);
                }

                bankDirectory = Path.GetFullPath(bankDirectory);
                if (!Directory.Exists(bankDirectory))
                {
                    error = $"FMOD bank directory was not found at '{bankDirectory}'.";
                    return false;
                }

                if (Directory.GetFiles(bankDirectory, "*.bank", SearchOption.TopDirectoryOnly).Length == 0)
                {
                    error = $"FMOD bank directory '{bankDirectory}' does not contain any built banks.";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                error = $"Failed to resolve the FMOD bank directory: {e.Message}";
                return false;
            }
        }

        public static bool TryValidateRequiredBanks(
            Settings settings,
            string bankDirectory,
            out string[] masterBankPaths,
            out string[] stringsBankPaths,
            out string error)
        {
            masterBankPaths = Array.Empty<string>();
            stringsBankPaths = Array.Empty<string>();
            error = null;

            if (settings?.MasterBanks == null || settings.MasterBanks.Count == 0)
            {
                error = "FMOD Settings does not contain any master bank definitions.";
                return false;
            }

            var masters = new List<string>(settings.MasterBanks.Count);
            var strings = new List<string>(settings.MasterBanks.Count);

            for (int i = 0; i < settings.MasterBanks.Count; i++)
            {
                string masterBankName = settings.MasterBanks[i];
                if (string.IsNullOrWhiteSpace(masterBankName))
                {
                    error = "FMOD Settings contains an empty master bank name.";
                    return false;
                }

                string masterBankPath = Path.Combine(bankDirectory, masterBankName + ".bank");
                string stringsBankPath = Path.Combine(bankDirectory, masterBankName + ".strings.bank");

                if (!File.Exists(masterBankPath))
                {
                    error = $"FMOD master bank was not found at '{masterBankPath}'.";
                    return false;
                }

                if (!File.Exists(stringsBankPath))
                {
                    error = $"FMOD strings bank was not found at '{stringsBankPath}'.";
                    return false;
                }

                masters.Add(masterBankPath);
                strings.Add(stringsBankPath);
            }

            masterBankPaths = masters.ToArray();
            stringsBankPaths = strings.ToArray();
            return true;
        }

        internal static void LogCleanupResult(global::FMOD.RESULT result, string operation)
        {
            if (result == global::FMOD.RESULT.OK) return;

            string errorDescription = global::FMOD.Error.String(result);
            UnityEngine.Debug.LogWarning($"[AudioConductor - FMOD] {operation} failed: {result} - {errorDescription}");
        }

        public static bool TryCreateStudioSystem(out FMODStudio.System system, out string error)
        {
            system = default;
            error = null;

            try
            {
                global::FMOD.RESULT result = FMODStudio.System.create(out system);
                if (result != global::FMOD.RESULT.OK || !system.isValid())
                {
                    error = $"FMOD Studio system creation failed. Result: {result}.";
                    system = default;
                    return false;
                }

                FMODStudio.INITFLAGS studioFlags =
                    FMODStudio.INITFLAGS.ALLOW_MISSING_PLUGINS |
                    FMODStudio.INITFLAGS.SYNCHRONOUS_UPDATE;

                result = system.initialize(
                    1,
                    studioFlags,
                    global::FMOD.INITFLAGS.MIX_FROM_UPDATE,
                    IntPtr.Zero);

                if (result != global::FMOD.RESULT.OK)
                {
                    LogCleanupResult(system.release(), "Release FMOD Studio system after initialization failure");
                    system = default;
                    error = $"FMOD Studio system initialization failed. Result: {result}.";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                if (system.isValid()) LogCleanupResult(system.release(), "Release FMOD Studio system after initialization exception");
                system = default;
                error = $"FMOD native system initialization failed: {e.GetType().Name}: {e.Message}";
                return false;
            }
        }
    }
}