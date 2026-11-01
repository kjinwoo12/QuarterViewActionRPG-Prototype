using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gil.Core.Cyber;
using Gil.Core.Data;
using Gil.Core.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>
    /// 리퍼닥(마고)의 의체 시술 창. 장착 규칙(심장 용량·부하·슬롯 교체)과 결제는 Core의 GameSession.InstallCyberware,
    /// 여기서는 목록·비교·결과 표시만 한다.
    /// </summary>
    public sealed class CyberwareView : ModalView
    {
        [SerializeField] TMP_Text _coins;
        [SerializeField] TMP_Text _loadout;
        [SerializeField] TMP_Text _detail;
        [SerializeField] TMP_Text _result;
        [SerializeField] Image _loadFill;
        [SerializeField] Image _previewFill;
        [SerializeField] TMP_Text _loadText;
        [SerializeField] VerticalMenu _menu;
        readonly List<string> _ids = new List<string>();
        int _lastSelected = -1;

        public void Open()
        {
            var db = Game.Session.Db;
            _ids.Clear();
            _menu.Clear();
            _result.text = "";
            // 슬롯 순서대로, 기본 장착품(시작 심장)은 목록에서 뺀다.
            foreach (var slot in db.CyberSlots)
            foreach (var c in db.CyberwareList.Where(c => c.Slot == slot.Id && !c.Starter))
            {
                var id = c.Id;
                _ids.Add(id);
                _menu.Add("", () => Install(id));
            }
            _menu.Add("나가기  (X / Esc)", Hide);
            Refresh();
            Show();
        }

        internal void TestInstall(string id) => Install(id);
        internal void TestClose() => Hide();
        internal string ResultText => _result.text;

        void Install(string id)
        {
            var session = Game.Session;
            var def = session.Db.Cyberware[id];
            switch (session.InstallCyberware(id))
            {
                case InstallResult.Ok:
                    _result.text = $"<color=#9df5b0>시술 완료 — {def.Name}. \"움직여 봐. 낯설면 그게 정상이야.\"</color>";
                    break;
                case InstallResult.AlreadyInstalled:
                    _result.text = "<color=#9aa0a8>이미 달고 있다.</color>";
                    break;
                case InstallResult.NotEnoughCoins:
                    _result.text = "<color=#ff8080>엽전이 모자란다. 뒷골목 로봇 부품이면 값이 좀 나올 텐데.</color>";
                    break;
                case InstallResult.OverCapacity:
                    _result.text = "<color=#ff8080>심장이 버티질 못한다. 부하를 줄이거나 심장부터 바꿔야 한다.</color>";
                    break;
                default:
                    _result.text = "<color=#ff8080>지금은 다룰 수 없는 의체다.</color>";
                    break;
            }
            Refresh();
        }

        void Refresh()
        {
            var session = Game.Session;
            _coins.text = $"◆ {Money.Format(session.Wallet.Coins)}";
            for (var i = 0; i < _ids.Count; i++)
            {
                var c = session.Db.Cyberware[_ids[i]];
                var slot = SlotName(c.Slot);
                var state = session.Cyberware.IsInstalled(c.Id)
                    ? "<color=#9df5b0>장착 중</color>"
                    : session.Cyberware.Check(c) == InstallResult.OverCapacity
                        ? "<color=#ff8080>부하 초과</color>"
                        : $"<color=#ffd27a>{Money.Format(c.Price)}</color>";
                _menu.SetText(i, $"<color=#9aa0a8>[{slot}]</color> {c.Name}   {state}");
            }

            var sb = new StringBuilder("<b>장착한 의체</b>\n");
            foreach (var slot in session.Db.CyberSlots)
            {
                var installed = session.Cyberware.InSlot(slot.Id);
                sb.Append($"{slot.Name}: {(installed != null ? installed.Name : "<color=#666a70>비어 있음</color>")}\n");
            }
            _loadout.text = sb.ToString();
            UpdateDetail();
        }

        void UpdateDetail()
        {
            var session = Game.Session;
            var loadout = session.Cyberware;
            var selected = _menu.Selected;
            var def = selected < _ids.Count ? session.Db.Cyberware[_ids[selected]] : null;

            var (load, capacity) = def != null ? loadout.Preview(def) : (loadout.Load, loadout.Capacity);
            var maxScale = Mathf.Max(1, Mathf.Max(capacity, loadout.Capacity));
            SetFill(_loadFill, loadout.Load / (float)maxScale);
            SetFill(_previewFill, Mathf.Min(1f, load / (float)maxScale));
            _previewFill.color = load > capacity ? new Color(1f, 0.35f, 0.4f, 0.6f) : new Color(0.45f, 1f, 0.65f, 0.35f);
            _loadText.text = def != null && !loadout.IsInstalled(def.Id)
                ? $"{loadout.Load} / {loadout.Capacity}  →  <color={(load > capacity ? "#ff8080" : "#9df5b0")}>{load} / {capacity}</color>"
                : $"{loadout.Load} / {loadout.Capacity}";

            if (def == null)
            {
                _detail.text = "";
                return;
            }
            var sb = new StringBuilder();
            sb.Append($"<size=30><b>{def.Name}</b></size>\n");
            sb.Append($"<color=#9aa0a8>{SlotName(def.Slot)} 슬롯 · 부하 {def.Load} · {Money.Format(def.Price)}</color>\n\n");
            foreach (var pair in def.Stats)
                sb.Append($"<color=#9df5b0>› {StatKeys.Describe(pair.Key, pair.Value)}</color>\n");
            if (def.Gimmick != null) sb.Append($"<color=#7fe8ff>› 기믹: {GimmickName(def.Gimmick.Type)}</color>\n");
            sb.Append($"\n{def.Description}");
            var current = loadout.InSlot(def.Slot);
            if (current != null && current.Id != def.Id)
                sb.Append($"\n\n<color=#ffb070>교체하면 지금의 「{current.Name}」은(는) 회수된다(환불 없음).</color>");
            _detail.text = sb.ToString();
        }

        static void SetFill(Image image, float ratio)
        {
            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static string SlotName(string slotId)
        {
            var slot = Game.Session.Db.CyberSlots.FirstOrDefault(s => s.Id == slotId);
            return slot != null ? slot.Name : slotId;
        }

        static string GimmickName(string type)
        {
            switch (type)
            {
                case Gimmicks.NeuralSlowmo: return "저스트 회피 시 긴 시간 감속";
                case Gimmicks.ShockFinisher: return "콤보 마무리 충격파";
                case Gimmicks.ChainShock: return "확률 연쇄 전격";
                case Gimmicks.DoubleRolling: return "구르기 연속 사용";
                case Gimmicks.KineticLanding: return "내려찍기에 기절 효과";
                case Gimmicks.SecondWind: return "지역당 1회 부활";
                default: return type;
            }
        }

        void Update()
        {
            if (!InputReady) return;
            var input = Game.Input;
            if (input.PausePressed || input.CancelPressed)
            {
                Hide();
                return;
            }
            _menu.HandleInput(input);
            // 키보드·마우스 어느 쪽으로 골라도 상세가 따라오게 매 프레임 비교한다.
            if (IsOpen && _menu.Selected != _lastSelected)
            {
                _lastSelected = _menu.Selected;
                UpdateDetail();
            }
        }
    }
}
