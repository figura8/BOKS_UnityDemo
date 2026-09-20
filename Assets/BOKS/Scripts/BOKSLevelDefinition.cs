using System;
using UnityEngine;

namespace BOKS.Demo
{
    /// <summary>
    /// Command block kinds. Level 2 only ever executes <see cref="Forward"/>; the remaining values
    /// are part of the shared model so later campaign levels (turns, function sub-routines) can be
    /// added without changing the data contract.
    /// </summary>
    public enum BOKSCommandType
    {
        None = 0,
        Forward = 1,
        Left = 2,
        Right = 3,
        Function = 4
    }

    /// <summary>Cardinal facing. Level 2 starts facing <see cref="Right"/>.</summary>
    public enum BOKSDirection
    {
        Right = 0,
        Up = 1,
        Left = 2,
        Down = 3
    }

    /// <summary>A zero-based grid cell, matching the source "x"/"y" JSON fields.</summary>
    [Serializable]
    public sealed class BOKSGridCell
    {
        public int x;
        public int y;
    }

    [Serializable]
    public sealed class BOKSGrid
    {
        public int columns = 6;
        public int rows = 6;
    }

    [Serializable]
    public sealed class BOKSProgram
    {
        public int mainCapacity = 8;
        public int functionCapacity = 4;
        public bool[] mainSlotEnabled = new bool[0];
        public bool[] functionSlotEnabled = new bool[0];
        public string[] enabledCommands = new string[0];
    }

    [Serializable]
    public sealed class BOKSEnabledBlocks
    {
        public bool forward;
        public bool left;
        public bool right;
        public bool function;
    }

    [Serializable]
    public sealed class BOKSDecoration
    {
        public string id;
        public int x;
        public int y;
        public float anchorX;
        public float anchorY;
        public string asset;
        public string layer;
        public float scale = 1f;
        public int count = 1;
        public string foliageColor;
        public string trunkColor;
    }

    /// <summary>
    /// Data for one campaign level, loaded from the source level JSON (e.g. level-02.json).
    /// Field names intentionally mirror the JSON keys (camelCase) so Unity's JsonUtility maps them
    /// with no adapter. Any JSON keys not modelled here (decorations, theme, hints, progression,
    /// initialMain/initialFunction) are ignored.
    /// </summary>
    [Serializable]
    public sealed class BOKSLevelDefinition
    {
        public string id;
        public int levelNumber;
        public string name;
        public string icon;
        public string baseLevel;
        public string characterId = "boks_red";
        public string startDirection = "right";
        public BOKSGrid grid = new BOKSGrid();
        public BOKSGridCell start = new BOKSGridCell();
        public BOKSGridCell goal = new BOKSGridCell();
        public BOKSGridCell[] obstacles = new BOKSGridCell[0];
        public BOKSProgram program = new BOKSProgram();

        // Native editor-levels.json fields. NormalizeSourceFields maps these onto the
        // original per-level Unity schema above, keeping both source formats readable.
        public int number;
        public string startOri;
        public BOKSDecoration[] decorations = new BOKSDecoration[0];
        public bool[] mainSlotEnabled = new bool[0];
        public bool[] fnSlotEnabled = new bool[0];
        public BOKSEnabledBlocks enabledBlocks;
        public bool glowEnabled = true;

        public int StartColumn => start != null ? start.x : 2;
        public int StartRow => start != null ? start.y : 2;
        public int GoalColumn => goal != null ? goal.x : 5;
        public int GoalRow => goal != null ? goal.y : 5;
        public int GridColumns => grid != null && grid.columns > 0 ? grid.columns : 6;
        public int GridRows => grid != null && grid.rows > 0 ? grid.rows : 6;

        public BOKSDirection Direction => ParseDirection(startDirection);

        public void NormalizeSourceFields()
        {
            if (levelNumber <= 0) levelNumber = number;
            if (!string.IsNullOrEmpty(startOri)) startDirection = startOri;
            if (grid == null) grid = new BOKSGrid();
            if (program == null) program = new BOKSProgram();
            if (mainSlotEnabled != null && mainSlotEnabled.Length > 0)
                program.mainSlotEnabled = mainSlotEnabled;
            if (fnSlotEnabled != null && fnSlotEnabled.Length > 0)
                program.functionSlotEnabled = fnSlotEnabled;
            if (enabledBlocks != null)
            {
                var commands = new System.Collections.Generic.List<string>(4);
                if (enabledBlocks.forward) commands.Add("forward");
                if (enabledBlocks.left) commands.Add("left");
                if (enabledBlocks.right) commands.Add("right");
                if (enabledBlocks.function) commands.Add("function");
                program.enabledCommands = commands.ToArray();
            }
        }

        public int EnabledMainSlotCount
        {
            get
            {
                int count = 0;
                if (program != null && program.mainSlotEnabled != null)
                    foreach (bool enabled in program.mainSlotEnabled)
                        if (enabled) count++;
                return count;
            }
        }

        public bool IsMainSlotEnabled(int index)
        {
            if (program == null || program.mainSlotEnabled == null) return false;
            return index >= 0 && index < program.mainSlotEnabled.Length && program.mainSlotEnabled[index];
        }

        public bool IsFunctionSlotEnabled(int index)
        {
            if (program == null || program.functionSlotEnabled == null) return false;
            return index >= 0 && index < program.functionSlotEnabled.Length && program.functionSlotEnabled[index];
        }

        public bool HasObstacle(int x, int y)
        {
            if (obstacles == null) return false;
            for (int i = 0; i < obstacles.Length; i++)
                if (obstacles[i] != null && obstacles[i].x == x && obstacles[i].y == y) return true;
            return false;
        }

        public bool IsCommandEnabled(BOKSCommandType command)
        {
            if (command == BOKSCommandType.None || program == null || program.enabledCommands == null) return false;
            string name = CommandToName(command);
            for (int i = 0; i < program.enabledCommands.Length; i++)
                if (string.Equals(program.enabledCommands[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string CommandToName(BOKSCommandType command)
        {
            switch (command)
            {
                case BOKSCommandType.Forward: return "forward";
                case BOKSCommandType.Left: return "left";
                case BOKSCommandType.Right: return "right";
                case BOKSCommandType.Function: return "function";
                default: return string.Empty;
            }
        }

        public static BOKSDirection ParseDirection(string value)
        {
            switch (value)
            {
                case "up": return BOKSDirection.Up;
                case "left": return BOKSDirection.Left;
                case "down": return BOKSDirection.Down;
                default: return BOKSDirection.Right;
            }
        }
    }
}
