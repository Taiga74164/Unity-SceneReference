#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Taiga74164.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
namespace Taiga74164.Editor
{
    [CustomEditor(typeof(SceneReference))]
    public class SceneReferenceEditor : UnityEditor.Editor
    {
        private const float EnumNameWidth = 120.0f;
        private const float HorizontalSpacing = 2.0f;
        private const string EnumNamePattern = "^[A-Z][a-zA-Z0-9]*$";

        private ReorderableList _reorderableList;
        private SerializedProperty _sceneEnumPathProperty;
        private SerializedProperty _sceneNamespaceProperty;
        private SerializedProperty _sceneFolderPathProperty;
        private SerializedProperty _sceneReferencesProperty;
        private bool _showConfigList = true;
        private bool _showSceneList = true;

        #region Editor Callbacks

        private void OnEnable()
        {
            _sceneReferencesProperty = serializedObject.FindProperty("sceneReferences");
            _sceneEnumPathProperty = serializedObject.FindProperty("sceneEnumPath");
            _sceneNamespaceProperty = serializedObject.FindProperty("sceneNamespace");
            _sceneFolderPathProperty = serializedObject.FindProperty("sceneFolderPath");
            InitReorderableList();

            var sceneRef = (SceneReference)target;
            if (sceneRef.sceneReferences.Count != 0) return;

            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length > 0)
            {
                PopulateFromBuildSettings(sceneRef, buildScenes);
            }

            // Only populate from enum if we still have no scenes after build settings
            if (sceneRef.sceneReferences.Count == 0 && File.Exists(sceneRef.sceneEnumPath))
            {
                PopulateFromEnumFile(sceneRef);
            }
        }

        private void InitReorderableList()
        {
            _reorderableList = new ReorderableList(serializedObject, _sceneReferencesProperty,
                true,
                false,
                true,
                true)
            {
                drawElementCallback = DrawSceneElement,
                onAddCallback = OnAddElement,
                elementHeight = EditorGUIUtility.singleLineHeight
            };
        }

        private void PopulateFromBuildSettings(SceneReference sceneRef, EditorBuildSettingsScene[] buildScenes)
        {
            foreach (var buildScene in buildScenes)
            {
                var scenePath = buildScene.path;
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (!sceneAsset) continue;

                // Generate enum name from scene name
                var enumName = GenerateEnumName(sceneAsset.name);

                var reference = new SceneReferenceField
                {
                    enumName = enumName,
                    sceneAsset = sceneAsset
                };
                sceneRef.sceneReferences.Add(reference);
            }

            if (sceneRef.sceneReferences.Count > 0)
            {
                EditorUtility.SetDirty(sceneRef);
            }
        }

        private void PopulateFromEnumFile(SceneReference sceneRef)
        {
            var content = File.ReadAllText(sceneRef.sceneEnumPath);

            // Parse SceneId enum
            var match = Regex.Match(content, @"public enum SceneId\s*{([^}]*)}", RegexOptions.Singleline);
            if (!match.Success) return;

            var enumContent = match.Groups[1].Value;
            var lines = enumContent.Split(',')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("None"));

            foreach (var line in lines)
            {
                var parts = line.Split('=')
                    .Select(p => p.Trim())
                    .ToArray();

                if (parts.Length < 1) continue;

                var reference = new SceneReferenceField
                {
                    enumName = parts[0].Trim(),
                    sceneAsset = null
                };
                sceneRef.sceneReferences.Add(reference);
            }

            if (sceneRef.sceneReferences.Count > 0)
            {
                EditorUtility.SetDirty(sceneRef);
            }
        }

        private string GenerateEnumName(string sceneName)
        {
            // Remove any non-alphanumeric characters
            var enumName = Regex.Replace(sceneName, "[^a-zA-Z0-9]", "");

            // Ensure first character is uppercase
            if (enumName.Length > 0)
            {
                enumName = char.ToUpper(enumName[0]) + enumName.Substring(1);
            }

            // If the name is empty after cleaning, use a default
            if (string.IsNullOrEmpty(enumName))
            {
                enumName = "Scene";
            }

            return enumName;
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var sceneRef = (SceneReference)target;

            EditorGUILayout.Space(10);

            // Path Configuration
            _showConfigList = EditorGUILayout.Foldout(_showConfigList, "Path Configuration", true);
            if (_showConfigList)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(_sceneEnumPathProperty, new GUIContent("Enum File Path"));
                EditorGUILayout.PropertyField(_sceneNamespaceProperty, new GUIContent("Enum Namespace"));
                EditorGUILayout.PropertyField(_sceneFolderPathProperty, new GUIContent("Scene Folder Path"));
                EditorGUILayout.Space(5);
                
                EditorGUILayout.Space(5);
            
                if (!Directory.Exists(sceneRef.sceneFolderPath))
                {
                    EditorGUILayout.HelpBox(
                        $"Scene folder does not exist: {sceneRef.sceneFolderPath}\nPlease create it or specify a different path.", 
                        MessageType.Warning);
                }

                EditorGUILayout.HelpBox(
                    $"Only scenes from {sceneRef.sceneFolderPath} will be available for selection.", 
                    MessageType.Info);

                EditorGUILayout.Space(5);
            }

            // Scene list foldout
            _showSceneList = EditorGUILayout.Foldout(_showSceneList, "Scene References", true);

            if (_showSceneList)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Order determines build index. Make sure to Generate Scene Enum after adding or removing scenes.",
                    MessageType.Info);
                EditorGUILayout.Space(5);

                _reorderableList.DoLayoutList();

                // Show warning for invalid names
                var invalidNames = GetInvalidEnumNames(sceneRef);
                var enumerable = invalidNames as string[] ?? invalidNames.ToArray();
                if (enumerable.Any())
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(
                        "Invalid enum names found:\n" + string.Join("\n", enumerable) +
                        "\n\nPlease fix these names before generating the enum.",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledGroupScope(HasInvalidNames(sceneRef)))
            {
                if (GUILayout.Button("Generate Scene Enum"))
                {
                    GenerateSceneEnum(sceneRef);
                    UpdateBuildSettings(sceneRef);
                }
            }

            serializedObject.ApplyModifiedProperties();

            // Automatically update build settings if any changes are made
            if (GUI.changed)
            {
                UpdateBuildSettings(sceneRef);
            }
        }

        #endregion

        #region ReorderableList Callbacks

        private void DrawSceneElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = _sceneReferencesProperty.GetArrayElementAtIndex(index);
            var enumNameProp = element.FindPropertyRelative("enumName");
            var sceneAssetProp = element.FindPropertyRelative("sceneAsset");

            // Enum name field with validation feedback
            var enumNameRect = new Rect(rect.x, rect.y, EnumNameWidth, rect.height);

            // Draw the text field with different colors based on validation
            var oldColor = GUI.color;
            GUI.color = IsValidEnumName(enumNameProp.stringValue) ? oldColor : new Color(1, 0.8f, 0.8f);
            var newEnumName = EditorGUI.TextField(enumNameRect, enumNameProp.stringValue);
            GUI.color = oldColor;

            // Clean and validate the input
            newEnumName = Regex.Replace(newEnumName, "[^a-zA-Z0-9]", "");
            if (newEnumName.Length > 0)
            {
                // Ensure first character is uppercase
                newEnumName = char.ToUpper(newEnumName[0]) + newEnumName.Substring(1);
            }

            if (newEnumName != enumNameProp.stringValue)
            {
                enumNameProp.stringValue = newEnumName;
            }

            // Scene asset field
            var sceneFieldWidth = rect.width - EnumNameWidth - HorizontalSpacing;
            var sceneRect = new Rect(enumNameRect.xMax + HorizontalSpacing, rect.y, sceneFieldWidth, rect.height);

            // Store the old scene asset
            var oldScene = sceneAssetProp.objectReferenceValue;

            // Draw the object field
            if (GUI.Button(sceneRect, oldScene ? oldScene.name : "None", EditorStyles.objectField))
            {
                CreateScenePicker(sceneAssetProp);
            }

            // Draw the object field icon
            var iconRect = new Rect(
                sceneRect.x + sceneRect.width - 16.0f,
                sceneRect.y + (sceneRect.height - 16.0f) * 0.5f,
                16.0f,
                16.0f
            );
            GUI.Label(iconRect, EditorGUIUtility.IconContent("SceneAsset Icon"));
        }

        private void CreateScenePicker(SerializedProperty sceneAssetProp)
        {
            var sceneRef = (SceneReference)target;
            // Get all scene assets in the project
            var sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
            var filteredScenes = sceneGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith(sceneRef.sceneFolderPath))
                .Select(AssetDatabase.LoadAssetAtPath<SceneAsset>)
                .Where(scene => scene)
                .ToList();

            // Show the picker window
            SceneObjectPickerWindow.ShowWindow(filteredScenes, selectedScene =>
            {
                sceneAssetProp.objectReferenceValue = selectedScene;
                sceneAssetProp.serializedObject.ApplyModifiedProperties();

                UpdateBuildSettings(sceneRef);
            });
        }

        private void OnAddElement(ReorderableList list)
        {
            var index = list.serializedProperty.arraySize;
            list.serializedProperty.InsertArrayElementAtIndex(index);
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("enumName").stringValue = "NewScene";
            element.FindPropertyRelative("sceneAsset").objectReferenceValue = null;
        }

        #endregion

        #region Validation

        private bool IsValidEnumName(string enumName)
            => !string.IsNullOrEmpty(enumName) && Regex.IsMatch(enumName, EnumNamePattern);

        private bool HasInvalidNames(SceneReference sceneRef)
            => sceneRef.sceneReferences.Any(reference => !IsValidEnumName(reference.enumName));

        private IEnumerable<string> GetInvalidEnumNames(SceneReference sceneRef)
            => sceneRef.sceneReferences
                .Where(reference => !IsValidEnumName(reference.enumName))
                .Select(reference => reference.enumName);

        #endregion

        #region Helpers

        private void GenerateSceneEnum(SceneReference sceneRef)
        {
            var enumEntries = sceneRef.sceneReferences
                .Select((reference, index) => $"        {reference.enumName} = {index}")
                .ToList();

            var enumContent =
                $@"// This file is auto-generated. Do not modify.
namespace {sceneRef.sceneNamespace}
{{
    public enum SceneId
    {{
        None = -1,
{string.Join(",\n", enumEntries)}
    }}
}}";

            File.WriteAllText(sceneRef.sceneEnumPath, enumContent);
            AssetDatabase.Refresh();
        }

        private void UpdateBuildSettings(SceneReference sceneRef)
        {
            var validScenes = sceneRef.sceneReferences
                .Where(reference => reference.sceneAsset)
                .Select(reference => new EditorBuildSettingsScene(
                    AssetDatabase.GetAssetPath(reference.sceneAsset), true))
                .ToArray();

            EditorBuildSettings.scenes = validScenes;
        }

        #endregion
    }

    public class SceneObjectPickerWindow : EditorWindow
    {
        private static SceneObjectPickerWindow _activeWindow;
        private GUIStyle _labelStyle;
        private Action<SceneAsset> _onSelect;

        private List<SceneAsset> _scenes;
        private Vector2 _scrollPosition;
        private string _searchString = "";

        private void OnDestroy()
        {
            // Clear the active window reference
            if (_activeWindow == this) _activeWindow = null;
        }

        private void OnGUI()
        {
            // Search bar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _searchString = EditorGUILayout.TextField(_searchString, EditorStyles.toolbarSearchField);
            EditorGUILayout.EndHorizontal();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var scene in _scenes.Where(scene
                         => string.IsNullOrEmpty(_searchString) || scene.name.ToLower().Contains(_searchString.ToLower())))
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(EditorGUIUtility.IconContent("SceneAsset Icon"),
                        GUILayout.Width(30), GUILayout.Height(30)))
                {
                    SelectScene(scene);
                }

                if (GUILayout.Button(scene.name, _labelStyle, GUILayout.Height(30)))
                {
                    SelectScene(scene);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        public static void ShowWindow(List<SceneAsset> scenes, Action<SceneAsset> onSelect)
        {
            // If a window is already open, just update it
            if (_activeWindow)
            {
                _activeWindow.Initialize(scenes, onSelect);
                _activeWindow.Focus();
                return;
            }

            // Otherwise create a new window
            var window = CreateInstance<SceneObjectPickerWindow>();
            window.titleContent = new GUIContent("Select Scene Asset");
            window.minSize = new Vector2(300, 300);
            window.Initialize(scenes, onSelect);
            window.Show();
            _activeWindow = window;
        }

        private void Initialize(List<SceneAsset> scenes, Action<SceneAsset> onSelect)
        {
            _scenes = scenes;
            _onSelect = onSelect;

            _labelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft
            };
        }

        private void SelectScene(SceneAsset scene)
        {
            _onSelect?.Invoke(scene);

            if (Selection.activeObject is SceneReference sceneRef)
            {
                EditorUtility.SetDirty(sceneRef);
            }

            Close();
        }
    }
}
#endif