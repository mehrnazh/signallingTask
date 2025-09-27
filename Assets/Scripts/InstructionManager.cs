using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class InstructionManager : MonoBehaviour
{
    [Header("Instruction Settings")]
    public Image instructionImage;

    [Header("Instruction Sets")]
    // Series 1 instruction sprites
    [Tooltip("Series 1 instruction sprites for Deception task in English")]
    public Sprite[] series1DeceptionEnglishSprites;
    [Tooltip("Series 1 instruction sprites for Deception task in Farsi")]
    public Sprite[] series1DeceptionFarsiSprites;
    [Tooltip("Series 1 instruction sprites for Control task in English")]
    public Sprite[] series1ControlEnglishSprites;
    [Tooltip("Series 1 instruction sprites for Control task in Farsi")]
    public Sprite[] series1ControlFarsiSprites;

    // Series 2 instruction sprites
    [Tooltip("Series 2 instruction sprites for Deception task in English")]
    public Sprite[] series2DeceptionEnglishSprites;
    [Tooltip("Series 2 instruction sprites for Deception task in Farsi")]
    public Sprite[] series2DeceptionFarsiSprites;
    [Tooltip("Series 2 instruction sprites for Control task in English")]
    public Sprite[] series2ControlEnglishSprites;
    [Tooltip("Series 2 instruction sprites for Control task in Farsi")]
    public Sprite[] series2ControlFarsiSprites;

    [Header("Display Settings")]
    public float displayDuration = 10f;  // 10 seconds per image

    private GameManager gameManager;
    private int currentImageIndex = 0;
    public bool instructionsComplete = false;
    private Sprite[] activeInstructionSprites;

    public void InitializeInstructions(int series, TaskType taskType, string languageCode, GameManager gmInstance)
    {
        gameManager = gmInstance;
        if (gameManager == null)
        {
            Debug.LogError("InstructionManager: InitializeInstructions called with a null GameManager instance!");
            gameObject.SetActive(false);
            return;
        }

        Debug.Log($"InstructionManager: Initializing instructions for Series: {series}, Task: {taskType}, Language: {languageCode}");

        // Select the correct instruction set based on parameters
        if (series == 1)
        {
            if (taskType == TaskType.Deception)
                activeInstructionSprites = (languageCode == "fa") ? series1DeceptionFarsiSprites : series1DeceptionEnglishSprites;
            else
                activeInstructionSprites = (languageCode == "fa") ? series1ControlFarsiSprites : series1ControlEnglishSprites;
        }
        else // series 2
        {
            if (taskType == TaskType.Deception)
                activeInstructionSprites = (languageCode == "fa") ? series2DeceptionFarsiSprites : series2DeceptionEnglishSprites;
            else
                activeInstructionSprites = (languageCode == "fa") ? series2ControlFarsiSprites : series2ControlEnglishSprites;
        }

        // Validate that we have sprites to display
        if (activeInstructionSprites == null || activeInstructionSprites.Length == 0)
        {
            Debug.LogError($"InstructionManager: No sprites found for Series: {series}, Task: {taskType}, Language: {languageCode}");
            Sprite[][] allSets = {
                series1DeceptionEnglishSprites, series1DeceptionFarsiSprites, series1ControlEnglishSprites, series1ControlFarsiSprites,
                series2DeceptionEnglishSprites, series2DeceptionFarsiSprites, series2ControlEnglishSprites, series2ControlFarsiSprites
            };

            foreach (var set in allSets)
            {
                if (set != null && set.Length > 0)
                {
                    Debug.LogWarning("InstructionManager: Using fallback instruction set");
                    activeInstructionSprites = set;
                    break;
                }
            }

            if (activeInstructionSprites == null || activeInstructionSprites.Length == 0)
            {
                Debug.LogError("InstructionManager: No fallback instruction sets available. Completing immediately.");
                CompleteInstructions();
                return;
            }
        }

        currentImageIndex = 0;
        instructionsComplete = false; // Reset completion flag

        if (instructionImage != null)
        {
            instructionImage.sprite = activeInstructionSprites[0];
            Debug.Log($"InstructionManager: Starting instruction display with {activeInstructionSprites.Length} images. Use arrow keys to navigate.");
            // REMOVED: No longer starting a coroutine. The Update method will handle input.
            // StartCoroutine(CycleInstructions());
        }
        else
        {
            Debug.LogError("InstructionManager: instructionImage not assigned. Cannot display instructions.");
            CompleteInstructions();
        }
    }

    /// <summary>
    /// ADDED: Update is called once per frame and will handle navigation.
    /// </summary>
    void Update()
    {
        // Don't process input if instructions are done or there are no sprites
        if (instructionsComplete || activeInstructionSprites == null || activeInstructionSprites.Length == 0)
        {
            return;
        }

        // --- FORWARD NAVIGATION ---
        // Move to the next image or complete instructions
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            // If we are on the last image, complete the instructions
            if (currentImageIndex >= activeInstructionSprites.Length - 1)
            {
                CompleteInstructions();
            }
            else
            {
                // Otherwise, move to the next image
                currentImageIndex++;
                UpdateInstructionImage();
            }
        }
        // --- BACKWARD NAVIGATION ---
        // Move to the previous image
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            // Only go back if we are not on the first image
            if (currentImageIndex > 0)
            {
                currentImageIndex--;
                UpdateInstructionImage();
            }
        }
    }

    /// <summary>
    /// ADDED: Helper method to update the displayed sprite.
    /// </summary>
    void UpdateInstructionImage()
    {
        if (instructionImage != null && currentImageIndex < activeInstructionSprites.Length)
        {
            instructionImage.sprite = activeInstructionSprites[currentImageIndex];
            Debug.Log($"Displaying instruction image {currentImageIndex + 1}/{activeInstructionSprites.Length}");
        }
    }

    // REMOVED: The CycleInstructions coroutine is no longer needed.
    // IEnumerator CycleInstructions() { ... }

    void CompleteInstructions()
    {
        // Prevent this from being called multiple times
        if (instructionsComplete) return;

        instructionsComplete = true;

        if (gameManager != null)
        {
            Debug.Log("InstructionManager: Instructions complete. Notifying GameManager.");
            gameManager.StartGameInternal();
        }
        else
        {
            Debug.LogError("InstructionManager: GameManager reference is missing when trying to complete instructions!");
        }
    }
}