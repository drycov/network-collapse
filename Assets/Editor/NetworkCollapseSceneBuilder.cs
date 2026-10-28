#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NetworkCollapse.Editor
{
    /// <summary>Creates and registers a full playable scene from project scripts.</summary>
    public static class NetworkCollapseSceneBuilder
    {
        private const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        [MenuItem("Network Collapse/Create Main Scene")]
        public static void CreateScene()
        {
            string directory = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .039f, .071f);
            cameraObject.transform.position = new Vector3(0, 0, -20);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<NetworkCameraController>();
            new GameObject("Network Collapse - Game Controller").AddComponent<NetworkGame>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.Refresh();
            Debug.Log("Playable scene generated and registered: " + ScenePath);
        }
    }
}
#endif
