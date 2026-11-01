using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gil.Core.Items;
using Gil.Core.Quests;
using Gil.Runtime.Combat;
using Gil.Runtime.Enemies;
using Gil.Runtime.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>전투·탐색 중 상시 표시 UI. 세션 상태를 읽어 그리기만 하고 규칙은 갖지 않는다.</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField, Tooltip("코드가 칠하는 색(Assets/Gil/Settings/UiTheme)")] UiTheme _theme;
        [SerializeField] RectTransform _root;
        [SerializeField] Bar _hp;
        [SerializeField] TMP_Text _hpText;
        [SerializeField, Tooltip("체력바 아래 스태미나 바")] Bar _stamina;
        [SerializeField] Image _staminaFill;
        [SerializeField, Tooltip("스태미나 바 색")] Color _staminaColor = new Color(0.45f, 0.95f, 0.65f);
        [SerializeField, Tooltip("스태미나가 모자라 구르지 못했을 때 깜빡이는 색")] Color _staminaLowColor = new Color(1f, 0.35f, 0.35f);
        [SerializeField, Tooltip("스태미나 부족 깜빡임 시간(초)")] float _staminaLowFlash = 0.5f;
        [SerializeField] Bar _heart;
        [SerializeField] TMP_Text _heartText;
        [SerializeField] Image _heartFill;
        [SerializeField] TMP_Text _coins;
        [SerializeField] TMP_Text _potions;
        [SerializeField] TMP_Text _quest;
        [SerializeField] TMP_Text _waves;
        [SerializeField] TMP_Text _prompt;
        [SerializeField] TMP_Text _areaTitle;
        [SerializeField] TMP_Text _areaSubtitle;
        [SerializeField] TMP_Text _banner;
        [SerializeField] TMP_Text _controls;
        [SerializeField] RectTransform _notices;
        [SerializeField] RectTransform _enemyBars;
        [SerializeField, Tooltip("알림 한 줄의 프리팹. Prefabs/UI/NoticeLabel")] TMP_Text _noticePrefab;
        [SerializeField, Tooltip("적 머리 위 체력바 프리팹. Prefabs/UI/EnemyHpBar")] Bar _enemyBarPrefab;

        float _areaTitleUntil;
        float _bannerUntil;
        float _heartNotReadyUntil;
        float _staminaLowUntil;
        string _lastQuestText;
        readonly List<(TMP_Text text, float until)> _noticeItems = new List<(TMP_Text, float)>();
        readonly Dictionary<EnemyController, Bar> _bars = new Dictionary<EnemyController, Bar>();

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        public void ShowAreaTitle(string title, string subtitle)
        {
            _areaTitle.text = title;
            _areaSubtitle.text = subtitle;
            _areaTitleUntil = Time.unscaledTime + 2.6f;
        }

        public void ShowBanner(string text, float seconds)
        {
            _banner.text = text;
            _bannerUntil = Time.unscaledTime + seconds;
        }

        public void Notice(string text)
        {
            var label = Instantiate(_noticePrefab, _notices);
            label.text = text;
            _noticeItems.Add((label, Time.unscaledTime + 3.5f));
            if (_noticeItems.Count > 6)
            {
                Destroy(_noticeItems[0].text.gameObject);
                _noticeItems.RemoveAt(0);
            }
        }

        public void FlashHeartNotReady() => _heartNotReadyUntil = Time.unscaledTime + 0.5f;

        /// <summary>스태미나가 모자라 구르기를 못 했을 때 바를 잠깐 깜빡인다.</summary>
        public void FlashStaminaLow() => _staminaLowUntil = Time.unscaledTime + _staminaLowFlash;

        void Update()
        {
            var session = Game.Session;
            if (session == null || _root == null) return;
            var now = Time.unscaledTime;

            _hp.Set(session.PlayerHealth.Current / (float)session.PlayerHealth.Max);
            _hpText.text = $"{session.PlayerHealth.Current} / {session.PlayerHealth.Max}";
            _stamina.Set(session.Stamina.Normalized);
            var blink = now < _staminaLowUntil && Mathf.Repeat(now * 10f, 1f) < 0.5f;
            _staminaFill.color = blink ? _staminaLowColor : session.Stamina.Exhausted ? _staminaColor * 0.6f : _staminaColor;

            var heart = session.Heart;
            _heart.Set(heart.Normalized);
            if (heart.CanOverdrive)
            {
                var pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(now * 6f));
                _heartFill.color = Color.Lerp(_theme.Heart, Color.white, pulse * 0.5f);
                _heartText.text = "과부하 준비! [A]";
                _heartText.color = _theme.AccentWarm;
            }
            else
            {
                _heartFill.color = now < _heartNotReadyUntil ? Color.white : _theme.Heart * 0.9f;
                _heartText.text = $"{Mathf.FloorToInt(heart.Value)}%";
                _heartText.color = now < _heartNotReadyUntil ? Color.white : _theme.DimText;
            }

            _coins.text = $"◆ {Money.Format(session.Wallet.Coins)}";
            _potions.text = $"회복약 {session.Inventory.Count("potion")} · 고급 {session.Inventory.Count("hi_potion")}  [S]";

            UpdateQuest(session);
            UpdateWaves();
            UpdatePrompt();
            UpdateTimedTexts(now);
            UpdateEnemyBars();
        }

        void UpdateQuest(Core.GameSession session)
        {
            var sb = new StringBuilder();
            foreach (var q in session.Quests.Tracked)
            {
                sb.Append("<b><color=#ffd27a>").Append(q.Def.Title).Append("</color></b>\n");
                if (q.State == QuestState.ReadyToTurnIn)
                {
                    var npc = session.Db.Npcs[q.Def.TurnInNpc].Name;
                    sb.Append("<color=#7dffb0>▶ ").Append(npc).Append("에게 보고하기</color>\n");
                }
                for (var i = 0; i < q.Def.Objectives.Count; i++)
                {
                    var o = q.Def.Objectives[i];
                    var done = q.Counts[i] >= o.Count;
                    sb.Append(done ? "<color=#7dffb0>✓ " : "· ").Append(o.Label);
                    if (o.Count > 1) sb.Append($" {q.Counts[i]}/{o.Count}");
                    sb.Append(done ? "</color>\n" : "\n");
                }
                sb.Append('\n');
            }
            var text = sb.Length == 0 ? "<color=#9aa0a8>진행 중인 일이 없다</color>" : sb.ToString().TrimEnd();
            if (text == _lastQuestText) return;
            _lastQuestText = text;
            _quest.text = text;
        }

        void UpdateWaves()
        {
            var waves = Game.Areas != null ? Game.Areas.Waves : null;
            _waves.text = waves != null && waves.InProgress && waves.WaveNumber > 0
                ? $"웨이브 {waves.WaveNumber}/{waves.WaveCount} · 남은 적 {waves.AliveCount}"
                : "";
        }

        void UpdatePrompt()
        {
            var player = Combatants.Player;
            var target = player != null && !Game.GameplayLocked ? Interactable.Nearest(player.transform.position) : null;
            if (target == null)
            {
                _prompt.text = "";
                return;
            }
            var locked = target is Portal portal ? portal.LockReason : null;
            _prompt.text = string.IsNullOrEmpty(locked)
                ? $"<color=#4feaff>[Space]</color> {target.Prompt}"
                : $"<color=#888888>[Space] {target.Prompt}</color>";
        }

        void UpdateTimedTexts(float now)
        {
            var titleAlpha = Mathf.Clamp01((_areaTitleUntil - now) / 0.6f);
            _areaTitle.color = new Color(1, 1, 1, titleAlpha);
            _areaSubtitle.color = new Color(_theme.DimText.r, _theme.DimText.g, _theme.DimText.b, titleAlpha);

            var bannerAlpha = Mathf.Clamp01((_bannerUntil - now) / 0.4f);
            var c = _theme.AccentWarm;
            _banner.color = new Color(c.r, c.g, c.b, bannerAlpha);

            for (var i = _noticeItems.Count - 1; i >= 0; i--)
            {
                var (text, until) = _noticeItems[i];
                if (now > until)
                {
                    Destroy(text.gameObject);
                    _noticeItems.RemoveAt(i);
                }
            }
            for (var i = 0; i < _noticeItems.Count; i++)
            {
                var (text, until) = _noticeItems[i];
                text.rectTransform.At(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, (_noticeItems.Count - 1 - i) * 36), new Vector2(700, 34));
                text.color = new Color(1, 1, 1, Mathf.Clamp01((until - now) / 0.5f));
            }
        }

        void UpdateEnemyBars()
        {
            var camera = Game.Camera;
            var alive = Combatants.Targets.OfType<EnemyController>().ToList();

            foreach (var gone in _bars.Keys.Where(e => e == null || !alive.Contains(e)).ToList())
            {
                Destroy(_bars[gone].Root.gameObject);
                _bars.Remove(gone);
            }
            if (camera == null) return;

            foreach (var enemy in alive)
            {
                if (!_bars.TryGetValue(enemy, out var bar))
                {
                    bar = Instantiate(_enemyBarPrefab, _enemyBars);
                    bar.Root.sizeDelta = enemy.IsHeavy ? new Vector2(140, 12) : new Vector2(84, 10);
                    _bars[enemy] = bar;
                }
                var screen = camera.GroundToScreenPoint(enemy.Position + Vector3.up * enemy.BarHeight);
                bar.Root.position = screen;
                bar.Set(enemy.Health.Current / (float)enemy.Health.Max);
            }
        }
    }
}
