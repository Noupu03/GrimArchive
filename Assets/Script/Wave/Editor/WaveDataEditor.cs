using UnityEngine;
using UnityEditor;
using GrimArchive.Wave;

namespace GrimArchive.Wave.Editor
{
    [CustomEditor(typeof(WaveData))]
    public class WaveDataEditor : UnityEditor.Editor
    {
        private SerializedProperty waveCooldownProp;
        private SerializedProperty spawnModeProp;
        private SerializedProperty targetFloorProp;
        private SerializedProperty spawnCenterProp;
        private SerializedProperty spawnTileRadiusProp;
        private SerializedProperty targetRoomRoleProp;
        private SerializedProperty targetRoomIdProp;
        private SerializedProperty partiesProp;

        private void OnEnable()
        {
            waveCooldownProp = serializedObject.FindProperty("waveCooldown");
            spawnModeProp = serializedObject.FindProperty("spawnMode");
            targetFloorProp = serializedObject.FindProperty("targetFloor");
            spawnCenterProp = serializedObject.FindProperty("spawnCenter");
            spawnTileRadiusProp = serializedObject.FindProperty("spawnTileRadius");
            targetRoomRoleProp = serializedObject.FindProperty("targetRoomRole");
            targetRoomIdProp = serializedObject.FindProperty("targetRoomId");
            partiesProp = serializedObject.FindProperty("parties");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(waveCooldownProp);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(spawnModeProp);
            
            SpawnMode mode = (SpawnMode)spawnModeProp.enumValueIndex;

            EditorGUI.indentLevel++;
            switch (mode)
            {
                case SpawnMode.AroundTransform:
                    EditorGUILayout.PropertyField(targetFloorProp);
                    EditorGUILayout.PropertyField(spawnCenterProp);
                    EditorGUILayout.PropertyField(spawnTileRadiusProp);
                    break;
                case SpawnMode.ByRoomRole:
                    EditorGUILayout.PropertyField(targetFloorProp);
                    EditorGUILayout.PropertyField(targetRoomRoleProp);
                    break;
                case SpawnMode.ByRoomId:
                    EditorGUILayout.PropertyField(targetRoomIdProp);
                    break;
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(partiesProp, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
