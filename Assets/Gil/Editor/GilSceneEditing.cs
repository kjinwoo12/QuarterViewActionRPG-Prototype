using Gil.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gil.Editor
{
    /// <summary>
    /// 지역·플레이어·UI 씬을 따로 열어 편집하기 쉽게 한다.
    /// 지역 씬(Scenes/Areas)·플레이어 씬(Player)·UI 씬(UI)을 활성 씬으로 열어 둔 채 플레이하면 시스템 씬(Systems)부터 띄운다
    /// (이 씬들은 게임이 시스템 씬 위에 Additive로 불러오므로, 열려 있는 그대로 플레이하면 두 번 로드되거나 매니저 없이 뜬다).
    /// 지역 씬이 열려 있었으면 그 지역에서 새 게임을 시작한다. 플레이를 멈추면 편집하던 씬 구성으로 돌아온다(playModeStartScene).
    /// 타이틀·시스템 씬이 활성 씬이면 열린 그대로 플레이한다(시스템 씬은 타이틀 없이 바로 새 게임, 함께 열린 플레이어·UI 씬은 다시 열지 않는다).
    /// </summary>
    [InitializeOnLoad]
    public static class GilSceneEditing
    {
        const string AreaPrefix = "Area_";

        static GilSceneEditing()
        {
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => UpdatePlayStartScene();
            EditorSceneManager.sceneOpened += (_, _) => UpdatePlayStartScene();
            EditorSceneManager.sceneClosed += _ => UpdatePlayStartScene();
            EditorApplication.delayCall += UpdatePlayStartScene;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void UpdatePlayStartScene()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorSceneManager.playModeStartScene = NeedsSystemsScene()
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(GilProjectSetup.SystemsScenePath)
                : null;
        }

        /// <summary>플레이를 누를 때 열려 있던 지역을 GameRoot에 넘긴다(멈추면 지운다).</summary>
        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                UpdatePlayStartScene();
                var area = Application.isBatchMode ? null : OpenAreaId();
                if (area != null) SessionState.SetString(GameRoot.EditorStartAreaKey, area);
                else SessionState.EraseString(GameRoot.EditorStartAreaKey);
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseString(GameRoot.EditorStartAreaKey);
            }
        }

        /// <summary>
        /// 시스템 씬만 띄워 시작해야 하면 true: 활성 씬이 지역·플레이어·UI 씬이거나, 지역 씬이 하나라도 열려 있을 때.
        /// (시스템 씬과 지역 씬을 같이 열어 둔 채 그대로 플레이하면 AreaManager가 그 지역을 한 번 더 불러와 맵·NPC·훈련 봇이 두 겹이 된다.)
        /// </summary>
        static bool NeedsSystemsScene()
        {
            var path = SceneManager.GetActiveScene().path;
            if (path == GilProjectSetup.PlayerScenePath || path == GilProjectSetup.UiScenePath) return true;
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (AreaId(SceneManager.GetSceneAt(i)) != null) return true;
            return false;
        }

        /// <summary>시스템 씬부터 띄울 때 시작할 지역: 활성 씬이 지역 씬이면 그 지역, 아니면 열려 있는 첫 지역 씬, 없으면 null.</summary>
        static string OpenAreaId()
        {
            if (!NeedsSystemsScene()) return null;
            var active = AreaId(SceneManager.GetActiveScene());
            if (active != null) return active;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var id = AreaId(SceneManager.GetSceneAt(i));
                if (id != null) return id;
            }
            return null;
        }

        /// <summary>지역 씬이면 그 id(Area_market → market), 아니면 null.</summary>
        static string AreaId(Scene scene)
        {
            if (!scene.path.StartsWith(GilProjectSetup.AreaSceneDir + "/") || !scene.name.StartsWith(AreaPrefix)) return null;
            return scene.name.Substring(AreaPrefix.Length);
        }

        [MenuItem("Gil/지역 씬 열기/장터", priority = 3)]
        static void OpenMarket() => OpenArea("market");

        [MenuItem("Gil/지역 씬 열기/진료소", priority = 4)]
        static void OpenClinic() => OpenArea("clinic");

        [MenuItem("Gil/지역 씬 열기/골목", priority = 5)]
        static void OpenAlley() => OpenArea("alley");

        static void OpenArea(string id)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene($"{GilProjectSetup.AreaSceneDir}/Area_{id}.unity");
        }
    }
}
