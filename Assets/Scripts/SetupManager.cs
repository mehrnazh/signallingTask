using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic; // Required for Dropdown options & Coroutine

public class SetupManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject setupPanelObject;
    public TMP_Dropdown seriesDropdown;
    public TMP_Dropdown languageDropdown;
    public TMP_Dropdown taskTypeDropdown;
    public Button startButton;

    private GameManager gameManager;

    void Awake()
    {
        // Populate Dropdowns - this doesn't depend on GameManager
        PopulateDropdowns();

        // Defer GameManager-dependent setup to a coroutine
        StartCoroutine(WaitForGameManagerAndSetup());
    }

    IEnumerator WaitForGameManagerAndSetup()
    {
        Debug.Log("SetupManager: Waiting for GameManager instance...");

        // Wait until GameManager.Instance is assigned
        yield return new WaitUntil(() => GameManager.Instance != null);

        // Now that Instance is available, grab it
        gameManager = GameManager.Instance;
        Debug.Log("SetupManager: GameManager instance found!");

        if (gameManager == null) // Should not happen after WaitUntil, but double-check
        {
            Debug.LogError("SetupManager: GameManager instance became null immediately after wait! Aborting setup.");
            if(startButton) startButton.interactable = false;
            yield break; // Exit coroutine
        }

        // Add listener to the button *after* we have the GameManager
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners(); // Ensure no duplicates if run multiple times
            startButton.onClick.AddListener(OnStartButtonClicked);
            startButton.interactable = true; // Ensure button is interactable
            Debug.Log("SetupManager: Start button listener added.");
        }
        else
        {
            Debug.LogError("SetupManager: Start Button reference is missing!");
        }

        // Optional: Ensure other panels controlled by GameManager are initially off
        // Can be helpful redundancy
        if (gameManager.instructionPanel) gameManager.instructionPanel.SetActive(false);
        if (gameManager.trialPanel) gameManager.trialPanel.SetActive(false);
        if (gameManager.fixationPanel) gameManager.fixationPanel.SetActive(false);
    }

    void PopulateDropdowns()
    {
        // Series
        if (seriesDropdown != null)
        {
            seriesDropdown.ClearOptions();
            seriesDropdown.AddOptions(new List<string> { "1", "2" });
        }
        else Debug.LogError("SetupManager: Series Dropdown not assigned!");

        // Language
        if (languageDropdown != null)
        {
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new List<string> { "English", "Farsi" });
        }
         else Debug.LogError("SetupManager: Language Dropdown not assigned!");

        // Task Type
        if (taskTypeDropdown != null)
        {
            taskTypeDropdown.ClearOptions();
            taskTypeDropdown.AddOptions(new List<string> { "Deception", "Control" });
        }
         else Debug.LogError("SetupManager: Task Type Dropdown not assigned!");
    }

    void OnStartButtonClicked()
    {
        // Ensure gameManager reference is still valid before proceeding
        if (gameManager == null)
        {
            Debug.LogError("SetupManager: GameManager reference is null on button click! Trying to find again...");
            gameManager = GameManager.Instance; // Try to re-acquire
            if (gameManager == null)
            {
                Debug.LogError("SetupManager: Could not re-acquire GameManager instance. Cannot start.");
                if (startButton) startButton.interactable = true; // Re-enable button to allow retry?
                return;
            }
        }

        Debug.Log("SetupManager: Start Button Clicked.");
        // Disable button to prevent double clicks
        if(startButton) startButton.interactable = false;

        // --- Read selections --- 
        // (Adding null checks for safety)
        int selectedSeries = (seriesDropdown != null) ? seriesDropdown.value + 1 : 1; // Default to 1 if null

        string selectedLanguageCode = "en"; // Default
        if (languageDropdown != null)
        {
            string selectedLanguageText = languageDropdown.options[languageDropdown.value].text;
            switch (selectedLanguageText)
            {
                case "English": selectedLanguageCode = "en"; break;
                case "Farsi": selectedLanguageCode = "fa"; break;
                default: Debug.LogError($"SetupManager: Unknown language selected: {selectedLanguageText}. Defaulting to 'en'."); break;
            }
        }
        else { Debug.LogError("SetupManager: Language Dropdown missing, defaulting to 'en'."); }

        TaskType selectedTaskType = TaskType.Deception; // Default
        if (taskTypeDropdown != null)
        {
             string selectedTaskText = taskTypeDropdown.options[taskTypeDropdown.value].text;
             if (!System.Enum.TryParse<TaskType>(selectedTaskText, true, out selectedTaskType))
             {
                 Debug.LogError($"SetupManager: Unknown task type selected: {selectedTaskText}. Defaulting to Deception.");
                 selectedTaskType = TaskType.Deception;
             }
        }
         else { Debug.LogError("SetupManager: Task Type Dropdown missing, defaulting to Deception."); }


        Debug.Log($"SetupManager: Selections - Series: {selectedSeries}, Language Code: {selectedLanguageCode}, Task Type: {selectedTaskType}");

        // --- Pass selections to GameManager and start its initialization --- 
        gameManager.StartInitializationWithOptions(selectedTaskType, selectedSeries, selectedLanguageCode);

        // --- Hide the correct setup panel object --- 
        if (setupPanelObject != null)
        {
             setupPanelObject.SetActive(false);
             Debug.Log("SetupManager: Setup Panel deactivated.");
        }
        else
        {
            Debug.LogError("SetupManager: setupPanelObject reference not set in Inspector! Cannot hide panel.");
            // Optionally, hide this manager's object too as a fallback?
            // gameObject.SetActive(false);
        }
    }
}