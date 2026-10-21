using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class MainMenuFlow : MonoBehaviour
    {
        [SerializeField] private GameObject characters;
        [SerializeField] private ShopScreen shop;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private ShopSectionRequest shopRequest;
        [SerializeField] private GameObject ranking;
        [SerializeField] private Button rankingButton;
        [SerializeField] private Button rankingBackButton;
        [SerializeField] private SettingsPanel settings;
        [SerializeField] private Button settingsButton;

        private void Awake()
        {
            if (rankingBackButton == null)
                rankingBackButton = FindRankingBackButton();

            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(ShowCarShop);

            if (rankingButton != null)
                rankingButton.onClick.AddListener(ShowRanking);

            if (rankingBackButton != null)
                rankingBackButton.onClick.AddListener(ShowCharacters);

            if (shop != null)
                shop.BackRequested += ShowCharacters;

            if (shopRequest != null)
                shopRequest.Raised += ShowShop;

            if (settingsButton == null && characters != null)
            {
                Transform back = FindChild(characters.transform, "Button_Back");
                if (back != null)
                    settingsButton = back.GetComponent<Button>();
            }

            if (settingsButton != null)
                settingsButton.onClick.AddListener(ShowSettings);

            ShowCharacters();
            ApplyRankingAccess();
        }

        private void OnEnable()
        {
            LeaderboardBoard.AvailabilityChanged += ApplyRankingAccess;
            ApplyRankingAccess();
        }

        private void OnDisable()
        {
            LeaderboardBoard.AvailabilityChanged -= ApplyRankingAccess;
        }

        private void Start()
        {
            PlatformSignals.LoadingStopped();
            PlatformSignals.GameReady();
        }

        private void OnDestroy()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(ShowCarShop);

            if (rankingButton != null)
                rankingButton.onClick.RemoveListener(ShowRanking);

            if (rankingBackButton != null)
                rankingBackButton.onClick.RemoveListener(ShowCharacters);

            if (shop != null)
                shop.BackRequested -= ShowCharacters;

            if (shopRequest != null)
                shopRequest.Raised -= ShowShop;

            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(ShowSettings);
        }

        private void ShowSettings()
        {
            if (settings != null)
                settings.Open();
        }

        private void ShowCharacters()
        {
            if (shop != null)
                shop.Hide();

            if (ranking != null)
                ranking.SetActive(false);

            if (characters != null)
                characters.SetActive(true);
        }

        private void ShowCarShop()
        {
            ShowShop(ShopSection.Car);
        }

        private void ShowShop(ShopSection section)
        {
            if (characters != null)
                characters.SetActive(false);

            if (ranking != null)
                ranking.SetActive(false);

            if (shop != null)
                shop.Open(section);
        }

        private void ApplyRankingAccess()
        {
            bool canUse = LeaderboardBoard.CanUse;
            if (rankingButton != null)
                rankingButton.gameObject.SetActive(canUse);

            if (!canUse && ranking != null && ranking.activeSelf)
                ShowCharacters();
        }

        private void ShowRanking()
        {
            if (!LeaderboardBoard.CanUse)
                return;

            if (characters != null)
                characters.SetActive(false);

            if (shop != null)
                shop.Hide();

            if (ranking != null)
                ranking.SetActive(true);
        }

        private Button FindRankingBackButton()
        {
            if (ranking == null)
                return null;

            Transform back = FindChild(ranking.transform, "Button_Back");
            return back != null ? back.GetComponent<Button>() : null;
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
