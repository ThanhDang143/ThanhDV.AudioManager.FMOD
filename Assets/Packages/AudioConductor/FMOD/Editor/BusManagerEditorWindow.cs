using System.Collections.Generic;
using FMOD;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using FMODStudio = global::FMOD.Studio;
using Debug = UnityEngine.Debug;

namespace ThanhDV.AudioConductor.FMOD
{
    public class BusManagerEditorWindow : EditorWindow
    {
        [SerializeField] private List<BusEntry> _buses = new();
        [SerializeField] private string _searchText = "";

        private Vector2 _scroll;

        private SerializedObject _so;
        private SerializedProperty _busesProp;

        private bool _hasDataUnsaved = false;
        private FMODReferencesLoadStatus _loadStatus;
        private string _loadError;
        private string _saveError;
        private string _codegenError;
        private string _fmodIssue;
        private MessageType _fmodIssueType = MessageType.None;

        private bool HasInvalidData => _loadStatus == FMODReferencesLoadStatus.Invalid;

        [MenuItem(Common.MENU_ITEM + "Bus Manager", false, 2)]
        public static void ShowWindow()
        {
            BusManagerEditorWindow window = GetWindow<BusManagerEditorWindow>();
            window.titleContent = new GUIContent("Bus Manager");
            window.minSize = new Vector2(500, 300);
            window.Show();
        }

        private void OnEnable()
        {
            saveChangesMessage = "Save pending AudioConductor bus changes before closing?";
            SetDataUnsaved(false);
            _so = new SerializedObject(this);
            _busesProp = _so.FindProperty(nameof(_buses));
            LoadBuses();
            RefreshFmodStatus();
        }

        private void OnGUI()
        {
            string title = "AudioConductor - FMOD - Bus";
            string subtitle = "Created by ThanhDV";
            EditorHelper.CreateHeader(title, subtitle);

            DrawStatusBanners();

            EditorGUI.BeginDisabledGroup(_hasDataUnsaved);
            if (GUILayout.Button(new GUIContent("Refresh", "Reload buses from data and refresh displayed data.")))
            {
                LoadBuses();
                RefreshFmodStatus();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!_hasDataUnsaved || HasInvalidData);
            if (GUILayout.Button(new GUIContent("Clean & Save", "Removes buses with empty keys or values, removes duplicates, and saves changes.")))
            {
                CleanBuses();
                if (TryPrepareWrapper(out string source) && SaveBuses()) GenerateWrapper(source);
            }

            if (GUILayout.Button(new GUIContent("Discard Changes", "Discard all changes.")))
            {
                LoadBuses();
            }
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_codegenError))
            {
                EditorGUI.BeginDisabledGroup(HasInvalidData);
                if (GUILayout.Button(new GUIContent("Retry Generate", "Regenerate FMODBus from the saved JSON data.")))
                {
                    RetryGenerateWrapper();
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUI.BeginDisabledGroup(HasInvalidData);
            Color originalBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button(new GUIContent("Load All Buses", "Load all buses from the FMOD project. \nNote: this will delete all currently saved buses.")))
            {
                GetAllBus();
            }
            GUI.backgroundColor = originalBackgroundColor;
            EditorGUI.EndDisabledGroup();

            EditorHelper.DrawHorizontalLine();

            EditorGUI.BeginDisabledGroup(HasInvalidData);
            DrawSearchToolbar();

            _so ??= new SerializedObject(this);
            _so.Update();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            bool deletedInFilteredView = false;
            string termBeforeDelete = null;

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                EditorHelper.DrawListWithoutHeader(_busesProp, "Buses");
            }
            else
            {
                int pendingDeleteIndex = DrawFilteredBuses(_busesProp, _searchText);
                if (pendingDeleteIndex >= 0)
                {
                    termBeforeDelete = _searchText;
                    _busesProp.DeleteArrayElementAtIndex(pendingDeleteIndex);
                    deletedInFilteredView = true;
                }
            }

            EditorGUILayout.EndScrollView();

            bool changed = _so.ApplyModifiedProperties();
            if (changed)
            {
                SetDataUnsaved(true);
            }

            if (deletedInFilteredView)
            {
                if (!HasAnyBusMatch(_buses, termBeforeDelete))
                    ExitSearchMode();
                else
                    Repaint();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawStatusBanners()
        {
            if (HasInvalidData)
            {
                EditorGUILayout.HelpBox(_loadError, MessageType.Error);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open JSON")) OpenReferencesJson();
                    if (GUILayout.Button("Restore Latest Backup")) RestoreLatestBackup();
                }
            }
            else if (_loadStatus == FMODReferencesLoadStatus.Missing)
            {
                EditorGUILayout.HelpBox("FMOD references JSON does not exist yet. Clean & Save will create it.", MessageType.Info);
            }

            if (!string.IsNullOrEmpty(_saveError))
                EditorGUILayout.HelpBox(_saveError, MessageType.Error);

            if (!string.IsNullOrEmpty(_codegenError))
                EditorGUILayout.HelpBox(_codegenError, MessageType.Error);

            if (!string.IsNullOrEmpty(_fmodIssue))
            {
                EditorGUILayout.HelpBox(_fmodIssue, _fmodIssueType);
                if (GUILayout.Button("Open FMOD Settings")) FMODUnity.EditorSettings.EditSettings();
            }
        }

        private void OpenReferencesJson()
        {
            TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Common.FMOD_JSON_PATH);
            if (jsonAsset != null && AssetDatabase.OpenAsset(jsonAsset)) return;

            string error = $"Could not open FMOD references JSON at '{Common.FMOD_JSON_PATH}'.";
            _loadError = error;
            Debug.LogError($"[AudioConductor - FMOD] {error}");
            Repaint();
        }

        private void RestoreLatestBackup()
        {
            if (!FMODReferencesStore.TryRestoreLatestBackup(out string error))
            {
                _loadError = error;
                Debug.LogError($"[AudioConductor - FMOD] {error}");
                Repaint();
                return;
            }

            Debug.Log("[AudioConductor - FMOD] Restored the latest valid FMOD references JSON backup.");
            LoadBuses();
            _codegenError = "JSON backup restored. FMODBus regeneration is required.";
            UpdateWindowUnsavedState();
            Repaint();
        }

        private void SetDataUnsaved(bool value)
        {
            _hasDataUnsaved = value;
            UpdateWindowUnsavedState();
        }

        private void UpdateWindowUnsavedState()
        {
            hasUnsavedChanges = _hasDataUnsaved || !string.IsNullOrEmpty(_codegenError);
        }

        public override void SaveChanges()
        {
            if (_hasDataUnsaved)
            {
                CleanBuses();
                if (!TryPrepareWrapper(out string source) || !SaveBuses()) return;
                GenerateWrapper(source);
                if (!string.IsNullOrEmpty(_codegenError)) return;
            }
            else if (!string.IsNullOrEmpty(_codegenError))
            {
                RetryGenerateWrapper();
                if (!string.IsNullOrEmpty(_codegenError)) return;
            }

            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            _codegenError = null;
            LoadBuses();
            SetDataUnsaved(false);
            base.DiscardChanges();
        }

        private void ReportFmodIssue(string message, MessageType messageType)
        {
            _fmodIssue = message;
            _fmodIssueType = messageType;

            if (messageType == MessageType.Warning)
                Debug.LogWarning($"[AudioConductor - FMOD] {message}");
            else
                Debug.LogError($"[AudioConductor - FMOD] {message}");

            Repaint();
        }

        private void RefreshFmodStatus()
        {
            _fmodIssue = null;
            _fmodIssueType = MessageType.None;

            if (!FMODEditorPreflight.TryGetSettings(out Settings settings, out string error))
            {
                ReportFmodIssue(error, MessageType.Error);
                return;
            }

            if (!FMODEditorPreflight.TryResolveBankDirectory(settings, out string bankDirectory, out error))
            {
                ReportFmodIssue($"{error} Build banks in FMOD Studio, then refresh the FMOD banks.", MessageType.Warning);
                return;
            }

            if (!FMODEditorPreflight.TryValidateRequiredBanks(settings, bankDirectory, out _, out _, out error))
            {
                ReportFmodIssue($"{error} Build banks in FMOD Studio, then refresh the FMOD banks.", MessageType.Warning);
                return;
            }

            Repaint();
        }

        private void DrawSearchToolbar()
        {
            GUIStyle searchFieldStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? GUI.skin.textField;
            GUIStyle cancelButtonStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? GUI.skin.button;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string newSearch = GUILayout.TextField(_searchText, searchFieldStyle, GUILayout.ExpandWidth(true));
                if (newSearch != _searchText)
                {
                    _searchText = newSearch;
                    Repaint();
                }

                if (GUILayout.Button(GUIContent.none, cancelButtonStyle))
                    ExitSearchMode();
            }
        }

        private void ExitSearchMode()
        {
            _searchText = string.Empty;
            EditorGUIUtility.editingTextField = false;
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
            Repaint();
        }

        private static int DrawFilteredBuses(SerializedProperty busesProp, string searchText)
        {
            if (busesProp == null) return -1;

            string term = searchText?.Trim();
            if (string.IsNullOrEmpty(term))
            {
                EditorHelper.DrawListWithoutHeader(busesProp, "Buses");
                return -1;
            }

            int total = busesProp.arraySize;
            int shown = 0;

            EditorGUILayout.LabelField($"Search results: {term}  (showing matches)", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            int pendingDeleteIndex = -1;

            for (int i = 0; i < total; i++)
            {
                SerializedProperty element = busesProp.GetArrayElementAtIndex(i);

                SerializedProperty keyProp = element.FindPropertyRelative(nameof(BusEntry.Key));
                SerializedProperty busPathProp = element.FindPropertyRelative(nameof(BusEntry.BusPath));

                string key = keyProp?.stringValue;
                string busPath = busPathProp?.stringValue;

                if (!ContainsIgnoreCase(key, term) && !ContainsIgnoreCase(busPath, term))
                    continue;

                shown++;
                EditorGUILayout.PropertyField(element, includeChildren: true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        pendingDeleteIndex = i;
                    }
                }

                EditorHelper.DrawHorizontalLine(thickness: 1, padding: 6);
            }

            if (shown == 0)
            {
                EditorGUILayout.HelpBox("No buses matched your search.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Showing {shown}/{total}", EditorStyles.miniLabel);
            }

            return pendingDeleteIndex;
        }

        private static bool ContainsIgnoreCase(string source, string term)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(term)) return false;
            return source.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasAnyBusMatch(List<BusEntry> buses, string searchText)
        {
            if (buses == null) return false;

            string term = searchText?.Trim();
            if (string.IsNullOrEmpty(term)) return false;

            for (int i = 0; i < buses.Count; i++)
            {
                BusEntry bus = buses[i];
                if (bus == null) continue;

                string key = bus.Key;
                string busPath = bus.BusPath;

                if (ContainsIgnoreCase(key, term) || ContainsIgnoreCase(busPath, term))
                    return true;
            }

            return false;
        }

        private void LoadBuses()
        {
            _loadStatus = FMODReferencesStore.Load(out FMODReferencesData data, out _loadError);
            _saveError = null;
            if (HasInvalidData)
            {
                Debug.LogError($"[AudioConductor - FMOD] {_loadError}");
                SetDataUnsaved(false);
                Repaint();
                return;
            }

            _buses = new List<BusEntry>(data.Buses);
            _so = new SerializedObject(this);
            _busesProp = _so.FindProperty(nameof(_buses));
            SetDataUnsaved(false);
        }

        private void CleanBuses()
        {
            HashSet<string> uniqueKeys = new();
            List<BusEntry> validBuses = new();

            foreach (BusEntry bus in _buses)
            {
                if (bus == null)
                {
                    Debug.LogWarning("[AudioConductor - FMOD] Removed null bus entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(bus.Key))
                {
                    Debug.LogWarning("[AudioConductor - FMOD] Removed bus entry due to missing or empty key.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(bus.BusPath))
                {
                    Debug.LogWarning($"[AudioConductor - FMOD] Removed bus entry '{bus.Key}' due to missing or empty bus path.");
                    continue;
                }

                if (!uniqueKeys.Add(bus.Key))
                {
                    Debug.LogWarning($"[AudioConductor - FMOD] Removed duplicate bus entry with key '{bus.Key}'.");
                    continue;
                }

                validBuses.Add(bus);
            }

            _buses = validBuses;
        }

        private bool SaveBuses()
        {
            FMODReferencesSaveStatus status = FMODReferencesStore.SaveBuses(_buses, out string error);
            if (status == FMODReferencesSaveStatus.InvalidSource)
            {
                _loadStatus = FMODReferencesLoadStatus.Invalid;
                _loadError = error;
                _saveError = null;
                Debug.LogError($"[AudioConductor - FMOD] {error}");
                Repaint();
                return false;
            }

            if (status == FMODReferencesSaveStatus.Failed)
            {
                _saveError = error;
                Debug.LogError($"[AudioConductor - FMOD] {error}");
                Repaint();
                return false;
            }

            _loadStatus = FMODReferencesLoadStatus.Success;
            _loadError = null;
            _saveError = null;
            SetDataUnsaved(false);
            return true;
        }

        private bool TryPrepareWrapper(out string source)
        {
            if (WrapperGenerator.TryBuildFMODBus(_buses, out source, out string error)) return true;

            _codegenError = $"Code generation preparation failed. Data was not saved: {error}";
            UpdateWindowUnsavedState();
            Debug.LogError($"[AudioConductor - FMOD] {_codegenError}");
            Repaint();
            return false;
        }

        private void GenerateWrapper(string source)
        {
            if (!WrapperGenerator.TryWriteFMODBus(source, out string error))
            {
                SetCodegenFailed(error);
                return;
            }

            _codegenError = null;
            UpdateWindowUnsavedState();
            Debug.Log("[AudioConductor - FMOD] Bus data saved and wrapper generation completed.");
            Repaint();
        }

        private void RetryGenerateWrapper()
        {
            FMODReferencesLoadStatus status = FMODReferencesStore.Load(out FMODReferencesData data, out string error);
            if (status == FMODReferencesLoadStatus.Invalid)
            {
                _loadStatus = status;
                _loadError = error;
                _codegenError = null;
                UpdateWindowUnsavedState();
                Debug.LogError($"[AudioConductor - FMOD] {error}");
                Repaint();
                return;
            }

            if (status == FMODReferencesLoadStatus.Missing)
            {
                SetCodegenFailed("FMOD references JSON is missing.");
                return;
            }

            if (!WrapperGenerator.TryBuildFMODBus(data.Buses, out string source, out error) ||
                !WrapperGenerator.TryWriteFMODBus(source, out error))
            {
                SetCodegenFailed(error);
                return;
            }

            _codegenError = null;
            UpdateWindowUnsavedState();
            Debug.Log("[AudioConductor - FMOD] FMODBus generation completed from saved JSON data.");
            Repaint();
        }

        private void SetCodegenFailed(string error)
        {
            _codegenError = $"Data saved, code generation failed: {error}";
            UpdateWindowUnsavedState();
            Debug.LogError($"[AudioConductor - FMOD] {_codegenError}");
            Repaint();
        }

        private void GetAllBus()
        {
            _fmodIssue = null;
            _fmodIssueType = MessageType.None;

            if (!FMODEditorPreflight.TryGetSettings(out Settings settings, out string error))
            {
                ReportFmodIssue(error, MessageType.Error);
                return;
            }

            if (!FMODEditorPreflight.TryResolveBankDirectory(settings, out string bankDirectory, out error))
            {
                ReportFmodIssue($"{error} Build banks in FMOD Studio, then refresh the FMOD banks.", MessageType.Warning);
                return;
            }

            if (!FMODEditorPreflight.TryValidateRequiredBanks(
                    settings,
                    bankDirectory,
                    out string[] masterBankPaths,
                    out string[] stringsBankPaths,
                    out error))
            {
                ReportFmodIssue($"{error} Build banks in FMOD Studio, then refresh the FMOD banks.", MessageType.Warning);
                return;
            }

            if (!FMODEditorPreflight.TryCreateStudioSystem(out FMODStudio.System system, out error))
            {
                ReportFmodIssue(error, MessageType.Error);
                return;
            }

            var loadedStringsBanks = new List<FMODStudio.Bank>();
            var loadedMasterBanks = new List<FMODStudio.Bank>();
            var busPaths = new List<string>();
            bool loadCompleted = false;

            try
            {
                for (int i = 0; i < stringsBankPaths.Length; i++)
                {
                    RESULT result = system.loadBankFile(
                        stringsBankPaths[i],
                        FMODStudio.LOAD_BANK_FLAGS.NORMAL,
                        out FMODStudio.Bank stringsBank);

                    if (result != RESULT.OK)
                    {
                        ReportFmodIssue($"Failed to load strings bank at '{stringsBankPaths[i]}'. Result: {result}.", MessageType.Error);
                        return;
                    }

                    loadedStringsBanks.Add(stringsBank);
                }

                for (int i = 0; i < masterBankPaths.Length; i++)
                {
                    RESULT result = system.loadBankFile(
                        masterBankPaths[i],
                        FMODStudio.LOAD_BANK_FLAGS.NORMAL,
                        out FMODStudio.Bank masterBank);

                    if (result != RESULT.OK)
                    {
                        ReportFmodIssue($"Failed to load master bank at '{masterBankPaths[i]}'. Result: {result}.", MessageType.Error);
                        return;
                    }

                    loadedMasterBanks.Add(masterBank);
                }

                for (int bankIndex = 0; bankIndex < loadedMasterBanks.Count; bankIndex++)
                {
                    FMODStudio.Bank masterBank = loadedMasterBanks[bankIndex];
                    RESULT result = masterBank.getBusCount(out int busCount);
                    if (result != RESULT.OK)
                    {
                        ReportFmodIssue($"Failed to query bus count from '{masterBankPaths[bankIndex]}'. Result: {result}.", MessageType.Error);
                        return;
                    }

                    if (busCount <= 0) continue;

                    result = masterBank.getBusList(out FMODStudio.Bus[] buses);
                    if (result != RESULT.OK)
                    {
                        ReportFmodIssue($"Failed to query bus list from '{masterBankPaths[bankIndex]}'. Result: {result}.", MessageType.Error);
                        return;
                    }

                    for (int busIndex = 0; busIndex < buses.Length; busIndex++)
                    {
                        result = buses[busIndex].getPath(out string path);
                        if (result != RESULT.OK)
                        {
                            ReportFmodIssue($"Failed to query a bus path from '{masterBankPaths[bankIndex]}'. Result: {result}.", MessageType.Error);
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(path)) busPaths.Add(path);
                    }
                }

                if (busPaths.Count == 0)
                {
                    ReportFmodIssue("Load All Buses failed because no valid bus paths were collected. Existing data was preserved.", MessageType.Error);
                    return;
                }

                loadCompleted = true;
            }
            catch (System.Exception e)
            {
                ReportFmodIssue($"Failed to load FMOD buses: {e.GetType().Name}: {e.Message}", MessageType.Error);
            }
            finally
            {
                for (int i = loadedMasterBanks.Count - 1; i >= 0; i--)
                {
                    FMODEditorPreflight.LogCleanupResult(loadedMasterBanks[i].unload(), $"Unload master bank '{masterBankPaths[i]}'");
                }

                for (int i = loadedStringsBanks.Count - 1; i >= 0; i--)
                {
                    FMODEditorPreflight.LogCleanupResult(loadedStringsBanks[i].unload(), $"Unload strings bank '{stringsBankPaths[i]}'");
                }

                FMODEditorPreflight.LogCleanupResult(system.release(), "Release temporary FMOD Studio system");
            }

            if (!loadCompleted) return;

            List<BusEntry> loadedBuses = BuildBusEntries(busPaths);
            if (!EditorUtility.DisplayDialog(
                    "Replace Bus Data?",
                    $"Load All found {loadedBuses.Count} buses. Replace the current bus list? Unsaved edits will be lost.",
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            _buses = loadedBuses;
            _so = new SerializedObject(this);
            _busesProp = _so.FindProperty(nameof(_buses));
            SetDataUnsaved(true);
            Repaint();
        }
        private static List<BusEntry> BuildBusEntries(List<string> busPaths)
        {
            List<BusEntry> busEntries = new();
            HashSet<string> seenPaths = new(System.StringComparer.Ordinal);
            HashSet<string> usedKeys = new(System.StringComparer.Ordinal);

            foreach (string busPath in busPaths)
            {
                if (string.IsNullOrWhiteSpace(busPath) || !seenPaths.Add(busPath)) continue;

                busEntries.Add(new BusEntry
                {
                    Key = CreateUniqueBusKey(busPath, usedKeys),
                    BusPath = busPath,
                });
            }

            return busEntries;
        }

        private static string CreateUniqueBusKey(string busPath, HashSet<string> usedKeys)
        {
            string rawKey = busPath;
            if (rawKey.StartsWith("bus:/", System.StringComparison.OrdinalIgnoreCase))
            {
                rawKey = rawKey.Substring("bus:/".Length);
            }

            rawKey = rawKey.Trim('/');
            if (string.IsNullOrWhiteSpace(rawKey)) rawKey = "Master";

            System.Text.StringBuilder builder = new(rawKey.Length);
            for (int i = 0; i < rawKey.Length; i++)
            {
                char character = rawKey[i];
                builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
            }

            string key = builder.Length == 0 ? "Master" : builder.ToString();
            if (!char.IsLetter(key[0]) && key[0] != '_')
            {
                key = "_" + key;
            }

            string uniqueKey = key;
            int suffix = 2;
            while (!usedKeys.Add(uniqueKey))
            {
                uniqueKey = key + "_" + suffix;
                suffix++;
            }

            return uniqueKey;
        }


    }
}