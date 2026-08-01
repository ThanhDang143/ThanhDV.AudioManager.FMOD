using System.Collections.Generic;
using FMOD;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using FMODStudio = global::FMOD.Studio;
using Debug = UnityEngine.Debug;

namespace ThanhDV.AudioConductor.FMOD
{
    public class EventRefManagerEditorWindow : EditorWindow
    {
        [SerializeField] private List<EventReferenceEntry> _eventReferences = new();
        [SerializeField] private string _searchText = "";

        private Vector2 _scroll;

        private SerializedObject _so;
        private SerializedProperty _eventReferencesProp;

        private bool _hasDataUnsaved = false;
        private FMODReferencesLoadStatus _loadStatus;
        private string _loadError;
        private string _saveError;
        private string _codegenError;
        private string _fmodIssue;
        private MessageType _fmodIssueType = MessageType.None;

        private bool HasInvalidData => _loadStatus == FMODReferencesLoadStatus.Invalid;

        [MenuItem(Common.MENU_ITEM + "EventReference Manager", false, 3)]
        public static void ShowWindow()
        {
            EventRefManagerEditorWindow window = GetWindow<EventRefManagerEditorWindow>();
            window.titleContent = new GUIContent("EventReference Manager");
            window.minSize = new Vector2(500, 300);
            window.Show();
        }

        private void OnEnable()
        {
            saveChangesMessage = "Save pending AudioConductor event reference changes before closing?";
            SetDataUnsaved(false);
            _so = new SerializedObject(this);
            _eventReferencesProp = _so.FindProperty(nameof(_eventReferences));

            LoadEventReferences();
            RefreshFmodStatus();
        }

        private void OnGUI()
        {
            string title = "AudioConductor - FMOD - EventReference";
            string subtitle = "Created by ThanhDV";
            EditorHelper.CreateHeader(title, subtitle);

            DrawStatusBanners();

            EditorGUI.BeginDisabledGroup(_hasDataUnsaved);
            if (GUILayout.Button(new GUIContent("Refresh", "Reload eventReferences from data and refresh displayed data.")))
            {
                LoadEventReferences();
                RefreshFmodStatus();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!_hasDataUnsaved || HasInvalidData);
            if (GUILayout.Button(new GUIContent("Clean & Save", "Removes eventReferences with empty keys or values, removes duplicates, and saves changes.")))
            {
                CleanEventReferences();
                if (TryPrepareWrapper(out string source) && SaveEventReferences()) GenerateWrapper(source);
            }

            if (GUILayout.Button(new GUIContent("Discard Changes", "Discard all changes.")))
            {
                LoadEventReferences();
            }
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_codegenError))
            {
                EditorGUI.BeginDisabledGroup(HasInvalidData);
                if (GUILayout.Button(new GUIContent("Retry Generate", "Regenerate FMODEventReference from the saved JSON data.")))
                {
                    RetryGenerateWrapper();
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUI.BeginDisabledGroup(HasInvalidData);
            Color originalBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button(new GUIContent("Load All EventReferences", "Load all EventReference from the FMOD project. \nNote: this will delete all currently saved EventReferences.")))
            {
                GetAllEventReferences();
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
                EditorHelper.DrawListWithoutHeader(_eventReferencesProp, "EventReferences");
            }
            else
            {
                int pendingDeleteIndex = DrawFilteredEventReferences(_eventReferencesProp, _searchText);
                if (pendingDeleteIndex >= 0)
                {
                    termBeforeDelete = _searchText;
                    _eventReferencesProp.DeleteArrayElementAtIndex(pendingDeleteIndex);
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
                if (!HasAnyEventRefMatch(_eventReferences, termBeforeDelete))
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
            LoadEventReferences();
            _codegenError = "JSON backup restored. FMODEventReference regeneration is required.";
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
                CleanEventReferences();
                if (!TryPrepareWrapper(out string source) || !SaveEventReferences()) return;
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
            LoadEventReferences();
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

        private static int DrawFilteredEventReferences(SerializedProperty eventRefsProp, string searchText)
        {
            if (eventRefsProp == null) return -1;

            string term = searchText?.Trim();
            if (string.IsNullOrEmpty(term))
            {
                EditorHelper.DrawListWithoutHeader(eventRefsProp, "EventReferences");
                return -1;
            }

            int total = eventRefsProp.arraySize;
            int shown = 0;

            EditorGUILayout.LabelField($"Search results: {term}  (showing matches)", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            int pendingDeleteIndex = -1;

            for (int i = 0; i < total; i++)
            {
                SerializedProperty element = eventRefsProp.GetArrayElementAtIndex(i);

                SerializedProperty keyProp = element.FindPropertyRelative(nameof(EventReferenceEntry.Key));
                SerializedProperty eventRefProp = element.FindPropertyRelative(nameof(EventReferenceEntry.EventReference));

                string key = keyProp?.stringValue;
                string eventPath = eventRefProp?.FindPropertyRelative("Path")?.stringValue;

                if (!ContainsIgnoreCase(key, term) && !ContainsIgnoreCase(eventPath, term))
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
                EditorGUILayout.HelpBox("No event references matched your search.", MessageType.Info);
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

        private static bool HasAnyEventRefMatch(List<EventReferenceEntry> eventRefs, string searchText)
        {
            if (eventRefs == null) return false;

            string term = searchText?.Trim();
            if (string.IsNullOrEmpty(term)) return false;

            for (int i = 0; i < eventRefs.Count; i++)
            {
                EventReferenceEntry entry = eventRefs[i];

                string key = entry.Key;
                string eventPath = entry.EventReference.Path;

                if (ContainsIgnoreCase(key, term) || ContainsIgnoreCase(eventPath, term))
                    return true;
            }

            return false;
        }

        private void LoadEventReferences()
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

            _eventReferences = new List<EventReferenceEntry>(data.EventReferences);
            _so = new SerializedObject(this);
            _eventReferencesProp = _so.FindProperty(nameof(_eventReferences));
            SetDataUnsaved(false);
        }

        private void CleanEventReferences()
        {
            HashSet<string> uniqueKeys = new();
            List<EventReferenceEntry> validEventReferences = new();

            foreach (EventReferenceEntry er in _eventReferences)
            {
                if (er == null)
                {
                    Debug.LogWarning("[AudioConductor - FMOD] Removed null event reference entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(er.Key))
                {
                    Debug.LogWarning("[AudioConductor - FMOD] Removed eventReference entry due to missing or empty key.");
                    continue;
                }

                if (er.EventReference.IsNull)
                {
                    Debug.LogWarning($"[AudioConductor - FMOD] Removed eventReference entry '{er.Key}' due to missing or empty eventReference path.");
                    continue;
                }

                if (!uniqueKeys.Add(er.Key))
                {
                    Debug.LogWarning($"[AudioConductor - FMOD] Removed duplicate eventReference entry with key '{er.Key}'.");
                    continue;
                }

                validEventReferences.Add(er);
            }

            _eventReferences = validEventReferences;
        }

        private bool SaveEventReferences()
        {
            FMODReferencesSaveStatus status = FMODReferencesStore.SaveEventReferences(_eventReferences, out string error);
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
            if (WrapperGenerator.TryBuildFMODEventReference(_eventReferences, out source, out string error)) return true;

            _codegenError = $"Code generation preparation failed. Data was not saved: {error}";
            UpdateWindowUnsavedState();
            Debug.LogError($"[AudioConductor - FMOD] {_codegenError}");
            Repaint();
            return false;
        }

        private void GenerateWrapper(string source)
        {
            if (!WrapperGenerator.TryWriteFMODEventReference(source, out string error))
            {
                SetCodegenFailed(error);
                return;
            }

            _codegenError = null;
            UpdateWindowUnsavedState();
            Debug.Log("[AudioConductor - FMOD] EventReference data saved and wrapper generation completed.");
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

            if (!WrapperGenerator.TryBuildFMODEventReference(data.EventReferences, out string source, out error) ||
                !WrapperGenerator.TryWriteFMODEventReference(source, out error))
            {
                SetCodegenFailed(error);
                return;
            }

            _codegenError = null;
            UpdateWindowUnsavedState();
            Debug.Log("[AudioConductor - FMOD] FMODEventReference generation completed from saved JSON data.");
            Repaint();
        }

        private void SetCodegenFailed(string error)
        {
            _codegenError = $"Data saved, code generation failed: {error}";
            UpdateWindowUnsavedState();
            Debug.LogError($"[AudioConductor - FMOD] {_codegenError}");
            Repaint();
        }

        private void GetAllEventReferences()
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

            if (!FMODEditorPreflight.TryCreateStudioSystem(out FMODStudio.System system, out error))
            {
                ReportFmodIssue(error, MessageType.Error);
                return;
            }

            List<EventReferenceEntry> loadedEventReferences = new();
            bool loadCompleted = false;

            try
            {

                string[] allBankFiles = System.IO.Directory.GetFiles(bankDirectory, "*.bank", System.IO.SearchOption.TopDirectoryOnly);
                List<string> stringsBankPaths = new();
                List<string> contentBankPaths = new();

                for (int i = 0; i < allBankFiles.Length; i++)
                {
                    string bankFilePath = allBankFiles[i];
                    if (bankFilePath.EndsWith(".strings.bank", System.StringComparison.OrdinalIgnoreCase))
                    {
                        stringsBankPaths.Add(bankFilePath);
                    }
                    else
                    {
                        contentBankPaths.Add(bankFilePath);
                    }
                }

                if (contentBankPaths.Count == 0)
                {
                    ReportFmodIssue($"No content banks were found in '{bankDirectory}'. Build banks in FMOD Studio, then refresh the FMOD banks.", MessageType.Warning);
                    return;
                }

                List<FMODStudio.Bank> loadedStringsBanks = new();
                try
                {
                    for (int i = 0; i < stringsBankPaths.Count; i++)
                    {
                        string stringsBankPath = stringsBankPaths[i];
                        RESULT result = system.loadBankFile(stringsBankPath, FMODStudio.LOAD_BANK_FLAGS.NORMAL, out FMODStudio.Bank stringsBank);
                        if (result != RESULT.OK)
                        {
                            ReportFmodIssue($"Strings bank not found or failed to load at '{stringsBankPath}'. Result: {result}. Existing data was preserved.", MessageType.Error);
                            return;
                        }

                        loadedStringsBanks.Add(stringsBank);
                    }

                    HashSet<string> seenPaths = new(System.StringComparer.OrdinalIgnoreCase);
                    HashSet<string> usedKeys = new(System.StringComparer.Ordinal);

                    for (int i = 0; i < contentBankPaths.Count; i++)
                    {
                        string contentBankPath = contentBankPaths[i];
                        RESULT result = system.loadBankFile(contentBankPath, FMODStudio.LOAD_BANK_FLAGS.NORMAL, out FMODStudio.Bank bank);
                        if (result != RESULT.OK)
                        {
                            ReportFmodIssue($"Bank not found or failed to load at '{contentBankPath}'. Result: {result}. Existing data was preserved.", MessageType.Error);
                            return;
                        }

                        try
                        {
                            result = bank.getEventList(out FMODStudio.EventDescription[] eventDescriptions);
                            if (result != RESULT.OK)
                            {
                                ReportFmodIssue($"Failed to query event list from '{contentBankPath}'. Result: {result}. Existing data was preserved.", MessageType.Error);
                                return;
                            }

                            for (int eventIndex = 0; eventIndex < eventDescriptions.Length; eventIndex++)
                            {
                                FMODStudio.EventDescription eventDescription = eventDescriptions[eventIndex];

                                result = eventDescription.getPath(out string path);
                                if (result != RESULT.OK)
                                {
                                    ReportFmodIssue($"Failed to query an event path from '{contentBankPath}'. Result: {result}. Existing data was preserved.", MessageType.Error);
                                    return;
                                }

                                if (string.IsNullOrWhiteSpace(path) || !seenPaths.Add(path)) continue;

                                result = eventDescription.getID(out global::FMOD.GUID guid);
                                if (result != RESULT.OK)
                                {
                                    ReportFmodIssue($"Failed to query event guid for '{path}'. Result: {result}. Existing data was preserved.", MessageType.Error);
                                    return;
                                }

                                loadedEventReferences.Add(new EventReferenceEntry
                                {
                                    Key = CreateUniqueEventKey(path, usedKeys),
                                    EventReference = new EventReference
                                    {
                                        Path = path,
                                        Guid = guid,
                                    }
                                });
                            }
                        }
                        finally
                        {
                            FMODEditorPreflight.LogCleanupResult(bank.unload(), $"Unload content bank '{contentBankPath}'");
                        }
                    }
                }
                finally
                {
                    for (int i = 0; i < loadedStringsBanks.Count; i++)
                    {
                        FMODEditorPreflight.LogCleanupResult(loadedStringsBanks[i].unload(), $"Unload strings bank '{stringsBankPaths[i]}'");
                    }
                }

                if (loadedEventReferences.Count == 0)
                {
                    ReportFmodIssue("Load All EventReferences failed because no valid event references were collected. Existing data was preserved.", MessageType.Error);
                    return;
                }

                loadCompleted = true;
            }
            catch (System.Exception e)
            {
                ReportFmodIssue($"Failed to load FMOD event references: {e.GetType().Name}: {e.Message}", MessageType.Error);
            }
            finally
            {
                FMODEditorPreflight.LogCleanupResult(system.release(), "Release temporary FMOD Studio system");
            }

            if (!loadCompleted) return;

            if (!EditorUtility.DisplayDialog(
                    "Replace EventReference Data?",
                    $"Load All found {loadedEventReferences.Count} event references. Replace the current list? Unsaved edits will be lost.",
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            _eventReferences = loadedEventReferences;
            _so = new SerializedObject(this);
            _eventReferencesProp = _so.FindProperty(nameof(_eventReferences));
            SetDataUnsaved(true);
            Repaint();
        }

        private static string CreateUniqueEventKey(string eventPath, HashSet<string> usedKeys)
        {
            string rawKey = eventPath;
            if (rawKey.StartsWith("event:/", System.StringComparison.OrdinalIgnoreCase))
            {
                rawKey = rawKey.Substring("event:/".Length);
            }

            rawKey = rawKey.Trim('/');
            if (string.IsNullOrWhiteSpace(rawKey)) rawKey = "Event";

            System.Text.StringBuilder builder = new(rawKey.Length);
            for (int i = 0; i < rawKey.Length; i++)
            {
                char character = rawKey[i];
                builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
            }

            string key = builder.Length == 0 ? "Event" : builder.ToString();
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