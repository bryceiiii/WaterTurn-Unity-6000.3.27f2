using UnityEngine;

namespace WaterTurn
{
    /// <summary>
    /// The puzzle definition that used to come from <see cref="Campaign.Levels"/>.
    /// One component per level scene: <see cref="rows"/> is the authoritative
    /// ASCII board, and the prefab instances under the scene's board are its
    /// visual projection.
    /// </summary>
    public sealed class LevelDefinition : MonoBehaviour
    {
        [Tooltip("Index into Campaign.Levels (0-based). Also used to work out the next level.")]
        public int levelIndex;

        [Tooltip("Display name shown in the HUD headline.")]
        public string levelName = "Level";

        [Tooltip("Hint shown in the HUD instructions panel.")]
        [TextArea(2, 4)]
        public string hint = "";

        [Tooltip("ASCII board, one string per row.  # wall   . floor   ~ water   P player   B crate   X heavy plate   1 / 2 portal pair   ^ > v < one-way valve   s timed sensor   G grate   D gate   S sensor   K pearl   E exit   M membrane (fish passes, water does not)   o spring (always wet, never slides)   d inverted gate (open by default, closes while the sensor is wet)")]
        public string[] rows = new string[0];

        public Level ToLevel() => new Level(levelName, hint, rows);
    }
}
