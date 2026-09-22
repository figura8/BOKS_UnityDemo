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
        BOKSLevelScrollTransition levelScrollTransition;
        Text completionLabel;

        public int CurrentLevel { get; private set; }
        public bool CampaignComplete { get; private set; }
        public string PlayerCharacterId { get; private set; }
        public bool AuthoringMode { get; set; }
        public BOKSLevel2Controller Gameplay => view != null ? view.Gameplay : null;

        public void Configure(BOKSCampaignView campaignView) => view = campaignView;

        void Awake()
        {
            BOKSDeviceLayout.DetectAndApply();
            BOKSCampaignResponsiveLayout.Ensure(transform as RectTransform);
            transition = gameObject.AddComponent<BOKSLevelTransition>();
            levelScrollTransition = gameObject.AddComponent<BOKSLevelScrollTransition>();
            view.Gameplay.LevelCompleted += OnLevelCompleted;
            BOKSLevelDefinition firstLevel = BOKSLevelLoader.CampaignLevel(FirstLevel);
            PlayerCharacterId = firstLevel != null && !string.IsNullOrEmpty(firstLevel.characterId)
                ? firstLevel.characterId
                : "boks_green";
            view.SetCampaignPlayerCharacter(PlayerCharacterId);
            ApplyLevel(FirstLevel);
        }

        void OnDestroy()
        {
            if (view != null && view.Gameplay != null) view.Gameplay.LevelCompleted -= OnLevelCompleted;
        }

        void OnLevelCompleted(BOKSLevel2Controller controller)
        {
            if (AuthoringMode) return;
            if (CurrentLevel >= FinalLevel)
            {
                CampaignComplete = true;
                controller.SetCampaignInputLocked(true);
                transition.Play(ShowCampaignComplete);
                return;
            }

            controller.SetCampaignInputLocked(true);
            int next = CurrentLevel + 1;
            BOKSLevelDefinition nextLevel = BOKSLevelLoader.CampaignLevel(next);
            if (nextLevel != null && levelScrollTransition != null)
            {
                levelScrollTransition.Play(view, nextLevel, () =>
                {
                    if (!ApplyLevel(next))
                    {
                        Debug.LogWarning($"[BOKS CAMPAIGN] Scroll handoff could not apply level {next}; input restored.");
                        controller.SetCampaignInputLocked(false);
                    }
                    else controller.SetCampaignInputLocked(false);
                });
                return;
            }

            Debug.LogWarning($"[BOKS CAMPAIGN] Level {next} could not be staged for scroll; using iris fallback.");
            transition.Play(() =>
            {
                if (!ApplyLevel(next)) controller.SetCampaignInputLocked(false);
            });
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
