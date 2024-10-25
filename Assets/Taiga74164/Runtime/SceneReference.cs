using System;
using System.Collections.Generic;
using UnityEngine;
namespace Taiga74164.Runtime
{
    [CreateAssetMenu(fileName = "SceneReference", menuName = "Taiga74164/Scene Reference")]
    public class SceneReference : ScriptableObject
    {
        public string sceneEnumPath = "Assets/Scripts/Runtime/Generated/SceneIds.cs";
        public string sceneNamespace = "Runtime.Generated";
        public string sceneFolderPath = "Assets/Scenes";
        public List<SceneReferenceField> sceneReferences = new List<SceneReferenceField>();
    }
    
    [Serializable]
    public class SceneReferenceField
    {
        public string enumName;
#if UNITY_EDITOR
        public UnityEditor.SceneAsset sceneAsset;
#endif
    }
}