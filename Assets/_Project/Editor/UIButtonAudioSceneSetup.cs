using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using _Project.UI.Animations;

namespace _Project.EditorTools
{
    /// <summary>为构建场景和运行时卡牌预制体中尚未配置音效的按钮补齐 UIButtonEffect。</summary>
    public static class UIButtonAudioSceneSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/UIScene.unity",
            "Assets/_Project/Scenes/GameScene.unity",
            "Assets/_Project/Scenes/SupplyScene.unity"
        };

        private const string CardPrefabPath = "Assets/_Project/Prefabs/UI/Card/CardView.prefab";

        /// <summary>为三个构建场景和卡牌预制体补齐缺少的按钮音效组件。</summary>
        [MenuItem("Tools/UI/Apply Button Audio To Build Scenes")]
        public static void Apply()
        {
            int addedCount = 0;

            foreach (string scenePath in ScenePaths)
            {
                addedCount += AddSceneEffects(scenePath);
            }

            addedCount += AddCardPrefabEffect();

            Debug.Log($"UIButtonEffect 已补齐，新增 {addedCount} 个组件。");
        }

        /// <summary>处理单个干净场景并恢复原来的活动场景。</summary>
        /// <param name="scenePath">待处理场景的资源路径。</param>
        /// <returns>新添加的组件数量。</returns>
        private static int AddSceneEffects(string scenePath)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;

            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            else if (scene.isDirty)
            {
                Debug.LogWarning($"场景有未保存修改，跳过按钮音效配置：{scenePath}");
                return 0;
            }

            try
            {
                int addedCount = AddEffects(scene.GetRootGameObjects());
                if (addedCount > 0)
                {
                    EditorSceneManager.SaveScene(scene);
                }

                return addedCount;
            }
            finally
            {
                if (openedHere)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                if (activeScene.IsValid() && activeScene.isLoaded)
                {
                    EditorSceneManager.SetActiveScene(activeScene);
                }
            }
        }

        /// <summary>为缺少音效组件的按钮添加无视觉动画的 UIButtonEffect。</summary>
        /// <param name="roots">待扫描的场景根对象。</param>
        /// <returns>新添加的组件数量。</returns>
        private static int AddEffects(GameObject[] roots)
        {
            int addedCount = 0;

            foreach (GameObject root in roots)
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.GetComponent<UIButtonEffect>() != null)
                    {
                        continue;
                    }

                    Undo.AddComponent<UIButtonEffect>(button.gameObject);
                    addedCount++;
                }
            }

            return addedCount;
        }

        /// <summary>为运行时卡牌预制体中的按钮补齐音效组件。</summary>
        /// <returns>新添加的组件数量。</returns>
        private static int AddCardPrefabEffect()
        {
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(CardPrefabPath);

            try
            {
                int addedCount = AddEffects(new[] { prefabContents });
                if (addedCount > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, CardPrefabPath);
                }

                return addedCount;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }
    }
}
