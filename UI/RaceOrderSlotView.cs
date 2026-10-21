using UnityEngine;
using UnityEngine.UI;

namespace SprunkiRacing
{
    public class RaceOrderSlotView : MonoBehaviour
    {
        [SerializeField] private Image plate;
        [SerializeField] private Sprite aliveFrame;
        [SerializeField] private Sprite eliminatedFrame;
        [SerializeField] private Image portrait;
        [SerializeField] private Image arrow;

        private RaceStanding _standing;

        public RaceStanding Standing => _standing;

        public void Bind(RaceStanding standing)
        {
            _standing = standing;
            if (standing == null)
                return;

            if (portrait != null)
            {
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
            }

            if (arrow != null)
            {
                arrow.raycastTarget = false;
                arrow.gameObject.SetActive(standing.IsPlayer);
            }

            Refresh();
        }

        public void Refresh()
        {
            RefreshPortrait();
            RefreshFrame();
        }

        public void RefreshPortrait()
        {
            if (portrait == null || _standing == null)
                return;

            Sprite sprite = PortraitFor(_standing);
            if (portrait.sprite == sprite)
                return;

            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
        }

        private static Sprite PortraitFor(RaceStanding standing)
        {
            if (standing.IsEliminated)
                return standing.LowHealthPreview != null ? standing.LowHealthPreview : standing.Preview;

            RaceRacer racer = standing.Racer;
            return racer != null ? racer.CurrentPreview : standing.Preview;
        }

        private void RefreshFrame()
        {
            if (plate == null || _standing == null)
                return;

            Sprite frame = _standing.IsEliminated ? eliminatedFrame : aliveFrame;
            if (frame == null)
                return;

            plate.sprite = frame;
            plate.color = Color.white;
            plate.raycastTarget = false;
        }
    }
}
