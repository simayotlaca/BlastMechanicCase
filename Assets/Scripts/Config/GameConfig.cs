using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "BlastGame/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Board Settings")]
    [Range(2, 12)]
    public int rows = 10;

    [Range(2, 12)]
    public int columns = 10;

    [Header("Color Settings")]
    [Range(1, 6)]
    public int colorCount = 3;

    [Header("Group Settings")]
    public int minGroupSize = 2;

    [Header("Icon Thresholds")]
    public int thresholdA = 4;
    public int thresholdB = 7;
    public int thresholdC = 9;

    [Header("Visuals")]
    public float blockSize = 1.05f;
    public float blockSpacing = 0f;
    public float verticalSpacing = 0f;

    [Header("Animation")]
    public float fallDuration = 0.35f;

    [Header("Color Definitions")]
    public ColorDefinition[] colorDefinitions;

    public int GetSafeColorCount()
    {
        if (colorDefinitions == null || colorDefinitions.Length == 0)
        {
            return 1;
        }

        return Mathf.Clamp(colorCount, 1, colorDefinitions.Length);
    }

    public int GetSafeMinGroupSize()
    {
        return Mathf.Max(2, minGroupSize);
    }

    public void GetSafeThresholds(out int a, out int b, out int c)
    {
        a = Mathf.Max(2, thresholdA);
        b = Mathf.Max(a + 1, thresholdB);
        c = Mathf.Max(b + 1, thresholdC);
    }

}