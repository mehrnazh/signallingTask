using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // Required for keyboard navigation

public enum TaskType { Deception, Control }

public class GameManager : MonoBehaviour {
    public static GameManager Instance;

    [Header("UI Panels")]
    public GameObject instructionPanel;
    public GameObject trialPanel;
    public GameObject fixationPanel;
    public GameObject feedbackPanel; // Optional: Additional visual feedback
    public GameObject interRunPanel; // Inter-run Panel
    public GameObject instructionPanel;
    public InstructionManager instructionManager;

    [Header("UI Texts")]
    public TMP_Text instructionText;
    public TMP_Text trialInfoText;
    public TMP_Text interRunText; // Text for the Inter-run Panel

    [Header("Buttons")]
    public Button optionAButton;
    public Button optionBButton;
    public Button endExperimentButton; // End-of-experiment button

    [Header("Task Settings")]
    public TaskType currentTask = TaskType.Deception; // Can be set dynamically
    public int totalTrials = 45;                 // Total number of trials
    public int trialsPerRun = 9;                 // Trials in each run
    public float trialOnsetDuration = 2f;          // Trial onset duration
    public float decisionConfirmationMin = 2f;    // Minimum confirmation time
    public float decisionConfirmationMax = 4f;    // Maximum confirmation time
    public float fixationMin = 2f;               // Minimum fixation duration
    public float fixationMax = 4f;               // Maximum fixation duration
    public float interRunInterval = 10f;          // Inter-run interval
    public float closeDelay = 10f; // Time in seconds before closing the application

    private List<TrialData> currentTrialList;     // List to hold the current trial data
    private List<string> trialResponses = new List<string>();  // Participant's trial responses
    private List<float> responseTimes = new List<float>();     // List to store reaction times

    private int currentTrialIndex = 0;
    private bool decisionMade = false;
    private bool selectionEnabled = false;        // Disable selection during trial onset phase
    private float decisionStartTime;

    private Button currentlySelectedButton;       // Track the currently selected button

    void Awake() {
        if (Instance == null) {
            Instance = this;
        } else {
            Destroy(gameObject); // Ensure only one GameManager instance exists
        }
    }

    void Start() {
        // Show instruction panel first
        instructionPanel.SetActive(true);

        // Hide all other panels initially
        trialPanel.SetActive(false);
        fixationPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        interRunPanel.SetActive(false);
    
        DataLogger.Initialize(); // Initialize DataLogger

        currentTrialList = new List<TrialData>(TrialDataManager.DeceptionTrials); // Load trial data
        ShuffleTrials(currentTrialList); // Optional: Shuffle trial order

        // SetupUI(); // Set up initial UI states
        
        // currentlySelectedButton = optionAButton; // Default selected button for keyboard navigation
        // EventSystem.current.SetSelectedGameObject(currentlySelectedButton.gameObject);
    }

    void SetupUI() {
        instructionPanel.SetActive(true);
        trialPanel.SetActive(false);
        fixationPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        if (endExperimentButton != null) endExperimentButton.gameObject.SetActive(false);
        if (interRunPanel != null) {
            interRunPanel.SetActive(false);
            if (interRunText != null) interRunText.text = "Please take a short break...\n\n" + "meanwhile we will match you with another counterpart";
        }

        // optionAButton.GetComponentInChildren<TMP_Text>().text = "message loading";
        // optionBButton.GetComponentInChildren<TMP_Text>().text = "message loading";

        instructionText.text = "Welcome to the experiment.\n\n" +
                               "In each trial, you will see two monetary allocation options with corresponding messages.\n" +
                               "Press any key to begin.";
        
        SetButtonTransparency(optionAButton, 0.5f); // Set initial transparency for onset phase
        SetButtonTransparency(optionBButton, 0.5f); 
    }

    void Update() {
        if (instructionPanel.activeSelf && Input.anyKeyDown) {
            instructionPanel.SetActive(false);
            StartCoroutine(RunAllTrials());
        }

        HandleKeyboardNavigation(); // Handle keyboard navigation during decision phase
    }

   IEnumerator RunAllTrials() {
        // Wait until instructions are complete
        yield return new WaitUntil(() => instructionManager.instructionsComplete);
    
       int totalRuns = totalTrials / trialsPerRun;

       for (int run = 0; run < totalRuns; run++) {
           for (int trialInRun = 0; trialInRun < trialsPerRun; trialInRun++) {
               currentTrialIndex = run * trialsPerRun + trialInRun;

               if (currentTrialIndex < totalTrials) {
                   yield return StartCoroutine(RunTrial(currentTrialList[currentTrialIndex], currentTrialIndex + 1));
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

   IEnumerator RunTrial(TrialData trial, int trialNumber) {
       selectionEnabled = false; // Disable selection during onset phase

       trialPanel.SetActive(true);

       BarChartManager chartManager = FindObjectOfType<BarChartManager>();
       if (chartManager != null) {
           chartManager.CreateBarChart(trial.optionA_Self, trial.optionA_Other, trial.optionB_Self, trial.optionB_Other);
       } else {
           Debug.LogWarning("BarChartManager not found in scene!");
       }

       SetButtonTransparency(optionAButton, 0.5f); // Reduce transparency for onset phase
       SetButtonTransparency(optionBButton, 0.5f);

       yield return new WaitForSeconds(trialOnsetDuration); // Show onset screen for specified duration
    
        // --- Decision Phase ---
        // Set button texts based on the current task
        if (optionAButton != null && optionBButton != null) {
            if (currentTask == TaskType.Deception) {
                SetButtonText(optionAButton, "To Receiver:\n" + "Option A will earn you more money \nthan Option B");
                SetButtonText(optionBButton, "To Receiver:\n" + "Option B will earn you more money \nthan Option A");
            } else {
                SetButtonText(optionAButton, "To Receiver:\n" + "I would prefer you to choose Option A");
                SetButtonText(optionBButton, "To Receiver:\n" + "I would prefer you to choose Option B");
            }
        }
       selectionEnabled = true; // Enable selection after onset phase

    //    optionAButton.GetComponentInChildren<TMP_Text>().text =
    //        "To Receiver:\n\n" + "Option A will earn you\n more money than Option B";
        
    //    optionBButton.GetComponentInChildren<TMP_Text>().text =
    //        "To Receiver:\n\n" + "Option A will earn you\n more money than Option B";

       SetButtonHighlight(optionAButton, Color.green);
       SetButtonHighlight(optionBButton, Color.green);

       SetButtonTransparency(optionAButton, 1f); // Restore full transparency after onset phase
       SetButtonTransparency(optionBButton, 1f);

       decisionMade = false;
       decisionStartTime = Time.time;

       optionAButton.onClick.RemoveAllListeners();
       optionBButton.onClick.RemoveAllListeners();
        
       optionAButton.onClick.AddListener(() => OnDecisionMade("A"));
       optionBButton.onClick.AddListener(() => OnDecisionMade("B"));

       EventSystem.current.SetSelectedGameObject(optionAButton.gameObject); // Default selection for keyboard navigation

       yield return new WaitUntil(() => decisionMade);

       float responseTime = Time.time - decisionStartTime;
        
       string messageChosen = trialResponses.Count > 0 ? trialResponses[trialResponses.Count - 1] : "None";

       DataLogger.LogTrial(trialNumber, currentTask.ToString(), messageChosen, responseTime);

       yield return new WaitForSeconds(Random.Range(decisionConfirmationMin, decisionConfirmationMax));

       trialPanel.SetActive(false);
        
       yield return new WaitForSeconds(Random.Range(fixationMin, fixationMax));
        
       fixationPanel.SetActive(false);
   }

   void HandleKeyboardNavigation() {
      if (!selectionEnabled || EventSystem.current.currentSelectedGameObject == null)
          return;

      if (Input.GetKeyDown(KeyCode.RightArrow)) {
          EventSystem.current.SetSelectedGameObject(optionBButton.gameObject);
          optionBButton.onClick.Invoke(); 
      } else if (Input.GetKeyDown(KeyCode.LeftArrow)) {
          EventSystem.current.SetSelectedGameObject(optionAButton.gameObject);
          optionAButton.onClick.Invoke(); 
      }
   }

   void OnDecisionMade(string choice) {
      decisionMade = true;

      trialResponses.Add(choice);

      SetButtonHighlight(optionAButton, Color.clear);
      SetButtonHighlight(optionBButton, Color.clear);

      if (choice == "A") SetButtonHighlight(optionAButton, Color.red);
      else SetButtonHighlight(optionBButton, Color.red);
   }

   void EndTrials() { 
      if (endExperimentButton != null) {
            endExperimentButton.gameObject.SetActive(true);
            endExperimentButton.onClick.RemoveAllListeners();
            endExperimentButton.onClick.AddListener(EndExperiment);
        }
   } 

   void EndExperiment() {
        // Generate a unique filename using a random number
        int randomNumber = Random.Range(1000, 9999);
        string filename = "TrialData_" + randomNumber + ".csv";
        DataLogger.SaveData(filename);
        
        if (instructionPanel != null) instructionPanel.SetActive(true);
        if (instructionText != null) {
            instructionText.text = "Task complete.\n\n" +
                                   "One trial from each task will be randomly selected for payment.\n" +
                                   "Thank you for participating.";
        }

        if (endExperimentButton != null) {
            Destroy(endExperimentButton.gameObject);
            Debug.Log("End Experiment Button has been removed. Game Over");
        }

        // Close the application after a delay
        Invoke("CloseApplication", closeDelay);

        // Set the time scale
        Time.timeScale = 0;
    }

           // Close Application function
    void CloseApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stop playing in the editor
#else
        Application.Quit(); // Quit the application
#endif
    }

    // // Called when a decision is made.
    // void OnDecisionMade(string choice) {
    //     if (decisionMade) return; // Prevent multiple responses
    //     decisionMade = true;

    //     trialResponses.Add(choice); // Record the choice.
    //     if (optionAButton != null && optionBButton != null) {
    //         if (choice == "A") {
    //             SetButtonHighlight(optionAButton, Color.red);
    //             SetButtonHighlight(optionBButton, Color.clear); // Remove highlight from the other option.
    //         } else if (choice == "B") {
    //             SetButtonHighlight(optionBButton, Color.red);
    //             SetButtonHighlight(optionAButton, Color.clear);
    //         }
    //     }
    // }


//    void EndExperiment() { 
//       DataLogger.SaveData("TrialData.csv"); 
//       Application.Quit(); 
//    } 

    // Set the TMP Text on the button
    void SetButtonText(Button button, string text) {
        if (button != null && button.GetComponentInChildren<TMP_Text>() != null) {
            button.GetComponentInChildren<TMP_Text>().text = text;
        }
    }
    
   void SetButtonHighlight(Button btn, Color color) { 
      Image img = btn.GetComponent<Image>(); 
      img.color = color; 
   } 

   void SetButtonTransparency(Button btn, float alpha) { 
      Image img = btn.GetComponent<Image>(); 
      if (img != null) { 
          Color color = img.color; 
          color.a = alpha; 
          img.color = color; 
      } 
   } 

   void ShuffleTrials(List<TrialData> trials) { 
      for (int i=trials.Count-1;i>0;i--) { 
          int j=Random.Range(0,i+1); 
          TrialData temp=trials[i]; 
          trials[i]=trials[j]; 
          trials[j]=temp;} }}
