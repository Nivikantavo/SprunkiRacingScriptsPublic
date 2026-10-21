using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class LeaderboardItemView : MonoBehaviour
    {
        private Image _background;
        private Image _medal;
        private Image _character;
        private Image _country;
        private TextMeshProUGUI _place;
        private TextMeshProUGUI _userName;
        private TextMeshProUGUI _score;

        private void Awake()
        {
            Resolve();
            RectTransform rect = transform as RectTransform;
            if (rect == null)
                return;

            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout == null)
                layout = gameObject.AddComponent<LayoutElement>();

            layout.minHeight = rect.sizeDelta.y;
            layout.preferredHeight = rect.sizeDelta.y;
        }

        public void Bind(int rank, string userName, string score, Sprite frame, Sprite medal, Color placeColor, Sprite portrait = null, Sprite flag = null)
        {
            Resolve();

            if (_place != null)
            {
                _place.text = rank > 0 ? rank.ToString() : "—";
                _place.color = placeColor;
            }

            if (_userName != null)
                _userName.text = userName ?? string.Empty;

            if (_score != null)
                _score.text = score ?? string.Empty;

            if (_background != null && frame != null)
                _background.sprite = frame;

            if (_medal != null)
            {
                _medal.enabled = medal != null;
                if (medal != null)
                    _medal.sprite = medal;
            }

            ApplyIcon(_character, portrait);
            ApplyIcon(_country, flag);
        }

        private static void ApplyIcon(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.enabled = sprite != null;
            if (sprite == null)
                return;

            image.sprite = sprite;
            image.preserveAspect = true;
        }

        private void Resolve()
        {
            if (_background == null)
                _background = GetComponent<Image>();

            if (_medal == null)
                _medal = FindImage("Icon_Medal_Gold");

            if (_character == null)
                _character = FindImage("Character");

            if (_country == null)
                _country = FindImage("Icon_Country");

            if (_place == null)
                _place = FindText("Text_1");

            if (_userName == null)
                _userName = FindText("Text_UserName");

            if (_score == null)
                _score = FindText("Text_Score_Value");
        }

        private Image FindImage(string childName)
        {
            Transform child = FindChild(transform, childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private TextMeshProUGUI FindText(string childName)
        {
            Transform child = FindChild(transform, childName);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
