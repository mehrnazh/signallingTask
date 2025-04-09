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

    // *** Store the reference passed by GameManager ***
    private GameManager gameManager;      // Reference to GameManager

    private int currentImageIndex = 0;
    public bool instructionsComplete = false;
    
    // Currently active sprite array
    private Sprite[] activeInstructionSprites;

    // *** Start is no longer needed for initialization logic ***
    // void Start()
    // {
    //     // Initialization logic moved to InitializeInstructions
    // }

    /// <summary>
    /// Initialize the instruction manager with the appropriate set of sprites and the GameManager reference.
    /// Called explicitly by GameManager.
    /// </summary>
    /// <param name="series">Instruction series (1 or 2)</param>
    /// <param name="taskType">Task type (Deception or Control)</param>
    /// <param name="languageCode">Language code ("en" for English, "fa" for Farsi)</param>
    /// <param name="gmInstance">The GameManager instance calling this method</param>
    public void InitializeInstructions(int series, TaskType taskType, string languageCode, GameManager gmInstance)
    {
        // *** Store the passed GameManager reference ***
        gameManager = gmInstance;
        if (gameManager == null)
        {
            Debug.LogError("InstructionManager: InitializeInstructions called with a null GameManager instance!");
            // Cannot proceed without GameManager
            gameObject.SetActive(false); // Deactivate self
            return;
        }

        Debug.Log($"InstructionManager: Initializing instructions for Series: {series}, Task: {taskType}, Language: {languageCode}");
        
        // Select the correct instruction set based on parameters
        if (series == 1)
        {
            if (taskType == TaskType.Deception)
            {
                activeInstructionSprites = (languageCode == "fa") ? series1DeceptionFarsiSprites : series1DeceptionEnglishSprites;
            }
            else // Control
            {
                activeInstructionSprites = (languageCode == "fa") ? series1ControlFarsiSprites : series1ControlEnglishSprites;
            }
        }
        else // series 2
        {
            if (taskType == TaskType.Deception)
            {
                activeInstructionSprites = (languageCode == "fa") ? series2DeceptionFarsiSprites : series2DeceptionEnglishSprites;
            }
            else // Control
            {
                activeInstructionSprites = (languageCode == "fa") ? series2ControlFarsiSprites : series2ControlEnglishSprites;
            }
        }
        
        // Validate that we have sprites to display
        if (activeInstructionSprites == null || activeInstructionSprites.Length == 0)
        {
            Debug.LogError($"InstructionManager: No sprites found for Series: {series}, Task: {taskType}, Language: {languageCode}");
            // Try to find a fallback set
            Sprite[][] allSets = new Sprite[][] {
                series1DeceptionEnglishSprites, series1DeceptionFarsiSprites,
                series1ControlEnglishSprites, series1ControlFarsiSprites,
                series2DeceptionEnglishSprites, series2DeceptionFarsiSprites,
                series2ControlEnglishSprites, series2ControlFarsiSprites
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
            
            // If still no valid set found, skip instructions
            if (activeInstructionSprites == null || activeInstructionSprites.Length == 0)
            {
                Debug.LogError("InstructionManager: No fallback instruction sets available. Completing immediately.");
                // Signal GameManager to proceed without instructions
                CompleteInstructions(); // Call complete which will notify the stored GameManager
                return;
            }
        }
        
        // Reset current index and set the first image
        currentImageIndex = 0;
        
        // Make sure we have a valid Image component
        if (instructionImage != null)
        {
            instructionImage.sprite = activeInstructionSprites[0];
            
            // Ensure this GameObject is active to display instructions (GameManager should have activated it)
            // gameObject.SetActive(true); // Redundant if GM activates it
            
            // Start the instruction cycle
            Debug.Log($"InstructionManager: Starting instruction cycle with {activeInstructionSprites.Length} images");
            StartCoroutine(CycleInstructions());
        }
        else
        {
            Debug.LogError("InstructionManager: instructionImage not assigned. Cannot display instructions.");
            CompleteInstructions(); // Complete immediately if UI is missing
        }
    }

    IEnumerator CycleInstructions()
    {
        // Wait for key press or time expiration for each image
        while (currentImageIndex < activeInstructionSprites.Length)
        {
            float timer = 0;
            bool keyPressed = false;
            
            while (timer < displayDuration && !keyPressed)
            {
                if (Input.anyKeyDown)
                {
                    keyPressed = true;
                }
                
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Move to next image
            currentImageIndex++;
            
            // If we have more images to show
            if (currentImageIndex < activeInstructionSprites.Length)
            {
                instructionImage.sprite = activeInstructionSprites[currentImageIndex];
            }
            else
            {
                // All images shown, complete the instruction phase
                CompleteInstructions();
                break; // Exit coroutine
            }
        }
    }
    
    void CompleteInstructions()
    {
        instructionsComplete = true;
        
        // Notify the stored GameManager that instructions are complete
        if (gameManager != null)
        {
            Debug.Log("InstructionManager: Instructions complete. Notifying GameManager.");
            // Use StartGameInternal instead of StartGame to skip the initialization part
            // that already happened
            gameManager.StartGameInternal();
        }
        else
        {
            // This should ideally not happen if initialization flow is correct
            Debug.LogError("InstructionManager: GameManager reference is missing when trying to complete instructions!");
        }
        
        // Hide this panel (GameManager might also deactivate the whole object later)
        // gameObject.SetActive(false);
    }
}
