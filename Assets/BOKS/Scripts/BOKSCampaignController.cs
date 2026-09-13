using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>Owns Level 1→10 progression; gameplay remains in the shared gameplay controller.</summary>
    public sealed class BOKSCampaignController : MonoBehaviour
    {
        public const int FirstLevel = 1;
        public const int FinalLevel = 10;

        [SerializeField] BOKSCampaignView view;
        BOKSLevelTransition transition;
        Text completionLabel;

        public int CurrentLevel { get; private set; }
        public bool CampaignComplete { get; private set; }
        public BOKSLevel2Controller Gameplay => view != null ? view.Gameplay : null;

        public void Configure(BOKSCampaignView campaignView) => view = campaignView;

        void Awake()
        {
            transition = gameObject.AddComponent<BOKSLevelTransition>();
            view.Gameplay.LevelCompleted += OnLevelCompleted;
            ApplyLevel(FirstLevel);
        }

        void OnDestroy()
        {
            if (view != null && view.Gameplay != null) view.Gameplay.LevelCompleted -= OnLevelCompleted;
        }

        void OnLevelCompleted(BOKSLevel2Controller controller)
        {
            if (CurrentLevel >= FinalLevel)
            {
                CampaignComplete = true;
                controller.SetCampaignInputLocked(true);
                transition.Play(ShowCampaignComplete);
                return;
            }

            controller.SetCampaignInputLocked(true);
            int next = CurrentLevel + 1;
            transition.Play(() => ApplyLevel(next));
        }

        public bool ApplyLevel(int levelNumber)
        {
            if (levelNumber < FirstLevel || levelNumber > FinalLevel) return false;
            BOKSLevelDefinition level = BOKSLevelLoader.CampaignLevel(levelNumber);
            if (level == null) return false;
            CurrentLevel = levelNumber;
            CampaignComplete = false;
            if (completionLabel != null) completionLabel.gameObject.SetActive(false);
            view.ApplyLevel(level);
            return true;
        }

        void ShowCampaignComplete()
        {
            if (completionLabel == null)
            {
                GameObject go = new GameObject("Campaign Complete", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(transform, false);
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                rt.sizeDelta = new Vector2(440, 120);
                completionLabel = go.GetComponent<Text>();
                completionLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                completionLabel.fontSize = 38;
                completionLabel.alignment = TextAnchor.MiddleCenter;
                completionLabel.color = new Color32(46, 97, 128, 255);
                completionLabel.text = "Campaign Complete";
            }
            completionLabel.gameObject.SetActive(true);
        }
    }
}
