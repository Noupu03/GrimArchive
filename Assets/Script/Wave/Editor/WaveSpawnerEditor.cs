using UnityEngine;
using UnityEditor;
using GrimArchive.Wave;

namespace GrimArchive.Wave.Editor
{
    [CustomEditor(typeof(WaveSpawner))]
    public class WaveSpawnerEditor : UnityEditor.Editor
    {
        private SerializedProperty waveDataProp;
        private UnityEditor.Editor waveDataEditor;

        private void OnEnable()
        {
            waveDataProp = serializedObject.FindProperty("waveData");
        }

        private void OnDisable()
        {
            if (waveDataEditor != null)
            {
                DestroyImmediate(waveDataEditor);
            }
        }

        public override void OnInspectorGUI()
        {
            // 1. Draw NativeRoutine base fields
            base.OnInspectorGUI();

            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("▶ Wave Data (Inline Settings)", EditorStyles.boldLabel);
            
            // 2. If a WaveData object is assigned, draw its custom editor inline
            if (waveDataProp.objectReferenceValue != null)
            {
                // Create a cached editor for the WaveData object
                CreateCachedEditor(waveDataProp.objectReferenceValue, null, ref waveDataEditor);
                
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUI.BeginChangeCheck();
                
                // This will use the WaveDataEditor's OnInspectorGUI()!
                waveDataEditor.OnInspectorGUI();
                
                if (EditorGUI.EndChangeCheck())
                {
                    // Repaint to ensure UI reacts immediately to SpawnMode changes
                    Repaint();
                }
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox("WaveData가 할당되지 않았습니다. Spawn Mode를 설정하려면 WaveData 에셋을 먼저 할당하거나 새로 생성하세요.", MessageType.Warning);
                if (GUILayout.Button("Create & Assign New WaveData"))
                {
                    WaveData newData = ScriptableObject.CreateInstance<WaveData>();
                    string path = UnityEditor.EditorUtility.SaveFilePanelInProject("Save Wave Data", "NewWaveData", "asset", "Save new WaveData");
                    if (!string.IsNullOrEmpty(path))
                    {
                        UnityEditor.AssetDatabase.CreateAsset(newData, path);
                        UnityEditor.AssetDatabase.SaveAssets();
                        waveDataProp.objectReferenceValue = newData;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
