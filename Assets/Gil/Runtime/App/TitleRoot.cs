using System.Collections;
using Gil.Runtime.Input;
using Gil.Runtime.Systems;
using Gil.Runtime.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gil.Runtime
{
    /// <summary>
    /// 타이틀 씬(Assets/Gil/Scenes/Title.unity)의 진입점. 화면(Prefabs/UI/Screens/Title)·페이드·입력은 씬에 배치돼 있고,
    /// 메뉴를 고르면 시스템 씬(Systems)을 연다(시스템 씬이 플레이어·UI·지역 씬을 이어서 연다). 게임 데이터·플레이어·지역은 타이틀에 없다.
    /// </summary>
    public sealed class TitleRoot : MonoBehaviour
    {
        [SerializeField, Tooltip("게임을 시작할 때 여는 씬 이름(Build Settings에 있어야 한다)")] string _gameScene = "Systems";
        [SerializeField, Tooltip("게임 씬으로 넘어갈 때 검게 덮는 시간(초)")] float _fadeOut = 0.4f;
        [SerializeField] GameInput _input;
        [SerializeField] TitleView _title;
        [SerializeField] FadeView _fade;

        bool _starting;

        void Awake()
        {
            Game.Input = _input;
        }

        void Start()
        {
            _fade.SetInstant(0f);
            _title.Open(SaveSystem.HasSave, NewGame, ContinueGame, Quit);
        }

        void OnDestroy()
        {
            if (Game.Input == _input) Game.Input = null;
        }

        /// <summary>「새로 시작」. PlayMode 테스트도 이 진입점을 쓴다.</summary>
        internal void NewGame() => StartGame(false);

        void ContinueGame() => StartGame(true);

        void StartGame(bool continueSave)
        {
            if (_starting) return;
            _starting = true;
            Game.ContinueRequested = continueSave;
            StartCoroutine(LoadGame());
        }

        IEnumerator LoadGame()
        {
            _title.Close();
            yield return _fade.To(1f, _fadeOut);
            SceneManager.LoadScene(_gameScene, LoadSceneMode.Single);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
