//using System.Collections.Generic;
//using System.IO;
//using UnityEngine;
//using System.Linq;

//public static class DataLogger
//{
//    private static List<string> csvLines = new List<string>();
//    private static float startTime = 0f;
//    private static string currentFilePath = ""; // Stores the path for the session

//    public static void Initialize()
//    {
//        startTime = Time.realtimeSinceStartup;
//        csvLines.Clear();
//        csvLines.Add("ParticipantID,EventNumber,AbsoluteTime,TaskTypeOrEvent,MessageChosenOrResponse,ReactionTime,BarData");
//    }

//    public static void LogTrial(int eventNumber, string taskType, string messageChosen, float reactionTime, List<float> barData)
//    {
//        float absoluteTime = Time.realtimeSinceStartup - startTime;
//        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
//        string barDataString = barData != null ? string.Join(",", barData.Select(b => b.ToString("F2"))) : "N/A";
//        string line = string.Format("{0},{1},{2:F4},{3},{4},{5:F4},{6}",
//                                    participantId, eventNumber, absoluteTime, taskType, messageChosen, reactionTime,
//                                    barDataString
//                                   );
//        csvLines.Add(line);
//    }

//    public static void LogAttentionTest(int eventNumber, string response, float reactionTime)
//    {
//        float absoluteTime = Time.realtimeSinceStartup - startTime;
//        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
//        string line = string.Format("{0},{1},{2:F4},{3},{4},{5:F4},{6}",
//                                    participantId, eventNumber, absoluteTime, "AttentionTest", response, reactionTime,
//                                    "N/A"
//                                   );
//        csvLines.Add(line);
//    }

//    public static void LogInterRunStart(int eventNumberBeforeBreak)
//    {
//        float absoluteTime = Time.realtimeSinceStartup - startTime;
//        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
//        string line = string.Format("{0},{1},{2:F4},{3},{4},{5},{6}",
//                                    participantId, eventNumberBeforeBreak, absoluteTime, "InterRunStart",
//                                    "N/A", "N/A", "N/A"
//                                   );
//        csvLines.Add(line);
//    }

//    /// <summary>
//    /// Saves the current data log. Call this at the end of the experiment for the final save.
//    /// </summary>
//    public static void SaveData(string participantId, string taskType, int series)
//    {
//        // If the file path hasn't been set yet for this session, create it.
//        if (string.IsNullOrEmpty(currentFilePath))
//        {
//            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
//            string filename = $"PID-{participantId}_Task-{taskType}_Series-{series}_{timestamp}.csv";
//            currentFilePath = Path.Combine(Application.persistentDataPath, filename);
//        }

//        FlushData(); // Call the flush method for the final save
//    }

//    /// <summary>
//    /// Flushes the currently logged data to the file system. Call this periodically to prevent data loss.
//    /// </summary>
//    public static void FlushData()
//    {
//        if (string.IsNullOrEmpty(currentFilePath) || csvLines.Count < 2) // Don't save if path is not set or only header exists
//        {
//            return;
//        }

//        try
//        {
//            // Create a copy to write to avoid modification during the operation
//            List<string> linesToWrite = new List<string>(csvLines);
//            File.WriteAllLines(currentFilePath, linesToWrite.ToArray());
//            Debug.Log($"Data flushed successfully ({linesToWrite.Count} lines) to: {currentFilePath}");
//        }
//        catch (System.Exception ex)
//        {
//            Debug.LogError($"Error flushing data to {currentFilePath}: {ex.Message}");
//        }
//    }
//}