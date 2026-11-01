using System;
using System.Collections;
using Gil.Runtime.CameraRig;
using Gil.Runtime.Player;
using Gil.Runtime.UI;
using Gil.Runtime.Visual;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gil.Runtime.World
{
    /// <summary>
    /// 지역 전환(페이드 → 이전 지역 씬 내리기 → 새 지역 씬 Additive 로드 → 플레이어 배치 → 페이드 인)을 담당한다.
    /// 지역은 Assets/Gil/Scenes/Areas/Area_{id}.unity에 미리 배치돼 있고, 여기서는 불러오고 내리기만 한다.
    /// 플레이어·카메라·UI는 다른 씬(Player, UI)에 있어 인스펙터로 연결할 수 없으므로, GameRoot가 리그를 받은 뒤 <see cref="Bind"/>로 넘겨준다.
    /// </summary>
    public sealed class AreaManager : MonoBehaviour
    {
        PlayerController _player;
        QuarterViewCamera _camera;
        UiRoot _ui;

        Scene _scene;
        AreaScene _current;

        public bool IsTransitioning { get; private set; }
        public string CurrentAreaId { get; private set; }
        public WaveDirector Waves { get; private set; }

        public event Action<string> AreaLoaded;

        /// <summary>다른 씬에 있는 플레이어·카메라·UI를 받는다. 지역을 불러오기 전에 GameRoot가 한 번 부른다.</summary>
        public void Bind(PlayerController player, QuarterViewCamera camera, UiRoot ui)
        {
            _player = player;
            _camera = camera;
            _ui = ui;
        }

        public void Travel(string areaId, string spawn)
        {
            if (IsTransitioning) return;
            StartCoroutine(TravelRoutine(areaId, spawn));
        }

        /// <summary>페이드 없이 바로 배치(게임 시작/불러오기). 씬 로드가 끝날 때까지 기다린다.</summary>
        public IEnumerator LoadImmediate(string areaId, string spawn)
        {
            IsTransitioning = true;
            yield return Load(areaId, spawn);
            IsTransitioning = false;
        }

        /// <summary>현재 지역 씬을 내린다(타이틀로 돌아갈 때).</summary>
        public IEnumerator Unload()
        {
            StopAllCoroutines();
            IsTransitioning = false;
            Waves = null;
            CurrentAreaId = null;
            _current = null;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                var scene = _scene;
                _scene = default;
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        IEnumerator TravelRoutine(string areaId, string spawn)
        {
            IsTransitioning = true;
            yield return _ui.Fade.To(1f, 0.35f);
            yield return Load(areaId, spawn);
            yield return new WaitForSecondsRealtime(0.1f);
            yield return _ui.Fade.To(0f, 0.35f);
            IsTransitioning = false;
        }

        IEnumerator Load(string areaId, string spawn)
        {
            Waves = null;
            _current = null;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                var old = _scene;
                _scene = default;
                yield return SceneManager.UnloadSceneAsync(old);
            }
            var sceneName = AreaScene.SceneName(areaId);
            // 이미 열려 있는 지역 씬은 다시 불러오지 않는다(두 번 불러오면 맵·NPC가 두 겹이 된다).
            if (!SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    Debug.LogError($"[Gil] 지역 씬 {sceneName}을(를) 불러올 수 없습니다. Build Settings에 들어 있는지 확인하세요(Gil ▸ 프로젝트 설정).");
                    yield break;
                }
                yield return op;
            }
            _scene = SceneManager.GetSceneByName(sceneName);
            // 지역 중에 새로 만드는 오브젝트(이펙트, 투사체)가 지역 씬에 들어가 함께 정리되게 한다.
            SceneManager.SetActiveScene(_scene);
            foreach (var go in _scene.GetRootGameObjects())
                if (go.TryGetComponent(out _current)) break;
            if (_current == null)
            {
                Debug.LogError($"[Gil] {sceneName}에 AreaScene 루트가 없습니다");
                yield break;
            }
            CurrentAreaId = areaId;

            // 2D 조명(환경광·점광원)은 모두 지역 씬 안에 있어 씬과 함께 켜지고 꺼진다.
            _camera.SetBackground(_current.Background);
            _camera.SetBounds(_current.Walkable);

            var point = _current.Spawn(spawn);
            if (point != null) _player.Teleport(point.position, point.forward);
            _camera.SetTarget(_player.transform, snap: true);

            var def = Game.Session.Db.Areas[areaId];
            if (_current.Waves != null)
            {
                Waves = _current.Waves;
                Waves.Cleared += () =>
                {
                    _ui.Hud.ShowBanner("골목이 조용해졌다", 2f);
                    _ui.Hud.Notice("장터로 돌아가 문탁에게 보고하자");
                };
                Waves.Begin(def);
            }

            Game.Session.EnterArea(areaId);
            _ui.Hud.ShowAreaTitle(def.Name, def.Subtitle);
            AreaLoaded?.Invoke(areaId);
        }
    }
}
