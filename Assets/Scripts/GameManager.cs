using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

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
    public int totalTrials = 45;  // Total number of regular trials (without attention tests)
    public int trialsPerRun = 9;
    public float trialOnsetDuration = 2f;
    public float decisionConfirmationMin = 2f;
    public float decisionConfirmationMax = 4f;
    public float fixationMin = 2f;
    public float fixationMax = 4f;
    public float interRunInterval = 10f;
    public float closeDelay = 10f;

    // Cached components
    private Image optionAButtonImage;
    private Image optionBButtonImage;
    private TMP_Text optionAButtonText;
    private TMP_Text optionBButtonText;
    private EventSystem eventSystem;
    private BarChartManager barChartManager;
    private LegendManager legendManager;

    // Optimized data structures
    private List<TrialData> currentTrialList;
    private List<string> trialResponses = new List<string>();
    private List<float> responseTimes = new List<float>();
    private List<AttentionTestData> attentionTests = new List<AttentionTestData>();
    private HashSet<int> attentionTestIndices = new HashSet<int>();
    private Dictionary<int, int> attentionTestIndexToTestIndex = new Dictionary<int, int>();

    private bool decisionMade = false;
    private bool selectionEnabled = false;
    private float decisionStartTime;
    private Button currentlySelectedButton;

    // Localization table reference
    private const string UILocalizationTable = "UI"; // Your table name

    void Awake() {
        if (Instance == null) {
            Instance = this;
            InitializeComponents();
        } else {
            Destroy(gameObject);
        }
        // Ensure Localization is initialized before using it
        StartCoroutine(InitializeLocalization());
    }

    IEnumerator InitializeLocalization() {
        yield return LocalizationSettings.InitializationOperation;
        var setupTask = SetupUIAsync();
        yield return new WaitUntil(() => setupTask.IsCompleted);
    }

    // Helper to set the locale
    IEnumerator SetLocale(string languageCode) {
        var locale = LocalizationSettings.AvailableLocales.GetLocale(languageCode);
        if (locale != null) {
            var currentLocale = LocalizationSettings.SelectedLocale;
            if (currentLocale != locale)
            {
                LocalizationSettings.SelectedLocale = locale;
                // Wait until the locale has changed and tables are loaded
                yield return LocalizationSettings.InitializationOperation;
                Debug.Log($"Locale changed to: {languageCode}");
                // Refresh UI elements that depend on localization
                StartCoroutine(RefreshLocalizedUICoroutine());
            }
        } else {
            Debug.LogWarning($"Locale '{languageCode}' not found.");
        }
    }

    // Coroutine version of RefreshLocalizedUI
    IEnumerator RefreshLocalizedUICoroutine()
    {
        // Refresh Legend
        if (legendManager != null)
        {
            var legendTask = legendManager.RefreshLegend();
            yield return new WaitUntil(() => legendTask.IsCompleted);
        }

        // Refresh other UI elements
        var setupTask = SetupUIAsync();
        yield return new WaitUntil(() => setupTask.IsCompleted);
    }

    private void InitializeComponents() {
        // Cache frequently accessed components
        optionAButtonImage = optionAButton.GetComponent<Image>();
        optionBButtonImage = optionBButton.GetComponent<Image>();
        optionAButtonText = optionAButton.GetComponentInChildren<TMP_Text>();
        optionBButtonText = optionBButton.GetComponentInChildren<TMP_Text>();
        eventSystem = EventSystem.current;
        barChartManager = FindObjectOfType<BarChartManager>();
        legendManager = FindObjectOfType<LegendManager>();

        // Initialize UI state
        currentlySelectedButton = optionAButton;
        eventSystem.SetSelectedGameObject(currentlySelectedButton.gameObject);
    }

    private void LoadAndShuffleTrials() {
        currentTrialList = new List<TrialData>(TrialDataManager.DeceptionTrials);
        ShuffleTrials(currentTrialList);
    }

    void Start() {
        // Show the instruction panel and hide other panels
        instructionPanel.SetActive(true);
        trialPanel.SetActive(false);
        fixationPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        interRunPanel.SetActive(false);

        DataLogger.Initialize();
        LoadAndShuffleTrials();
        InsertAttentionTests();

        // Set the language (e.g., Farsi) - ensure "fa" is a locale you've set up
        StartCoroutine(SetLocale("fa"));
    }

    async System.Threading.Tasks.Task SetupUIAsync() { // Renamed and made async
        instructionPanel.SetActive(true);
        trialPanel.SetActive(false);
        fixationPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        if (endExperimentButton != null)
            endExperimentButton.gameObject.SetActive(false);
        if (interRunPanel != null) {
            interRunPanel.SetActive(false);
            if (interRunText != null)
                interRunText.text = await GetLocalizedStringAsync(UILocalizationTable, "inter_run_text");
        }

        instructionText.text = await GetLocalizedStringAsync(UILocalizationTable, "welcome_text");

        SetButtonTransparency(optionAButtonImage, 0.5f);
        SetButtonTransparency(optionBButtonImage, 0.5f);
    }

    void Update() {
        if (instructionPanel.activeSelf && Input.anyKeyDown) {
            instructionPanel.SetActive(false);
            StartCoroutine(RunAllTrials());
        }
        HandleKeyboardNavigation();
    }

    IEnumerator RunAllTrials() {
        yield return new WaitUntil(() => instructionManager.instructionsComplete);

        int totalEvents = totalTrials + attentionTests.Count;
        int totalRuns = Mathf.CeilToInt((float)totalEvents / trialsPerRun);

        for (int run = 0; run < totalRuns; run++) {
            for (int trialInRun = 0; trialInRun < trialsPerRun; trialInRun++) {
                int eventIndex = run * trialsPerRun + trialInRun;
                if (eventIndex >= totalEvents) break;

                if (attentionTestIndices.Contains(eventIndex)) {
                    int testIndex = attentionTestIndexToTestIndex[eventIndex];
                    yield return StartCoroutine(RunAttentionTest(attentionTests[testIndex], eventIndex + 1));
                } else {
                    int adjustedIndex = GetAdjustedTrialIndex(eventIndex);
                    yield return StartCoroutine(RunTrial(currentTrialList[adjustedIndex], eventIndex + 1));
                }
            }

            if (run < totalRuns - 1 && interRunPanel != null) {
                interRunPanel.SetActive(true);
                yield return new WaitForSeconds(interRunInterval);
                interRunPanel.SetActive(false);
            }
        }
        EndTrials();
    }

    // Calculates the index offset caused by inserted attention tests.
    private int GetAdjustedTrialIndex(int currentIndex) {
        int adjustment = 0;
        foreach (int testIndex in attentionTestIndices) {
            if (currentIndex >= testIndex) {
                adjustment++;
            }
        }
        return currentIndex - adjustment;
    }

    IEnumerator RunTrial(TrialData trial, int trialNumber) {
        selectionEnabled = false;
        trialPanel.SetActive(true);

        // Format the localized string with parameters
        var task = GetLocalizedStringAsync(UILocalizationTable, "trial_info");
        yield return new WaitUntil(() => task.IsCompleted);
        string trialInfoFormat = task.Result;
        trialInfoText.text = string.Format(trialInfoFormat, trialNumber, totalTrials + attentionTests.Count);

        if (barChartManager != null) {
            barChartManager.CreateBarChart(trial.optionA_Self, trial.optionA_Other, trial.optionB_Self, trial.optionB_Other);
        }

        // Set button text immediately using localized strings
        string optionAKey = (currentTask == TaskType.Deception) ? "deception_option_a" : "control_option_a";
        string optionBKey = (currentTask == TaskType.Deception) ? "deception_option_b" : "control_option_b";
        
        var taskA = GetLocalizedStringAsync(UILocalizationTable, optionAKey);
        var taskB = GetLocalizedStringAsync(UILocalizationTable, optionBKey);
        yield return new WaitUntil(() => taskA.IsCompleted && taskB.IsCompleted);
        
        SetButtonText(optionAButtonText, taskA.Result);
        SetButtonText(optionBButtonText, taskB.Result);

        // Set buttons to unresponsive and transparent state
        SetButtonTransparency(optionAButtonImage, 0.5f);
        SetButtonTransparency(optionBButtonImage, 0.5f);

        // Wait for onset duration
        yield return new WaitForSeconds(trialOnsetDuration);

        // Make buttons responsive and fully visible
        SetupTrialButtons();
        yield return new WaitUntil(() => decisionMade);

        float responseTime = Time.time - decisionStartTime;
        string messageChosen = trialResponses.Count > 0 ? trialResponses[trialResponses.Count - 1] : "None";
        DataLogger.LogTrial(trialNumber, currentTask.ToString(), messageChosen, responseTime);

        yield return new WaitForSeconds(Random.Range(decisionConfirmationMin, decisionConfirmationMax));

        trialPanel.SetActive(false);
        fixationPanel.SetActive(true);
        yield return new WaitForSeconds(Random.Range(fixationMin, fixationMax));
        fixationPanel.SetActive(false);
    }

    IEnumerator RunAttentionTest(AttentionTestData test, int eventNumber) {
        selectionEnabled = false;
        trialPanel.SetActive(true);

        // Format the localized string with parameters
        var task = GetLocalizedStringAsync(UILocalizationTable, "trial_info");
        yield return new WaitUntil(() => task.IsCompleted);
        string trialInfoFormat = task.Result;
        trialInfoText.text = string.Format(trialInfoFormat, eventNumber, totalTrials + attentionTests.Count);

        if (barChartManager != null) {
            barChartManager.CreateBarChart(test.optionA_Self, test.optionA_Other, test.optionB_Self, test.optionB_Other);
        }

        // Set button text immediately using localized strings (using fixed keys for attention tests)
        var taskA = GetLocalizedStringAsync(UILocalizationTable, "attention_option_a");
        var taskB = GetLocalizedStringAsync(UILocalizationTable, "attention_option_b");
        yield return new WaitUntil(() => taskA.IsCompleted && taskB.IsCompleted);
        
        SetButtonText(optionAButtonText, taskA.Result);
        SetButtonText(optionBButtonText, taskB.Result);

        // Set buttons to unresponsive and transparent state
        SetButtonTransparency(optionAButtonImage, 0.5f);
        SetButtonTransparency(optionBButtonImage, 0.5f);

        // Wait for onset duration
        yield return new WaitForSeconds(trialOnsetDuration);

        // Make buttons responsive and fully visible
        SetupTrialButtons();
        yield return new WaitUntil(() => decisionMade);

        float responseTime = Time.time - decisionStartTime;
        string response = trialResponses.Count > 0 ? trialResponses[trialResponses.Count - 1] : "None";
        DataLogger.LogTrial(eventNumber, "AttentionTest", response, responseTime);

        yield return new WaitForSeconds(Random.Range(decisionConfirmationMin, decisionConfirmationMax));

        trialPanel.SetActive(false);
        fixationPanel.SetActive(true);
        yield return new WaitForSeconds(Random.Range(fixationMin, fixationMax));
        fixationPanel.SetActive(false);
    }

    private void SetupTrialButtons() {
        selectionEnabled = true;
        SetButtonHighlight(optionAButtonImage, Color.green);
        SetButtonHighlight(optionBButtonImage, Color.green);
        SetButtonTransparency(optionAButtonImage, 1f);
        SetButtonTransparency(optionBButtonImage, 1f);

        decisionMade = false;
        decisionStartTime = Time.time;

        optionAButton.onClick.RemoveAllListeners();
        optionBButton.onClick.RemoveAllListeners();
        optionAButton.onClick.AddListener(() => OnDecisionMade("A"));
        optionBButton.onClick.AddListener(() => OnDecisionMade("B"));

        eventSystem.SetSelectedGameObject(optionAButton.gameObject);
    }

    void OnDecisionMade(string choice) {
        if (!selectionEnabled) return;

        decisionMade = true;
        trialResponses.Add(choice);

        SetButtonHighlight(optionAButtonImage, Color.clear);
        SetButtonHighlight(optionBButtonImage, Color.clear);
        SetButtonHighlight(choice == "A" ? optionAButtonImage : optionBButtonImage, Color.red);
    }

    // Handles keyboard navigation during decision phases.
    void HandleKeyboardNavigation() {
        if (!selectionEnabled || eventSystem.currentSelectedGameObject == null)
            return;

        if (Input.GetKeyDown(KeyCode.RightArrow)) {
            eventSystem.SetSelectedGameObject(optionBButton.gameObject);
            optionBButton.onClick.Invoke();
        } else if (Input.GetKeyDown(KeyCode.LeftArrow)) {
            eventSystem.SetSelectedGameObject(optionAButton.gameObject);
            optionAButton.onClick.Invoke();
        }
    }

    void EndTrials() {
        if (endExperimentButton != null) {
            endExperimentButton.gameObject.SetActive(true);
            endExperimentButton.onClick.RemoveAllListeners();
            endExperimentButton.onClick.AddListener(EndExperiment);
        }
    }

    async void EndExperiment() { // Make async
        int randomNumber = Random.Range(1000, 9999);
        string filename = $"TrialData_{randomNumber}.csv";
        DataLogger.SaveData(filename);

        if (instructionPanel != null)
            instructionPanel.SetActive(true);
        if (instructionText != null)
            // Use GetLocalizedStringAsync from the "UI" table
            instructionText.text = await GetLocalizedStringAsync(UILocalizationTable, "end_experiment_text");

        if (endExperimentButton != null) {
            Destroy(endExperimentButton.gameObject);
            Debug.Log("End Experiment Button removed. Game Over");
        }

        Invoke("CloseApplication", closeDelay);
        Time.timeScale = 0;
    }

    void CloseApplication() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Sets the displayed text on a button.
    void SetButtonText(TMP_Text buttonText, string text) {
        if (buttonText != null) buttonText.text = text;
    }

    // Sets the highlight color of a button.
    void SetButtonHighlight(Image buttonImage, Color color) {
        if (buttonImage != null) buttonImage.color = color;
    }

    // Sets the transparency (alpha) of a button's image.
    void SetButtonTransparency(Image buttonImage, float alpha) {
        if (buttonImage != null) {
            Color color = buttonImage.color;
            color.a = alpha;
            buttonImage.color = color;
        }
    }

    // Randomly shuffles the list of regular trials.
    void ShuffleTrials(List<TrialData> trials) {
        for (int i = trials.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            TrialData temp = trials[i];
            trials[i] = trials[j];
            trials[j] = temp;
        }
    }

    // Inserts the attention tests into the overall event sequence.
    private void InsertAttentionTests() {
        CreateAttentionTests();

        // Clear previous indices
        attentionTestIndices.Clear();
        attentionTestIndexToTestIndex.Clear();

        // Start placing attention tests after the first few trials
        int currentPosition = Random.Range(4, 8); // Start between 4-7 trials
        int testCount = 0;

        while (currentPosition < totalTrials && testCount < attentionTests.Count) {
            // Add the attention test at this position
            attentionTestIndices.Add(currentPosition);
            attentionTestIndexToTestIndex[currentPosition] = testCount;
            testCount++;

            // Move to next position (4-7 trials ahead)
            currentPosition += Random.Range(4, 8);
        }
    }

    // Creates 5 attention tests with predetermined correct answers.
    private void CreateAttentionTests() {
        attentionTests.Clear();
        // Example attention test 1 - Option A is clearly better for self.
        attentionTests.Add(new AttentionTestData(10f, 5f, 5f, 5f, "A"));
        // Example attention test 2 - Option B is clearly better for self.
        attentionTests.Add(new AttentionTestData(5f, 5f, 10f, 5f, "B"));
        // Example attention test 3 - Option A is clearly better for both.
        attentionTests.Add(new AttentionTestData(10f, 10f, 5f, 5f, "A"));
        // Example attention test 4 - Option B is clearly better for both.
        attentionTests.Add(new AttentionTestData(5f, 5f, 10f, 10f, "B"));
        // Example attention test 5 - Option A is clearly better for receiver.
        attentionTests.Add(new AttentionTestData(5f, 10f, 10f, 5f, "B"));
    }

    // Helper method to get localized string asynchronously
    async System.Threading.Tasks.Task<string> GetLocalizedStringAsync(string tableName, string entryName) {
        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, entryName);
        await operation.Task; // Wait for the operation to complete
        if (operation.IsDone && operation.Result != null) {
            return operation.Result;
        } else {
            Debug.LogWarning($"Could not find localized string for key '{entryName}' in table '{tableName}'. Returning key.");
            return entryName; // Fallback
        }
    }
}
