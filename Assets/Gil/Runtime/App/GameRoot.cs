using System.Collections;
using System.Linq;
using Gil.Core.Items;
using Gil.Core;
using Gil.Core.Data;
using Gil.Runtime.Combat;
using Gil.Runtime.Data;
using Gil.Runtime.Input;
using Gil.Runtime.Player;
using Gil.Runtime.Systems;
using Gil.Runtime.UI;
using Gil.Runtime.Visual;
using Gil.Runtime.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gil.Runtime
{
    /// <summary>
    /// 시스템 씬(Assets/Gil/Scenes/Systems.unity)의 진입점. 시스템 씬은 한 판 동안 계속 열려 있는 상주 씬으로,
    /// 지역 관리자·입력·시간 같은 매니저가 배치돼 있고(UI 없음) 데이터 로드·검증 → 한 판(세션) 시작/종료 흐름을 돌린다.
    /// 시작하면 플레이어 씬(Scenes/Player: 플레이어·카메라)과 UI 씬(Scenes/UI: HUD·대화·메뉴 등)을 Additive로 불러오고,
    /// 두 씬이 리그(<see cref="PlayerRig"/>, <see cref="UiRig"/>)를 등록하면 그 참조로 새 게임을 시작한다.
    /// 씬 사이에는 인스펙터 참조를 저장할 수 없어서 이 등록 방식으로 잇고, 같은 씬 안은 인스펙터로 직접 연결한다.
    /// 지역 씬(Scenes/Areas/Area_{id})에는 맵만 있고 <see cref="AreaManager"/>가 그 위에 Additive로 불러온다.
    /// 타이틀은 별도 씬(Scenes/Title)이다. 타이틀에서 이 씬을 열면 바로 새 게임(또는 이어하기)이 시작되고,
    /// 에디터에서 이 씬을 열어 플레이해도 타이틀을 거치지 않고 새 게임으로 시작한다. 지역·플레이어·UI 씬을 열어 둔 채 플레이하면
    /// 에디터(GilSceneEditing)가 시스템 씬부터 띄우고 (지역 씬이었으면) 그 지역에서 새 게임을 시작한다.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [Header("시작")]
        [SerializeField, Tooltip("새 게임과 부활 지점이 있는 지역 id(areas.json)")] string _startArea = "clinic";
        [SerializeField, Tooltip("타이틀로 돌아갈 때 여는 씬 이름(Build Settings에 있어야 한다)")] string _titleScene = "Title";
        [SerializeField, Tooltip("목표 프레임(-1이면 플랫폼 기본)")] int _targetFrameRate = 60;
        [SerializeField, Tooltip("게임 데이터 목록(Assets/Gil/Data/GameData)")] GameData _data;

        [Header("함께 여는 씬")]
        [SerializeField, Tooltip("플레이어·카메라가 있는 씬 이름(Build Settings에 있어야 한다). 루트의 PlayerRig가 등록한다")] string _playerScene = "Player";
        [SerializeField, Tooltip("HUD·대화·메뉴 등 게임 중 UI가 있는 씬 이름(Build Settings에 있어야 한다). 루트의 UiRig가 등록한다")] string _uiScene = "UI";

        [Header("씬 오브젝트")]
        [SerializeField] GameInput _input;
        [SerializeField] AreaManager _areas;
        [SerializeField, Tooltip("이펙트 프리팹 묶음(Assets/Gil/Prefabs/Fx/VfxLibrary)")] VfxLibrary _vfx;

        GameDatabase _db;
        string _dataError;
        // 플레이어 씬이 등록한 플레이어(PlayerRig에서 받는다).
        PlayerController _player;
        float _sessionStartTime;
        bool _demoEnded;
        bool _inSession;

        /// <summary>전투 중(웨이브 진행)에는 저장하지 않는다.</summary>
        public bool CanSave => _inSession && Game.Session != null && (Game.Areas.Waves == null || !Game.Areas.Waves.InProgress)
                               && _player != null && _player.IsAlive;

        internal PlayerController PlayerForTest => _player;

        void Awake()
        {
            Application.targetFrameRate = _targetFrameRate;
            Game.Input = _input;
            Game.Areas = _areas;
            Vfx.Library = _vfx;
            LoadData(out _dataError);
        }

        IEnumerator Start()
        {
            yield return LoadRigScenes();
            if (!BindRigs()) yield break;

            if (_dataError != null)
            {
                Game.Ui.Hud.SetVisible(false);
                Game.Ui.ShowFatal(_dataError);
                enabled = false;
                yield break;
            }

            Game.Areas.AreaLoaded += OnAreaLoaded;
            Game.Ui.Fade.SetInstant(1f);
            SaveData save = null;
            if (Game.ContinueRequested)
            {
                Game.ContinueRequested = false;
                save = SaveSystem.Load();
                if (save == null) Debug.LogWarning("[Gil] 세이브를 읽지 못해 새 게임으로 시작합니다");
            }
            yield return BeginSession(save);
        }

        void OnDestroy()
        {
            if (_player != null) _player.Died -= OnPlayerDied;
            Game.Clear();
            Vfx.Library = null;
            Combatants.Targets.Clear();
        }

        // ── 준비 ──────────────────────────────────────────────

        bool LoadData(out string error)
        {
            error = null;
            try
            {
                if (_data == null) throw new System.InvalidOperationException("GameRoot의 Data(GameData 에셋)가 비어 있습니다");
                _db = _data.Build();
            }
            catch (System.Exception e)
            {
                error = e.Message;
                Debug.LogError($"[GilData] {e}");
                return false;
            }

            var report = DataValidator.Validate(_db);
            foreach (var d in report.Items)
            {
                if (d.Severity == Gil.Core.Util.Severity.Error) Debug.LogError($"[GilData] {d}");
                else Debug.LogWarning($"[GilData] {d}");
            }
            if (report.HasErrors)
            {
                error = string.Join("\n", report.Items.Where(d => d.Severity == Gil.Core.Util.Severity.Error).Select(d => d.ToString()));
                return false;
            }
            return true;
        }

        /// <summary>플레이어·UI 씬을 Additive로 연다. 에디터에서 이미 함께 열어 둔 씬은 다시 열지 않는다.</summary>
        IEnumerator LoadRigScenes()
        {
            var ops = new System.Collections.Generic.List<AsyncOperation>();
            foreach (var name in new[] { _playerScene, _uiScene })
            {
                if (SceneManager.GetSceneByName(name).isLoaded) continue;
                var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                if (op == null) Debug.LogError($"[Gil] {name} 씬을 불러올 수 없습니다. Build Settings에 들어 있는지 확인하세요(Gil ▸ 프로젝트 설정).");
                else ops.Add(op);
            }
            foreach (var op in ops) yield return op;
            // 리그는 씬이 켜질 때(OnEnable) 등록하므로 로드가 끝나면 이미 들어와 있다. 한 프레임만 더 여유를 둔다.
            if (Game.PlayerRig == null || Game.UiRig == null) yield return null;
        }

        /// <summary>등록된 리그에서 플레이어·카메라·UI를 받아 전역 접근점과 지역 관리자에 넘긴다.</summary>
        bool BindRigs()
        {
            if (Game.PlayerRig == null || Game.UiRig == null)
            {
                Debug.LogError($"[Gil] 리그가 등록되지 않았습니다(PlayerRig: {(Game.PlayerRig != null ? "있음" : "없음")}, UiRig: {(Game.UiRig != null ? "있음" : "없음")}). " +
                               $"{_playerScene}·{_uiScene} 씬 루트에 PlayerRig·UiRig가 있는지 확인하세요.");
                enabled = false;
                return false;
            }
            _player = Game.PlayerRig.Player;
            Game.Camera = Game.PlayerRig.Camera;
            Game.Ui = Game.UiRig.Ui;
            _areas.Bind(_player, Game.Camera, Game.Ui);
            _player.Died += OnPlayerDied;
            return true;
        }

        // ── 흐름 ──────────────────────────────────────────────

        IEnumerator BeginSession(SaveData save)
        {
            yield return Game.Ui.Fade.To(1f, 0.4f);
            yield return EndSession();

            var session = new GameSession(_db);
            if (save != null) session.Restore(save);
            Game.Session = session;
            BindSession(session);
            PreparePlayer();
            _sessionStartTime = Time.unscaledTime;
            _demoEnded = false;

            var area = save != null && _db.Areas.ContainsKey(save.Area) ? save.Area : NewGameArea();
            Game.Ui.Fade.SetInstant(1f);
            yield return Game.Areas.LoadImmediate(area, "start");
            _inSession = true;
            OnAreaLoaded(area);
            Game.Ui.Hud.SetVisible(true);
            Game.Ui.Fade.SetInstant(1f);
            yield return Game.Ui.Fade.To(0f, 0.6f);

            // 처음 시작하면 마고의 정비소에서 깨어나는 장면부터(에디터에서 다른 지역부터 시작했으면 건너뛴다).
            if (save == null && area == _startArea) Game.Ui.Dialogue.Play("mago", null);
        }

        /// <summary>에디터에서 지역 씬을 열어 둔 채 플레이했으면 그 지역, 아니면 시작 지역.</summary>
        string NewGameArea()
        {
#if UNITY_EDITOR
            var area = UnityEditor.SessionState.GetString(EditorStartAreaKey, "");
            if (!string.IsNullOrEmpty(area) && _db.Areas.ContainsKey(area)) return area;
#endif
            return _startArea;
        }

        /// <summary>에디터 전용: 플레이를 누를 때 열려 있던 지역 id(SessionState). GilSceneEditing이 쓴다.</summary>
        public const string EditorStartAreaKey = "Gil.EditorStartArea";

        IEnumerator EndSession()
        {
            _inSession = false;
            TimeController.ResetAll();
            yield return Game.Areas.Unload();
            Combatants.Targets.Clear();
        }

        /// <summary>씬에 있는 플레이어를 새 세션 상태로 되돌린다(파괴·재생성하지 않는다).</summary>
        void PreparePlayer()
        {
            _player.Init(_db.Player);
            _player.ApplyStats();
            Game.Session.StatsChanged += _player.ApplyStats;
        }

        void BindSession(GameSession session)
        {
            var ui = Game.Ui;
            session.Notice += ui.Hud.Notice;
            session.ShopRequested += id => ui.Dialogue.AfterClose(() => ui.Shop.Open(id));
            session.CyberwareRequested += () => ui.Dialogue.AfterClose(ui.Cyberware.Open);
            session.TravelRequested += area => ui.Dialogue.AfterClose(() => Game.Areas.Travel(area, "start"));
            session.DemoEndRequested += () => ui.Dialogue.AfterClose(ShowDemoEnd);
        }

        void OnAreaLoaded(string areaId)
        {
            // 지역에 들어설 때마다 자동 저장.
            if (!CanSave) return;
            SaveSystem.Save(Game.Session.Capture());
        }

        void OnPlayerDied()
        {
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            TimeController.SlowMotion(0.3f, 1.2f);
            yield return new WaitForSecondsRealtime(1.4f);
            Game.Ui.Death.Show(Respawn);
        }

        void Respawn()
        {
            var session = Game.Session;
            session.PlayerHealth.Revive();
            _player.Revive();
            Game.Areas.Travel(_startArea, "respawn");
        }

        void ShowDemoEnd()
        {
            if (_demoEnded) return;
            _demoEnded = true;
            var session = Game.Session;
            var seconds = Mathf.RoundToInt(Time.unscaledTime - _sessionStartTime);
            var stats =
                $"플레이 시간  {seconds / 60}분 {seconds % 60:00}초\n" +
                $"쓰러뜨린 적  {session.EnemiesDefeated}\n" +
                $"남은 돈  {Money.Format(session.Wallet.Coins)}\n\n" +
                "<color=#ffd27a>데모는 여기까지입니다. 플레이해 주셔서 감사합니다.</color>";
            SaveSystem.Save(session.Capture());
            Game.Ui.DemoEnd.Show(stats, () => { }, ReturnToTitle);
        }

        // ── 메뉴 ──────────────────────────────────────────────

        void Update()
        {
            var ui = Game.Ui;
            if (ui == null || Game.Session == null) return;
            if (Game.Input.PausePressed && !ui.BlocksGameplay && !Game.Areas.IsTransitioning && _player != null && _player.IsAlive)
                ui.Pause.Open(SaveNow, CanSave, LoadSave, SaveSystem.HasSave, ReturnToTitle);
        }

        void SaveNow()
        {
            if (CanSave && SaveSystem.Save(Game.Session.Capture())) Game.Ui.Hud.Notice("저장했습니다");
        }

        void LoadSave()
        {
            var save = SaveSystem.Load();
            if (save == null) return;
            StartCoroutine(BeginSession(save));
        }

        void ReturnToTitle()
        {
            StartCoroutine(ReturnToTitleRoutine());
        }

        IEnumerator ReturnToTitleRoutine()
        {
            yield return Game.Ui.Fade.To(1f, 0.4f);
            yield return EndSession();
            SceneManager.LoadScene(_titleScene, LoadSceneMode.Single);
        }
    }
}
