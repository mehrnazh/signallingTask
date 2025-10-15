using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.Threading.Tasks;
using System.Linq;
//using SignallingTaskData;

// Enum to distinguish between task types.
public enum TaskType { Deception, Control }

// --- NEW STRUCT ---
[System.Serializable]
public struct FeedbackTrialOutcome
{
    public int originalTrialListIndex; // Index in the shuffledTrialOrder list
    public string participantChoice;    // "A" or "B"
    public float selfPayoff;
    public float otherPayoff;
    public bool partnerFollowed; // Whether the partner followed the shared message
}
// --- NEW STRUCT ---

public class GameManager : MonoBehaviour
{
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
    public TMP_Text TrialInstructionText;

    [Header("Feedback Visuals")]
    public RectTransform FeedbackChartContainer; // Assign the RUQ chart container here.
    public Transform FeedbackGroupAContainer; // <-- ADD THIS
    public Transform FeedbackGroupBContainer; // <-- ADD THIS
    public RectTransform FeedbackGroupLabelsContainer; // <-- ADD THIS
    public GameObject feedbackAnnotationLinePrefab; // <--- NEW: Assign your line prefab here!


    [Header("Buttons")]
    public Button optionAButton;
    public Button optionBButton;
    public Button endExperimentButton;

    [Header("Task Settings")]
    public TaskType currentTask = TaskType.Deception;
    public int currentSeries = 1; // Added: To select instruction series (1 or 2)
    public int totalTrials = 5;  // Total number of *regular* trials (updated from loaded data)
    public int trialsPerRun = 1;  // Number of *events* (trials + tests) per run
    public float trialOnsetDuration = 2f;
    public float decisionConfirmationMin = 2f;
    public float decisionConfirmationMax = 4f;
    public float fixationMin = 2f;
    public float fixationMax = 4f;
    public float interRunInterval = 10f;
    public float closeDelay = 10f; // Delay before closing after final message

    [Header("Training Annotation Images")]
    public Image annotationImageCenter;
    public Image annotationImageLeft;
    public Image annotationImageRight;
    public Image annotationImageCenterSmall; // NEW

    [Header("Training Feedback UI")]
    public GameObject trainingFeedbackPanel; // Assign your new panel
    public TMP_Text trainingFeedbackText;   // Assign the text from the new panel
    public Button trainingContinueButton;   // Assign the button from the new panel
    public FeedbackManager feedbackManager; // Assign your FeedbackManager object here

    // Enum for annotation positions
    public enum AnnotationPosition { Center, Left, Right, CenterSmall }

    // Struct to define a single training trial with images
    [System.Serializable]
    public struct TrainingTrial
    {
        [System.Serializable]
        public struct Annotation
        {
            public Sprite image;
            public AnnotationPosition position;
        }
        [Header("Monetary Allocations")]
        public float optionASelf;
        public float optionAOther;
        public float optionBSelf;
        public float optionBOther;

        [Header("Annotation Sequence")]
        public Annotation annotation1;
        public Annotation annotation2;
        public Annotation annotation3;
        public Annotation annotation4;
        public Annotation annotation5;
        public Annotation annotation6;

        [Header("Post-Decision Annotations")]
        public Annotation postDecisionAnnotationA;
        public Annotation postDecisionAnnotationB;

        [Header("Correct Answer")]
        public string correctAnswer; // "A" or "B"
    }

    [Header("Training Session Data")]
    public List<TrainingTrial> trainingTrials;

    // Enum and variable to manage the experiment's master flow
    private enum ExperimentPhase { Setup, InstructionsPart1, Training, InstructionsPart2, MainTrials, Finished }
    private ExperimentPhase currentPhase = ExperimentPhase.Setup;

    // Cached components
    private Image optionAButtonImage;
    private Image optionBButtonImage;
    private TMP_Text optionAButtonText;
    private TMP_Text optionBButtonText;
    private EventSystem eventSystem;
    private BarChartManager barChartManager;
    private LegendManager legendManager;

    // Optimized data structures
    private List<SignallingTaskData.TrialData> currentTrialList;
    private List<string> trialResponses = new List<string>();
    private List<SignallingTaskData.AttentionTestData> attentionTests = new List<SignallingTaskData.AttentionTestData>();
    private HashSet<int> attentionTestIndices = new HashSet<int>();
    private Dictionary<int, int> attentionTestIndexToTestIndex = new Dictionary<int, int>();
    private List<FeedbackTrialOutcome> feedbackTrialOutcomes = new List<FeedbackTrialOutcome>();

    private bool decisionMade = false;
    private bool selectionEnabled = false;
    private float decisionStartTime;

    private bool isInitialized = false;
    private bool isDataLoaded = false;
    private bool hasReceivedOptions = false;

    private string participantId = "DEFAULT_ID";
    private const string UILocalizationTable = "UI";
    private int startEventIndex = 0;


    private string GetStateFilePath(string pId, string task, int ser)
    {
        string filename = $"PID-{pId}_Task-{task}_Series-{ser}_resume.json";
        return System.IO.Path.Combine(Application.persistentDataPath, filename);
    }

    private bool ShouldUseRTL()
    {
        // Check if the selected locale's code is Farsi ('fa')
        return LocalizationSettings.SelectedLocale != null &&
               LocalizationSettings.SelectedLocale.Identifier.Code == "fa";
    }

    void Awake()
    {
        Debug.Log("GameManager Awake: Initializing Singleton.");
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DataLogger.Initialize();
            Debug.Log("GameManager Awake: DataLogger Initialized.");
            InitializeComponents();
            Debug.Log("GameManager Awake: Singleton set. Waiting for options from SetupManager.");
        }
        else
        {
            Debug.LogWarning($"Duplicate GameManager instance detected on GameObject '{gameObject.name}'. Destroying this one.", gameObject);
            Destroy(gameObject);
            return;
        }
    }

    public void StartInitializationWithOptions(TaskType task, int series, string langCode, string participantId)
    {
        DataLogger.Reset();

        if (hasReceivedOptions)
        {
            Debug.LogWarning("GameManager: StartInitializationWithOptions called more than once!");
            return;
        }

        Debug.Log($"GameManager: Received options - ID: {participantId}, Task: {task}, Series: {series}, Lang: {langCode}");

        this.currentTask = task;
        this.currentSeries = series;
        this.participantId = participantId;

        DataLogger.SetFilePath(participantId, task.ToString(), series);
        AttemptToLoadState(participantId, task, series);
        hasReceivedOptions = true;
        StartCoroutine(InitializeLocalizationAndUI(langCode));
    }

    IEnumerator InitializeLocalizationAndUI(string initialLangCode)
    {
        Debug.Log($"InitializeLocalizationAndUI: Starting with Language Code: {initialLangCode}");

        if (!hasReceivedOptions)
        {
            Debug.LogError("InitializeLocalizationAndUI started before options were received!");
            yield break;
        }

        if (!isDataLoaded)
        {
            StartCoroutine(LoadDataSequentially());
        }

        yield return LocalizationSettings.InitializationOperation;
        if (!LocalizationSettings.HasSettings || LocalizationSettings.InitializationOperation.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("Localization failed to initialize!");
            yield break;
        }
        Debug.Log("InitializeLocalizationAndUI: Localization System Initialized.");

        Debug.Log($"InitializeLocalizationAndUI: Setting initial locale to {initialLangCode}...");
        yield return StartCoroutine(SetLocale(initialLangCode));

        Debug.Log("InitializeLocalizationAndUI: Initial Panel States assumed correct.");
        yield return SetupUIAsync();
        Debug.Log("InitializeLocalizationAndUI: Async UI Setup Complete.");

        Debug.Log("InitializeLocalizationAndUI: Waiting for data loading...");
        yield return new WaitUntil(() => isDataLoaded);
        Debug.Log("InitializeLocalizationAndUI: Data loading confirmed complete.");

        isInitialized = true;
        Debug.Log("-----------------------------------------");
        Debug.Log("GameManager Core Initialized (including data).");
        Debug.Log($"Participant ID: {this.participantId}");
        Debug.Log($"Task Type: {currentTask}, Series: {currentSeries}");
        Debug.Log($"Locale: {LocalizationSettings.SelectedLocale?.Identifier.Code ?? "Not Set"}");
        Debug.Log($"Total Regular Trials Loaded: {currentTrialList?.Count ?? 0}");
        Debug.Log($"Total Attention Tests Loaded: {attentionTests?.Count ?? 0}");
        Debug.Log("-----------------------------------------");

        if (instructionManager != null)
        {
            if (LocalizationSettings.SelectedLocale != null)
            {
                Debug.Log("InitializeLocalizationAndUI: Initialization complete. Ready to start master flow.");
                StartExperimentFlow();
            }
            else
            {
                Debug.LogError("InitializeLocalizationAndUI: SelectedLocale became null after setting! Cannot init instructions.");
                StartExperimentFlow();
            }
        }
        else
        {
            Debug.LogWarning("InitializeLocalizationAndUI: InstructionManager reference missing. Skipping instructions and training phase.");
            instructionPanel?.SetActive(false);
            StartExperimentFlow();
        }
    }

    IEnumerator LoadDataSequentially()
    {
        Debug.Log("LoadDataSequentially: Waiting for SignallingTrialLoader instance...");
        yield return new WaitUntil(() => SignallingTaskData.SignallingTrialLoader.Instance != null);
        Debug.Log("LoadDataSequentially: SignallingTrialLoader instance found.");

        yield return null;

        Debug.Log("LoadDataSequentially: Calling LoadAndShuffleTrials...");
        LoadAndShuffleTrials();
        Debug.Log("LoadDataSequentially: Calling InsertAttentionTests...");
        InsertAttentionTests();

        isDataLoaded = true;
        Debug.Log("LoadDataSequentially: Data loading finished.");
    }

    IEnumerator SetLocale(string languageCode)
    {
        var locale = LocalizationSettings.AvailableLocales.GetLocale(languageCode);
        if (locale != null)
        {
            var currentLocale = LocalizationSettings.SelectedLocale;
            if (currentLocale != locale)
            {
                Debug.Log($"Changing locale from '{currentLocale?.Identifier.Code ?? "None"}' to '{languageCode}'...");
                LocalizationSettings.SelectedLocale = locale;
                yield return LocalizationSettings.InitializationOperation;
                if (LocalizationSettings.SelectedLocale == locale)
                {
                    Debug.Log($"Locale change to '{languageCode}' successful.");
                }
                else
                {
                    Debug.LogError($"Locale change to '{languageCode}' failed! Current locale is still '{LocalizationSettings.SelectedLocale?.Identifier.Code ?? "None"}'.");
                    yield break;
                }
                yield return StartCoroutine(RefreshLocalizedUICoroutine());
            }
            else
            {
                Debug.Log($"Locale already set to: {languageCode}. Refreshing UI just in case.");
                yield return StartCoroutine(RefreshLocalizedUICoroutine());
            }
        }
        else
        {
            Debug.LogWarning($"Locale Code '{languageCode}' not found in Available Locales. Cannot set locale.");
        }
    }

    IEnumerator RefreshLocalizedUICoroutine()
    {
        Debug.Log("RefreshLocalizedUICoroutine: Starting UI refresh.");
        yield return new WaitUntil(() => LocalizationSettings.InitializationOperation.IsDone);

        if (legendManager != null)
        {
            Debug.Log("Refreshing Legend...");
            Task legendTask = legendManager.RefreshLegend();
            if (legendTask != null)
            {
                yield return new WaitUntil(() => legendTask.IsCompleted);
                if (legendTask.IsFaulted) Debug.LogError($"Legend refresh failed: {legendTask.Exception?.Message ?? "Unknown Error"}");
                else if (!legendTask.IsCanceled) Debug.Log("Legend Refresh Completed.");
                else Debug.LogWarning("Legend Refresh Task Canceled.");
            }
            else
            {
                Debug.LogWarning("legendManager.RefreshLegend() did not return a Task.");
            }
        }

        yield return SetupUIAsync();
        Debug.Log("RefreshLocalizedUICoroutine: UI Refresh Complete.");
    }

    private void InitializeComponents()
    {
        Debug.Log("InitializeComponents: Caching components.");
        optionAButtonImage = optionAButton?.GetComponent<Image>();
        optionBButtonImage = optionBButton?.GetComponent<Image>();
        optionAButtonText = optionAButton?.GetComponentInChildren<TMP_Text>();
        optionBButtonText = optionBButton?.GetComponentInChildren<TMP_Text>();
        eventSystem = EventSystem.current;
        barChartManager = FindObjectOfType<BarChartManager>();
        legendManager = FindObjectOfType<LegendManager>();
        instructionManager = FindObjectOfType<InstructionManager>();

        if (optionAButton == null || optionBButton == null || optionAButtonImage == null || optionBButtonImage == null || optionAButtonText == null || optionBButtonText == null)
            Debug.LogError("One or more Option Button components (Button, Image, TMP_Text) are not assigned in the Inspector!");
        if (instructionPanel == null || trialPanel == null || fixationPanel == null)
            Debug.LogError("One or more core UI Panels (Instruction, Trial, Fixation) are not assigned!");
        if (trialInfoText == null) Debug.LogError("TrialInfoText is not assigned!");
        if (eventSystem == null) Debug.LogWarning("No EventSystem found in the scene. Keyboard/Controller navigation might not work.");
        if (barChartManager == null) Debug.LogWarning("BarChartManager not found in the scene.");
        if (legendManager == null) Debug.LogWarning("LegendManager not found in the scene.");
        if (instructionManager == null) Debug.LogWarning("InstructionManager not found during InitializeComponents. Will check again later.");
        if (TrialInstructionText == null) Debug.LogWarning("TrialInstructionText is not assigned in the Inspector!");
        if (FeedbackChartContainer == null) Debug.LogWarning("FeedbackChartContainer is not assigned in the Inspector! RUQ chart will not show during feedback.");

        SetButtonInteraction(false);
    }

    private void SaveExperimentState()
    {
        int lastCompletedIndex = trialResponses.Count - 1;

        if (lastCompletedIndex < 0) return;

        ExperimentState state = new ExperimentState
        {
            participantId = this.participantId,
            taskType = this.currentTask.ToString(),
            series = this.currentSeries,
            lastCompletedEventIndex = lastCompletedIndex,
            shuffledTrialOrder = this.currentTrialList,
            attentionTestEventIndices = new List<int>(this.attentionTestIndices), // Convert HashSet to List for serialization
            trialResponses = new List<string>(this.trialResponses)
        };

        string jsonState = JsonUtility.ToJson(state, true); // 'true' for pretty print
        string filePath = GetStateFilePath(this.participantId, this.currentTask.ToString(), this.currentSeries);

        try
        {
            System.IO.File.WriteAllText(filePath, jsonState);
            Debug.Log($"<color=green>Experiment state saved successfully to {filePath}</color>");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save experiment state: {ex.Message}");
        }
    }

    private void LoadAndShuffleTrials()
    {
        if (SignallingTaskData.SignallingTrialLoader.Instance == null)
        {
            Debug.LogError("LoadAndShuffleTrials: SignallingTrialLoader.Instance is NULL! Cannot load trials.");
            currentTrialList = new List<SignallingTaskData.TrialData>();
            totalTrials = 0;
            return;
        }

        Debug.Log($"LoadAndShuffleTrials: Loading trials for TaskType: {currentTask} using SignallingTrialLoader.Instance");
        if (currentTask == TaskType.Deception)
        {
            currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials);
            //if (SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials != null && SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials.Count > 0)
            //{
            //    currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials);
            //    Debug.Log($"Loaded {currentTrialList.Count} Deception trials.");
            //}
            //else
            //{
            //    Debug.LogError("SignallingTrialLoader.Instance.DeceptionTrials is null or empty! Cannot proceed.");
            //    currentTrialList = new List<SignallingTaskData.TrialData>();
            //}
        }
        else
        {
            currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials);
            //if (SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials != null && SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials.Count > 0)
            //{
            //    currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.ControlTrials);
            //    Debug.Log($"Loaded {currentTrialList.Count} Control trials.");
            //}
            //else
            //{
            //    Debug.LogWarning("SignallingTrialLoader.Instance.ControlTrials is null or empty. Using Deception trials as fallback for Control task.");
            //    if (SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials != null && SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials.Count > 0)
            //    {
            //        currentTrialList = new List<SignallingTaskData.TrialData>(SignallingTaskData.SignallingTrialLoader.Instance.DeceptionTrials);
            //    }
            //    else
            //    {
            //        Debug.LogError("Fallback failed: SignallingTrialLoader.Instance.DeceptionTrials is also null or empty! Cannot proceed.");
            //        currentTrialList = new List<SignallingTaskData.TrialData>();
            //    }
            //}
        }

        ShuffleTrials(currentTrialList);

        int maxTrialsToRun = this.totalTrials;
        if (currentTrialList != null && currentTrialList.Count > maxTrialsToRun)
        {
            currentTrialList = currentTrialList.Take(maxTrialsToRun).ToList();
            Debug.Log($"Limited total regular trials from initial load to: {currentTrialList.Count}");
        }

        totalTrials = currentTrialList?.Count ?? 0;
        Debug.Log($"Actual number of regular trials to run: {totalTrials}");
    }

    void Start()
    {
        Debug.Log("GameManager Start: Frame 1 execution.");
    }

    async Task SetupUIAsync()
    {
        Debug.Log("SetupUIAsync: Setting up async localized text.");
        if (!LocalizationSettings.HasSettings || !LocalizationSettings.InitializationOperation.IsDone)
        {
            Debug.LogWarning("SetupUIAsync: Waiting for Localization initialization...");
            var initOp = LocalizationSettings.InitializationOperation;
            if (initOp.IsValid() && !initOp.IsDone)
            {
                await initOp.Task;
            }
            while (!LocalizationSettings.InitializationOperation.IsDone)
            {
                await Task.Yield();
            }
        }

        if (interRunPanel != null && interRunText != null)
        {
            try
            {
                interRunText.text = await GetLocalizedStringAsync(UILocalizationTable, "inter_run_text");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to get inter-run text: {ex.Message}");
                interRunText.text = "[inter_run_text]";
            }
            Debug.Log("SetupUIAsync: Inter-run text set.");
        }
        Debug.Log("SetupUIAsync: Completed.");
    }

    void Update()
    {
        if (selectionEnabled)
        {
            HandleKeyboardNavigation();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("Escape key pressed. Requesting quit.");
            StartCoroutine(SaveAndQuitCoroutine("EscapeKey"));
        }
    }
    public void StartGameInternal()
    {
        Debug.Log("StartGameInternal: Received signal to start the main trial loop. Redirecting to master flow.");
        StartExperimentFlow();
    }

    public void StartExperimentFlow()
    {
        Debug.Log("StartExperimentFlow: Received signal to start the master experiment flow.");
        if (instructionPanel != null) instructionPanel.SetActive(false);
        if (instructionManager != null)
        {
            instructionManager.Initialize(this); // Pass reference to self
            instructionManager.gameObject.SetActive(false); // Start inactive
        }

        currentPhase = ExperimentPhase.InstructionsPart1;
        StartCoroutine(RunMasterFlow());
    }

    public void OnInstructionSetCompleted()
    {
        Debug.Log($"OnInstructionSetCompleted: Finished phase {currentPhase}.");
        currentPhase++;
        StartCoroutine(RunMasterFlow());
    }

    IEnumerator RunMasterFlow()
    {
        Debug.Log($"RunMasterFlow: Executing phase {currentPhase}.");

        switch (currentPhase)
        {
            case ExperimentPhase.InstructionsPart1:
                instructionPanel.SetActive(true);
                instructionManager.gameObject.SetActive(true);
                string langCode1 = LocalizationSettings.SelectedLocale.Identifier.Code;
                Sprite[] part1Sprites = instructionManager.GetSpriteSet(1, currentSeries, currentTask, langCode1);
                instructionManager.BeginInstructionSet(part1Sprites);
                break;

            case ExperimentPhase.Training:
                instructionPanel.SetActive(false);
                yield return StartCoroutine(RunTrainingSession());
                OnInstructionSetCompleted();
                break;

            case ExperimentPhase.InstructionsPart2:
                instructionPanel.SetActive(true);
                instructionManager.gameObject.SetActive(true);
                string langCode2 = LocalizationSettings.SelectedLocale.Identifier.Code;
                Sprite[] part2Sprites = instructionManager.GetSpriteSet(2, currentSeries, currentTask, langCode2);
                instructionManager.BeginInstructionSet(part2Sprites);
                break;

            case ExperimentPhase.MainTrials:
                instructionPanel.SetActive(false);
                yield return StartCoroutine(RunAllTrials());
                break;

            case ExperimentPhase.Finished:
                Debug.Log("Master flow finished.");
                break;
        }
    }
    // In GameManager.cs

    IEnumerator RunTrainingSession()
    {
        Debug.Log("--- Starting Advanced Training Session ---");

        trialPanel.SetActive(true);
        trialInfoText.text = "Training";

        // Load the localized template string once before the loop
        Task<string> instructionFormatTask = GetLocalizedStringAsync(UILocalizationTable, "training_progress"); // <-- Semantic Key

        for (int i = 0; i < trainingTrials.Count; i++)
        {
            TrainingTrial training = trainingTrials[i];
            Debug.Log($"Starting training trial {i + 1}/{trainingTrials.Count}");

            // --- Update Training Instruction Text ---
            if (TrialInstructionText != null)
            {
                yield return new WaitUntil(() => instructionFormatTask.IsCompleted);
                string localizedFormat = instructionFormatTask.IsCompletedSuccessfully ? instructionFormatTask.Result : "Training {0} of {1}";

                TrialInstructionText.text = string.Format(localizedFormat, i + 1, trainingTrials.Count);
            }
            // --- End Update Training Instruction Text ---

            barChartManager.CreateBarChart(training.optionASelf, training.optionAOther, training.optionBSelf, training.optionBOther);

            if (i == 0)
            {
                float annotationDuration = 2.0f;

                // --- MODIFIED: Bi-directional Annotation Navigation Logic ---
                TrainingTrial.Annotation[] annotationsToShow = {
                    training.annotation1,
                    training.annotation2,
                    training.annotation3,
                    training.annotation4,
                    training.annotation5,
                    training.annotation6
                };

                int currentAnnotationIndex = 0;
                while (currentAnnotationIndex < annotationsToShow.Length)
                {
                    // Always show the current annotation
                    yield return ShowAnnotation(annotationsToShow[currentAnnotationIndex], annotationDuration);

                    // Start timer/key wait
                    float startTime = Time.realtimeSinceStartup;
                    bool moved = false;
                    while (Time.realtimeSinceStartup < startTime + annotationDuration && !moved)
                    {
                        if (Input.GetKeyDown(KeyCode.RightArrow))
                        {
                            currentAnnotationIndex++;
                            moved = true;
                        }
                        else if (Input.GetKeyDown(KeyCode.LeftArrow))
                        {
                            // Decrement, but don't go below the start (index 0)
                            if (currentAnnotationIndex > 0)
                            {
                                currentAnnotationIndex--;
                                moved = true;
                            }
                            else
                            {
                                // At the beginning, wait for timer to expire or for RightArrow key
                            }
                        }
                        yield return null;
                    }

                    // If the timer ran out and no key was pressed, automatically advance
                    if (!moved)
                    {
                        currentAnnotationIndex++;
                    }

                    // Special case: If index is 2 or 4 (after 2nd or 4th annotation), hide all and pause briefly
                    if (currentAnnotationIndex == 2 || currentAnnotationIndex == 4)
                    {
                        HideAllAnnotationImages();
                        yield return StartCoroutine(WaitPrecise(0.5f));
                    }
                }

                // Final cleanup after the loop
                HideAllAnnotationImages();
                // --- END MODIFIED: Bi-directional Annotation Navigation Logic ---
            }

            Debug.Log("Training: Displaying decision buttons.");
            optionAButton.gameObject.SetActive(true);
            optionBButton.gameObject.SetActive(true);

            // --- MODIFICATION START (Button Delay) ---
            SetButtonInteraction(false);

            string optionAKey = (currentTask == TaskType.Deception) ? "deception_option_a" : "control_option_a";
            string optionBKey = (currentTask == TaskType.Deception) ? "deception_option_b" : "control_option_b";

            var taskA = GetLocalizedStringAsync(UILocalizationTable, optionAKey);
            var taskB = GetLocalizedStringAsync(UILocalizationTable, optionBKey);
            yield return new WaitUntil(() => taskA.IsCompleted && taskB.IsCompleted);

            if (taskA.IsFaulted || taskB.IsFaulted)
            {
                Debug.LogError($"LOCALIZATION ERROR: Failed to load button text! Check your Localization Table for '{optionAKey}' and '{optionBKey}'. Using fallback text.");
                SetButtonText(optionAButtonText, "[Option A]");
                SetButtonText(optionBButtonText, "[Option B]");
            }
            else
            {
                SetButtonText(optionAButtonText, taskA.Result);
                SetButtonText(optionBButtonText, taskB.Result);
            }

            // Wait for 2 seconds before allowing a decision (onset phase).
            yield return StartCoroutine(WaitPrecise(2.0f));

            Debug.Log("Training: Activating decision buttons.");
            SetButtonInteraction(true);
            // --- MODIFICATION END (Button Delay) ---

            string choice = "";
            bool decisionMadeThisTrial = false;
            optionAButton.onClick.RemoveAllListeners();
            optionBButton.onClick.RemoveAllListeners();
            optionAButton.onClick.AddListener(() => { choice = "A"; decisionMadeThisTrial = true; });
            optionBButton.onClick.AddListener(() => { choice = "B"; decisionMadeThisTrial = true; });

            while (!decisionMadeThisTrial)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    choice = "A";
                    decisionMadeThisTrial = true;
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    choice = "B";
                    decisionMadeThisTrial = true;
                }
                yield return null;
            }

            Debug.Log($"Training: Participant chose option {choice}.");
            SetButtonInteraction(false);

            // Post-Decision Annotations: Use the fixed timer wait.
            if (choice == "A")
                yield return ShowAnnotation(training.postDecisionAnnotationA, 3.0f);
            else
                yield return ShowAnnotation(training.postDecisionAnnotationB, 3.0f);

            HideAllAnnotationImages();

            trialPanel.SetActive(false);
            trainingFeedbackPanel.SetActive(true);

            if (trainingContinueButton != null)
            {
                trainingContinueButton.gameObject.SetActive(false);
            }

            bool partnerFollowed = Random.value > 0.3f;

            float selfPayoff, otherPayoff;
            if (partnerFollowed)
            {
                if (choice == "A") { selfPayoff = training.optionASelf; otherPayoff = training.optionAOther; }
                else { selfPayoff = training.optionBSelf; otherPayoff = training.optionBOther; }
            }
            else
            {
                if (choice == "A") { selfPayoff = training.optionBSelf; otherPayoff = training.optionBOther; }
                else { selfPayoff = training.optionASelf; otherPayoff = training.optionAOther; }
            }
            float totalPayoff = selfPayoff + otherPayoff;

            // --- Farsi Text Swap ---
            string displayChoice = choice;
            if (ShouldUseRTL())
            {
                if (choice == "A") displayChoice = "ب";
                else if (choice == "B") displayChoice = "الف";
            }

            string partnerMessageKey = partnerFollowed ? "partner_followed_msg" : "partner_ignored_msg";

            var feedbackFormatTask = GetLocalizedStringAsync(UILocalizationTable, "training_feedback_message");
            var partnerMessageTask = GetLocalizedStringAsync(UILocalizationTable, partnerMessageKey);
            yield return new WaitUntil(() => feedbackFormatTask.IsCompleted && partnerMessageTask.IsCompleted);

            string feedbackMessage;
            if (feedbackFormatTask.IsFaulted || partnerMessageTask.IsFaulted)
            {
                Debug.LogError("LOCALIZATION ERROR: Could not find feedback keys. Using default English text.");
                string partnerText = partnerFollowed ? "Your Partner has made their choice based on the information you shared" : "Your Partner has made their choice against the information you shared";
                feedbackMessage = $"You chose Option {displayChoice}.\n\n{partnerText}\n\nYour payoff is: {selfPayoff}\nThe other player's payoff is: {otherPayoff}\n\nThe total payoff for this choice is: {totalPayoff}\n\nPress SPACE to continue.";
            }
            else
            {
                string localizedFormat = feedbackFormatTask.Result;
                string partnerMessage = partnerMessageTask.Result;
                feedbackMessage = string.Format(localizedFormat, displayChoice, partnerMessage, selfPayoff, otherPayoff, totalPayoff);
            }
            // --- End Farsi Text Swap ---

            trainingFeedbackText.text = feedbackMessage;

            bool continuePressed = false;
            while (!continuePressed)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    continuePressed = true;
                }
                yield return null;
            }

            trainingFeedbackPanel.SetActive(false);
            trialPanel.SetActive(true);
        }

        // Clear the instruction text after training is complete
        if (TrialInstructionText != null)
        {
            TrialInstructionText.text = "";
        }

        trialPanel.SetActive(false);
        Debug.Log("--- Training Session Complete ---");
    }

    // --- REMOVED: WaitForAnnotationDisplayOrKey is now integrated into RunTrainingSession. ---

    private IEnumerator ShowAnnotation(TrainingTrial.Annotation annotation, float duration)
    {
        Image targetImage = null;
        switch (annotation.position)
        {
            case AnnotationPosition.Left:
                targetImage = annotationImageLeft;
                break;
            case AnnotationPosition.Right:
                targetImage = annotationImageRight;
                break;
            case AnnotationPosition.CenterSmall: // NEW: Added case for the small center image
                targetImage = annotationImageCenterSmall;
                break;
            default: // Center
                targetImage = annotationImageCenter;
                break;
        }

        if (targetImage != null && annotation.image != null)
        {
            HideAllAnnotationImages(); // Clear previous images
            targetImage.sprite = annotation.image;
            targetImage.color = Color.white; // Make it fully visible
            targetImage.gameObject.SetActive(true);
            yield return null; // Wait 1 frame after showing
            // Note: The duration is handled by the caller (RunTrainingSession loop logic)
        }
        else if (annotation.image == null)
        {
            Debug.LogWarning("ShowAnnotation called, but no sprite was assigned in the Inspector.");
        }
    }

    // MODIFIED: Helper function to hide all annotation IMAGES at once
    private void HideAllAnnotationImages()
    {
        if (annotationImageCenter != null)
        {
            annotationImageCenter.gameObject.SetActive(false);
            annotationImageCenter.sprite = null;
        }
        if (annotationImageLeft != null)
        {
            annotationImageLeft.gameObject.SetActive(false);
            annotationImageLeft.sprite = null;
        }
        if (annotationImageRight != null)
        {
            annotationImageRight.gameObject.SetActive(false);
            annotationImageRight.sprite = null;
        }
        if (annotationImageCenterSmall != null) // NEW: Added logic to hide the small center image
        {
            annotationImageCenterSmall.gameObject.SetActive(false);
            annotationImageCenterSmall.sprite = null;
        }
    }


    IEnumerator RunAllTrials()
    {
        Debug.Log("RunAllTrials: Starting experiment run sequence.");
        int actualRegularTrials = currentTrialList?.Count ?? 0;
        int actualAttentionTests = attentionTests?.Count ?? 0;
        int totalEvents = actualRegularTrials + actualAttentionTests; // This is the total number of items to run

        if (totalEvents == 0)
        {
            Debug.LogError("RunAllTrials: No trials or attention tests loaded/found. Cannot run experiment.");
            EndTrials();
            yield break;
        }
        if (totalTrials != actualRegularTrials)
        {
            Debug.LogWarning($"Mismatch between Inspector totalTrials ({totalTrials}) and loaded trials ({actualRegularTrials}). Using loaded count for event calculations.");
        }

        if (trialsPerRun <= 0)
        {
            Debug.LogError($"RunAllTrials: Invalid 'trialsPerRun' value ({trialsPerRun}). Setting to {totalEvents} to run all in one go.");
            trialsPerRun = totalEvents;
        }
        int totalRuns = (trialsPerRun > 0) ? Mathf.CeilToInt((float)totalEvents / trialsPerRun) : 1;

        Debug.Log($"RunAllTrials: {totalEvents} total events ({actualRegularTrials} regular, {actualAttentionTests} attention). {trialsPerRun} events/run. {totalRuns} runs total.");

        int eventCounter = 0;

        for (int eventIndex = startEventIndex; eventIndex < totalEvents; eventIndex++)
        {
            int run = eventIndex / trialsPerRun;
            int trialInRun = eventIndex % trialsPerRun;

            if (trialInRun == 0 && eventIndex > 0)
            {
                Debug.Log($"---------- Starting Run {run + 1} / {totalRuns} ----------");
            }

            eventCounter = eventIndex + 1;
            Debug.Log($"Run {run + 1}, Event {eventCounter}/{totalEvents} (Event Index: {eventIndex})");

            if (attentionTestIndices.Contains(eventIndex))
            {
                if (attentionTestIndexToTestIndex.TryGetValue(eventIndex, out int testIndex))
                {
                    if (testIndex >= 0 && testIndex < attentionTests.Count)
                    {
                        Debug.Log($"Running Attention Test (List Index: {testIndex})");
                        yield return StartCoroutine(RunAttentionTest(attentionTests[testIndex], eventCounter));
                    }
                    else
                    {
                        Debug.LogError($"Invalid attention test index {testIndex} mapped for event index {eventIndex}. Skipping.");
                    }
                }
                else
                {
                    Debug.LogError($"Attention test index found in Set ({eventIndex}) but not in Dictionary. Data mismatch! Skipping.");
                }
            }
            else
            {
                int adjustedIndex = GetAdjustedTrialIndex(eventIndex);
                if (adjustedIndex >= 0 && adjustedIndex < currentTrialList.Count)
                {
                    Debug.Log($"Running Regular Trial (List Index: {adjustedIndex})");
                    yield return StartCoroutine(RunTrial(currentTrialList[adjustedIndex], eventCounter));
                }
                else
                {
                    Debug.LogError($"Adjusted trial index {adjustedIndex} is out of bounds for currentTrialList (size: {currentTrialList.Count}) at event index {eventIndex}. Skipping and logging as an error.");
                    trialResponses.Add("Error/Skipped_OutOfBounds");
                    DataLogger.LogTrial(eventCounter, "Error", "Skipped_OutOfBounds", 0f, new List<float>());
                }
            }
            Debug.Log($"---------- Event {eventCounter} Finished ----------");

            if (trialInRun == trialsPerRun - 1 && run < totalRuns - 1)
            {
                Debug.Log($"---------- Run {run + 1} Finished ----------");

                // --- NEW LOGIC: RUN FEEDBACK ---
                int outcomeIndex = feedbackTrialOutcomes.Count - 1; // Last added outcome
                if (outcomeIndex >= 0)
                {
                    yield return StartCoroutine(DisplayRunFeedback(feedbackTrialOutcomes[outcomeIndex], run + 1));
                }
                // --- END NEW LOGIC: RUN FEEDBACK ---

                DataLogger.FlushData();
                SaveExperimentState();

                if (interRunPanel != null && interRunInterval > 0)
                {
                    Debug.Log($"Starting Inter-Run Break for {interRunInterval} seconds.");
                    DataLogger.LogInterRunStart(eventCounter);
                    if (interRunText != null)
                    {
                        Task<string> textTask = GetLocalizedStringAsync(UILocalizationTable, "inter_run_text");
                        yield return new WaitUntil(() => textTask.IsCompleted);
                        if (!textTask.IsFaulted && !textTask.IsCanceled) interRunText.text = textTask.Result;
                        else Debug.LogWarning("Failed to get inter-run text.");
                    }
                    interRunPanel.SetActive(true);
                    trialPanel?.SetActive(false);
                    fixationPanel?.SetActive(false);
                    yield return StartCoroutine(WaitPrecise(interRunInterval));
                    interRunPanel.SetActive(false);
                    Debug.Log("Inter-Run Break Finished.");
                }
                else
                {
                    if (interRunPanel == null) Debug.LogWarning("InterRunPanel not assigned. Skipping break.");
                    else Debug.Log("InterRunInterval is 0. Skipping break.");
                }
            }
        }

        Debug.Log($"---------- Run {totalRuns} Finished ----------");

        DataLogger.FlushData();
        SaveExperimentState();

        Debug.Log("RunAllTrials: All runs completed.");
        EndTrials();
    }
    IEnumerator DisplayRunFeedback(FeedbackTrialOutcome outcome, int runNumber)
    {
        trialPanel.SetActive(false);
        fixationPanel.SetActive(false);
        interRunPanel.SetActive(false);

        trainingFeedbackPanel.SetActive(true);
        if (trainingContinueButton != null)
        {
            trainingContinueButton.gameObject.SetActive(false);
        }

        GameObject annotationLineInstance = null;

        // --- CRITICAL SWAP LOGIC ---
        RectTransform originalChartContainer = null;
        Transform originalGroupA = null;
        Transform originalGroupB = null;
        RectTransform originalLabels = null;

        if (barChartManager != null && FeedbackChartContainer != null)
        {
            originalChartContainer = barChartManager.chartContainer;
            originalGroupA = barChartManager.groupAContainer;
            originalGroupB = barChartManager.groupBContainer;
            originalLabels = barChartManager.groupLabelsContainer;

            barChartManager.chartContainer = FeedbackChartContainer;
            barChartManager.groupAContainer = FeedbackGroupAContainer;
            barChartManager.groupBContainer = FeedbackGroupBContainer;
            barChartManager.groupLabelsContainer = FeedbackGroupLabelsContainer;

            FeedbackChartContainer.gameObject.SetActive(true);
        }
        // --- END CRITICAL SWAP LOGIC ---

        // --- DRAW CHART LOGIC ---
        SignallingTaskData.TrialData originalTrial = null;
        if (barChartManager != null && barChartManager.chartContainer != null)
        {
            if (outcome.originalTrialListIndex >= 0 && outcome.originalTrialListIndex < currentTrialList.Count)
            {
                originalTrial = currentTrialList[outcome.originalTrialListIndex];

                barChartManager.CreateBarChart(
                    originalTrial.optionA_Self,
                    originalTrial.optionA_Other,
                    originalTrial.optionB_Self,
                    originalTrial.optionB_Other);
            }
        }
        // --- END DRAW CHART LOGIC ---

        // --- ANNOTATION LINE LOGIC (Ensures correct placement relative to chart base) ---
        if (feedbackAnnotationLinePrefab != null && originalTrial != null && FeedbackChartContainer != null)
        {
            // 1. Determine the target group container
            RectTransform targetGroupRect = null;
            if (outcome.participantChoice == "A")
            {
                targetGroupRect = FeedbackGroupAContainer as RectTransform;
            }
            else if (outcome.participantChoice == "B")
            {
                targetGroupRect = FeedbackGroupBContainer as RectTransform;
            }

            if (targetGroupRect != null)
            {
                // 2. Instantiate the line as a child of the main FeedbackChartContainer (SEPARATE from text)
                annotationLineInstance = Instantiate(feedbackAnnotationLinePrefab, FeedbackChartContainer, false);
                annotationLineInstance.name = "SelectedAnnotationLine";
                RectTransform lineRect = annotationLineInstance.GetComponent<RectTransform>();

                if (lineRect != null)
                {
                    // 3. Match the line's horizontal position/width to the target group container
                    lineRect.anchorMin = new Vector2(0.5f, 0f);
                    lineRect.anchorMax = new Vector2(0.5f, 0f);
                    lineRect.pivot = new Vector2(0.5f, 0f);

                    // Copy the horizontal position and width of the target bar group
                    lineRect.anchoredPosition = new Vector2(
                        targetGroupRect.anchoredPosition.x,
                        -5f // Position slightly below the bar base line
                    );

                    lineRect.sizeDelta = new Vector2(
                        targetGroupRect.sizeDelta.x,
                        lineRect.sizeDelta.y // Keep the height (e.g., 5) set in the prefab
                    );
                }
            }
        }
        // --- END ANNOTATION LINE LOGIC ---

        // --- DISPLAY FEEDBACK MESSAGE LOGIC (No Change) ---
        float totalPayoff = outcome.selfPayoff + outcome.otherPayoff;
        string displayChoice = outcome.participantChoice;

        if (ShouldUseRTL())
        {
            if (displayChoice == "A") displayChoice = "ب";
            else if (displayChoice == "B") displayChoice = "الف";
        }

        string partnerMessageKey = outcome.partnerFollowed ? "partner_followed_msg" : "partner_ignored_msg";

        var feedbackFormatTask = GetLocalizedStringAsync(UILocalizationTable, "training_feedback_message");
        var partnerMessageTask = GetLocalizedStringAsync(UILocalizationTable, partnerMessageKey);
        yield return new WaitUntil(() => feedbackFormatTask.IsCompleted && partnerMessageTask.IsCompleted);

        string feedbackMessage;
        if (feedbackFormatTask.IsFaulted || partnerMessageTask.IsFaulted)
        {
            string partnerText = outcome.partnerFollowed ? "Your Partner has made their choice based on the information you shared" : "Your Partner has made their choice against the information you shared";
            feedbackMessage = $"Feedback for Run {runNumber}:\nYou chose Option {displayChoice}.\n\n{partnerText}\n\nYour payoff is: {outcome.selfPayoff}\nThe other player's payoff is: {outcome.otherPayoff}\n\nThe total payoff for this choice is: {totalPayoff}\n\nPress SPACE to continue.";
        }
        else
        {
            string localizedFormat = feedbackFormatTask.Result;
            string partnerMessage = partnerMessageTask.Result;
            feedbackMessage = string.Format(localizedFormat, displayChoice, partnerMessage, outcome.selfPayoff, outcome.otherPayoff, totalPayoff);
        }
        if (trainingFeedbackText != null) trainingFeedbackText.text = feedbackMessage;
        // --- END DISPLAY FEEDBACK MESSAGE LOGIC ---

        // **CRITICAL FIX: WAIT FOR SPACE KEY (No Change) **
        bool continuePressed = false;
        while (!continuePressed)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                continuePressed = true;
            }
            yield return null;
        }
        // **END CRITICAL FIX**

        // --- RESTORE SWAP LOGIC & CLEANUP ---
        if (barChartManager != null && originalChartContainer != null)
        {
            barChartManager.chartContainer = originalChartContainer;
            barChartManager.groupAContainer = originalGroupA;
            barChartManager.groupBContainer = originalGroupB;
            barChartManager.groupLabelsContainer = originalLabels;

            if (FeedbackChartContainer != null)
            {
                FeedbackChartContainer.gameObject.SetActive(false);
            }
        }

        // --- CLEANUP ANNOTATION LINE ---
        if (annotationLineInstance != null)
        {
            Destroy(annotationLineInstance);
        }
        // --- END CLEANUP ---

        trainingFeedbackPanel.SetActive(false);
    }
    private void DeleteStateFile()
    {
        string filePath = GetStateFilePath(this.participantId, this.currentTask.ToString(), this.currentSeries);
        if (System.IO.File.Exists(filePath))
        {
            try
            {
                System.IO.File.Delete(filePath);
                Debug.Log("Experiment completed. Resume file deleted.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not delete resume file: {ex.Message}");
            }
        }
    }

    private void AttemptToLoadState(string pId, TaskType task, int ser)
    {
        string filePath = GetStateFilePath(pId, task.ToString(), ser);
        if (System.IO.File.Exists(filePath))
        {
            Debug.Log($"<color=orange>Resume file found at {filePath}. Attempting to load state.</color>");
            try
            {
                string jsonState = System.IO.File.ReadAllText(filePath);
                ExperimentState loadedState = JsonUtility.FromJson<ExperimentState>(jsonState);

                if (loadedState.participantId == pId)
                {
                    this.currentTrialList = loadedState.shuffledTrialOrder;
                    this.attentionTestIndices = new HashSet<int>(loadedState.attentionTestEventIndices);
                    this.trialResponses = new List<string>(loadedState.trialResponses);
                    this.startEventIndex = loadedState.lastCompletedEventIndex + 1;
                    this.totalTrials = this.currentTrialList.Count;

                    if (SignallingTaskData.SignallingTrialLoader.Instance != null && SignallingTaskData.SignallingTrialLoader.Instance.AttentionTests != null)
                    {
                        this.attentionTests = new List<SignallingTaskData.AttentionTestData>(SignallingTaskData.SignallingTrialLoader.Instance.AttentionTests);
                        Debug.Log($"Loaded {this.attentionTests.Count} attention tests from SignallingTrialLoader during resume.");
                    }
                    else
                    {
                        Debug.LogError("AttemptToLoadState: Failed to load attention tests from SignallingTrialLoader. Attention tests will fail.");
                        this.attentionTests = new List<SignallingTaskData.AttentionTestData>(); // Ensure it's an empty list, not null
                    }

                    attentionTestIndexToTestIndex.Clear();
                    List<int> sortedIndices = new List<int>(this.attentionTestIndices);
                    sortedIndices.Sort();
                    for (int i = 0; i < sortedIndices.Count; i++)
                    {
                        attentionTestIndexToTestIndex[sortedIndices[i]] = i;
                    }

                    Debug.Log($"<color=green>State successfully loaded. Resuming from event number {this.startEventIndex + 1}.</color>");
                    isDataLoaded = true;
                }
                else
                {
                    Debug.LogWarning("Found resume file, but participant ID did not match. Starting a new session.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error loading experiment state, a new session will be started. Error: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("No resume file found. Starting a new session.");
        }
    }
    private int GetAdjustedTrialIndex(int eventIndex)
    {
        int adjustment = 0;
        foreach (int testIndex in attentionTestIndices)
        {
            if (eventIndex > testIndex)
            {
                adjustment++;
            }
        }
        return eventIndex - adjustment; //new
    }

    IEnumerator RunTrial(SignallingTaskData.TrialData trial, int eventNumber)
    {
        int totalEventCount = (currentTrialList?.Count ?? 0) + (attentionTests?.Count ?? 0);
        // Corrected property access to use underscores: optionA_Self, optionA_Other, optionB_Self, optionB_Other
        Debug.Log($"RunTrial {eventNumber}/{totalEventCount}: Start. Type: {currentTask}. A:[{trial.optionA_Self},{trial.optionA_Other}], B:[{trial.optionB_Self},{trial.optionB_Other}]");
        optionAButton?.gameObject.SetActive(true);
        optionBButton?.gameObject.SetActive(true);

        selectionEnabled = false;
        decisionMade = false;
        trialPanel.SetActive(true);
        fixationPanel.SetActive(false);

        // --- CHART ACTIVATION (TRIAL) ---
        // 1. Ensure the main trial chart is visible
        if (barChartManager != null && barChartManager.chartContainer != null)
        {
            barChartManager.chartContainer.gameObject.SetActive(true);
        }
        // 2. Hide the duplicate RUQ chart
        if (FeedbackChartContainer != null)
        {
            FeedbackChartContainer.gameObject.SetActive(false);
        }
        // --- END CHART ACTIVATION ---

        Task<string> trialInfoFormatTask = GetLocalizedStringAsync(UILocalizationTable, "trial_info");
        yield return new WaitUntil(() => trialInfoFormatTask.IsCompleted);
        if (!trialInfoFormatTask.IsFaulted && !trialInfoFormatTask.IsCanceled)
        {
            trialInfoText.text = string.Format(trialInfoFormatTask.Result, eventNumber, totalEventCount);
        }
        else
        {
            trialInfoText.text = $"Event {eventNumber}/{totalEventCount}";
        }

        if (barChartManager != null)
        {
            // Corrected property access to use underscores
            barChartManager.CreateBarChart(trial.optionA_Self, trial.optionA_Other, trial.optionB_Self, trial.optionB_Other);
        }
        else { Debug.LogWarning($"RunTrial {eventNumber}: BarChartManager not found."); }

        string optionAKey = (currentTask == TaskType.Deception) ? "deception_option_a" : "control_option_a";
        string optionBKey = (currentTask == TaskType.Deception) ? "deception_option_b" : "control_option_b";
        Task<string> optionATextTask = GetLocalizedStringAsync(UILocalizationTable, optionAKey);
        Task<string> optionBTextTask = GetLocalizedStringAsync(UILocalizationTable, optionBKey);
        yield return new WaitUntil(() => optionATextTask.IsCompleted && optionBTextTask.IsCompleted);
        SetButtonText(optionAButtonText, optionATextTask.IsCompletedSuccessfully ? optionATextTask.Result : $"[{optionAKey}]");
        SetButtonText(optionBButtonText, optionBTextTask.IsCompletedSuccessfully ? optionBTextTask.Result : $"[{optionBKey}]");

        SetButtonInteraction(false);
        Debug.Log($"RunTrial {eventNumber}: Onset Phase ({trialOnsetDuration}s)");
        yield return StartCoroutine(WaitPrecise(trialOnsetDuration));

        Debug.Log($"RunTrial {eventNumber}: Decision Phase Start (Waiting for input)");
        yield return new WaitForEndOfFrame();
        SetupTrialButtons();

        yield return new WaitUntil(() => decisionMade);
        float responseTime = Time.realtimeSinceStartup - decisionStartTime;
        selectionEnabled = false;

        string messageChosen = "Error/LogMismatch";
        if (trialResponses.Count >= eventNumber)
        {
            messageChosen = trialResponses[eventNumber - 1];
        }
        else
        {
            Debug.LogError($"Response log missing for event {eventNumber}! Log count: {trialResponses.Count}.");
            if (trialResponses.Count == eventNumber - 1) trialResponses.Add(messageChosen);
        }
        Debug.Log($"RunTrial {eventNumber}: Decision Made. Choice: {messageChosen}, RT: {responseTime:F3}s");

        List<float> barData = new List<float> { trial.optionA_Self, trial.optionA_Other, trial.optionB_Self, trial.optionB_Other };
        DataLogger.LogTrial(eventNumber, currentTask.ToString(), messageChosen, responseTime, barData);

        // --- NEW LOGIC: Check for Feedback Trial and Save Outcome ---
        int trialListIndex = currentTrialList.IndexOf(trial);

        // The first regular trial of the run is chosen for feedback
        // Check if the current event is a regular trial AND the first trial slot in the run
        if (!attentionTestIndices.Contains(eventNumber - 1) && (eventNumber - 1) % trialsPerRun == 0)
        {
            float selfPayoff, otherPayoff;
            bool partnerFollowed = Random.value > 0.3f; // 70% chance to follow

            if (partnerFollowed)
            {
                // Corrected property access to use underscores
                if (messageChosen == "A") { selfPayoff = trial.optionA_Self; otherPayoff = trial.optionA_Other; }
                else { selfPayoff = trial.optionB_Self; otherPayoff = trial.optionB_Other; }
            }
            else
            {
                // Partner ignores the message (chooses the opposite option)
                // Corrected property access to use underscores
                if (messageChosen == "A") { selfPayoff = trial.optionB_Self; otherPayoff = trial.optionB_Other; }
                else { selfPayoff = trial.optionA_Self; otherPayoff = trial.optionA_Other; }
            }

            feedbackTrialOutcomes.Add(new FeedbackTrialOutcome
            {
                originalTrialListIndex = trialListIndex,
                participantChoice = messageChosen,
                selfPayoff = selfPayoff,
                otherPayoff = otherPayoff,
                partnerFollowed = partnerFollowed
            });
            DataLogger.LogFeedbackTrial(eventNumber, selfPayoff, otherPayoff, partnerFollowed, messageChosen);
            Debug.Log($"RunTrial {eventNumber}: Chosen for Run Feedback. Self: {selfPayoff}, Other: {otherPayoff}");
        }
        // --- END NEW LOGIC ---

        float confirmationDuration = Random.Range(decisionConfirmationMin, decisionConfirmationMax);
        Debug.Log($"RunTrial {eventNumber}: Confirmation Phase ({confirmationDuration:F2}s)");
        yield return StartCoroutine(WaitPrecise(confirmationDuration));//new

        Debug.Log($"RunTrial {eventNumber}: Fixation Phase Start");
        trialPanel.SetActive(false);
        fixationPanel.SetActive(true);
        float fixationDuration = Random.Range(fixationMin, fixationMax);
        yield return StartCoroutine(WaitPrecise(fixationDuration));//new
        Debug.Log($"RunTrial {eventNumber}: Fixation Phase End ({fixationDuration:F2}s)");
        fixationPanel.SetActive(false);
        Debug.Log($"RunTrial {eventNumber}: Complete.");
    }

    IEnumerator RunAttentionTest(SignallingTaskData.AttentionTestData test, int eventNumber)
    {
        int totalEventCount = (currentTrialList?.Count ?? 0) + (attentionTests?.Count ?? 0);
        Debug.Log($"RunAttentionTest {eventNumber}/{totalEventCount}: Start. Correct: {test.correctAnswer}. A:[{test.optionA_Self},{test.optionA_Other}], B:[{test.optionB_Self},{test.optionB_Other}]");
        optionAButton?.gameObject.SetActive(true);
        optionBButton?.gameObject.SetActive(true);

        selectionEnabled = false;
        decisionMade = false;
        trialPanel.SetActive(true);
        fixationPanel.SetActive(false);

        // --- CHART ACTIVATION (TRIAL) ---
        // 1. Ensure the main trial chart is visible
        if (barChartManager != null && barChartManager.chartContainer != null)
        {
            barChartManager.chartContainer.gameObject.SetActive(true);
        }
        // 2. Hide the duplicate RUQ chart
        if (FeedbackChartContainer != null)
        {
            FeedbackChartContainer.gameObject.SetActive(false);
        }
        // --- END CHART ACTIVATION ---

        Task<string> trialInfoFormatTask = GetLocalizedStringAsync(UILocalizationTable, "trial_info");
        yield return new WaitUntil(() => trialInfoFormatTask.IsCompleted);
        if (!trialInfoFormatTask.IsFaulted && !trialInfoFormatTask.IsCanceled)
        {
            trialInfoText.text = string.Format(trialInfoFormatTask.Result, eventNumber, totalEventCount);
        }
        else { trialInfoText.text = $"Event {eventNumber}/{totalEventCount}"; }

        if (barChartManager != null)
        {
            barChartManager.CreateBarChart(test.optionA_Self, test.optionA_Other, test.optionB_Self, test.optionB_Other);
        }
        else { Debug.LogWarning($"RunAttentionTest {eventNumber}: BarChartManager not found."); }

        Task<string> optionATextTask = GetLocalizedStringAsync(UILocalizationTable, "attention_option_a");
        Task<string> optionBTextTask = GetLocalizedStringAsync(UILocalizationTable, "attention_option_b");
        yield return new WaitUntil(() => optionATextTask.IsCompleted && optionBTextTask.IsCompleted);
        SetButtonText(optionAButtonText, optionATextTask.IsCompletedSuccessfully ? optionATextTask.Result : "[attention_option_a]");
        SetButtonText(optionBButtonText, optionBTextTask.IsCompletedSuccessfully ? optionBTextTask.Result : "[attention_option_b]");

        SetButtonInteraction(false);
        Debug.Log($"RunAttentionTest {eventNumber}: Onset Phase ({trialOnsetDuration}s)");
        yield return StartCoroutine(WaitPrecise(trialOnsetDuration));//new

        Debug.Log($"RunAttentionTest {eventNumber}: Decision Phase Start (Waiting for input)");
        yield return new WaitForEndOfFrame();
        SetupTrialButtons();

        yield return new WaitUntil(() => decisionMade);
        float responseTime = Time.realtimeSinceStartup - decisionStartTime;
        selectionEnabled = false;

        string response = "Error/LogMismatch";
        if (trialResponses.Count >= eventNumber)
        {
            response = trialResponses[eventNumber - 1];
        }
        else
        {
            Debug.LogError($"Response log missing for event {eventNumber}! Log count: {trialResponses.Count}.");
            if (trialResponses.Count == eventNumber - 1) trialResponses.Add(response);
        }
        bool correct = response == test.correctAnswer;
        Debug.Log($"RunAttentionTest {eventNumber}: Decision Made. Choice: {response}, Correct: {correct}, RT: {responseTime:F3}s");

        DataLogger.LogAttentionTest(eventNumber, response, responseTime);

        float confirmationDuration = Random.Range(decisionConfirmationMin, decisionConfirmationMax);
        Debug.Log($"RunAttentionTest {eventNumber}: Confirmation Phase ({confirmationDuration:F2}s)");
        yield return StartCoroutine(WaitPrecise(confirmationDuration));

        Debug.Log($"RunAttentionTest {eventNumber}: Fixation Phase Start");
        trialPanel.SetActive(false);
        fixationPanel.SetActive(true);
        float fixationDuration = Random.Range(fixationMin, fixationMax);
        yield return StartCoroutine(WaitPrecise(fixationDuration));
        Debug.Log($"RunAttentionTest {eventNumber}: Fixation Phase End ({fixationDuration:F2}s)");
        fixationPanel.SetActive(false);
        Debug.Log($"RunAttentionTest {eventNumber}: Complete.");
    }

    void SetButtonInteraction(bool interactable)
    {
        float alpha = interactable ? 1.0f : 0.5f;
        if (optionAButton != null)
        {
            optionAButton.interactable = interactable;
            if (optionAButtonImage != null) SetButtonTransparency(optionAButtonImage, alpha);
        }
        if (optionBButton != null)
        {
            optionBButton.interactable = interactable;
            if (optionBButtonImage != null) SetButtonTransparency(optionBButtonImage, alpha);
        }
    }

    private void SetupTrialButtons()
    {
        optionAButton?.gameObject.SetActive(true);
        optionBButton?.gameObject.SetActive(true);
        selectionEnabled = true;
        SetButtonInteraction(true);
        decisionMade = false;
        decisionStartTime = Time.realtimeSinceStartup;
        optionAButton?.onClick.RemoveAllListeners();
        optionBButton?.onClick.RemoveAllListeners();
        optionAButton?.onClick.AddListener(() => OnDecisionMade("A"));
        optionBButton?.onClick.AddListener(() => OnDecisionMade("B"));
        if (eventSystem == null)
        {
            eventSystem = EventSystem.current;
        }
        if (eventSystem != null && optionAButton != null && optionAButton.interactable)
        {
            eventSystem.SetSelectedGameObject(optionAButton.gameObject);
        }
        else
        {
            if (eventSystem == null) Debug.LogWarning("SetupTrialButtons: EventSystem is null. Cannot set initial focus.");
            else if (optionAButton == null) Debug.LogWarning("SetupTrialButtons: Option A Button is null. Cannot set initial focus.");
        }
    }

    void OnDecisionMade(string choice)
    {
        if (!selectionEnabled || decisionMade)
        {
            return;
        }
        decisionMade = true;
        trialResponses.Add(choice);
        if (choice == "A")
        {
            optionBButton?.gameObject.SetActive(false);
        }
        else if (choice == "B")
        {
            optionAButton?.gameObject.SetActive(false);
        }
    }

    void HandleKeyboardNavigation()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (optionAButton != null && optionAButton.interactable)
            {
                OnDecisionMade("A");
            }
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (optionBButton != null && optionBButton.interactable)
            {
                OnDecisionMade("B");
            }
        }
    }

    void EndTrials()
    {
        Debug.Log("EndTrials: All trial runs complete. Preparing end sequence.");
        trialPanel?.SetActive(false);
        fixationPanel?.SetActive(false);
        interRunPanel?.SetActive(false);
        if (endExperimentButton != null)
        {
            Debug.Log("EndTrials: Activating End Experiment Button.");
            endExperimentButton.gameObject.SetActive(true);
            Button endButtonComponent = endExperimentButton.GetComponent<Button>();
            if (endButtonComponent != null)
            {
                endButtonComponent.interactable = true;
                endButtonComponent.onClick.RemoveAllListeners();
                endButtonComponent.onClick.AddListener(EndExperiment);
                if (eventSystem != null)
                {
                    eventSystem.SetSelectedGameObject(endExperimentButton.gameObject);
                    Debug.Log("EndTrials: Focus set to End Experiment Button.");
                }
            }
            else
            {
                Debug.LogError("EndTrials: EndExperimentButton prefab is missing the Button component!");
                StartCoroutine(SaveAndQuitCoroutine("EndButtonComponentMissing"));
            }
        }
        else
        {
            Debug.LogWarning("EndTrials: EndExperimentButton was not assigned in the Inspector. Saving and quitting automatically.");
            StartCoroutine(SaveAndQuitCoroutine("EndButtonNotAssigned"));
        }
    }

    async void EndExperiment()
    {
        Debug.Log("EndExperiment: Button clicked. Saving data and preparing to exit.");
        if (endExperimentButton != null)
        {
            Button endButton = endExperimentButton.GetComponent<Button>();
            if (endButton != null) endButton.interactable = false;
        }

        DataLogger.SaveData(participantId, currentTask.ToString(), currentSeries);
        Debug.Log($"EndExperiment: Final data flush requested.");
        DeleteStateFile();

        // --- NEW LOGIC: Calculate and Display Final Payoff ---
        float finalSelfPayoff = 0;
        float finalOtherPayoff = 0;
        foreach (var outcome in feedbackTrialOutcomes)
        {
            finalSelfPayoff += outcome.selfPayoff;
            finalOtherPayoff += outcome.otherPayoff;
        }
        float finalTotalPayoff = finalSelfPayoff + finalOtherPayoff;

        // Get localized final message parts
        var endMessageTask = GetLocalizedStringAsync(UILocalizationTable, "end_experiment_text");
        var payoffSummaryTask = GetLocalizedStringAsync(UILocalizationTable, "final_payoff_summary");

        await Task.WhenAll(endMessageTask, payoffSummaryTask);

        string endMessage = endMessageTask.IsCompletedSuccessfully ? endMessageTask.Result : "[end_experiment_text]";
        string payoffSummary;

        if (payoffSummaryTask.IsCompletedSuccessfully)
        {
            // Pass all four parameters, even if the localized string only uses {0}, {2}, and {3}.
            payoffSummary = string.Format(payoffSummaryTask.Result,
                                          finalSelfPayoff,
                                          finalOtherPayoff,
                                          finalTotalPayoff,
                                          feedbackTrialOutcomes.Count);
        }
        else
        {
            Debug.LogError("LOCALIZATION ERROR: Failed to load final payoff summary. Using default.");
            payoffSummary = $"\n\n--- FINAL PAYOFF SUMMARY (Based on {feedbackTrialOutcomes.Count} trials) ---\n" +
                            $"Your Total Payoff (from sampled trials): {finalSelfPayoff}\n" +
                            $"Other Player's Total Payoff: {finalOtherPayoff}\n" +
                            $"Grand Total: {finalTotalPayoff}";
        }
        // --- END NEW LOGIC ---

        if (instructionPanel != null && instructionText != null)
        {
            instructionPanel.SetActive(true);
            instructionText.text = endMessage + payoffSummary; // Combine the two texts
            Debug.Log("EndExperiment: End message and payoff summary loaded and displayed.");
        }
        else
        {
            if (instructionPanel == null) Debug.LogWarning("EndExperiment: InstructionPanel not assigned.");
            if (instructionText == null) Debug.LogWarning("EndExperiment: InstructionText not assigned.");
        }
        Debug.Log($"EndExperiment: Scheduling application close in {closeDelay} seconds.");
        StartCoroutine(DelayedClose(closeDelay));
    }

    IEnumerator SaveAndQuitCoroutine(string reason = "Automatic")
    {
        Debug.Log($"SaveAndQuitCoroutine: Triggered automatically ({reason}). Saving data and quitting.");
        trialPanel?.SetActive(false);
        fixationPanel?.SetActive(false);
        instructionPanel?.SetActive(false);

        DataLogger.SaveData(participantId, currentTask.ToString(), currentSeries);
        Debug.Log($"SaveAndQuitCoroutine: Final data flush requested.");
        yield return StartCoroutine(WaitPrecise(2.0f));

        CloseApplication();
    }

    IEnumerator DelayedClose(float delay)
    {
        if (delay < 0) delay = 0;
        Debug.Log($"DelayedClose: Waiting for {delay} seconds before quitting.");
        yield return StartCoroutine(WaitPrecise(delay));//new
        CloseApplication();
    }

    void CloseApplication()
    {
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

    IEnumerator WaitPrecise(float duration)
    {
        float startTime = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup < startTime + duration)
        {
            yield return null;
        }
    }

    void SetButtonText(TMP_Text textComponent, string text)
    {
        if (textComponent != null)
        {
            textComponent.text = text;
        }
    }

    void SetButtonTransparency(Image buttonImage, float alpha)
    {
        if (buttonImage != null)
        {
            Color currentColor = buttonImage.color;
            currentColor.a = Mathf.Clamp01(alpha);
            buttonImage.color = currentColor;
        }
    }

    private void ShuffleTrials(List<SignallingTaskData.TrialData> list)
    {
        if (list == null || list.Count <= 1) return;

        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
    void InsertAttentionTests()
    {
        if (SignallingTaskData.SignallingTrialLoader.Instance == null)
        {
            Debug.LogError("InsertAttentionTests: SignallingTrialLoader.Instance is NULL! Cannot insert tests.");
            attentionTests = new List<SignallingTaskData.AttentionTestData>();
            attentionTestIndices.Clear();
            attentionTestIndexToTestIndex.Clear();
            return;
        }

        if (SignallingTaskData.SignallingTrialLoader.Instance.AttentionTests == null)
        {
            Debug.LogWarning("SignallingTrialLoader.Instance.AttentionTests is null. Cannot insert tests.");
            attentionTests = new List<SignallingTaskData.AttentionTestData>();
        }
        else
        {
            attentionTests = new List<SignallingTaskData.AttentionTestData>(SignallingTaskData.SignallingTrialLoader.Instance.AttentionTests);
        }

        attentionTestIndices.Clear();
        attentionTestIndexToTestIndex.Clear();

        int numAttentionTests = attentionTests.Count;
        int numRegularTrials = currentTrialList?.Count ?? 0;
        int totalEvents = numRegularTrials + numAttentionTests;

        if (numAttentionTests == 0 || numRegularTrials == 0)
        {
            Debug.Log("InsertAttentionTests: Not enough regular trials or attention tests to perform insertion.");
            return;
        }

        int minStartIndex = 2;
        if (totalEvents <= minStartIndex)
        {
            Debug.LogWarning("Not enough total events to insert attention tests according to rules. Skipping insertion.");
            return;
        }
        List<int> possibleIndices = Enumerable.Range(minStartIndex, totalEvents - minStartIndex).ToList();

        System.Random rng = new System.Random();
        possibleIndices = possibleIndices.OrderBy(x => rng.Next()).ToList();

        List<int> chosenIndices = possibleIndices.Take(numAttentionTests).ToList();
        chosenIndices.Sort();

        for (int i = 0; i < chosenIndices.Count; i++)
        {
            int eventIndex = chosenIndices[i];
            attentionTestIndices.Add(eventIndex);
            attentionTestIndexToTestIndex[eventIndex] = i;
        }

        if (attentionTestIndices.Count > 0)
        {
            Debug.Log($"InsertAttentionTests: Inserted {attentionTestIndices.Count} tests at event indices: {string.Join(", ", chosenIndices)}. Total Events: {totalEvents}");
        }
        else if (numAttentionTests > 0)
        {
            Debug.LogWarning("InsertAttentionTests: Failed to place any attention tests with the new logic.");
        }
    }
    async Task<string> GetLocalizedStringAsync(string tableName, string entryName)
    {
        if (!LocalizationSettings.HasSettings || LocalizationSettings.StringDatabase == null)
        {
            Debug.LogError($"GetLocalizedStringAsync: Localization Settings/Database not available! Cannot get '{tableName}/{entryName}'.");
            return $"[{entryName}]";
        }
        var initOp = LocalizationSettings.InitializationOperation;
        if (initOp.IsValid() && !initOp.IsDone)
        {
            await initOp.Task;
            while (!initOp.IsDone) { await Task.Yield(); }
        }
        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, entryName);
        string result = null;
        if (!operation.IsDone)
        {
            await operation.Task;
        }
        result = operation.Result;
        if (operation.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded && result != null)
        {
            return result;
        }
        else
        {
            Debug.LogWarning($"GetLocalizedStringAsync: Failed to get key '{entryName}' from table '{tableName}'. Status: {operation.Status}. Error: {operation.OperationException?.Message ?? "None"}. Locale: {LocalizationSettings.SelectedLocale?.Identifier.Code ?? "N/A"}");
            return $"[{entryName}]";
        }
    }
    public string GetParticipantId()
    {
        return this.participantId;
    }
}