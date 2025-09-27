using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

public static class DataLogger
{
    private static List<string> csvLines = new List<string>();
    private static float startTime = 0f;
    private static string currentFilePath = ""; // Stores the path for the session

    /// <summary>
    /// Initializes the logger, clears previous data, and sets the CSV header.
    /// </summary>
    public static void Initialize()
    {
        startTime = Time.realtimeSinceStartup;
        csvLines.Clear();
        csvLines.Add("ParticipantID,EventNumber,AbsoluteTime,TaskTypeOrEvent,MessageChosenOrResponse,ReactionTime,BarData");
    }

    // Add this new public static method to DataLogger.cs

    /// <summary>
    /// Resets the logger for a new experiment session within the same application instance.
    /// </summary>
    public static void Reset()
    {
        currentFilePath = ""; // Clear the file path
        Initialize();         // Re-initialize lists and timers
        Debug.Log("DataLogger has been reset for a new session.");
    }

    /// <summary>
    /// Sets the full file path for the session's data log. Must be called once during setup.
    /// </summary>
    /// <param name="participantId">The ID of the participant.</param>
    /// <param name="taskType">The type of task being run ("Deception" or "Control").</param>
    /// <param name="series">The instruction series (1 or 2).</param>
    public static void SetFilePath(string participantId, string taskType, int series)
    {
        // Only set the path if it hasn't been set yet.
        if (string.IsNullOrEmpty(currentFilePath))
        {
            // Use the provided parameters to construct a unique, descriptive filename.
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"PID-{participantId}_Task-{taskType}_Series-{series}_{timestamp}.csv";
            currentFilePath = Path.Combine(Application.persistentDataPath, filename);
            Debug.Log($"DataLogger: File path set for session: {currentFilePath}");
        }
    }

    /// <summary>
    /// Logs data for a regular trial.
    /// </summary>
    public static void LogTrial(int eventNumber, string taskType, string messageChosen, float reactionTime, List<float> barData)
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
        string barDataString = barData != null ? string.Join(",", barData.Select(b => b.ToString("F2"))) : "N/A";
        string line = string.Format("{0},{1},{2:F4},{3},{4},{5:F4},{6}",
                                    participantId, eventNumber, absoluteTime, taskType, messageChosen, reactionTime,
                                    barDataString
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
        string line = string.Format("{0},{1},{2:F4},{3},{4},{5:F4},{6}",
                                    participantId, eventNumber, absoluteTime, "AttentionTest", response, reactionTime,
                                    "N/A"
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
        string line = string.Format("{0},{1},{2:F4},{3},{4},{5},{6}",
                                    participantId, eventNumberBeforeBreak, absoluteTime, "InterRunStart",
                                    "N/A", "N/A", "N/A"
                                   );
        csvLines.Add(line);
    }

    /// <summary>
    /// Saves the current data log. This is the final save point.
    /// </summary>
    public static void SaveData(string participantId, string taskType, int series)
    {
        // Ensure the path is set before the final save, in case SetFilePath was missed earlier.
        SetFilePath(participantId, taskType, series);
        FlushData(); // Calls the flush method for the final save
    }

    /// <summary>
    /// Flushes the currently logged data to the file system. Call this periodically (e.g., after each run)
    /// to prevent data loss. This method performs synchronous file writing.
    /// </summary>
    public static void FlushData()
    {
        // Don't save if path is not set or only header exists
        if (string.IsNullOrEmpty(currentFilePath) || csvLines.Count < 2)
        {
            Debug.LogWarning("DataLogger: Flush skipped. File path not set or only header exists.");
            return;
        }

        try
        {
            // Create a copy to write to avoid modification during the operation
            List<string> linesToWrite = new List<string>(csvLines);
            // File.WriteAllLines is synchronous and must complete before quitting
            File.WriteAllLines(currentFilePath, linesToWrite.ToArray());
            Debug.Log($"Data flushed successfully ({linesToWrite.Count} lines) to: {currentFilePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error flushing data to {currentFilePath}: {ex.Message}");
        }
    }
}