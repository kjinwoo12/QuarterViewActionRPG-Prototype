using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>RectTransform 배치 헬퍼. 실행 중에 바꾸는 UI 색은 <see cref="UiTheme"/> 에셋에 있다.</summary>
    public static class UiFactory
    {
        /// <summary>앵커(0~1)와 픽셀 오프셋으로 배치.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>앵커 한 점 기준으로 크기와 위치 지정.</summary>
        public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt) => rt.Place(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    }

    /// <summary>
    /// 세로 메뉴(대화 선택지, 상점, 일시정지, 타이틀 공용). 마우스 클릭과 키보드/패드 이동을 모두 받는다.
    /// </summary>
    [Serializable]
    public sealed class VerticalMenu
    {
        [SerializeField, Tooltip("선택 표시 등 코드가 칠하는 색(Assets/Gil/Settings/UiTheme)")] UiTheme _theme;
        [SerializeField, Tooltip("항목이 쌓이는 영역")] RectTransform _container;
        [SerializeField, Tooltip("항목 하나의 프리팹(Button + 자식 TextMeshPro). Prefabs/UI/MenuItem")] Button _itemPrefab;
        [SerializeField, Tooltip("씬에 미리 배치해 둔 항목(위에서부터). 항목 수가 정해진 메뉴(타이틀)는 여기 두고, 모자라면 프리팹을 복제한다")]
        List<Button> _placed = new List<Button>();
        // 열 때마다 바뀌는 항목 목록(동작·활성 여부)은 실행 중 상태라 저장하지 않는다(직렬화 복원 시 null일 수 있어 지연 생성).
        [NonSerialized] List<(Button button, Image background, TMP_Text label, Action action, bool enabled)> _itemList;
        [NonSerialized] int _selected;
        [NonSerialized] int _lastActivatedFrame = -1;

        List<(Button button, Image background, TMP_Text label, Action action, bool enabled)> _items =>
            _itemList ??= new List<(Button, Image, TMP_Text, Action, bool)>();

        public int Count => _items.Count;
        public int Selected => _selected;
        [Tooltip("항목 높이(px)")] public float ItemHeight = 58f;
        [Tooltip("항목 사이 간격(px)")] public float Spacing = 8f;
        [Tooltip("항목 글자 크기")] public int FontSize = 26;


        public void Clear()
        {
            foreach (var item in _items)
            {
                if (_placed.Contains(item.button))
                {
                    item.button.onClick.RemoveAllListeners();
                    item.button.gameObject.SetActive(false);
                }
                else
                {
                    UnityEngine.Object.Destroy(item.button.gameObject);
                }
            }
            _items.Clear();
            _selected = 0;
        }

        public void Add(string text, Action action, bool enabled = true)
        {
            var index = _items.Count;
            Button button;
            if (index < _placed.Count && _placed[index] != null)
            {
                button = _placed[index];
                button.gameObject.SetActive(true);
            }
            else
            {
                button = UnityEngine.Object.Instantiate(_itemPrefab, _container);
                button.name = $"Item{index}";
            }
            button.onClick.AddListener(() => Activate(index));
            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -index * (ItemHeight + Spacing));
            rt.sizeDelta = new Vector2(0, ItemHeight);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = text;
            label.fontSize = FontSize;
            if (!enabled) label.color = _theme.DimText * 0.8f;
            _items.Add((button, button.GetComponent<Image>(), label, action, enabled));
            Refresh();
        }

        public float TotalHeight => _items.Count * (ItemHeight + Spacing);

        public void SetText(int index, string text) => _items[index].label.text = text;

        /// <summary>키보드/패드 입력 처리. 메뉴가 열린 View의 Update에서 부른다.</summary>
        public void HandleInput(Input.GameInput input)
        {
            if (_items.Count == 0) return;
            var nav = input.NavigateVerticalPressed;
            if (nav != 0)
            {
                _selected = (_selected + nav + _items.Count) % _items.Count;
                Refresh();
            }
            // 마우스 클릭은 버튼 onClick이 처리하므로 제출키로 다시 처리하지 않는다.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (input.SubmitPressed && !clicked) Activate(_selected);
        }

        void Activate(int index)
        {
            // 같은 프레임에 클릭과 제출키가 동시에 들어와 두 번 실행되는 것을 막는다.
            if (_lastActivatedFrame == Time.frameCount || index < 0 || index >= _items.Count) return;
            _selected = index;
            Refresh();
            if (!_items[index].enabled) return;
            _lastActivatedFrame = Time.frameCount;
            _items[index].action?.Invoke();
        }

        void Refresh()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var selected = i == _selected;
                _items[i].background.color = selected ? _theme.MenuSelected : _theme.MenuIdle;
                if (_items[i].enabled) _items[i].label.color = selected ? Color.white : _theme.Text * 0.9f;
            }
        }
    }
}
