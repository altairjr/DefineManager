#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DoubleA.Editor
{
    /// <summary>
    /// Window to manage scripting defines per BuildTargetGroup.
    /// - Editable list of available defines
    /// - Toggle for each define
    /// - Apply to selected BuildTargetGroup or to all
    /// </summary>
    public class DefineManager : EditorWindow
    {
        [Serializable]
        private class SerializableList<T>
        {
            public List<T> items;
            public SerializableList(List<T> list) { items = list; }
        }

        private const string PREF_KEY = "DoubleA.DefineManager.AvailableDefines";

        private BuildTargetGroup selectedGroup;
        private List<string> availableDefines = new List<string> { "Debug_PlayerMovement" };

        // State of toggles mirrored by the order of availableDefines
        private List<bool> defineStates = new List<bool>();

        // Field to add a new define
        private string newDefine = string.Empty;

        // UI options
        private bool applyToAllGroups = false;
        private Vector2 scrollPos;

        [MenuItem("Tools/Define Manager")]
        public static void ShowWindow() =>
            GetWindow<DefineManager>("Define Manager");

        private void OnEnable()
        {
            LoadAvailableDefines();
            selectedGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            EnsureStateSize();
            LoadStatesForSelectedGroup();
        }

        private void OnDisable()
        {
            SaveAvailableDefines();
        }

        private void OnFocus()
        {
            // Refresh if user changes build target outside this window
            var current = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (current != selectedGroup)
            {
                selectedGroup = current;
                LoadStatesForSelectedGroup();
                Repaint();
            }
        }

        private void EnsureStateSize()
        {
            while (defineStates.Count < availableDefines.Count)
                defineStates.Add(false);

            if (defineStates.Count > availableDefines.Count)
                defineStates.RemoveRange(availableDefines.Count, defineStates.Count - availableDefines.Count);
        }

        #region Defines IO

        private void SaveAvailableDefines()
        {
            string json = JsonUtility.ToJson(new SerializableList<string>(availableDefines));
            EditorPrefs.SetString(PREF_KEY, json);
        }

        private void LoadAvailableDefines()
        {
            if (EditorPrefs.HasKey(PREF_KEY))
            {
                string json = EditorPrefs.GetString(PREF_KEY);
                var wrapper = JsonUtility.FromJson<SerializableList<string>>(json);
                availableDefines = wrapper?.items ?? new List<string>();
            }
            else
            {
                availableDefines = new List<string> { "Debug_PlayerMovement" };
            }
        }

        private static string[] GetDefinesForGroup(BuildTargetGroup group)
        {
            var s = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
            return s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => d.Trim())
                    .Where(d => !string.IsNullOrEmpty(d))
                    .ToArray();
        }

        private void LoadStatesForSelectedGroup()
        {
            var currentDefines = new HashSet<string>(GetDefinesForGroup(selectedGroup), StringComparer.Ordinal);
            EnsureStateSize();

            for (int i = 0; i < availableDefines.Count; i++)
                defineStates[i] = currentDefines.Contains(availableDefines[i]);
        }

        private void SaveDefinesForGroup(BuildTargetGroup group, IEnumerable<string> definesToSet)
        {
            string final = string.Join(";", definesToSet);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, final);
        }

        private IEnumerable<string> BuildFinalDefineListForGroup(BuildTargetGroup group)
        {
            // Keep defines not in availableDefines + add the ones toggled by the user
            var existing = new HashSet<string>(GetDefinesForGroup(group), StringComparer.Ordinal);

            foreach (var d in availableDefines)
                existing.Remove(d);

            for (int i = 0; i < availableDefines.Count; i++)
            {
                if (defineStates.Count > i && defineStates[i])
                    existing.Add(availableDefines[i]);
            }

            return existing.OrderBy(x => x);
        }
        #endregion

        #region UI
        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scripting Defines Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Build Target Group:", GUILayout.Width(140));
            var newSelected = (BuildTargetGroup)EditorGUILayout.EnumPopup(selectedGroup);
            if (newSelected != selectedGroup)
            {
                selectedGroup = newSelected;
                LoadStatesForSelectedGroup();
            }

            if (GUILayout.Button("Use Current", GUILayout.Width(90)))
            {
                selectedGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                LoadStatesForSelectedGroup();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Editable list of availableDefines
            EditorGUILayout.LabelField("Available Defines (editable)", EditorStyles.label);
            EditorGUILayout.HelpBox("Add defines you want to manage here. Avoid spaces or invalid characters.", MessageType.Info);

            // Add new define
            EditorGUILayout.BeginHorizontal();
            newDefine = EditorGUILayout.TextField(newDefine);
            if (GUILayout.Button("Add", GUILayout.Width(80)))
            {
                TryAddNewDefine(newDefine);
                newDefine = string.Empty;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Scroll with toggles and remove buttons
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            EnsureStateSize();

            for (int i = 0; i < availableDefines.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                defineStates[i] = EditorGUILayout.ToggleLeft(availableDefines[i], defineStates[i]);
                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    RemoveDefineAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Options & actions
            applyToAllGroups = EditorGUILayout.ToggleLeft("Apply to all BuildTargetGroups", applyToAllGroups);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Defines", GUILayout.Height(30)))
            {
                SaveDefinesAction();
            }

            if (GUILayout.Button("Reload", GUILayout.Height(30)))
            {
                LoadStatesForSelectedGroup();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Note:", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("This manager only adds/removes defines in PlayerSettings for the selected BuildTargetGroup(s). Use with caution.", MessageType.None);
        }
        #endregion

        #region UI Helpers
        private void TryAddNewDefine(string defineText)
        {
            if (string.IsNullOrWhiteSpace(defineText))
            {
                ShowNotification(new GUIContent("Empty define not allowed"));
                return;
            }

            var cleaned = defineText.Trim();
            if (cleaned.Contains(" ") || cleaned.Contains(";"))
            {
                ShowNotification(new GUIContent("Invalid define: avoid spaces or ';'"));
                return;
            }

            if (availableDefines.Contains(cleaned))
            {
                ShowNotification(new GUIContent("Define already exists"));
                return;
            }

            availableDefines.Add(cleaned);
            defineStates.Add(false);
            Repaint();
        }

        private void RemoveDefineAt(int index)
        {
            if (index < 0 || index >= availableDefines.Count) return;
            availableDefines.RemoveAt(index);
            if (index < defineStates.Count) defineStates.RemoveAt(index);
            Repaint();
        }

        private void SaveDefinesAction()
        {
            if (applyToAllGroups)
            {
                if (!EditorUtility.DisplayDialog("Confirm", "Apply defines to ALL BuildTargetGroups? This will overwrite (keeping other unmanaged ones).", "Yes", "Cancel"))
                    return;

                foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
                {
                    if (group == BuildTargetGroup.Unknown) continue;

                    var list = BuildFinalDefineListForGroup(group);
                    SaveDefinesForGroup(group, list);
                }

                ShowNotification(new GUIContent("Defines applied to all groups"));
            }
            else
            {
                var list = BuildFinalDefineListForGroup(selectedGroup);
                SaveDefinesForGroup(selectedGroup, list);
                ShowNotification(new GUIContent($"Defines saved for {selectedGroup}"));
            }
        }
        #endregion
    }
}

#endif