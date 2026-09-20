using UnityEngine;

namespace BOKS.Demo
{
    /// <summary>
    /// Loads a <see cref="BOKSLevelDefinition"/> from source JSON using Unity's JsonUtility (no
    /// third-party parser dependency). Also provides a hard-coded Level 2 fallback so the controller
    /// stays functional even when a definition has not been serialised into the scene yet.
    /// </summary>
    public static class BOKSLevelLoader
    {
        [System.Serializable]
        sealed class CampaignFile { public BOKSLevelDefinition[] levels; }

        static BOKSLevelDefinition[] campaign;
        public static string LevelJsonPath(int levelNumber) => $"Assets/BOKS/Source/Data/level-{levelNumber:D2}.json";

        /// <summary>For authoring tools: forget cached campaign JSON after the asset has changed.</summary>
        public static void ClearCampaignCache()
        {
            campaign = null;
        }

        public static BOKSLevelDefinition FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                BOKSLevelDefinition level = JsonUtility.FromJson<BOKSLevelDefinition>(json);
                if (level != null) level.NormalizeSourceFields();
                return level;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[BOKS] Failed to parse level JSON: " + exception.Message);
                return null;
            }
        }

        public static BOKSLevelDefinition[] LoadCampaign()
        {
            if (campaign != null) return campaign;
            TextAsset asset = Resources.Load<TextAsset>("BOKS/Data/campaign-levels");
            if (asset == null)
            {
                Debug.LogError("[BOKS] Missing Resources/BOKS/Data/campaign-levels.json");
                return campaign = new BOKSLevelDefinition[0];
            }
            CampaignFile file = JsonUtility.FromJson<CampaignFile>(asset.text);
            campaign = file != null && file.levels != null ? file.levels : new BOKSLevelDefinition[0];
            foreach (BOKSLevelDefinition level in campaign)
                if (level != null) level.NormalizeSourceFields();
            return campaign;
        }

        public static BOKSLevelDefinition CampaignLevel(int levelNumber)
        {
            foreach (BOKSLevelDefinition level in LoadCampaign())
                if (level != null && level.levelNumber == levelNumber) return level;
            return null;
        }

        /// <summary>
        /// Mirrors the Level 2 source record exactly (start (1,3) right, goal (3,3), two enabled main
        /// slots, forward-only). Used when no level JSON is available.
        /// </summary>
        public static BOKSLevelDefinition CreateLevel2Fallback()
        {
            return new BOKSLevelDefinition
            {
                levelNumber = 2,
                characterId = "boks_red",
                startDirection = "right",
                grid = new BOKSGrid { columns = 6, rows = 6 },
                start = new BOKSGridCell { x = 1, y = 3 },
                goal = new BOKSGridCell { x = 3, y = 3 },
                obstacles = new BOKSGridCell[0],
                program = new BOKSProgram
                {
                    mainCapacity = 8,
                    functionCapacity = 4,
                    mainSlotEnabled = new[] { true, true, false, false, false, false, false, false },
                    functionSlotEnabled = new[] { false, false, false, false },
                    enabledCommands = new[] { "forward" }
                }
            };
        }
    }
}
