using UnityEngine;
using UnityEngine.UI;

public class InstructionManager : MonoBehaviour
{
    [Header("UI Reference")]
    public Image instructionImage;

    // --- MODIFIED: Split instruction sets into Part 1 and Part 2 ---
    [Header("Part 1 Instruction Sets")]
    public Sprite[] series1DeceptionEnglishPart1Sprites;
    public Sprite[] series1DeceptionFarsiPart1Sprites;
    public Sprite[] series1ControlEnglishPart1Sprites;
    public Sprite[] series1ControlFarsiPart1Sprites;
    public Sprite[] series2DeceptionEnglishPart1Sprites;
    public Sprite[] series2DeceptionFarsiPart1Sprites;
    public Sprite[] series2ControlEnglishPart1Sprites;
    public Sprite[] series2ControlFarsiPart1Sprites;

    [Header("Part 2 Instruction Sets")]
    public Sprite[] series1DeceptionEnglishPart2Sprites;
    public Sprite[] series1DeceptionFarsiPart2Sprites;
    public Sprite[] series1ControlEnglishPart2Sprites;
    public Sprite[] series1ControlFarsiPart2Sprites;
    public Sprite[] series2DeceptionEnglishPart2Sprites;
    public Sprite[] series2DeceptionFarsiPart2Sprites;
    public Sprite[] series2ControlEnglishPart2Sprites;
    public Sprite[] series2ControlFarsiPart2Sprites;


    private GameManager gameManager;
    private int currentImageIndex = 0;
    private bool areInstructionsActive = false;
    private Sprite[] activeInstructionSprites;

    // MODIFIED: This just caches the game manager now.
    public void Initialize(GameManager gmInstance)
    {
        gameManager = gmInstance;
        if (gameManager == null)
        {
            Debug.LogError("InstructionManager: Initialize called with a null GameManager instance!");
            gameObject.SetActive(false);
        }
    }

    // NEW: A method to get the correct sprite set based on parameters
    public Sprite[] GetSpriteSet(int part, int series, TaskType taskType, string languageCode)
    {
        if (part == 1)
        {
            if (series == 1)
            {
                if (taskType == TaskType.Deception) return (languageCode == "fa") ? series1DeceptionFarsiPart1Sprites : series1DeceptionEnglishPart1Sprites;
                else return (languageCode == "fa") ? series1ControlFarsiPart1Sprites : series1ControlEnglishPart1Sprites;
            }
            else // series 2
            {
                if (taskType == TaskType.Deception) return (languageCode == "fa") ? series2DeceptionFarsiPart1Sprites : series2DeceptionEnglishPart1Sprites;
                else return (languageCode == "fa") ? series2ControlFarsiPart1Sprites : series2ControlEnglishPart1Sprites;
            }
        }
        else // part 2
        {
            if (series == 1)
            {
                if (taskType == TaskType.Deception) return (languageCode == "fa") ? series1DeceptionFarsiPart2Sprites : series1DeceptionEnglishPart2Sprites;
                else return (languageCode == "fa") ? series1ControlFarsiPart2Sprites : series1ControlEnglishPart2Sprites;
            }
            else // series 2
            {
                if (taskType == TaskType.Deception) return (languageCode == "fa") ? series2DeceptionFarsiPart2Sprites : series2DeceptionEnglishPart2Sprites;
                else return (languageCode == "fa") ? series2ControlFarsiPart2Sprites : series2ControlEnglishPart2Sprites;
            }
        }
    }

    // NEW: The main method GameManager will call to start showing a set of sprites.
    public void BeginInstructionSet(Sprite[] spritesToDisplay)
    {
        activeInstructionSprites = spritesToDisplay;

        if (activeInstructionSprites == null || activeInstructionSprites.Length == 0)
        {
            Debug.LogWarning("InstructionManager: The provided instruction set has no sprites. Completing immediately.");
            SignalCompletionToGameManager();
            return;
        }

        currentImageIndex = 0;
        areInstructionsActive = true;
        gameObject.SetActive(true); // Make sure this manager is active

        if (instructionImage != null)
        {
            instructionImage.gameObject.SetActive(true);
            UpdateInstructionImage();
            Debug.Log($"InstructionManager: Starting instruction display with {activeInstructionSprites.Length} images. Use arrow keys to navigate.");
        }
        else
        {
            Debug.LogError("InstructionManager: instructionImage not assigned. Cannot display instructions.");
            SignalCompletionToGameManager();
        }
    }

    void Update()
    {
        if (!areInstructionsActive || activeInstructionSprites == null || activeInstructionSprites.Length == 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (currentImageIndex >= activeInstructionSprites.Length - 1)
            {
                SignalCompletionToGameManager();
            }
            else
            {
                currentImageIndex++;
                UpdateInstructionImage();
            }
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (currentImageIndex > 0)
            {
                currentImageIndex--;
                UpdateInstructionImage();
            }
        }
    }

    void UpdateInstructionImage()
    {
        if (instructionImage != null && currentImageIndex < activeInstructionSprites.Length)
        {
            instructionImage.sprite = activeInstructionSprites[currentImageIndex];
            Debug.Log($"Displaying instruction image {currentImageIndex + 1}/{activeInstructionSprites.Length}");
        }
    }

    // MODIFIED: Renamed and now calls back to GameManager
    void SignalCompletionToGameManager()
    {
        if (!areInstructionsActive) return;

        areInstructionsActive = false;
        instructionImage.gameObject.SetActive(false); // Hide the image
        gameObject.SetActive(false); // Deactivate this manager until it's needed again

        if (gameManager != null)
        {
            Debug.Log("InstructionManager: Instruction set complete. Notifying GameManager.");
            gameManager.OnInstructionSetCompleted(); // Notify the master flow
        }
        else
        {
            Debug.LogError("InstructionManager: GameManager reference is missing when trying to complete instructions!");
        }
    }
}