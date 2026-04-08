using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reads live stats from BoardController and updates the HUD.
/// Assign the three Text references in the Inspector.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Board Reference")]
    public BoardController controller;

    [Header("Stat Labels")]
    public Text blastsText;
    public Text movesText;
    public Text shufflesText;

    void Update()
    {
        if (controller == null) return;

        if (blastsText  != null) blastsText.text  = $"Blasted: {controller.TotalBlasted}";
        if (movesText   != null) movesText.text   = $"Moves:   {controller.MoveCount}";
        if (shufflesText != null) shufflesText.text = $"Shuffles: {controller.ShuffleCount}";
    }
}
