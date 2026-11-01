using System.Collections;
using System.IO;
using System.Linq;
using Gil.Core.Quests;
using Gil.Runtime;
using Gil.Runtime.Combat;
using Gil.Runtime.Enemies;
using Gil.Runtime.Visual;
using Gil.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Gil.Tests
{
    /// <summary>
    /// 데모 한 바퀴를 입력 장치 없이 돌려 보는 스모크 테스트:
    /// (타이틀 씬 → 「새로 시작」 → 시스템 씬) / 시스템 씬 바로 열기 → 새 게임 → 마고 대화 → 의체 시술 → 장터 → 문탁에게 일 받기 → 뒷골목 파쿠르 → 경비 로봇 웨이브 전멸 → 보고.
    /// 로그 오류가 하나라도 나면 테스트 프레임워크가 실패 처리한다. 그래픽이 있으면 지역마다 스크린샷을 남긴다.
    /// </summary>
    public sealed class DemoSmokeTests
    {
        // Temp는 -runTests 실행이 끝나며 에디터가 비우므로 남지 않는다. 지워지지 않는 Logs 아래에 둔다.
        static readonly string ScreenDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "GilScreens");
        GameRoot _root;

        [SetUp]
        public void SetUp()
        {
            var save = Path.Combine(Application.persistentDataPath, "gil_save.json");
            if (File.Exists(save)) File.Delete(save);
        }

        /// <summary>시스템 씬(→ 플레이어·UI 씬)이 열린 뒤 새 게임이 시작돼 첫 대화가 열릴 때까지 기다린다.</summary>
        IEnumerator WaitForNewGame()
        {
            yield return WaitUntil(() => Object.FindAnyObjectByType<GameRoot>() != null, 10f, "Systems 씬에 GameRoot가 배치돼 있어야 한다");
            _root = Object.FindAnyObjectByType<GameRoot>();
            yield return WaitUntil(() => Game.Session != null && Game.Ui != null && Game.Ui.Dialogue.IsOpen, 10f, "새 게임 시작 대화가 열리지 않았다");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 빈 씬으로 바꿔 시스템·플레이어·UI·지역 씬을 모두 내린다.
            SceneManager.CreateScene("Empty");
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("Empty"));
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name != "Empty") yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = 1f;
            yield return null;
        }

        /// <summary>실제 게임 시작 경로: 타이틀 씬 → 「새로 시작」 → 시스템 씬에서 새 게임.</summary>
        [UnityTest]
        public IEnumerator TitleStartsNewGame()
        {
            SceneManager.LoadScene("Title", LoadSceneMode.Single);
            yield return WaitUntil(() => Object.FindAnyObjectByType<TitleRoot>() != null, 10f, "Title 씬에 TitleRoot가 배치돼 있어야 한다");
            var title = Object.FindAnyObjectByType<Gil.Runtime.UI.TitleView>();
            yield return WaitUntil(() => title.IsOpen, 5f, "타이틀 메뉴가 열리지 않았다");
            Assert.IsNull(Object.FindAnyObjectByType<GameRoot>(), "타이틀 씬에는 게임(플레이어·지역)이 없어야 한다");

            Object.FindAnyObjectByType<TitleRoot>().NewGame();
            yield return WaitForNewGame();
            Assert.AreEqual("clinic", Game.Areas.CurrentAreaId);
        }

        [UnityTest]
        public IEnumerator FullDemoLoop()
        {
            // 에디터에서 시스템 씬을 바로 플레이할 때와 같은 경로(타이틀 없이 새 게임).
            SceneManager.LoadScene("Systems", LoadSceneMode.Single);
            yield return WaitForNewGame();
            Assert.AreEqual("clinic", Game.Areas.CurrentAreaId);
            CheckIsoGrid();
            yield return Capture("01_clinic_dialogue");

            yield return FinishDialogue();
            var session = Game.Session;
            Assert.That(session.Flags.Contains("met_mago"), "마고 대화 후 met_mago 플래그");
            Assert.AreEqual(1000, session.Wallet.Coins, "마고가 준 10냥(1000푼)");

            // 리퍼닥 시술: 대화 첫 선택지 → 시술 창
            Game.Ui.Dialogue.Play("mago", null);
            yield return FinishDialogue(0, () => Game.Ui.Cyberware.IsOpen);
            Assert.That(Game.Ui.Cyberware.IsOpen, "의체 시술 창이 열려야 한다");
            yield return Capture("02_ripperdoc");
            Game.Ui.Cyberware.TestInstall("swallow_legs");
            Assert.That(session.Cyberware.IsInstalled("swallow_legs"), Game.Ui.Cyberware.ResultText);
            Assert.NotNull(session.Cyberware.Gimmick("double_rolling"), "제비 다리의 연속 구르기 기믹");
            Game.Ui.Cyberware.TestClose();
            yield return null;

            // 장터
            yield return Travel("market", "from_clinic");
            yield return Capture("03_market");
            var muntak = Object.FindObjectsByType<NpcController>(FindObjectsSortMode.None).First(n => n.NpcId == "muntak");
            Assert.NotNull(muntak.Def, "문탁의 데이터가 연결돼야 한다");
            Game.Ui.Dialogue.Play("muntak", null);
            yield return FinishDialogue();
            Assert.AreNotEqual(QuestState.NotStarted, session.Quests.StateOf("first_hunt"), "문탁에게 첫 사냥을 받아야 한다");
            Assert.NotNull(muntak);

            // 뒷골목
            yield return Travel("alley", "from_market");
            var player = _root.PlayerForTest;
            yield return Capture("04_alley");

            // 파쿠르: 컨테이너(높이 2.4) 앞에서 대시 → 오르기
            yield return TeleportAndRoll(player, new Vector3(6.4f, 0, -4.6f), new Vector3(0, 0, 1));
            Assert.Greater(player.transform.position.y, 2.0f, $"컨테이너 위로 올라가야 한다 (상태 {player.ModeName})");
            yield return Capture("05_parkour_climb");

            // 차단벽(높이 0.9) 넘기
            yield return TeleportAndRoll(player, new Vector3(-4f, 0, -2.6f), new Vector3(0, 0, 1));
            Assert.Greater(player.transform.position.z, -0.6f, $"차단벽을 넘어가야 한다 (상태 {player.ModeName})");

            // 웨이브: 나오는 로봇을 전부 쓰러뜨린다(판정은 실제 ReceiveHit 경로)
            var waves = Game.Areas.Waves;
            Assert.NotNull(waves);
            var timeout = Time.time + 60f;
            var captured = false;
            while (waves.InProgress && Time.time < timeout)
            {
                var enemies = Combatants.Targets.OfType<EnemyController>().Where(e => e.IsAlive).ToList();
                if (enemies.Count > 0 && !captured)
                {
                    yield return new WaitForSeconds(1.2f);
                    yield return Capture("06_alley_combat");
                    captured = true;
                }
                foreach (var e in enemies)
                    e.ReceiveHit(new HitInfo { Amount = 9999, Source = player.transform.position, Knockback = 3f });
                yield return new WaitForSeconds(0.3f);
            }
            Assert.IsFalse(waves.InProgress, "웨이브를 60초 안에 정리해야 한다");
            Assert.AreEqual(QuestState.ReadyToTurnIn, session.Quests.StateOf("first_hunt"));
            Assert.That(session.EnemiesDefeated >= 6);

            yield return Travel("market", "from_alley");
            Game.Ui.Dialogue.Play("muntak", null);
            yield return FinishDialogue();
            Assert.AreEqual(QuestState.Completed, session.Quests.StateOf("first_hunt"));
            yield return null;
            Assert.That(Game.Ui.DemoEnd.IsOpen, "보고 후 데모 종료 화면");
            yield return Capture("07_demo_end");
        }

        /// <summary>타일맵(Isometric Grid)과 시뮬레이션 투영이 같은 좌표를 쓰는지.</summary>
        static void CheckIsoGrid()
        {
            var grid = GameObject.Find("Floor").GetComponent<Grid>();
            var cell = new Vector3Int(3, -2, 0);
            var world = grid.GetCellCenterWorld(cell) - grid.transform.position;
            var projected = Iso.ToScreen(cell.x + 0.5f, 0, cell.y + 0.5f);
            Assert.That(Vector2.Distance(world, projected), Is.LessThan(0.001f), $"격자 {world} ≠ 투영 {projected}");
        }

        static IEnumerator TeleportAndRoll(Gil.Runtime.Player.PlayerController player, Vector3 at, Vector3 direction)
        {
            player.Teleport(at, direction);
            yield return new WaitForSeconds(0.3f);
            player.TestRolling(direction);
            yield return new WaitForSeconds(1.0f);
        }

        static IEnumerator FinishDialogue(int choice = 0, System.Func<bool> stopWhen = null)
        {
            var guard = 0;
            while (Game.Ui.Dialogue.IsOpen && guard++ < 200)
            {
                Game.Ui.Dialogue.TestAdvance(choice);
                yield return null;
                if (stopWhen != null && stopWhen()) yield break;
            }
            yield return null;
            Assert.IsFalse(Game.Ui.Dialogue.IsOpen, "대화가 끝나야 한다");
        }

        static IEnumerator Travel(string area, string spawn)
        {
            Game.Areas.Travel(area, spawn);
            yield return null;
            yield return WaitUntil(() => !Game.Areas.IsTransitioning, 5f, $"{area}(으)로 이동하지 못했다");
            Assert.AreEqual(area, Game.Areas.CurrentAreaId);
            yield return new WaitForSeconds(0.5f);
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float seconds, string message)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail(message);
                yield return null;
            }
        }

        /// <summary>그래픽 장치가 있을 때만(‑nographics가 아닐 때) 화면을 PNG로 남긴다.</summary>
        static IEnumerator Capture(string name)
        {
            // batchmode에서는 화면 출력이 없어 WaitForEndOfFrame이 돌아오지 않는다. 카메라를 직접 렌더하므로 한 프레임만 넘긴다.
            if (Application.isBatchMode) yield return null;
            else yield return new WaitForEndOfFrame();
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log($"[GilScreens] 그래픽 장치 없음, {name} 생략");
                yield break;
            }
            var camera = Game.Camera.Camera;
            var rt = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previous;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(ScreenDir);
            var file = Path.Combine(ScreenDir, name + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Debug.Log($"[GilScreens] {file}");
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
