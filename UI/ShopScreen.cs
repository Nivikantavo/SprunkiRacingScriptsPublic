using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public enum ShopSection
    {
        Car,
        GoldPack,
        Trails
    }

    public class ShopScreen : MonoBehaviour
    {
        [Serializable]
        private struct Tab
        {
            public ShopSection section;
            public Button button;
            public Image background;
            public TextMeshProUGUI label;
            public GameObject[] pages;
        }

        [SerializeField] private Tab[] tabs = Array.Empty<Tab>();
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color normalColor = new Color(122f / 255f, 152f / 255f, 182f / 255f, 1f);
        [SerializeField] private Button backButton;
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scroll;

        private UnityAction[] _tabClicks;
        private Coroutine _scrollReset;

        public event Action BackRequested;

        private void Awake()
        {
            _tabClicks = new UnityAction[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                Button button = tabs[i].button;
                if (button == null)
                    continue;

                int index = i;
                _tabClicks[i] = () => Select(tabs[index].section);
                button.onClick.AddListener(_tabClicks[i]);
            }

            if (backButton != null)
                backButton.onClick.AddListener(HandleBack);
        }

        private void OnDestroy()
        {
            if (_tabClicks != null)
            {
                for (int i = 0; i < tabs.Length && i < _tabClicks.Length; i++)
                {
                    if (tabs[i].button != null && _tabClicks[i] != null)
                        tabs[i].button.onClick.RemoveListener(_tabClicks[i]);
                }
            }

            if (backButton != null)
                backButton.onClick.RemoveListener(HandleBack);
        }

        public void Open(ShopSection section)
        {
            gameObject.SetActive(true);
            Select(section);
        }

        public void Hide()
        {
            StopScrollReset();
            gameObject.SetActive(false);
        }

        private void Select(ShopSection section)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                Tab tab = tabs[i];
                bool selected = tab.section == section;
                ApplyVisual(tab, selected);
                SetPagesActive(tab.pages, selected);
            }

            RebuildContent();
        }

        private void RebuildContent()
        {
            if (content == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            ResetScroll();
            if (!isActiveAndEnabled)
                return;

            StopScrollReset();
            _scrollReset = StartCoroutine(ResetScrollNextFrame());
        }

        private IEnumerator ResetScrollNextFrame()
        {
            yield return null;
            _scrollReset = null;
            if (content == null)
                yield break;

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            ResetScroll();
        }

        private void ResetScroll()
        {
            if (scroll == null)
                return;

            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void StopScrollReset()
        {
            if (_scrollReset == null)
                return;

            StopCoroutine(_scrollReset);
            _scrollReset = null;
        }

        private void ApplyVisual(Tab tab, bool selected)
        {
            if (tab.background != null)
                tab.background.sprite = selected ? selectedSprite : normalSprite;

            if (tab.label != null)
                tab.label.color = selected ? selectedColor : normalColor;
        }

        private static void SetPagesActive(GameObject[] pages, bool active)
        {
            if (pages == null)
                return;

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null)
                    pages[i].SetActive(active);
            }
        }

        private void HandleBack()
        {
            BackRequested?.Invoke();
        }
    }
}
