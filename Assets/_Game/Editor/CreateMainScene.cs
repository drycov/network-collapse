#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NetworkCollapse.Editor
{
    public static class CreateMainScene
    {
        [MenuItem("Network Collapse/Create Main Scene")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("Network Collapse");
            game.AddComponent<NetworkGame>();
            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/Main.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/_Game/Scenes/Main.unity", true) };
            Debug.Log("Network Collapse main scene created and added to Build Settings.");
        }
    }
}
#endif
