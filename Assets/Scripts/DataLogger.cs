using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

public static class DataLogger
{
    private static List<string> csvLines = new List<string>();
    private static float startTime = 0f;
    private static string currentFilePath = "";

    /// <summary>
    /// Initializes the logger, clears previous data, and sets the CSV header.
    /// </summary>
    public static void Initialize()
    {
        startTime = Time.realtimeSinceStartup;
        csvLines.Clear();
        // UPDATED HEADER: Added FeedbackTrialStatus column
        csvLines.Add("ParticipantID,EventNumber,AbsoluteTime,TaskTypeOrEvent,MessageChosenOrResponse,ReactionTime,BarData,FeedbackTrialStatus");
    }

    /// <summary>
    /// Resets the logger for a new experiment session within the same application instance.
    /// </summary>
    public static void Reset()
    {
        currentFilePath = "";
        Initialize();
        Debug.Log("DataLogger has been reset for a new session.");
    }

    /// <summary>
    /// Sets the full file path for the session's data log. Must be called once during setup.
    /// </summary>
    public static void SetFilePath(string participantId, string taskType, int series)
    {
        if (string.IsNullOrEmpty(currentFilePath))
        {
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"PID-{participantId}_Task-{taskType}_Series-{series}_{timestamp}.csv";
            currentFilePath = Path.Combine(Application.persistentDataPath, filename);
            Debug.Log($"DataLogger: File path set for session: {currentFilePath}");
        }
    }

    /// <summary>
    /// Logs data for a regular trial.
    /// </summary>
    // MODIFIED: Added isFeedbackTrial parameter
    public static void LogTrial(int eventNumber, string taskType, string messageChosen, float reactionTime, List<float> barData, bool isFeedbackTrial)
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
        string barDataString = barData != null ? string.Join(",", barData.Select(b => b.ToString("F2"))) : "N/A";

        // NEW COLUMN VALUE
        string feedbackStatus = isFeedbackTrial ? "Selected" : "NotSelected";

        string line = string.Format("{0},{1},{2:F4},{3},{4},{5:F4},{6},{7}",
                                    participantId, eventNumber, absoluteTime, taskType, messageChosen, reactionTime,
                                    barDataString, feedbackStatus
                                   );
        csvLines.Add(line);
    }

    /// <summary>
    /// Logs data for a trial that was randomly chosen for feedback.
    /// </summary>
    public static void LogFeedbackTrial(int eventNumber, float selfPayoff, float otherPayoff, bool partnerFollowed, string participantChoice)
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
        string partnerResult = partnerFollowed ? "Followed" : "Ignored";

        // Log the outcome payoffs and partner's action in the BarData column
        // Format: SelfPayoff,OtherPayoff,PartnerAction
        string barDataString = $"{selfPayoff:F2},{otherPayoff:F2},{partnerResult}";

        // Note: ReactionTime is logged as "N/A" here because this entry logs the OUTCOME.
        string line = string.Format("{0},{1},{2:F4},{3},{4},{5},{6},{7}",
                                    participantId, eventNumber, absoluteTime, "FeedbackTrialOutcome",
                                    participantChoice, "N/A",
                                    barDataString, "Outcome" // FeedbackTrialStatus: Outcome
                                   );
        csvLines.Add(line);
    }

    // NEW METHOD: Log Final Payoff
    /// <summary>
    /// Logs the final calculated payoff at the end of the experiment.
    /// </summary>
    public static void LogFinalPayoff(float finalSelfPayoff, float finalOtherPayoff, int trialsCounted)
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";

        float finalTotalPayoff = finalSelfPayoff + finalOtherPayoff;

        // Log the final payoffs in the MessageChosenOrResponse column
        // Format: FinalSelfPayoff,FinalOtherPayoff,FinalTotalPayoff,TrialsCounted
        string payoffData = $"{finalSelfPayoff:F2},{finalOtherPayoff:F2},{finalTotalPayoff:F2},{trialsCounted}";

        string line = string.Format("{0},{1},{2:F4},{3},{4},{5},{6},{7}",
                                    participantId, 9999, absoluteTime, "FinalPayoffSummary",
                                    payoffData, "N/A",
                                    "N/A", "Final"
                                   );
        csvLines.Add(line);
    }

    /// <summary>
    /// Logs data for an attention check test.
    /// </summary>
    public static void LogAttentionTest(int eventNumber, string response, float reactionTime)
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
        // MODIFIED: Added final column as "N/A"
        string line = string.Format("{0},{1},{2:F4},{3},{4},{5:F4},{6},{7}",
                                    participantId, eventNumber, absoluteTime, "AttentionTest", response, reactionTime,
                                    "N/A", "N/A"
                                   );
        csvLines.Add(line);
    }

    /// <summary>
    /// Logs a marker indicating the start of an inter-run break.
    /// </summary>
    public static void LogInterRunStart(int eventNumberBeforeBreak)
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
        // MODIFIED: Added final column as "N/A"
        string line = string.Format("{0},{1},{2:F4},{3},{4},{5},{6},{7}",
                                    participantId, eventNumberBeforeBreak, absoluteTime, "InterRunStart",
                                    "N/A", "N/A", "N/A", "N/A"
                                   );
        csvLines.Add(line);
    }

    // NEW METHOD: Log Training Trial
    /// <summary>
    /// Logs data for a training trial.
    /// </summary>
    public static void LogTrainingTrial(int trialNumber, string participantChoice, string correctAnswer, float reactionTime)
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
        string outcome = (participantChoice == correctAnswer) ? "Correct" : "Incorrect";

        // MessageChosenOrResponse: {Choice} ({Outcome})
        string choiceAndOutcome = $"{participantChoice} ({outcome})";

        string line = string.Format("{0},{1},{2:F4},{3},{4},{5:F4},{6},{7}",
                                    participantId, trialNumber, absoluteTime, "TrainingTrial",
                                    choiceAndOutcome, reactionTime,
                                    correctAnswer, "N/A" // BarData: CorrectAnswer
                                   );
        csvLines.Add(line);
    }


    /// <summary>
    /// Saves the current data log. This is the final save point.
    /// </summary>
    public static void SaveData(string participantId, string taskType, int series)
    {
        SetFilePath(participantId, taskType, series);
        FlushData();
    }

    /// <summary>
    /// Flushes the currently logged data to the file system. Call this periodically (e.g., after each run)
    /// to prevent data loss. This method performs synchronous file writing.
    /// </summary>
    public static void FlushData()
    {
        if (string.IsNullOrEmpty(currentFilePath) || csvLines.Count < 2)
        {
            Debug.LogWarning("DataLogger: Flush skipped. File path not set or only header exists.");
            return;
        }

        try
        {
            List<string> linesToWrite = new List<string>(csvLines);
            File.WriteAllLines(currentFilePath, linesToWrite.ToArray());
            Debug.Log($"Data flushed successfully ({linesToWrite.Count} lines) to: {currentFilePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error flushing data to {currentFilePath}: {ex.Message}");
        }
    }
}