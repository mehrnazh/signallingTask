//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;
//using TMPro;
//using UnityEngine.EventSystems;
//using UnityEngine.Localization;
//using UnityEngine.Localization.Settings;
//using UnityEngine.Localization.Tables;
//using System.Threading.Tasks;
//using System.Linq;
//using SignallingTaskData;

//// Enum to distinguish between task types.
//public enum TaskType { Deception, Control }

//public class GameManager : MonoBehaviour
//{
//    public static GameManager Instance;

//    [Header("UI Panels")]
//    public GameObject instructionPanel;
//    public GameObject trialPanel;
//    public GameObject fixationPanel;
//    public GameObject feedbackPanel;       // Optional: for additional visual feedback
//    public GameObject interRunPanel;       // Inter-run panel for breaks
//    public InstructionManager instructionManager;

//    [Header("UI Texts")]
//    public TMP_Text instructionText;
//    public TMP_Text trialInfoText;
//    public TMP_Text interRunText;

//    [Header("Buttons")]
//    public Button optionAButton;
//    public Button optionBButton;
//    public Button endExperimentButton;

//    [Header("Task Settings")]
//    public TaskType currentTask = TaskType.Deception;
//    public int currentSeries = 1; // Added: To select instruction series (1 or 2)
//    public int totalTrials = 5;  // Total number of *regular* trials (updated from loaded data)
//    public int trialsPerRun = 1;  // Number of *events* (trials + tests) per run
//    public float trialOnsetDuration = 2f;
//    public float decisionConfirmationMin = 2f;
//    public float decisionConfirmationMax = 4f;
//    public float fixationMin = 2f;
//    public float fixationMax = 4f;
//    public float interRunInterval = 10f;
//    public float closeDelay = 10f; // Delay before closing after final message

//    // Cached components
//    private Image optionAButtonImage;
//    private Image optionBButtonImage;
//    private TMP_Text optionAButtonText;
//    private TMP_Text optionBButtonText;
//    private EventSystem eventSystem;
//    private BarChartManager barChartManager;
//    private LegendManager legendManager;

//    // Optimized data structures
//    private List<SignallingTaskData.TrialData> currentTrialList; // Holds the shuffled regular trials
//    private List<string> trialResponses = new List<string>(); // Stores response ("A" or "B") for each event (trial or test) in order
//    private List<SignallingTaskData.AttentionTestData> attentionTests = new List<SignallingTaskData.AttentionTestData>(); // Holds loaded attention tests
//    private HashSet<int> attentionTestIndices = new HashSet<int>(); // Stores the 0-based *event index* where attention tests occur
//    private Dictionary<int, int> attentionTestIndexToTestIndex = new Dictionary<int, int>(); // Maps event index to index in attentionTests list

//    private bool decisionMade = false; // Flag: true when participant makes choice in current trial/test
//    private bool selectionEnabled = false; // Flag: true when buttons/keys are active for input
//    private float decisionStartTime; // Time.realtimeSinceStartup when decision phase starts

//    // Flag to ensure GameManager setup (including localization AND data loading) is complete
//    private bool isInitialized = false;
//    private bool isDataLoaded = false; // New flag specifically for data loading
//    private bool hasReceivedOptions = false; // New flag: Ensures options are set before init

//    // Store participant ID
//    private string participantId = "DEFAULT_ID"; // Default value

//    // Localization table reference
//    private const string UILocalizationTable = "UI"; // Your table name

//    void Awake()
//    {
//        Debug.Log("GameManager Awake: Initializing Singleton.");
//        if (Instance == null)
//        {
//            Instance = this;
//            DontDestroyOnLoad(gameObject);

//            DataLogger.Initialize();
//            Debug.Log("GameManager Awake: DataLogger Initialized.");

//            InitializeComponents();
//            Debug.Log("GameManager Awake: Singleton set. Waiting for options from SetupManager.");
//        }
//        else
//        {
//            Debug.LogWarning($"Duplicate GameManager instance detected on GameObject '{gameObject.name}'. Destroying this one.", gameObject);
//            Destroy(gameObject);
//            return;
//        }
//    }

//    public void StartInitializationWithOptions(TaskType task, int series, string langCode, string participantId)
//    {
//        if (hasReceivedOptions)
//        {
//            Debug.LogWarning("GameManager: StartInitializationWithOptions called more than once!");
//            return;
//        }

//        Debug.Log($"GameManager: Received options - ID: {participantId}, Task: {task}, Series: {series}, Lang: {langCode}");

//        this.currentTask = task;
//        this.currentSeries = series;
//        this.participantId = participantId;

//        hasReceivedOptions = true;
//        StartCoroutine(InitializeLocalizationAndUI(langCode));
//    }

//    IEnumerator InitializeLocalizationAndUI(string initialLangCode)
//    {
//        Debug.Log($"InitializeLocalizationAndUI: Starting with Language Code: {initialLangCode}");

//        if (!hasReceivedOptions)
//        {
//            Debug.LogError("InitializeLocalizationAndUI started before options were received!");
//            yield break;
//        }

//        StartCoroutine(LoadDataSequentially());

//        yield return LocalizationSettings.InitializationOperation;
//        if (!LocalizationSettings.HasSettings || LocalizationSettings.InitializationOperation.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
//        {
//            Debug.LogError("Localization failed to initialize!");
//            yield break;
//        }
//        Debug.Log("InitializeLocalizationAndUI: Localization System Initialized.");

//        Debug.Log($"InitializeLocalizationAndUI: Setting initial locale to {initialLangCode}...");
//        yield return StartCoroutine(SetLocale(initialLangCode));

//        Debug.Log("InitializeLocalizationAndUI: Initial Panel States assumed correct.");
//        yield return SetupUIAsync();
//        Debug.Log("InitializeLocalizationAndUI: Async UI Setup Complete.");

//        Debug.Log("InitializeLocalizationAndUI: Waiting for data loading...");
//        yield return new WaitUntil(() => isDataLoaded);
//        Debug.Log("InitializeLocalizationAndUI: Data loading confirmed complete.");

//        isInitialized = true;
//        Debug.Log("-----------------------------------------");
//        Debug.Log("GameManager Core Initialized (including data).");
//        Debug.Log($"Participant ID: {this.participantId}");
//        Debug.Log($"Task Type: {currentTask}, Series: {currentSeries}");
//        Debug.Log($"Locale: {LocalizationSettings.SelectedLocale?.Identifier.Code ?? "Not Set"}");
//        Debug.Log($"Total Regular Trials Loaded: {currentTrialList?.Count ?? 0}");
//        Debug.Log($"Total Attention Tests Loaded: {attentionTests?.Count ?? 0}");
//        Debug.Log("-----------------------------------------");

//        if (instructionManager != null)
//        {
//            if (LocalizationSettings.SelectedLocale != null)
//            {
//                string langCode = LocalizationSettings.SelectedLocale.Identifier.Code;
//                Debug.Log($"InitializeLocalizationAndUI: Initializing instructions via InstructionManager for Series: {currentSeries}, Task: {currentTask}, Lang: {langCode}");

//                instructionManager.gameObject.SetActive(true);
//                instructionManager.InitializeInstructions(currentSeries, currentTask, langCode, this);
//                instructionPanel?.SetActive(true);
//                Debug.Log("InitializeLocalizationAndUI: Handed control to InstructionManager. Waiting for completion signal.");

//            }
//            else
//            {
//                Debug.LogError("InitializeLocalizationAndUI: SelectedLocale became null after setting! Cannot init instructions.");
//                StartGameInternal();
//            }
//        }
//        else
//        {
//            Debug.LogWarning("InitializeLocalizationAndUI: InstructionManager reference missing. Skipping instructions phase.");
//            instructionPanel?.SetActive(false);
//            StartGameInternal();
//        }
//    }

//    IEnumerator LoadDataSequentially()
//    {
//        Debug.Log("LoadDataSequentially: Waiting for SignallingTrialLoader instance...");
//        yield return new WaitUntil(() => SignallingTaskData.SignallingTrialLoader.Instance != null);
//        Debug.Log("LoadDataSequentially: SignallingTrialLoader instance found.");

//        yield return null;

//        Debug.Log("LoadDataSequentially: Calling LoadAndShuffleTrials...");
//        LoadAndShuffleTrials();
//        Debug.Log("LoadDataSequentially: Calling InsertAttentionTests...");
//        InsertAttentionTests();

//        isDataLoaded = true;
//        Debug.Log("LoadDataSequentially: Data loading finished.");
//    }

//    IEnumerator SetLocale(string languageCode)
//    {
//        var locale = LocalizationSettings.AvailableLocales.GetLocale(languageCode);
//        if (locale != null)
//        {
//            var currentLocale = LocalizationSettings.SelectedLocale;
//            if (currentLocale != locale)
//            {
//                Debug.Log($"Changing locale from '{currentLocale?.Identifier.Code ?? "None"}' to '{languageCode}'...");
//                LocalizationSettings.SelectedLocale = locale;
//                yield return LocalizationSettings.InitializationOperation;
//                if (LocalizationSettings.SelectedLocale == locale)
//                {
//                    Debug.Log($"Locale change to '{languageCode}' successful.");
//                }
//                else
//                {
//                    Debug.LogError($"Locale change to '{languageCode}' failed! Current locale is still '{LocalizationSettings.SelectedLocale?.Identifier.Code ?? "None"}'.");
//                    yield break;
//                }
//                yield return StartCoroutine(RefreshLocalizedUICoroutine());
//            }
//            else
//            {
//                Debug.Log($"Locale already set to: {languageCode}. Refreshing UI just in case.");
//                yield return StartCoroutine(RefreshLocalizedUICoroutine());
//            }
//        }
//        else
//        {
//            Debug.LogWarning($"Locale Code '{languageCode}' not found in Available Locales. Cannot set locale.");
//        }
//    }

//    IEnumerator RefreshLocalizedUICoroutine()
//    {
//        Debug.Log("RefreshLocalizedUICoroutine: Starting UI refresh.");
//        yield return new WaitUntil(() => LocalizationSettings.InitializationOperation.IsDone);

//        if (legendManager != null)
//        {
//            Debug.Log("Refreshing Legend...");
//            Task legendTask = legendManager.RefreshLegend();
//            if (legendTask != null)
//            {
//                yield return new WaitUntil(() => legendTask.IsCompleted);
//                if (legendTask.IsFaulted) Debug.LogError($"Legend refresh failed: {legendTask.Exception?.Message ?? "Unknown Error"}");
//                else if (!legendTask.IsCanceled) Debug.Log("Legend Refresh Completed.");
//                else Debug.LogWarning("Legend Refresh Task Canceled.");
//            }
//            else
//            {
//                Debug.LogWarning("legendManager.RefreshLegend() did not return a Task.");
//            }
//        }

//        yield return SetupUIAsync();
//        Debug.Log("RefreshLocalizedUICoroutine: UI Refresh Complete.");
//    }

//    private void InitializeComponents()
//    {
//        Debug.Log("InitializeComponents: Caching components.");
//        optionAButtonImage = optionAButton?.GetComponent<Image>();
//        optionBButtonImage = optionBButton?.GetComponent<Image>();
//        optionAButtonText = optionAButton?.GetComponentInChildren<TMP_Text>();
//        optionBButtonText = optionBButton?.GetComponentInChildren<TMP_Text>();
//        eventSystem = EventSystem.current;
//        barChartManager = FindObjectOfType<BarChartManager>();
//        legendManager = FindObjectOfType<LegendManager>();
//        instructionManager = FindObjectOfType<InstructionManager>();

//        if (optionAButton == null || optionBButton == null || optionAButtonImage == null || optionBButtonImage == null || optionAButtonText == null || optionBButtonText == null)
//            Debug.LogError("One or more Option Button components (Button, Image, TMP_Text) are not assigned in the Inspector!");
//        if (instructionPanel == null || trialPanel == null || fixationPanel == null)
//            Debug.LogError("One or more core UI Panels (Instruction, Trial, Fixation) are not assigned!");
//        if (trialInfoText == null) Debug.LogError("TrialInfoText is not assigned!");
//        if (eventSystem == null) Debug.LogWarning("No EventSystem found in the scene. Keyboard/Controller navigation might not work.");
//        if (barChartManager == null) Debug.LogWarning("BarChartManager not found in the scene.");
//        if (legendManager == null) Debug.LogWarning("LegendManager not found in the scene.");
//        if (instructionManager == null) Debug.LogWarning("InstructionManager not found during InitializeComponents. Will check again later.");

//        SetButtonInteraction(false);
//    }

//    private void LoadAndShuffleTrials()
//    {
//        if (SignallingTaskData.SignallingTrialLoader.Instance == null)
//        {
//            Debug.LogError("LoadAndShuffleTrials: SignallingTrialLoader.Instance is NULL! Cannot load trials.");
//            currentTrialList = new List<SignallingTaskData.TrialData>();
//            totalTrials = 0;
//            return;
//        }

//        Debug.Log($"LoadAndShuffleTrials: Loading trials for TaskType: {currentTask} using SignallingTrialLoader.Instance");
//        if (currentTask == TaskType.Deception)
//        {
//            if (SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials != null && SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials.Count > 0)
//            {
//                currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials);
//                Debug.Log($"Loaded {currentTrialList.Count} Deception trials.");
//            }
//            else
//            {
//                Debug.LogError("SignallingTrialLoader.Instance.DeceptionTrials is null or empty! Cannot proceed.");
//                currentTrialList = new List<SignallingTaskData.TrialData>();
//            }
//        }
//        else
//        {
//            if (SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials != null && SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials.Count > 0)
//            {
//                currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials);
//                Debug.Log($"Loaded {currentTrialList.Count} Control trials.");
//            }
//            else
//            {
//                Debug.LogWarning("SignallingTrialLoader.Instance.ControlTrials is null or empty. Using Deception trials as fallback for Control task.");
//                if (SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials != null && SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials.Count > 0)
//                {
//                    currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials);
//                }
//                else
//                {
//                    Debug.LogError("Fallback failed: SignallingTrialLoader.Instance.DeceptionTrials is also null or empty! Cannot proceed.");
//                    currentTrialList = new List<SignallingTaskData.TrialData>();
//                }
//            }
//        }

//        ShuffleTrials(currentTrialList);

//        // 💡 NEW CODE: Define the maximum number of trials you want to run.
//        // You can use the 'totalTrials' variable, or hard-code a value like 5.
//        // We'll use the 'totalTrials' member variable (defaulted to 5, set in Inspector).
//        int maxTrialsToRun = this.totalTrials;

//        // Ensure the list is not null and has items before limiting
//        if (currentTrialList != null && currentTrialList.Count > maxTrialsToRun)
//        {
//            // Limit the list to 'maxTrialsToRun' trials after they have been shuffled.
//            currentTrialList = currentTrialList.Take(maxTrialsToRun).ToList();
//            Debug.Log($"Limited total regular trials from initial load to: {currentTrialList.Count}");
//        }

//        totalTrials = currentTrialList?.Count ?? 0;
//        Debug.Log($"Actual number of regular trials to run: {totalTrials}");
//    }

//    void Start()
//    {
//        Debug.Log("GameManager Start: Frame 1 execution.");
//    }

//    async Task SetupUIAsync()
//    {
//        Debug.Log("SetupUIAsync: Setting up async localized text.");
//        if (!LocalizationSettings.HasSettings || !LocalizationSettings.InitializationOperation.IsDone)
//        {
//            Debug.LogWarning("SetupUIAsync: Waiting for Localization initialization...");
//            var initOp = LocalizationSettings.InitializationOperation;
//            if (initOp.IsValid() && !initOp.IsDone)
//            {
//                await initOp.Task;
//            }
//            while (!LocalizationSettings.InitializationOperation.IsDone)
//            {
//                await Task.Yield();
//            }
//        }

//        if (interRunPanel != null && interRunText != null)
//        {
//            try
//            {
//                interRunText.text = await GetLocalizedStringAsync(UILocalizationTable, "inter_run_text");
//            }
//            catch (System.Exception ex)
//            {
//                Debug.LogError($"Failed to get inter-run text: {ex.Message}");
//                interRunText.text = "[inter_run_text]";
//            }
//            Debug.Log("SetupUIAsync: Inter-run text set.");
//        }
//        Debug.Log("SetupUIAsync: Completed.");
//    }

//    void Update()
//    {
//        if (selectionEnabled)
//        {
//            HandleKeyboardNavigation();
//        }

//        if (Input.GetKeyDown(KeyCode.Escape))
//        {
//            Debug.Log("Escape key pressed. Requesting quit.");
//            StartCoroutine(SaveAndQuitCoroutine("EscapeKey"));
//        }
//    }

//    public void StartGameInternal()
//    {
//        Debug.Log("StartGameInternal: Received signal to start the main trial loop.");
//        if (instructionPanel != null)
//        {
//            instructionPanel.SetActive(false);
//            Debug.Log("StartGameInternal: Instruction Panel hidden.");
//        }
//        else
//        {
//            Debug.LogWarning("StartGameInternal: InstructionPanel reference is null, cannot hide it.");
//        }
//        if (instructionManager != null)
//        {
//            instructionManager.gameObject.SetActive(false);
//            Debug.Log("StartGameInternal: InstructionManager GameObject deactivated.");
//        }
//        StartCoroutine(RunAllTrials());
//    }

//    IEnumerator RunAllTrials()
//    {
//        Debug.Log("RunAllTrials: Starting experiment run sequence.");
//        int actualRegularTrials = currentTrialList?.Count ?? 0;
//        int actualAttentionTests = attentionTests?.Count ?? 0;
//        int totalEvents = actualRegularTrials + actualAttentionTests; // This is the total number of items to run

//        if (totalEvents == 0)
//        {
//            Debug.LogError("RunAllTrials: No trials or attention tests loaded/found. Cannot run experiment.");
//            EndTrials();
//            yield break;
//        }
//        if (totalTrials != actualRegularTrials)
//        {
//            Debug.LogWarning($"Mismatch between Inspector totalTrials ({totalTrials}) and loaded trials ({actualRegularTrials}). Using loaded count for event calculations.");
//        }

//        if (trialsPerRun <= 0)
//        {
//            Debug.LogError($"RunAllTrials: Invalid 'trialsPerRun' value ({trialsPerRun}). Setting to {totalEvents} to run all in one go.");
//            trialsPerRun = totalEvents;
//        }
//        int totalRuns = (trialsPerRun > 0) ? Mathf.CeilToInt((float)totalEvents / trialsPerRun) : 1;
//        Debug.Log($"RunAllTrials: {totalEvents} total events ({actualRegularTrials} regular, {actualAttentionTests} attention). {trialsPerRun} events/run. {totalRuns} runs total.");

//        int eventCounter = 0;

//        // FIX: Use a single loop that strictly iterates from 0 up to, but not including, totalEvents.
//        for (int eventIndex = 0; eventIndex < totalEvents; eventIndex++)
//        {
//            // Calculate the current run number (0-based) and the trial index within the run (0-based)
//            int run = eventIndex / trialsPerRun;
//            int trialInRun = eventIndex % trialsPerRun;

//            // Check for the start of a new run
//            if (trialInRun == 0)
//            {
//                Debug.Log($"---------- Starting Run {run + 1} / {totalRuns} ----------");
//            }

//            eventCounter = eventIndex + 1;
//            Debug.Log($"Run {run + 1}, Event {eventCounter}/{totalEvents} (Event Index: {eventIndex})");

//            if (attentionTestIndices.Contains(eventIndex))
//            {
//                if (attentionTestIndexToTestIndex.TryGetValue(eventIndex, out int testIndex))
//                {
//                    if (testIndex >= 0 && testIndex < attentionTests.Count)
//                    {
//                        Debug.Log($"Running Attention Test (List Index: {testIndex})");
//                        yield return StartCoroutine(RunAttentionTest(attentionTests[testIndex], eventCounter));
//                    }
//                    else
//                    {
//                        Debug.LogError($"Invalid attention test index {testIndex} mapped for event index {eventIndex}. Skipping.");
//                    }
//                }
//                else
//                {
//                    Debug.LogError($"Attention test index found in Set ({eventIndex}) but not in Dictionary. Data mismatch! Skipping.");
//                }
//            }
//            else
//            {
//                int adjustedIndex = GetAdjustedTrialIndex(eventIndex);
//                if (adjustedIndex >= 0 && adjustedIndex < currentTrialList.Count)
//                {
//                    Debug.Log($"Running Regular Trial (List Index: {adjustedIndex})");
//                    yield return StartCoroutine(RunTrial(currentTrialList[adjustedIndex], eventCounter));
//                }
//                else
//                {
//                    Debug.LogError($"Adjusted trial index {adjustedIndex} is out of bounds (0 to {currentTrialList.Count - 1}) for event index {eventIndex}. Skipping event.");
//                    if (trialResponses.Count == eventCounter - 1)
//                        trialResponses.Add("Error/Skipped");
//                    else
//                        Debug.LogError($"Could not add Error/Skipped response, response count ({trialResponses.Count}) != expected ({eventCounter - 1})");
//                }
//            }
//            Debug.Log($"---------- Event {eventCounter} Finished ----------");

//            // Check for Inter-Run break condition: end of a run AND not the very last run
//            if (trialInRun == trialsPerRun - 1 && run < totalRuns - 1)
//            {
//                Debug.Log($"---------- Run {run + 1} Finished ----------");

//                // *** ROBUSTNESS EDIT: Flush data to file after each run ***
//                DataLogger.FlushData();

//                if (interRunPanel != null && interRunInterval > 0)
//                {
//                    Debug.Log($"Starting Inter-Run Break for {interRunInterval} seconds.");
//                    DataLogger.LogInterRunStart(eventCounter);
//                    if (interRunText != null)
//                    {
//                        Task<string> textTask = GetLocalizedStringAsync(UILocalizationTable, "inter_run_text");
//                        yield return new WaitUntil(() => textTask.IsCompleted);
//                        if (!textTask.IsFaulted && !textTask.IsCanceled) interRunText.text = textTask.Result;
//                        else Debug.LogWarning("Failed to get inter-run text.");
//                    }
//                    interRunPanel.SetActive(true);
//                    trialPanel?.SetActive(false);
//                    fixationPanel?.SetActive(false);
//                    yield return StartCoroutine(WaitPrecise(interRunInterval)); // Use precise wait
//                    interRunPanel.SetActive(false);
//                    Debug.Log("Inter-Run Break Finished.");
//                }
//                else
//                {
//                    if (interRunPanel == null) Debug.LogWarning("InterRunPanel not assigned. Skipping break.");
//                    else Debug.Log("InterRunInterval is 0. Skipping break.");
//                }
//            }
//        }

//        // This is the correct place for the final log and cleanup after the loop completes
//        Debug.Log($"---------- Run {totalRuns} Finished ----------");

//        // Ensure data is flushed after the very last run
//        DataLogger.FlushData();

//        Debug.Log("RunAllTrials: All runs completed.");
//        EndTrials();
//    }
//    private int GetAdjustedTrialIndex(int eventIndex)
//    {
//        int adjustment = 0;
//        foreach (int testIndex in attentionTestIndices)
//        {
//            if (eventIndex > testIndex)
//            {
//                adjustment++;
//            }
//        }
//        return eventIndex - adjustment;
//    }

//    IEnumerator RunTrial(SignallingTaskData.TrialData trial, int eventNumber)
//    {
//        int totalEventCount = (currentTrialList?.Count ?? 0) + (attentionTests?.Count ?? 0);
//        Debug.Log($"RunTrial {eventNumber}/{totalEventCount}: Start. Type: {currentTask}. A:[{trial.optionA_Self},{trial.optionA_Other}], B:[{trial.optionB_Self},{trial.optionB_Other}]");
//        optionAButton?.gameObject.SetActive(true);
//        optionBButton?.gameObject.SetActive(true);

//        // --- Phase 1: Onset ---
//        selectionEnabled = false;
//        decisionMade = false;
//        trialPanel.SetActive(true);
//        fixationPanel.SetActive(false);

//        Task<string> trialInfoFormatTask = GetLocalizedStringAsync(UILocalizationTable, "trial_info");
//        yield return new WaitUntil(() => trialInfoFormatTask.IsCompleted);
//        if (!trialInfoFormatTask.IsFaulted && !trialInfoFormatTask.IsCanceled)
//        {
//            trialInfoText.text = string.Format(trialInfoFormatTask.Result, eventNumber, totalEventCount);
//        }
//        else { trialInfoText.text = $"Event {eventNumber}/{totalEventCount}"; }

//        if (barChartManager != null)
//        {
//            barChartManager.CreateBarChart(trial.optionA_Self, trial.optionA_Other, trial.optionB_Self, trial.optionB_Other);
//        }
//        else { Debug.LogWarning($"RunTrial {eventNumber}: BarChartManager not found."); }

//        string optionAKey = (currentTask == TaskType.Deception) ? "deception_option_a" : "control_option_a";
//        string optionBKey = (currentTask == TaskType.Deception) ? "deception_option_b" : "control_option_b";
//        Task<string> optionATextTask = GetLocalizedStringAsync(UILocalizationTable, optionAKey);
//        Task<string> optionBTextTask = GetLocalizedStringAsync(UILocalizationTable, optionBKey);
//        yield return new WaitUntil(() => optionATextTask.IsCompleted && optionBTextTask.IsCompleted);
//        SetButtonText(optionAButtonText, optionATextTask.IsCompletedSuccessfully ? optionATextTask.Result : $"[{optionAKey}]");
//        SetButtonText(optionBButtonText, optionBTextTask.IsCompletedSuccessfully ? optionBTextTask.Result : $"[{optionBKey}]");

//        SetButtonInteraction(false);
//        Debug.Log($"RunTrial {eventNumber}: Onset Phase ({trialOnsetDuration}s)");
//        yield return StartCoroutine(WaitPrecise(trialOnsetDuration));

//        // --- Phase 2: Decision ---
//        Debug.Log($"RunTrial {eventNumber}: Decision Phase Start (Waiting for input)");
//        yield return new WaitForEndOfFrame();
//        SetupTrialButtons();

//        yield return new WaitUntil(() => decisionMade);
//        float responseTime = Time.realtimeSinceStartup - decisionStartTime;
//        selectionEnabled = false;

//        string messageChosen = "Error/LogMismatch";
//        if (trialResponses.Count >= eventNumber)
//        {
//            messageChosen = trialResponses[eventNumber - 1];
//        }
//        else
//        {
//            Debug.LogError($"Response log missing for event {eventNumber}! Log count: {trialResponses.Count}.");
//            if (trialResponses.Count == eventNumber - 1) trialResponses.Add(messageChosen);
//        }
//        Debug.Log($"RunTrial {eventNumber}: Decision Made. Choice: {messageChosen}, RT: {responseTime:F3}s");

//        List<float> barData = new List<float> { trial.optionA_Self, trial.optionA_Other, trial.optionB_Self, trial.optionB_Other };
//        DataLogger.LogTrial(eventNumber, currentTask.ToString(), messageChosen, responseTime, barData);

//        // --- Phase 3: Confirmation ---
//        float confirmationDuration = Random.Range(decisionConfirmationMin, decisionConfirmationMax);
//        Debug.Log($"RunTrial {eventNumber}: Confirmation Phase ({confirmationDuration:F2}s)");
//        yield return StartCoroutine(WaitPrecise(confirmationDuration));

//        // --- Phase 4: Fixation ---
//        Debug.Log($"RunTrial {eventNumber}: Fixation Phase Start");
//        trialPanel.SetActive(false);
//        fixationPanel.SetActive(true);
//        float fixationDuration = Random.Range(fixationMin, fixationMax);
//        yield return StartCoroutine(WaitPrecise(fixationDuration));
//        Debug.Log($"RunTrial {eventNumber}: Fixation Phase End ({fixationDuration:F2}s)");
//        fixationPanel.SetActive(false);
//        Debug.Log($"RunTrial {eventNumber}: Complete.");
//    }

//    IEnumerator RunAttentionTest(SignallingTaskData.AttentionTestData test, int eventNumber)
//    {
//        int totalEventCount = (currentTrialList?.Count ?? 0) + (attentionTests?.Count ?? 0);
//        Debug.Log($"RunAttentionTest {eventNumber}/{totalEventCount}: Start. Correct: {test.correctAnswer}. A:[{test.optionA_Self},{test.optionA_Other}], B:[{test.optionB_Self},{test.optionB_Other}]");
//        optionAButton?.gameObject.SetActive(true);
//        optionBButton?.gameObject.SetActive(true);

//        // --- Phase 1: Onset ---
//        selectionEnabled = false;
//        decisionMade = false;
//        trialPanel.SetActive(true);
//        fixationPanel.SetActive(false);

//        Task<string> trialInfoFormatTask = GetLocalizedStringAsync(UILocalizationTable, "trial_info");
//        yield return new WaitUntil(() => trialInfoFormatTask.IsCompleted);
//        if (!trialInfoFormatTask.IsFaulted && !trialInfoFormatTask.IsCanceled)
//        {
//            trialInfoText.text = string.Format(trialInfoFormatTask.Result, eventNumber, totalEventCount);
//        }
//        else { trialInfoText.text = $"Event {eventNumber}/{totalEventCount}"; }

//        if (barChartManager != null)
//        {
//            barChartManager.CreateBarChart(test.optionA_Self, test.optionA_Other, test.optionB_Self, test.optionB_Other);
//        }
//        else { Debug.LogWarning($"RunAttentionTest {eventNumber}: BarChartManager not found."); }

//        Task<string> optionATextTask = GetLocalizedStringAsync(UILocalizationTable, "attention_option_a");
//        Task<string> optionBTextTask = GetLocalizedStringAsync(UILocalizationTable, "attention_option_b");
//        yield return new WaitUntil(() => optionATextTask.IsCompleted && optionBTextTask.IsCompleted);
//        SetButtonText(optionAButtonText, optionATextTask.IsCompletedSuccessfully ? optionATextTask.Result : "[attention_option_a]");
//        SetButtonText(optionBButtonText, optionBTextTask.IsCompletedSuccessfully ? optionBTextTask.Result : "[attention_option_b]");

//        SetButtonInteraction(false);
//        Debug.Log($"RunAttentionTest {eventNumber}: Onset Phase ({trialOnsetDuration}s)");
//        yield return StartCoroutine(WaitPrecise(trialOnsetDuration));

//        // --- Phase 2: Decision ---
//        Debug.Log($"RunAttentionTest {eventNumber}: Decision Phase Start (Waiting for input)");
//        yield return new WaitForEndOfFrame();
//        SetupTrialButtons();

//        yield return new WaitUntil(() => decisionMade);
//        float responseTime = Time.realtimeSinceStartup - decisionStartTime;
//        selectionEnabled = false;

//        string response = "Error/LogMismatch";
//        if (trialResponses.Count >= eventNumber)
//        {
//            response = trialResponses[eventNumber - 1];
//        }
//        else
//        {
//            Debug.LogError($"Response log missing for event {eventNumber}! Log count: {trialResponses.Count}.");
//            if (trialResponses.Count == eventNumber - 1) trialResponses.Add(response);
//        }
//        bool correct = response == test.correctAnswer;
//        Debug.Log($"RunAttentionTest {eventNumber}: Decision Made. Choice: {response}, Correct: {correct}, RT: {responseTime:F3}s");

//        DataLogger.LogAttentionTest(eventNumber, response, responseTime);

//        // --- Phase 3: Confirmation ---
//        float confirmationDuration = Random.Range(decisionConfirmationMin, decisionConfirmationMax);
//        Debug.Log($"RunAttentionTest {eventNumber}: Confirmation Phase ({confirmationDuration:F2}s)");
//        yield return StartCoroutine(WaitPrecise(confirmationDuration));

//        // --- Phase 4: Fixation ---
//        Debug.Log($"RunAttentionTest {eventNumber}: Fixation Phase Start");
//        trialPanel.SetActive(false);
//        fixationPanel.SetActive(true);
//        float fixationDuration = Random.Range(fixationMin, fixationMax);
//        yield return StartCoroutine(WaitPrecise(fixationDuration));
//        Debug.Log($"RunAttentionTest {eventNumber}: Fixation Phase End ({fixationDuration:F2}s)");
//        fixationPanel.SetActive(false);
//        Debug.Log($"RunAttentionTest {eventNumber}: Complete.");
//    }

//    void SetButtonInteraction(bool interactable)
//    {
//        float alpha = interactable ? 1.0f : 0.5f;
//        if (optionAButton != null)
//        {
//            optionAButton.interactable = interactable;
//            if (optionAButtonImage != null) SetButtonTransparency(optionAButtonImage, alpha);
//        }
//        if (optionBButton != null)
//        {
//            optionBButton.interactable = interactable;
//            if (optionBButtonImage != null) SetButtonTransparency(optionBButtonImage, alpha);
//        }
//    }

//    private void SetupTrialButtons()
//    {
//        optionAButton?.gameObject.SetActive(true);
//        optionBButton?.gameObject.SetActive(true);
//        selectionEnabled = true;
//        SetButtonInteraction(true);
//        decisionMade = false;
//        decisionStartTime = Time.realtimeSinceStartup; // Use precise clock
//        optionAButton?.onClick.RemoveAllListeners();
//        optionBButton?.onClick.RemoveAllListeners();
//        optionAButton?.onClick.AddListener(() => OnDecisionMade("A"));
//        optionBButton?.onClick.AddListener(() => OnDecisionMade("B"));
//        if (eventSystem != null && optionAButton != null && optionAButton.interactable)
//        {
//            eventSystem.SetSelectedGameObject(optionAButton.gameObject);
//        }
//        else
//        {
//            if (eventSystem == null) Debug.LogWarning("SetupTrialButtons: EventSystem is null. Cannot set initial focus.");
//            else if (optionAButton == null) Debug.LogWarning("SetupTrialButtons: Option A Button is null. Cannot set initial focus.");
//        }
//    }

//    void OnDecisionMade(string choice)
//    {
//        if (!selectionEnabled || decisionMade)
//        {
//            return;
//        }
//        decisionMade = true;
//        trialResponses.Add(choice);
//        if (choice == "A")
//        {
//            optionBButton?.gameObject.SetActive(false);
//        }
//        else if (choice == "B")
//        {
//            optionAButton?.gameObject.SetActive(false);
//        }
//    }

//    void HandleKeyboardNavigation()
//    {
//        if (Input.GetKeyDown(KeyCode.LeftArrow))
//        {
//            if (optionAButton != null && optionAButton.interactable)
//            {
//                OnDecisionMade("A");
//            }
//        }
//        else if (Input.GetKeyDown(KeyCode.RightArrow))
//        {
//            if (optionBButton != null && optionBButton.interactable)
//            {
//                OnDecisionMade("B");
//            }
//        }
//    }

//    void EndTrials()
//    {
//        Debug.Log("EndTrials: All trial runs complete. Preparing end sequence.");
//        trialPanel?.SetActive(false);
//        fixationPanel?.SetActive(false);
//        interRunPanel?.SetActive(false);
//        if (endExperimentButton != null)
//        {
//            Debug.Log("EndTrials: Activating End Experiment Button.");
//            endExperimentButton.gameObject.SetActive(true);
//            Button endButtonComponent = endExperimentButton.GetComponent<Button>();
//            if (endButtonComponent != null)
//            {
//                endButtonComponent.interactable = true;
//                endButtonComponent.onClick.RemoveAllListeners();
//                endButtonComponent.onClick.AddListener(EndExperiment);
//                if (eventSystem != null)
//                {
//                    eventSystem.SetSelectedGameObject(endExperimentButton.gameObject);
//                    Debug.Log("EndTrials: Focus set to End Experiment Button.");
//                }
//            }
//            else
//            {
//                Debug.LogError("EndTrials: EndExperimentButton prefab is missing the Button component!");
//                StartCoroutine(SaveAndQuitCoroutine("EndButtonComponentMissing"));
//            }
//        }
//        else
//        {
//            Debug.LogWarning("EndTrials: EndExperimentButton was not assigned in the Inspector. Saving and quitting automatically.");
//            StartCoroutine(SaveAndQuitCoroutine("EndButtonNotAssigned"));
//        }
//    }

//    async void EndExperiment()
//    {
//        Debug.Log("EndExperiment: Button clicked. Saving data and preparing to exit.");
//        if (endExperimentButton != null)
//        {
//            Button endButton = endExperimentButton.GetComponent<Button>();
//            if (endButton != null) endButton.interactable = false;
//        }
//        DataLogger.SaveData(participantId, currentTask.ToString(), currentSeries);
//        Debug.Log($"EndExperiment: Final data flush requested.");
//        if (instructionPanel != null && instructionText != null)
//        {
//            instructionPanel.SetActive(true);
//            try
//            {
//                var endMessageTask = GetLocalizedStringAsync(UILocalizationTable, "end_experiment_text");
//                instructionText.text = await endMessageTask;
//                Debug.Log("EndExperiment: End message loaded and displayed.");
//            }
//            catch (System.Exception ex)
//            {
//                Debug.LogError($"Failed to get localized end message: {ex.Message}");
//                instructionText.text = "[end_experiment_text]";
//            }
//        }
//        else
//        {
//            if (instructionPanel == null) Debug.LogWarning("EndExperiment: InstructionPanel not assigned.");
//            if (instructionText == null) Debug.LogWarning("EndExperiment: InstructionText not assigned.");
//        }
//        Debug.Log($"EndExperiment: Scheduling application close in {closeDelay} seconds.");
//        StartCoroutine(DelayedClose(closeDelay));
//    }

//    IEnumerator SaveAndQuitCoroutine(string reason = "Automatic")
//    {
//        Debug.Log($"SaveAndQuitCoroutine: Triggered automatically ({reason}). Saving data and quitting.");
//        trialPanel?.SetActive(false);
//        fixationPanel?.SetActive(false);
//        instructionPanel?.SetActive(false);
//        DataLogger.SaveData(participantId, currentTask.ToString(), currentSeries);
//        Debug.Log($"SaveAndQuitCoroutine: Final data flush requested.");
//        yield return StartCoroutine(WaitPrecise(2.0f));
//        CloseApplication();
//    }

//    IEnumerator DelayedClose(float delay)
//    {
//        if (delay < 0) delay = 0;
//        Debug.Log($"DelayedClose: Waiting for {delay} seconds before quitting.");
//        yield return StartCoroutine(WaitPrecise(delay));
//        CloseApplication();
//    }

//    void CloseApplication()
//    {
//        Debug.Log("CloseApplication: Attempting to quit.");
//#if UNITY_EDITOR
//        Debug.Log("Quitting Play Mode (Unity Editor).");
//        UnityEditor.EditorApplication.isPlaying = false;
//#elif UNITY_WEBGL
//        Debug.Log("Application.Quit() ignored in WebGL. Close the browser tab.");
//#else
//        Debug.Log("Quitting application.");
//        Application.Quit();
//#endif
//    }

//    IEnumerator WaitPrecise(float duration)
//    {
//        float startTime = Time.realtimeSinceStartup;
//        while (Time.realtimeSinceStartup < startTime + duration)
//        {
//            yield return null;
//        }
//    }

//    void SetButtonText(TMP_Text textComponent, string text) { if (textComponent != null) textComponent.text = text; }
//    void SetButtonTransparency(Image buttonImage, float alpha) { if (buttonImage != null) { Color c = buttonImage.color; c.a = Mathf.Clamp01(alpha); buttonImage.color = c; } }
//    void ShuffleTrials(List<SignallingTaskData.TrialData> list) { if (list == null || list.Count <= 1) return; System.Random rng = new System.Random(); int n = list.Count; while (n > 1) { n--; int k = rng.Next(n + 1); (list[k], list[n]) = (list[n], list[k]); } }

//    private void InsertAttentionTests()
//    {
//        if (SignallingTaskData.SignallingTrialLoader.Instance == null)
//        {
//            Debug.LogError("InsertAttentionTests: SignallingTrialLoader.Instance is NULL! Cannot insert tests.");
//            attentionTests = new List<SignallingTaskData.AttentionTestData>();
//            attentionTestIndices.Clear();
//            attentionTestIndexToTestIndex.Clear();
//            return;
//        }
//        if (SignallingTaskData.SignallingTrialLoader.Instance.AttentionTests == null)
//        {
//            Debug.LogWarning("SignallingTrialLoader.Instance.AttentionTests is null. Cannot insert tests.");
//            attentionTests = new List<SignallingTaskData.AttentionTestData>();
//        }
//        else
//        {
//            attentionTests = new List<SignallingTaskData.AttentionTestData>(SignallingTaskData.SignallingTrialLoader.Instance.AttentionTests);
//        }

//        attentionTestIndices.Clear();
//        attentionTestIndexToTestIndex.Clear();
//        int numAttentionTests = attentionTests.Count;
//        int numRegularTrials = currentTrialList?.Count ?? 0;

//        if (numAttentionTests == 0 || numRegularTrials == 0) return;

//        // --- NEW, ROBUST LOGIC ---
//        System.Random rng = new System.Random();

//        // 1. Determine a set of potential insertion indices (0 to numRegularTrials - 1).
//        // Since attention tests are inserted *between* regular trials, we use the number of trials as the base.
//        List<int> possibleInsertSlots = Enumerable.Range(0, numRegularTrials + numAttentionTests).ToList();

//        // Remove indices that are too close to the end if we have limited data.
//        // A common practice is to skip the first and last few slots.

//        // 2. Select `numAttentionTests` random, non-repeating positions from the total available events slots.
//        // This implicitly handles spacing and collisions by picking unique indices from the final list.
//        List<int> randomInsertionIndices = new List<int>();

//        // Get the list of all possible event indices (0 to totalEvents - 1)
//        List<int> allEventIndices = Enumerable.Range(0, numRegularTrials + numAttentionTests).ToList();

//        // Ensure the regular trials are not all in one clump, e.g. skip the first few slots for attention tests
//        int minStartOffset = 2;

//        // Select random indices for the attention tests
//        int testsPlaced = 0;
//        while (testsPlaced < numAttentionTests)
//        {
//            // Calculate remaining slots *after* accounting for tests already placed
//            int maxIndex = numRegularTrials + testsPlaced;
//            if (maxIndex < minStartOffset) maxIndex = minStartOffset; // Safety check

//            // Choose a random index between minStartOffset and the end of the current possible events
//            int randomIndex = rng.Next(minStartOffset, maxIndex + 1);

//            // If we successfully place the test (no collision with already placed tests)
//            if (attentionTestIndices.Add(randomIndex))
//            {
//                // We must ensure this insertion index is unique across ALL tests.
//                // The HashSet 'attentionTestIndices' handles this perfectly.
//                attentionTestIndexToTestIndex[randomIndex] = testsPlaced;
//                testsPlaced++;
//            }
//        }

//        // --- END OF NEW, ROBUST LOGIC ---

//        if (attentionTestIndices.Count > 0)
//        {
//            List<int> sortedIndices = new List<int>(attentionTestIndices);
//            sortedIndices.Sort();
//            Debug.Log($"InsertAttentionTests: Inserted {attentionTestIndices.Count} tests at event indices: {string.Join(", ", sortedIndices)}. Total Events: {numRegularTrials + attentionTestIndices.Count}");
//        }
//        else if (numAttentionTests > 0)
//        {
//            Debug.LogWarning("InsertAttentionTests: Failed to place any attention tests.");
//        }
//    }

//    async Task<string> GetLocalizedStringAsync(string tableName, string entryName)
//    {
//        if (!LocalizationSettings.HasSettings || LocalizationSettings.StringDatabase == null)
//        {
//            Debug.LogError($"GetLocalizedStringAsync: Localization Settings/Database not available! Cannot get '{tableName}/{entryName}'.");
//            return $"[{entryName}]";
//        }
//        var initOp = LocalizationSettings.InitializationOperation;
//        if (initOp.IsValid() && !initOp.IsDone)
//        {
//            await initOp.Task;
//            while (!initOp.IsDone) { await Task.Yield(); }
//        }
//        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, entryName);
//        string result = null;
//        if (!operation.IsDone)
//        {
//            await operation.Task;
//        }
//        result = operation.Result;
//        if (operation.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded && result != null)
//        {
//            return result;
//        }
//        else
//        {
//            Debug.LogWarning($"GetLocalizedStringAsync: Failed to get key '{entryName}' from table '{tableName}'. Status: {operation.Status}. Error: {operation.OperationException?.Message ?? "None"}. Locale: {LocalizationSettings.SelectedLocale?.Identifier.Code ?? "N/A"}");
//            return $"[{entryName}]";
//        }
//    }

//    public string GetParticipantId() { return this.participantId; }
//}