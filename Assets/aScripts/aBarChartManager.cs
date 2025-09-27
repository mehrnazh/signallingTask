//using UnityEngine;
//using UnityEngine.UI;
//using TMPro;
//using UnityEngine.Localization;
//using UnityEngine.Localization.Settings;
//using UnityEngine.Localization.Tables;
//using System.Threading.Tasks;
//using RTLTMPro;
//using System.Globalization;

//public class BarChartManager : MonoBehaviour
//{
//    [Header("UI References")]
//    // Overall chart container (optional, for layout purposes)
//    public RectTransform chartContainer; 
//    // Prefab for a single bar (must include an Image component and a child TMP_Text)
//    public GameObject barPrefab; 

//    [Header("Group Containers")]
//    // Containers for the bars (Option A and Option B respectively)
//    public Transform groupAContainer;  
//    public Transform groupBContainer;  

//    [Header("Group Labels (Outside)")]
//    // Container for group labels; this should be positioned underneath the group containers.
//    public RectTransform groupLabelsContainer;
//    // Font asset for RTL text
//    public TMP_FontAsset rtlFontAsset;

//    [Header("Chart Settings")]
//    // Maximum height in pixels corresponding to the highest monetary value.
//    public float chartMaxHeight = 300f; 

//    // Localization table reference
//    private const string UILocalizationTable = "UI";

//    /// <summary>
//    /// Generates the bar chart using trial monetary allocations with localized text.
//    /// </summary>
//    public async void CreateBarChart(float optionASelf, float optionAOther, float optionBSelf, float optionBOther)
//    {
//        // Get localized strings for group labels
//        var taskA = GetLocalizedStringAsync(UILocalizationTable, "option_a_label");
//        var taskB = GetLocalizedStringAsync(UILocalizationTable, "option_b_label");
//        await Task.WhenAll(taskA, taskB);

//        // Create or update group labels with localized text
//        RTLTextMeshPro groupALabel = GetOrCreateGroupLabelOutside("GroupALabel", taskA.Result);
//        RTLTextMeshPro groupBLabel = GetOrCreateGroupLabelOutside("GroupBLabel", taskB.Result);

//        // Clear previous bars in the group containers.
//        ClearChildren(groupAContainer);
//        ClearChildren(groupBContainer);

//        // Compute the scaling factor based on the maximum monetary value.
//        float maxValue = Mathf.Max(1f, optionASelf, optionAOther, optionBSelf, optionBOther); // Ensure maxValue is at least 1 to avoid division by zero.
//        float scaleFactor = chartMaxHeight / maxValue;

//        // Create bars for Option A: red for "Self" and blue for "Other".
//        CreateBar(groupAContainer, optionASelf, scaleFactor, Color.red);
//        CreateBar(groupAContainer, optionAOther, scaleFactor, Color.blue);

//        // Create bars for Option B.
//        CreateBar(groupBContainer, optionBSelf, scaleFactor, Color.red);
//        CreateBar(groupBContainer, optionBOther, scaleFactor, Color.blue);
//    }

//    /// <summary>
//    /// Searches for an existing label with the given name in GroupLabelsContainer.
//    /// If not found, creates a new RTLTextMeshPro element and adds it to the container.
//    /// </summary>
//    private RTLTextMeshPro GetOrCreateGroupLabelOutside(string labelName, string defaultText)
//    {
//        Transform existingLabel = groupLabelsContainer.Find(labelName);
//        RTLTextMeshPro textComp;
//        if (existingLabel != null)
//        {
//            textComp = existingLabel.GetComponent<RTLTextMeshPro>();
//            if (textComp != null)
//            {
//                textComp.text = defaultText;
//                return textComp;
//            }
//            else
//            {
//                // If the GameObject exists but the component is missing, destroy and recreate
//                Destroy(existingLabel.gameObject);
//            }
//        }
        
//        GameObject labelGO = new GameObject(labelName);
//        labelGO.transform.SetParent(groupLabelsContainer, false);
        
//        textComp = labelGO.AddComponent<RTLTextMeshPro>();
//        textComp.text = defaultText;
//        textComp.alignment = TextAlignmentOptions.Center;
//        textComp.fontSize = 24;
        
//        // Set the RTL font asset if provided
//        if (rtlFontAsset != null)
//        {
//            textComp.font = rtlFontAsset;
//        }
        
//        // Add LayoutElement to ensure proper sizing within a potential layout group
//        LayoutElement layoutElement = labelGO.AddComponent<LayoutElement>();
//        layoutElement.preferredWidth = 200; // Adjust width as needed
        
//        return textComp;
//    }

//    /// <summary>
//    /// Destroys all children of the given parent.
//    /// </summary>
//    private void ClearChildren(Transform parent)
//    {
//        foreach (Transform child in parent)
//        {
//            Destroy(child.gameObject);
//        }
//    }

//    /// <summary>
//    /// Instantiates a bar (from the prefab) into the specified parent container.
//    /// Sets its height (using the scale factor and monetary value), color, and positions its internal label.
//    /// </summary>
//    private void CreateBar(Transform parentContainer, float value, float scaleFactor, Color barColor)
//    {
//        GameObject newBar = Instantiate(barPrefab, parentContainer, false);
//        newBar.transform.localPosition = Vector3.zero;
//        RectTransform barRect = newBar.GetComponent<RectTransform>();
//        float barHeight = value * scaleFactor;
//        barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, barHeight);
//        Image barImage = newBar.GetComponent<Image>();
//        if(barImage != null)
//            barImage.color = barColor;
//        TMP_Text barLabel = newBar.GetComponentInChildren<TMP_Text>();
//        if(barLabel != null)
//        {
//            // Ensure the label text renders on top
//            Canvas labelCanvas = barLabel.gameObject.GetComponent<Canvas>();
//            if (labelCanvas == null)
//            {
//                labelCanvas = barLabel.gameObject.AddComponent<Canvas>();
//            }
//            labelCanvas.overrideSorting = true;
//            labelCanvas.sortingOrder = 1;

//            // Add GraphicRaycaster if needed for interactions (optional, but good practice)
//            if (barLabel.gameObject.GetComponent<GraphicRaycaster>() == null)
//            {
//                barLabel.gameObject.AddComponent<GraphicRaycaster>();
//            }

//            // *** REFACTORED: Use the corrected number formatting function ***
//            barLabel.text = FormatNumber(value);

//            RectTransform labelRect = barLabel.GetComponent<RectTransform>();
//            labelRect.anchorMin = new Vector2(0.5f, 0f);
//            labelRect.anchorMax = new Vector2(0.5f, 0f);
//            labelRect.pivot = new Vector2(0.5f, 0f);
//            float margin = 5f;
//            labelRect.anchoredPosition = new Vector2(0f, barHeight + margin);
//        }
//    }

//    /// <summary>
//    /// *** REFACTORED: Replaced faulty RTL formatting with a simple, correct version. ***
//    /// Formats a number to a string with up to 2 decimal places, using a period as the separator.
//    /// </summary>
//    private string FormatNumber(float number)
//    {
//        // CultureInfo.InvariantCulture ensures '.' is used universally, avoiding issues with locales that use ','.
//        return number.ToString("0.##", CultureInfo.InvariantCulture);
//    }

//    private async Task<string> GetLocalizedStringAsync(string tableName, string entryName)
//    {
//        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, entryName);
//        await operation.Task;
//        if (operation.IsDone && operation.Result != null)
//        {
//            return operation.Result;
//        }
//        return entryName; // Fallback to the entry name if localization fails
//    }
//}