using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.Threading.Tasks; // Added for Task
using System.Linq; // Added for FirstOrDefault
using SignallingTaskData;

// Enum to distinguish between task types.
public enum TaskType { Deception, Control }

public class GameManager : MonoBehaviour {
    public static GameManager Instance;

    [Header("UI Panels")]
    public GameObject instructionPanel;
    public GameObject trialPanel;
    public GameObject fixationPanel;
    public GameObject feedbackPanel;       // Optional: for additional visual feedback
    public GameObject interRunPanel;       // Inter-run panel for breaks
    public InstructionManager instructionManager;

    [Header("UI Texts")]
    public TMP_Text instructionText;
    public TMP_Text trialInfoText;
    public TMP_Text interRunText;

    [Header("Buttons")]
    public Button optionAButton;
    public Button optionBButton;
    public Button endExperimentButton;

    [Header("Task Settings")]
    public TaskType currentTask = TaskType.Deception;
    public int currentSeries = 1; // Added: To select instruction series (1 or 2)
    public int totalTrials = 45;  // Total number of *regular* trials (updated from loaded data)
    public int trialsPerRun = 9;  // Number of *events* (trials + tests) per run
    public float trialOnsetDuration = 2f;
    public float decisionConfirmationMin = 2f;
    public float decisionConfirmationMax = 4f;
    public float fixationMin = 2f;
    public float fixationMax = 4f;
    public float interRunInterval = 10f;
    public float closeDelay = 10f; // Delay before closing after final message

    // Cached components
    private Image optionAButtonImage;
    private Image optionBButtonImage;
    private TMP_Text optionAButtonText;
    private TMP_Text optionBButtonText;
    private EventSystem eventSystem;
    private BarChartManager barChartManager;
    private LegendManager legendManager;

    // Optimized data structures
    private List<SignallingTaskData.TrialData> currentTrialList; // Holds the shuffled regular trials
    private List<string> trialResponses = new List<string>(); // Stores response ("A" or "B") for each event (trial or test) in order
    private List<SignallingTaskData.AttentionTestData> attentionTests = new List<SignallingTaskData.AttentionTestData>(); // Holds loaded attention tests
    private HashSet<int> attentionTestIndices = new HashSet<int>(); // Stores the 0-based *event index* where attention tests occur
    private Dictionary<int, int> attentionTestIndexToTestIndex = new Dictionary<int, int>(); // Maps event index to index in attentionTests list

    private bool decisionMade = false; // Flag: true when participant makes choice in current trial/test
    private bool selectionEnabled = false; // Flag: true when buttons/keys are active for input
    private float decisionStartTime; // Time.time when decision phase starts

    // Flag to ensure GameManager setup (including localization AND data loading) is complete
    private bool isInitialized = false;
    private bool isDataLoaded = false; // New flag specifically for data loading
    private bool hasReceivedOptions = false; // New flag: Ensures options are set before init

    // Localization table reference
    private const string UILocalizationTable = "UI"; // Your table name

    void Awake() {
        Debug.Log("GameManager Awake: Initializing Singleton.");
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: Keep GameManager across scene loads if needed
            InitializeComponents(); // Cache components early, but *don't* load trials yet
            // *** Do NOT start InitializeLocalizationAndUI here anymore ***
            // StartCoroutine(InitializeLocalizationAndUI());
             Debug.Log("GameManager Awake: Singleton set. Waiting for options from SetupManager.");
        } else {
            Debug.LogWarning($"Duplicate GameManager instance detected on GameObject '{gameObject.name}'. Destroying this one.", gameObject); // Log which object is destroyed
            Destroy(gameObject);
            return; // Exit Awake if duplicate
        }
    }

    // *** NEW METHOD: Called by SetupManager ***
    public void StartInitializationWithOptions(TaskType task, int series, string langCode)
    {
        if (hasReceivedOptions)
        {
            Debug.LogWarning("GameManager: StartInitializationWithOptions called more than once!");
            return;
        }

        Debug.Log($"GameManager: Received options - Task: {task}, Series: {series}, Lang: {langCode}");

        // Store the selected options
        this.currentTask = task;
        this.currentSeries = series;
        // Language code will be passed directly to the init coroutine

        hasReceivedOptions = true;

        // NOW start the main initialization process with the chosen language
        StartCoroutine(InitializeLocalizationAndUI(langCode));
    }

    // Combined Initialization Coroutine - Now accepts language code
    IEnumerator InitializeLocalizationAndUI(string initialLangCode) {
        Debug.Log($"InitializeLocalizationAndUI: Starting with Language Code: {initialLangCode}");

        // Ensure options were set (safety check)
        if (!hasReceivedOptions)
        {
            Debug.LogError("InitializeLocalizationAndUI started before options were received!");
            yield break;
        }

        // --- Start Data Loading Concurrently --- 
        StartCoroutine(LoadDataSequentially());

        // 1. Wait for Localization to be ready
        yield return LocalizationSettings.InitializationOperation;
        if (!LocalizationSettings.HasSettings || LocalizationSettings.InitializationOperation.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) {
             Debug.LogError("Localization failed to initialize!");
             yield break; // Stop coroutine
        }
        Debug.Log("InitializeLocalizationAndUI: Localization System Initialized.");

        // 2. Set initial locale using the provided code
        Debug.Log($"InitializeLocalizationAndUI: Setting initial locale to {initialLangCode}...");
        yield return StartCoroutine(SetLocale(initialLangCode)); // Use passed language code

        // 3. Perform initial UI setup (panels are assumed to be correctly inactive except SetupPanel)
        // GameManager might not need to explicitly set panel states here anymore if SetupManager ensures it.
        // instructionPanel.SetActive(true); // Show temporarily for potential IM setup
        // trialPanel.SetActive(false);
        // fixationPanel.SetActive(false);
        // feedbackPanel?.SetActive(false);
        // interRunPanel?.SetActive(false);
        // endExperimentButton?.gameObject.SetActive(false);
        Debug.Log("InitializeLocalizationAndUI: Initial Panel States assumed correct.");

        // 4. Final Async UI Setup
         yield return SetupUIAsync();
        Debug.Log("InitializeLocalizationAndUI: Async UI Setup Complete.");

        // --- Wait for Data Loading to Finish --- 
        Debug.Log("InitializeLocalizationAndUI: Waiting for data loading...");
        yield return new WaitUntil(() => isDataLoaded);
        Debug.Log("InitializeLocalizationAndUI: Data loading confirmed complete.");

        // 5. Indicate that core initialization is complete
        isInitialized = true;
         Debug.Log("-----------------------------------------");
        Debug.Log("GameManager Core Initialized (including data).");
        Debug.Log($"Task Type: {currentTask}, Series: {currentSeries}"); // Now reflects user choice
        Debug.Log($"Locale: {LocalizationSettings.SelectedLocale?.Identifier.Code ?? "Not Set"}"); // Reflects user choice
        Debug.Log($"Total Regular Trials Loaded: {currentTrialList?.Count ?? 0}");
        Debug.Log($"Total Attention Tests Loaded: {attentionTests?.Count ?? 0}");
        Debug.Log("-----------------------------------------");

        // 6. Initialize and Start Instructions via InstructionManager
        if (instructionManager != null) {
            // Use the already set locale
            if (LocalizationSettings.SelectedLocale != null) {
                string langCode = LocalizationSettings.SelectedLocale.Identifier.Code;
                Debug.Log($"InitializeLocalizationAndUI: Initializing instructions via InstructionManager for Series: {currentSeries}, Task: {currentTask}, Lang: {langCode}");

                instructionManager.gameObject.SetActive(true);
                instructionManager.InitializeInstructions(currentSeries, currentTask, langCode, this);
                // Ensure the instruction panel (if separate) is active for IM to use.
                 instructionPanel?.SetActive(true);
                Debug.Log("InitializeLocalizationAndUI: Handed control to InstructionManager. Waiting for completion signal.");

            } else {
                 Debug.LogError("InitializeLocalizationAndUI: SelectedLocale became null after setting! Cannot init instructions.");
                 StartGameInternal(); // Skip instructions
            }
        } else {
             Debug.LogWarning("InitializeLocalizationAndUI: InstructionManager reference missing. Skipping instructions phase.");
             instructionPanel?.SetActive(false); // Hide instruction panel if no IM
             StartGameInternal(); // Start trials directly
         }
    }

    // New Coroutine to handle data loading dependency
    IEnumerator LoadDataSequentially()
    {
        Debug.Log("LoadDataSequentially: Waiting for SignallingTrialLoader instance...");
        // Wait until the singleton instance is assigned
        yield return new WaitUntil(() => SignallingTaskData.SignallingTrialLoader.Instance != null);
        Debug.Log("LoadDataSequentially: SignallingTrialLoader instance found.");

        // Optional: Add a small delay or wait a frame to increase certainty Awake completed
        yield return null; // Wait one frame

        // Now load the data
        Debug.Log("LoadDataSequentially: Calling LoadAndShuffleTrials...");
        LoadAndShuffleTrials();
        Debug.Log("LoadDataSequentially: Calling InsertAttentionTests...");
        InsertAttentionTests();

        isDataLoaded = true; // Set the flag
        Debug.Log("LoadDataSequentially: Data loading finished.");
    }

    // Helper to set the locale and refresh UI
    IEnumerator SetLocale(string languageCode) {
        var locale = LocalizationSettings.AvailableLocales.GetLocale(languageCode);
        if (locale != null) {
            var currentLocale = LocalizationSettings.SelectedLocale;
            if (currentLocale != locale)
            {
                 Debug.Log($"Changing locale from '{currentLocale?.Identifier.Code ?? "None"}' to '{languageCode}'...");
                LocalizationSettings.SelectedLocale = locale;
                // Wait until the locale has changed and tables are loaded
                yield return LocalizationSettings.InitializationOperation; // Crucial wait
                 // Verify change
                 if (LocalizationSettings.SelectedLocale == locale) {
                    Debug.Log($"Locale change to '{languageCode}' successful.");
                 } else {
                     Debug.LogError($"Locale change to '{languageCode}' failed! Current locale is still '{LocalizationSettings.SelectedLocale?.Identifier.Code ?? "None"}'.");
                     yield break; // Stop if locale change failed
                 }
                // Refresh UI elements that depend on localization
                yield return StartCoroutine(RefreshLocalizedUICoroutine()); // Wait for refresh
            } else {
                 Debug.Log($"Locale already set to: {languageCode}. Refreshing UI just in case.");
                 // Even if locale is the same, refresh might be needed during initial setup
                 yield return StartCoroutine(RefreshLocalizedUICoroutine());
            }
        } else {
            Debug.LogWarning($"Locale Code '{languageCode}' not found in Available Locales. Cannot set locale.");
        }
    }

    // Coroutine to refresh localized UI elements
    IEnumerator RefreshLocalizedUICoroutine()
    {
         Debug.Log("RefreshLocalizedUICoroutine: Starting UI refresh.");
         // Ensure initialization is complete before refreshing complex elements
         yield return new WaitUntil(() => LocalizationSettings.InitializationOperation.IsDone);

        // Refresh Legend (if applicable and ready)
        if (legendManager != null)
        {
             Debug.Log("Refreshing Legend...");
            // Assuming RefreshLegend is now async Task or returns a Task
             // Correct way to handle Task within Coroutine: Start Task, then yield until done
             Task legendTask = legendManager.RefreshLegend(); // Assuming it returns Task
             if (legendTask != null) { // Check if the task was actually returned
                 yield return new WaitUntil(() => legendTask.IsCompleted); // Wait for the task
                 if (legendTask.IsFaulted) Debug.LogError($"Legend refresh failed: {legendTask.Exception?.Message ?? "Unknown Error"}");
                 else if (!legendTask.IsCanceled) Debug.Log("Legend Refresh Completed.");
                 else Debug.LogWarning("Legend Refresh Task Canceled.");
             } else {
                 Debug.LogWarning("legendManager.RefreshLegend() did not return a Task.");
             }
        }

        // Refresh other UI elements by calling SetupUIAsync again, which fetches current localized strings
        yield return SetupUIAsync(); // Wait for async operation
        Debug.Log("RefreshLocalizedUICoroutine: UI Refresh Complete.");
    }

    private void InitializeComponents() {
        Debug.Log("InitializeComponents: Caching components."); // Note: Not loading data here anymore
        // Cache frequently accessed components
        optionAButtonImage = optionAButton?.GetComponent<Image>(); // Use null-conditional
        optionBButtonImage = optionBButton?.GetComponent<Image>();
        optionAButtonText = optionAButton?.GetComponentInChildren<TMP_Text>();
        optionBButtonText = optionBButton?.GetComponentInChildren<TMP_Text>();
        eventSystem = EventSystem.current; // Get current EventSystem
        barChartManager = FindObjectOfType<BarChartManager>();
        legendManager = FindObjectOfType<LegendManager>();
        instructionManager = FindObjectOfType<InstructionManager>();

         // Validate required components
         if (optionAButton == null || optionBButton == null || optionAButtonImage == null || optionBButtonImage == null || optionAButtonText == null || optionBButtonText == null)
             Debug.LogError("One or more Option Button components (Button, Image, TMP_Text) are not assigned in the Inspector!");
         if (instructionPanel == null || trialPanel == null || fixationPanel == null)
             Debug.LogError("One or more core UI Panels (Instruction, Trial, Fixation) are not assigned!");
         if (trialInfoText == null) Debug.LogError("TrialInfoText is not assigned!");
         if (eventSystem == null) Debug.LogWarning("No EventSystem found in the scene. Keyboard/Controller navigation might not work.");
         if (barChartManager == null) Debug.LogWarning("BarChartManager not found in the scene.");
         if (legendManager == null) Debug.LogWarning("LegendManager not found in the scene.");
         if (instructionManager == null) Debug.LogWarning("InstructionManager not found during InitializeComponents. Will check again later.");

        // *** Data loading moved to LoadDataSequentially ***
        // DataLogger.Initialize(); // This might be okay here if it has no dependencies
        // LoadAndShuffleTrials(); 
        // InsertAttentionTests(); 

        // Initial UI state (buttons usually start non-interactive)
        SetButtonInteraction(false); // Start with buttons non-interactable
    }

    private void LoadAndShuffleTrials() {
         // Add extra check for instance just in case
         if (SignallingTaskData.SignallingTrialLoader.Instance == null)
         {
            Debug.LogError("LoadAndShuffleTrials: SignallingTrialLoader.Instance is NULL! Cannot load trials.");
            currentTrialList = new List<SignallingTaskData.TrialData>();
            totalTrials = 0;
            return;
         }

         Debug.Log($"LoadAndShuffleTrials: Loading trials for TaskType: {currentTask} using SignallingTrialLoader.Instance");
         // Accessing via Singleton Instance
         if (currentTask == TaskType.Deception) {
             if (SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials != null && SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials.Count > 0) {
                 currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials);
                 Debug.Log($"Loaded {currentTrialList.Count} Deception trials.");
             } else {
                 Debug.LogError("SignallingTrialLoader.Instance.DeceptionTrials is null or empty! Cannot proceed.");
                 currentTrialList = new List<SignallingTaskData.TrialData>(); // Prevent null reference errors
             }
         } else { // Control Task
             if (SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials != null && SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials.Count > 0) {
                 currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials);
                 Debug.Log($"Loaded {currentTrialList.Count} Control trials.");
             } else {
                 Debug.LogWarning("SignallingTrialLoader.Instance.ControlTrials is null or empty. Using Deception trials as fallback for Control task.");
                 // Fallback to Deception trials if Control trials are missing
                 if (SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials != null && SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials.Count > 0) {
                     currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials);
                 } else {
                     Debug.LogError("Fallback failed: SignallingTrialLoader.Instance.DeceptionTrials is also null or empty! Cannot proceed.");
                     currentTrialList = new List<SignallingTaskData.TrialData>();
                 }
             }
         }

        // Shuffle the loaded list
        ShuffleTrials(currentTrialList); // Shuffle method handles logging success/failure

        // Update totalTrials based on the actual loaded count
        totalTrials = currentTrialList?.Count ?? 0;
         Debug.Log($"Actual number of regular trials to run: {totalTrials}");
    }

    void Start() {
         // Most initialization is now handled in Awake -> InitializeLocalizationAndUI
         Debug.Log("GameManager Start: Frame 1 execution.");
    }

    // Method signature is async Task SetupUIAsync()
    async Task SetupUIAsync() { // Mark as async Task
        Debug.Log("SetupUIAsync: Setting up async localized text.");
        // Ensure localization is ready before getting strings
        if (!LocalizationSettings.HasSettings || !LocalizationSettings.InitializationOperation.IsDone) {
             Debug.LogWarning("SetupUIAsync: Waiting for Localization initialization...");
              var initOp = LocalizationSettings.InitializationOperation;
              if (initOp.IsValid() && !initOp.IsDone) {
                   // Correct way to wait for Task inside async method
                   await initOp.Task;
              }
              // Add additional check/wait if Task is not reliable
              while(!LocalizationSettings.InitializationOperation.IsDone) {
                  await Task.Yield(); // Yield execution until next frame
              }
        }

        // Setup InterRunPanel Text (if panel exists and text component exists)
        if (interRunPanel != null && interRunText != null) {
            // Corrected code using await:
            try {
                 interRunText.text = await GetLocalizedStringAsync(UILocalizationTable, "inter_run_text");
            }
            catch (System.Exception ex) {
                Debug.LogError($"Failed to get inter-run text: {ex.Message}");
                interRunText.text = "[inter_run_text]"; // Fallback text
            }
             Debug.Log("SetupUIAsync: Inter-run text set.");
        }

        // Setup InstructionPanel Text (if panel exists and text component exists)
        // Let InstructionManager handle its own text/images primarily.
        // if (instructionPanel != null && instructionText != null) {
            // instructionText.text = await GetLocalizedStringAsync(UILocalizationTable, "welcome_text");
        // }

        // Other async UI setup tasks can go here...
        Debug.Log("SetupUIAsync: Completed.");
    }

     void Update() {
         // Handle real-time input checks, like keyboard navigation during the decision phase
         if (selectionEnabled) { // Only process input if selection is allowed
            HandleKeyboardNavigation();
         }

         // Example: Allow quitting with Escape key (optional)
         if (Input.GetKeyDown(KeyCode.Escape)) {
             Debug.Log("Escape key pressed. Requesting quit.");
             StartCoroutine(SaveAndQuitCoroutine("EscapeKey")); // Save data before quitting
         }
     }

     // INTERNAL METHOD: Called by InstructionManager AFTER instructions are DONE, or by InitializeLocalizationAndUI if skipping instructions.
     public void StartGameInternal() {
         Debug.Log("StartGameInternal: Received signal to start the main trial loop.");
          // Ensure instruction panel is hidden before starting trials
         if (instructionPanel != null) {
             instructionPanel.SetActive(false);
             Debug.Log("StartGameInternal: Instruction Panel hidden.");
         } else {
             Debug.LogWarning("StartGameInternal: InstructionPanel reference is null, cannot hide it.");
         }
          // Ensure Instruction Manager GameObject is deactivated if it exists
          if (instructionManager != null) {
               instructionManager.gameObject.SetActive(false);
               Debug.Log("StartGameInternal: InstructionManager GameObject deactivated.");
          }

         // Start the coroutine that runs all trials and attention tests
        StartCoroutine(RunAllTrials());
    }

    // Main Coroutine for running the sequence of trials and attention tests.
    IEnumerator RunAllTrials() {
         Debug.Log("RunAllTrials: Starting experiment run sequence.");

        // Calculate total number of events (regular trials + attention tests)
        int actualRegularTrials = currentTrialList?.Count ?? 0;
        int actualAttentionTests = attentionTests?.Count ?? 0;
        int totalEvents = actualRegularTrials + actualAttentionTests;

         if (totalEvents == 0) {
             Debug.LogError("RunAllTrials: No trials or attention tests loaded/found. Cannot run experiment.");
             EndTrials(); // Go directly to the end sequence
             yield break; // Exit coroutine
         }
         // Update the 'totalTrials' variable if it was just a placeholder initially
         if (totalTrials != actualRegularTrials) {
              Debug.LogWarning($"Mismatch between Inspector totalTrials ({totalTrials}) and loaded trials ({actualRegularTrials}). Using loaded count for event calculations.");
              // Keep Inspector totalTrials for reference? Or update it? Let's use actual count internally.
         }


        // Calculate total runs needed based on events per run
        // Ensure trialsPerRun is positive to avoid division by zero or infinite loops
        if (trialsPerRun <= 0) {
             Debug.LogError($"RunAllTrials: Invalid 'trialsPerRun' value ({trialsPerRun}). Setting to {totalEvents} to run all in one go.");
             trialsPerRun = totalEvents; // Avoid division by zero
         }
         // Use float division for Ceiling operation
        int totalRuns = (trialsPerRun > 0) ? Mathf.CeilToInt((float)totalEvents / trialsPerRun) : 1;


        Debug.Log($"RunAllTrials: {totalEvents} total events ({actualRegularTrials} regular, {actualAttentionTests} attention). {trialsPerRun} events/run. {totalRuns} runs total.");

        int eventCounter = 0; // Track overall event number (1-based for logging)

        // Loop through each run
        for (int run = 0; run < totalRuns; run++) {
            Debug.Log($"---------- Starting Run {run + 1} / {totalRuns} ----------");
            // Loop through each event within the run
            for (int trialInRun = 0; trialInRun < trialsPerRun; trialInRun++) {
                int eventIndex = run * trialsPerRun + trialInRun; // Calculate the 0-based index in the overall event sequence

                 // Stop if we've processed all planned events
                 if (eventIndex >= totalEvents) {
                     Debug.Log($"Run {run + 1}: Reached end of event list ({eventIndex}/{totalEvents}). Ending run early.");
                     break; // Exit inner loop for this run
                 }

                 eventCounter = eventIndex + 1; // 1-based counter for logging

                 Debug.Log($"Run {run + 1}, Event {eventCounter}/{totalEvents} (Event Index: {eventIndex})");

                // Check if the current event index corresponds to an attention test
                if (attentionTestIndices.Contains(eventIndex)) {
                    // It's an attention test
                    if (attentionTestIndexToTestIndex.TryGetValue(eventIndex, out int testIndex)) {
                         // Ensure the test index is valid
                         if (testIndex >= 0 && testIndex < attentionTests.Count) {
                            Debug.Log($"Running Attention Test (List Index: {testIndex})");
                            yield return StartCoroutine(RunAttentionTest(attentionTests[testIndex], eventCounter)); // Pass 1-based counter
                         } else {
                             Debug.LogError($"Invalid attention test index {testIndex} mapped for event index {eventIndex}. Skipping.");
                         }
                    } else {
                         Debug.LogError($"Attention test index found in Set ({eventIndex}) but not in Dictionary. Data mismatch! Skipping.");
                    }
                } else {
                    // It's a regular trial
                    int adjustedIndex = GetAdjustedTrialIndex(eventIndex); // Calculate index into currentTrialList

                    // Safety check for the adjusted index
                    if (adjustedIndex >= 0 && adjustedIndex < currentTrialList.Count) {
                        Debug.Log($"Running Regular Trial (List Index: {adjustedIndex})");
                        yield return StartCoroutine(RunTrial(currentTrialList[adjustedIndex], eventCounter)); // Pass 1-based counter
                    } else {
                        Debug.LogError($"Adjusted trial index {adjustedIndex} is out of bounds (0 to {currentTrialList.Count - 1}) for event index {eventIndex}. Skipping event.");
                        // Log an error response for this missing event?
                         if (trialResponses.Count == eventCounter -1) // Only add if it matches the sequence
                            trialResponses.Add("Error/Skipped");
                        else
                             Debug.LogError($"Could not add Error/Skipped response, response count ({trialResponses.Count}) != expected ({eventCounter-1})");
                    }
                }
                 Debug.Log($"---------- Event {eventCounter} Finished ----------");
            } // End loop for trials within a run

            Debug.Log($"---------- Run {run + 1} Finished ----------");

            // --- Inter-run break logic ---
            // Check if it's not the very last run
            if (run < totalRuns - 1) {
                 if (interRunPanel != null && interRunInterval > 0) {
                    Debug.Log($"Starting Inter-Run Break for {interRunInterval} seconds.");
                    // Optionally refresh inter-run text (if dynamic)
                     if (interRunText != null) {
                         // Use Task.Run or similar if needed inside coroutine, or ensure GetLocalizedStringAsync handles it
                         // Calling async directly and awaiting should be fine if GetLocalizedStringAsync is Task-based
                         // For safety, wrap in a sub-coroutine or handle potential blocking
                         Task<string> textTask = GetLocalizedStringAsync(UILocalizationTable, "inter_run_text");
                         yield return new WaitUntil(() => textTask.IsCompleted);
                         if (!textTask.IsFaulted && !textTask.IsCanceled) interRunText.text = textTask.Result;
                         else Debug.LogWarning("Failed to get inter-run text.");
                     }
                    interRunPanel.SetActive(true);
                    // Hide other panels during break
                    trialPanel?.SetActive(false);
                    fixationPanel?.SetActive(false);
                    yield return new WaitForSeconds(interRunInterval); // Wait for the specified duration
                    interRunPanel.SetActive(false);
                    Debug.Log("Inter-Run Break Finished.");
                 } else {
                    // No panel assigned or interval is zero, skip the break
                     if (interRunPanel == null) Debug.LogWarning("InterRunPanel not assigned. Skipping break.");
                     else Debug.Log("InterRunInterval is 0. Skipping break.");
                 }
            } else {
                 Debug.Log($"Finished last run ({run + 1}/{totalRuns}). No more breaks.");
            }
        } // End loop for runs

        Debug.Log("RunAllTrials: All runs completed.");
        EndTrials(); // Proceed to the end-of-experiment sequence
    }

    // Calculates the correct index into the `currentTrialList` based on the overall event index,
    // accounting for the positions of inserted attention tests.
    private int GetAdjustedTrialIndex(int eventIndex) {
        int adjustment = 0;
        // Count how many attention tests have occurred *before* the current eventIndex
        foreach (int testIndex in attentionTestIndices) {
            if (eventIndex > testIndex) {
                adjustment++;
            }
        }
        // The adjusted index is the event index minus the number of preceding attention tests
        int adjusted = eventIndex - adjustment;
         // Log calculation for debugging
         // Debug.Log($"GetAdjustedTrialIndex: EventIndex={eventIndex}, PrecedingTests={adjustment}, AdjustedIndex={adjusted}");
         return adjusted;
    }

    // Coroutine to execute a single regular trial.
    IEnumerator RunTrial(SignallingTaskData.TrialData trial, int eventNumber) {
        // Calculate total event count once for this trial's display
        int totalEventCount = (currentTrialList?.Count ?? 0) + (attentionTests?.Count ?? 0);

        Debug.Log($"RunTrial {eventNumber}/{totalEventCount}: Start. Type: {currentTask}. A:[{trial.optionA_Self},{trial.optionA_Other}], B:[{trial.optionB_Self},{trial.optionB_Other}]");
        // --- Phase 1: Onset ---
        selectionEnabled = false; // Disable input
        decisionMade = false;     // Reset decision flag
        trialPanel.SetActive(true);  // Show trial elements
        fixationPanel.SetActive(false); // Hide fixation cross

        // Update Trial Info Text (e.g., "Trial 5 of 50")
         Task<string> trialInfoFormatTask = GetLocalizedStringAsync(UILocalizationTable, "trial_info");
         yield return new WaitUntil(() => trialInfoFormatTask.IsCompleted);
         if (!trialInfoFormatTask.IsFaulted && !trialInfoFormatTask.IsCanceled) {
             trialInfoText.text = string.Format(trialInfoFormatTask.Result, eventNumber, totalEventCount);
         } else { trialInfoText.text = $"Event {eventNumber}/{totalEventCount}"; }


        // Update Bar Chart
        if (barChartManager != null) {
            barChartManager.CreateBarChart(trial.optionA_Self, trial.optionA_Other, trial.optionB_Self, trial.optionB_Other);
        } else { Debug.LogWarning($"RunTrial {eventNumber}: BarChartManager not found."); }

        // Update Button Text based on Task Type
        string optionAKey = (currentTask == TaskType.Deception) ? "deception_option_a" : "control_option_a";
        string optionBKey = (currentTask == TaskType.Deception) ? "deception_option_b" : "control_option_b";
        // Fetch both texts concurrently
        Task<string> optionATextTask = GetLocalizedStringAsync(UILocalizationTable, optionAKey);
        Task<string> optionBTextTask = GetLocalizedStringAsync(UILocalizationTable, optionBKey);
        yield return new WaitUntil(() => optionATextTask.IsCompleted && optionBTextTask.IsCompleted);

        SetButtonText(optionAButtonText, optionATextTask.IsCompletedSuccessfully ? optionATextTask.Result : $"[{optionAKey}]");
        SetButtonText(optionBButtonText, optionBTextTask.IsCompletedSuccessfully ? optionBTextTask.Result : $"[{optionBKey}]");


        // Set buttons to non-interactive, semi-transparent state during onset
        SetButtonInteraction(false);

        Debug.Log($"RunTrial {eventNumber}: Onset Phase ({trialOnsetDuration}s)");
        yield return new WaitForSeconds(trialOnsetDuration);

        // --- Phase 2: Decision ---
        Debug.Log($"RunTrial {eventNumber}: Decision Phase Start (Waiting for input)");
        SetupTrialButtons(); // Enables input, sets visuals, starts RT timer

        // Wait until OnDecisionMade sets decisionMade = true
        yield return new WaitUntil(() => decisionMade);
        float responseTime = Time.time - decisionStartTime; // Calculate RT
        selectionEnabled = false; // Disable input immediately after decision

         // Ensure response was recorded correctly before logging
         string messageChosen = "Error/LogMismatch";
         if (trialResponses.Count >= eventNumber) { // Check if index exists (1-based vs 0-based list)
             messageChosen = trialResponses[eventNumber - 1];
         } else {
             Debug.LogError($"Response log missing for event {eventNumber}! Log count: {trialResponses.Count}.");
             // Attempt to add placeholder if possible
             if (trialResponses.Count == eventNumber -1) trialResponses.Add(messageChosen);
         }
        Debug.Log($"RunTrial {eventNumber}: Decision Made. Choice: {messageChosen}, RT: {responseTime:F3}s");

        // First, log the basic trial data
        DataLogger.LogTrial(eventNumber, currentTask.ToString(), messageChosen, responseTime);
        // Then log additional data about the options if needed (this depends on your DataLogger implementation)
        // You might need to create a new method in DataLogger to log the additional parameters

        // --- Phase 3: Confirmation (Decision shown, input disabled) ---
        float confirmationDuration = Random.Range(decisionConfirmationMin, decisionConfirmationMax);
        Debug.Log($"RunTrial {eventNumber}: Confirmation Phase ({confirmationDuration:F2}s)");
        // Buttons remain visible but non-interactive (already set by selectionEnabled = false / SetButtonInteraction)
        // Highlight might be active here from OnDecisionMade if implemented
        yield return new WaitForSeconds(confirmationDuration);

        // --- Phase 4: Fixation ---
        Debug.Log($"RunTrial {eventNumber}: Fixation Phase Start");
        trialPanel.SetActive(false); // Hide trial elements
        fixationPanel.SetActive(true); // Show fixation cross
        float fixationDuration = Random.Range(fixationMin, fixationMax);
        yield return new WaitForSeconds(fixationDuration);
        Debug.Log($"RunTrial {eventNumber}: Fixation Phase End ({fixationDuration:F2}s)");
        fixationPanel.SetActive(false); // Hide fixation cross

         Debug.Log($"RunTrial {eventNumber}: Complete.");
    }

    // Coroutine to execute a single attention test trial.
    IEnumerator RunAttentionTest(SignallingTaskData.AttentionTestData test, int eventNumber) {
         int totalEventCount = (currentTrialList?.Count ?? 0) + (attentionTests?.Count ?? 0);
         Debug.Log($"RunAttentionTest {eventNumber}/{totalEventCount}: Start. Correct: {test.correctAnswer}. A:[{test.optionA_Self},{test.optionA_Other}], B:[{test.optionB_Self},{test.optionB_Other}]");
        // --- Phase 1: Onset ---
        selectionEnabled = false;
        decisionMade = false;
        trialPanel.SetActive(true);
        fixationPanel.SetActive(false);

        // Update Trial Info Text
        Task<string> trialInfoFormatTask = GetLocalizedStringAsync(UILocalizationTable, "trial_info");
        yield return new WaitUntil(() => trialInfoFormatTask.IsCompleted);
         if (!trialInfoFormatTask.IsFaulted && !trialInfoFormatTask.IsCanceled) {
             trialInfoText.text = string.Format(trialInfoFormatTask.Result, eventNumber, totalEventCount);
         } else { trialInfoText.text = $"Event {eventNumber}/{totalEventCount}"; }

        // Update Bar Chart
        if (barChartManager != null) {
            barChartManager.CreateBarChart(test.optionA_Self, test.optionA_Other, test.optionB_Self, test.optionB_Other);
        } else { Debug.LogWarning($"RunAttentionTest {eventNumber}: BarChartManager not found."); }

        // Update Button Text (using specific keys for attention tests)
        Task<string> optionATextTask = GetLocalizedStringAsync(UILocalizationTable, "attention_option_a");
        Task<string> optionBTextTask = GetLocalizedStringAsync(UILocalizationTable, "attention_option_b");
        yield return new WaitUntil(() => optionATextTask.IsCompleted && optionBTextTask.IsCompleted);

        SetButtonText(optionAButtonText, optionATextTask.IsCompletedSuccessfully ? optionATextTask.Result : "[attention_option_a]");
        SetButtonText(optionBButtonText, optionBTextTask.IsCompletedSuccessfully ? optionBTextTask.Result : "[attention_option_b]");


        // Set buttons non-interactive during onset
        SetButtonInteraction(false);

        Debug.Log($"RunAttentionTest {eventNumber}: Onset Phase ({trialOnsetDuration}s)");
        yield return new WaitForSeconds(trialOnsetDuration);

        // --- Phase 2: Decision ---
        Debug.Log($"RunAttentionTest {eventNumber}: Decision Phase Start (Waiting for input)");
        SetupTrialButtons(); // Enables input, sets visuals, starts timer

        yield return new WaitUntil(() => decisionMade);
        float responseTime = Time.time - decisionStartTime;
        selectionEnabled = false; // Disable input

         // Ensure response was recorded correctly before logging
         string response = "Error/LogMismatch";
          if (trialResponses.Count >= eventNumber) {
              response = trialResponses[eventNumber - 1];
          } else {
              Debug.LogError($"Response log missing for event {eventNumber}! Log count: {trialResponses.Count}.");
               if (trialResponses.Count == eventNumber -1) trialResponses.Add(response);
          }
        bool correct = response == test.correctAnswer;
        Debug.Log($"RunAttentionTest {eventNumber}: Decision Made. Choice: {response}, Correct: {correct}, RT: {responseTime:F3}s");

        // Log the attention test data
        DataLogger.LogAttentionTest(eventNumber, response, responseTime, correct);

        // --- Phase 3: Confirmation ---
        float confirmationDuration = Random.Range(decisionConfirmationMin, decisionConfirmationMax);
        Debug.Log($"RunAttentionTest {eventNumber}: Confirmation Phase ({confirmationDuration:F2}s)");
        yield return new WaitForSeconds(confirmationDuration);

        // --- Phase 4: Fixation ---
        Debug.Log($"RunAttentionTest {eventNumber}: Fixation Phase Start");
        trialPanel.SetActive(false);
        fixationPanel.SetActive(true);
        float fixationDuration = Random.Range(fixationMin, fixationMax);
        yield return new WaitForSeconds(fixationDuration);
        Debug.Log($"RunAttentionTest {eventNumber}: Fixation Phase End ({fixationDuration:F2}s)");
        fixationPanel.SetActive(false);

         Debug.Log($"RunAttentionTest {eventNumber}: Complete.");
    }

     // Helper to manage button interactability and visual state (transparency)
     void SetButtonInteraction(bool interactable) {
         float alpha = interactable ? 1.0f : 0.5f; // Full alpha when interactable, half when not
         if (optionAButton != null) {
             optionAButton.interactable = interactable;
             if (optionAButtonImage != null) SetButtonTransparency(optionAButtonImage, alpha);
         }
         if (optionBButton != null) {
             optionBButton.interactable = interactable;
             if (optionBButtonImage != null) SetButtonTransparency(optionBButtonImage, alpha);
         }
          // Debug.Log($"SetButtonInteraction: Interactable = {interactable}, Alpha = {alpha}");
     }


    // Configures buttons for the start of a decision phase.
    private void SetupTrialButtons() {
        // 1. Enable Interaction State
        selectionEnabled = true;    // Allow Update loop to process HandleKeyboardNavigation
        SetButtonInteraction(true); // Make buttons clickable and fully opaque

        // 2. Reset Visuals (Highlights) - Optional
        // If using custom highlight logic via SetButtonHighlight, reset it here.
        // If using Unity's Button Color Tint system, it handles transitions automatically.
         // SetButtonHighlight(optionAButtonImage, Color.clear); // Example reset if using custom highlights
         // SetButtonHighlight(optionBButtonImage, Color.clear);

        // 3. Reset Decision Logic State
        decisionMade = false;       // Clear the flag for the new decision
        decisionStartTime = Time.time; // Record the precise start time for Response Time (RT)

        // 4. Setup Button Listeners
        optionAButton?.onClick.RemoveAllListeners();
        optionBButton?.onClick.RemoveAllListeners();
        optionAButton?.onClick.AddListener(() => OnDecisionMade("A"));
        optionBButton?.onClick.AddListener(() => OnDecisionMade("B"));


        // 5. Set Initial Focus for Keyboard/Controller Navigation
        if (eventSystem != null && optionAButton != null && optionAButton.interactable) {
             eventSystem.SetSelectedGameObject(optionAButton.gameObject); // Set focus to Option A
             // Debug.Log("SetupTrialButtons: Initial focus set to Option A Button.");
        } else {
             // Log warning if focus cannot be set
             if (eventSystem == null) Debug.LogWarning("SetupTrialButtons: EventSystem is null. Cannot set initial focus.");
             else if (optionAButton == null) Debug.LogWarning("SetupTrialButtons: Option A Button is null. Cannot set initial focus.");
             // else Debug.LogWarning("SetupTrialButtons: Option A Button not interactable. Cannot set focus."); // Button state checked by interactable flag
        }
         // Debug.Log("SetupTrialButtons: Buttons ready for decision.");
    }


    // Called when Option A/B button is clicked OR corresponding key is pressed.
    void OnDecisionMade(string choice) {
        // Prevent multiple decisions for the same trial/test
        if (!selectionEnabled || decisionMade) {
             // Debug.Log($"OnDecisionMade: Input '{choice}' ignored. SelectionEnabled={selectionEnabled}, DecisionMade={decisionMade}");
             return;
        }

        decisionMade = true; // Set flag to signal completion in waiting coroutines
        // selectionEnabled = false; // Disable further input (This is now handled *after* the WaitUntil in RunTrial/RunTest)

        // Record the participant's response ("A" or "B")
        trialResponses.Add(choice);
        // Debug.Log($"OnDecisionMade: Choice '{choice}' recorded. Total responses: {trialResponses.Count}");


        // Optional: Provide immediate visual feedback for the selected button
        // Example: Change highlight color using SetButtonHighlight (requires reset in SetupTrialButtons)
        // Or trigger the Button component's "Pressed" state transition if configured.
         // Button selectedButton = (choice == "A") ? optionAButton : optionBButton;
         // selectedButton?.onClick.Invoke(); // This might re-trigger OnDecisionMade if not careful
         // Consider a dedicated highlighting method instead if needed.

        // Do NOT disable buttons or change selectionEnabled here; let the calling coroutine manage state transitions.
    }


    // Handles keyboard input during the decision phase (when selectionEnabled is true).
    void HandleKeyboardNavigation() {
        // Direct Mapping: Left Arrow -> Option A, Right Arrow -> Option B
        if (Input.GetKeyDown(KeyCode.LeftArrow)) {
             // Debug.Log("HandleKeyboardNavigation: Left Arrow Pressed.");
             if (optionAButton != null && optionAButton.interactable) {
                 // eventSystem?.SetSelectedGameObject(optionAButton.gameObject); // Optional visual focus move
                 OnDecisionMade("A"); // Directly trigger the decision logic
             }
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow)) {
             // Debug.Log("HandleKeyboardNavigation: Right Arrow Pressed.");
             if (optionBButton != null && optionBButton.interactable) {
                 // eventSystem?.SetSelectedGameObject(optionBButton.gameObject); // Optional visual focus move
                 OnDecisionMade("B"); // Directly trigger the decision logic
             }
        }
    }


    // Called after the last trial/test in the RunAllTrials coroutine.
    void EndTrials() {
         Debug.Log("EndTrials: All trial runs complete. Preparing end sequence.");
         // Hide active game panels
         trialPanel?.SetActive(false);
         fixationPanel?.SetActive(false);
         interRunPanel?.SetActive(false); // Hide break panel if it was last

        // Show the "End Experiment" button if assigned
        if (endExperimentButton != null) {
            Debug.Log("EndTrials: Activating End Experiment Button.");
            endExperimentButton.gameObject.SetActive(true);
            Button endButtonComponent = endExperimentButton.GetComponent<Button>();
            if (endButtonComponent != null) {
                 endButtonComponent.interactable = true; // Ensure it's clickable
                 // Setup listener for the button click
                 endButtonComponent.onClick.RemoveAllListeners(); // Clear any old listeners
                 endButtonComponent.onClick.AddListener(EndExperiment); // Add the handler
                 // Set focus to the end button for keyboard/controller users
                 if (eventSystem != null) {
                    eventSystem.SetSelectedGameObject(endExperimentButton.gameObject);
                    Debug.Log("EndTrials: Focus set to End Experiment Button.");
                 }
            } else {
                 Debug.LogError("EndTrials: EndExperimentButton prefab is missing the Button component!");
                 StartCoroutine(SaveAndQuitCoroutine("EndButtonComponentMissing"));
            }
        } else {
             Debug.LogWarning("EndTrials: EndExperimentButton was not assigned in the Inspector. Saving and quitting automatically.");
             StartCoroutine(SaveAndQuitCoroutine("EndButtonNotAssigned"));
        }
    }


    // Called when the EndExperiment button is clicked. Handles saving data and showing final message.
    async void EndExperiment() { // Can be async void if just launching tasks/coroutines
         Debug.Log("EndExperiment: Button clicked. Saving data and preparing to exit.");
         // Prevent multiple clicks by disabling the button immediately
         if (endExperimentButton != null) {
             Button endButton = endExperimentButton.GetComponent<Button>();
             if (endButton != null) endButton.interactable = false;
         }

         // --- 1. Save Data ---
         string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
         int randomSuffix = Random.Range(1000, 9999);
         string filename = $"TrialData_Task-{currentTask}_Series-{currentSeries}_{timestamp}_{randomSuffix}.csv";
         DataLogger.SaveData(filename); // Assume SaveData handles logging internally
         Debug.Log($"EndExperiment: Data save requested to '{filename}'.");

         // --- 2. Display Final Message ---
         if (instructionPanel != null && instructionText != null) {
             instructionPanel.SetActive(true);
             // Fetch the localized end text asynchronously
             try {
                 // Use Task.Run to ensure we don't block
                 var endMessageTask = GetLocalizedStringAsync(UILocalizationTable, "end_experiment_text");
                 // We can use await here since EndExperiment is already marked as async
                 instructionText.text = await endMessageTask;
                 Debug.Log("EndExperiment: End message loaded and displayed.");
             }
             catch (System.Exception ex) {
                 Debug.LogError($"Failed to get localized end message: {ex.Message}");
                 instructionText.text = "[end_experiment_text]";
             }
         } else {
             if (instructionPanel == null) Debug.LogWarning("EndExperiment: InstructionPanel not assigned.");
             if (instructionText == null) Debug.LogWarning("EndExperiment: InstructionText not assigned.");
         }

         // --- 3. Schedule Application Close ---
         Debug.Log($"EndExperiment: Scheduling application close in {closeDelay} seconds.");
         StartCoroutine(DelayedClose(closeDelay));
    }

    // Coroutine for saving and quitting automatically (used as fallback).
    IEnumerator SaveAndQuitCoroutine(string reason = "Automatic") {
        Debug.Log($"SaveAndQuitCoroutine: Triggered automatically ({reason}). Saving data and quitting.");
        // Ensure panels are hidden
        trialPanel?.SetActive(false);
        fixationPanel?.SetActive(false);
        instructionPanel?.SetActive(false);

        // Generate filename and save data
        string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
        int randomSuffix = Random.Range(1000, 9999);
        string filename = $"TrialData_Task-{currentTask}_Series-{currentSeries}_{timestamp}_{randomSuffix}_AutoEnd-{reason}.csv";
        DataLogger.SaveData(filename);
        Debug.Log($"SaveAndQuitCoroutine: Data save requested to '{filename}'.");

        // Optional short delay before quitting
        yield return new WaitForSecondsRealtime(2.0f);

        CloseApplication();
    }

    // Coroutine to close the application after a delay.
    IEnumerator DelayedClose(float delay) {
         if (delay < 0) delay = 0;
         Debug.Log($"DelayedClose: Waiting for {delay} seconds before quitting.");
         yield return new WaitForSecondsRealtime(delay);
        CloseApplication();
    }

    // Handles the actual application closing.
    void CloseApplication() {
        Debug.Log("CloseApplication: Attempting to quit.");
#if UNITY_EDITOR
        Debug.Log("Quitting Play Mode (Unity Editor).");
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
         Debug.Log("Application.Quit() ignored in WebGL. Close the browser tab.");
#else
        Debug.Log("Quitting application.");
        Application.Quit();
#endif
    }

    // --- UI Helper Methods ---

    // Sets the displayed text on a TMP_Text component.
    void SetButtonText(TMP_Text textComponent, string text) {
        if (textComponent != null) {
             textComponent.text = text;
        } // else { Debug.LogWarning($"Attempted to set text on a null TMP_Text component. Text was: '{text}'"); } // Reduce log spam
    }

    // Sets the transparency (alpha) of an Image component.
    void SetButtonTransparency(Image buttonImage, float alpha) {
        if (buttonImage != null) {
            Color currentColor = buttonImage.color;
            currentColor.a = Mathf.Clamp01(alpha);
            buttonImage.color = currentColor;
        } // else { Debug.LogWarning("Attempted to set transparency on a null Image component."); } // Reduce log spam
    }


    // --- Trial Management Methods ---

    // Randomly shuffles a list using the Fisher-Yates algorithm.
    void ShuffleTrials(List<SignallingTaskData.TrialData> list) {
        if (list == null || list.Count <= 1) return;

        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1) {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]); // Use tuple swap
        }
         // Debug.Log($"ShuffleTrials: Shuffled {list.Count} trials.");
    }


    // Inserts attention tests into the event sequence based on defined rules.
    private void InsertAttentionTests() {
         // Add extra check for instance just in case
         if (SignallingTaskData.SignallingTrialLoader.Instance == null)
         {
            Debug.LogError("InsertAttentionTests: SignallingTrialLoader.Instance is NULL! Cannot insert tests.");
            attentionTests = new List<SignallingTaskData.AttentionTestData>();
            attentionTestIndices.Clear();
            attentionTestIndexToTestIndex.Clear();
            return;
         }

         // Get tests from DataManager
         // Accessing via Singleton Instance
         if (SignallingTaskData.SignallingTrialLoader.Instance.AttentionTests == null) {
             Debug.LogWarning("SignallingTrialLoader.Instance.AttentionTests is null. Cannot insert tests.");
             attentionTests = new List<SignallingTaskData.AttentionTestData>();
         } else {
            attentionTests = new List<SignallingTaskData.AttentionTestData>(SignallingTaskData.SignallingTrialLoader.Instance.AttentionTests);
         }

        // Clear previous indices
        attentionTestIndices.Clear();
        attentionTestIndexToTestIndex.Clear();

        int numAttentionTests = attentionTests.Count;
        // Use the actual loaded count, not the inspector value which might be outdated
        int numRegularTrials = currentTrialList?.Count ?? 0;

        if (numAttentionTests == 0) {
             // Debug.Log("InsertAttentionTests: No attention tests to insert.");
             return;
        }
        if (numRegularTrials == 0 && numAttentionTests > 0) {
            Debug.LogWarning("InsertAttentionTests: Zero regular trials loaded. Inserting all attention tests at the beginning.");
            for(int i=0; i<numAttentionTests; i++) {
                 attentionTestIndices.Add(i);
                 attentionTestIndexToTestIndex[i] = i;
            }
        } else if (numRegularTrials > 0) {
            // Define insertion parameters
            int minSpacing = 4;
            int maxSpacing = 7;
            int minStartIndex = 4;
            int maxStartIndex = 7;

            // Clamp start index based on available regular trials (cannot start *after* the last regular trial slot)
            minStartIndex = Mathf.Min(minStartIndex, numRegularTrials);
            maxStartIndex = Mathf.Min(maxStartIndex, numRegularTrials);
            if (minStartIndex > maxStartIndex) minStartIndex = maxStartIndex; // Ensure min <= max


            // --- Simple Sequential Placement Logic ---
            System.Random rng = new System.Random();
            int currentEventIndex = (numRegularTrials > 0 && maxStartIndex >= minStartIndex) ? rng.Next(minStartIndex, maxStartIndex + 1) : 0;

            int testsPlaced = 0;
            int totalEventSlots = numRegularTrials + numAttentionTests;

            while (testsPlaced < numAttentionTests && currentEventIndex < totalEventSlots) {
                 // Place the test at the current index (if not already taken)
                 if (!attentionTestIndices.Contains(currentEventIndex)) {
                    attentionTestIndices.Add(currentEventIndex);
                    attentionTestIndexToTestIndex[currentEventIndex] = testsPlaced;
                    testsPlaced++;
                 } else {
                      // Index conflict (should be rare with this logic unless parameters are strange)
                      // Increment and try next slot in the next iteration
                      currentEventIndex++;
                      continue;
                 }

                 // Calculate the position for the *next* test
                 if (testsPlaced < numAttentionTests) {
                     currentEventIndex += rng.Next(minSpacing, maxSpacing + 1);
                 }
            }
            // --- End Simple Sequential Placement ---
        }

         // Log the final placement
         if (attentionTestIndices.Count > 0) {
             List<int> sortedIndices = new List<int>(attentionTestIndices);
             sortedIndices.Sort();
             Debug.Log($"InsertAttentionTests: Inserted {attentionTestIndices.Count} tests at event indices: {string.Join(", ", sortedIndices)}");
         } else if (numAttentionTests > 0) {
             Debug.LogWarning("InsertAttentionTests: Failed to place any attention tests.");
         }
    }


    // --- Localization Helper ---

    // Helper method to get localized string asynchronously with error handling.
     async Task<string> GetLocalizedStringAsync(string tableName, string entryName) {
         // Ensure localization is ready
         if (!LocalizationSettings.HasSettings || LocalizationSettings.StringDatabase == null) {
             Debug.LogError($"GetLocalizedStringAsync: Localization Settings/Database not available! Cannot get '{tableName}/{entryName}'.");
             return $"[{entryName}]"; // Fallback text indicating the key
         }
         // Wait if initialization isn't fully complete yet
          var initOp = LocalizationSettings.InitializationOperation;
          if (initOp.IsValid() && !initOp.IsDone) {
             // Debug.LogWarning($"GetLocalizedStringAsync: Waiting for Localization Op for '{tableName}/{entryName}'...");
             await initOp.Task;
             // Add secondary check as sometimes Task completes but IsDone is false briefly
              while(!initOp.IsDone) { await Task.Yield(); }
         }


         // Attempt to get the string
         var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, entryName);
         string result = null;
          // Await completion, checking if already done to avoid unnecessary await
          if (!operation.IsDone) {
             await operation.Task;
         }
         result = operation.Result;


         // Check result status
         if (operation.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded && result != null) {
             return result; // Success!
         } else {
             // Log failure details
             Debug.LogWarning($"GetLocalizedStringAsync: Failed to get key '{entryName}' from table '{tableName}'. Status: {operation.Status}. Error: {operation.OperationException?.Message ?? "None"}. Locale: {LocalizationSettings.SelectedLocale?.Identifier.Code ?? "N/A"}");
             // Fallback: return the key itself
             return $"[{entryName}]";
         }
     }

} // End of GameManager class
