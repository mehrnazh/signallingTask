using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.Threading.Tasks;
using RTLTMPro;
using System.Globalization;

public class BarChartManager : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform chartContainer;
    public GameObject barPrefab;

    [Header("Group Containers")]
    public Transform groupAContainer;
    public Transform groupBContainer;

    [Header("Group Labels (Outside)")]
    public RectTransform groupLabelsContainer;
    public TMP_FontAsset rtlFontAsset;

    [Header("Chart Settings")]
    public float chartMaxHeight = 300f;

    private const string UILocalizationTable = "UI";

    public async void CreateBarChart(float optionASelf, float optionAOther, float optionBSelf, float optionBOther)
    {
        var taskA = GetLocalizedStringAsync(UILocalizationTable, "option_a_label");
        var taskB = GetLocalizedStringAsync(UILocalizationTable, "option_b_label");
        await Task.WhenAll(taskA, taskB);

        GetOrCreateGroupLabelOutside("GroupALabel", taskA.Result);
        GetOrCreateGroupLabelOutside("GroupBLabel", taskB.Result);

        ClearChildren(groupAContainer);
        ClearChildren(groupBContainer);

        float maxValue = Mathf.Max(optionASelf, optionAOther, optionBSelf, optionBOther);
        float scaleFactor = (maxValue > 0) ? chartMaxHeight / maxValue : 0;

        // Option A: Now creates Receiver (Blue) first, then Sender (Red)
        CreateBar(groupAContainer, optionAOther, scaleFactor, Color.blue); // Receiver (Left)
        CreateBar(groupAContainer, optionASelf, scaleFactor, Color.red);  // Sender (Right)

        // Option B: Now creates Receiver (Blue) first, then Sender (Red)
        CreateBar(groupBContainer, optionBOther, scaleFactor, Color.blue); // Receiver (Left)
        CreateBar(groupBContainer, optionBSelf, scaleFactor, Color.red);  // Sender (Right)
    }

    private RTLTextMeshPro GetOrCreateGroupLabelOutside(string labelName, string defaultText)
    {
        Transform existingLabel = groupLabelsContainer.Find(labelName);
        RTLTextMeshPro textComp;
        if (existingLabel != null)
        {
            textComp = existingLabel.GetComponent<RTLTextMeshPro>();
            if (textComp != null)
            {
                textComp.text = defaultText;
                return textComp;
            }
            else
            {
                Destroy(existingLabel.gameObject);
            }
        }

        GameObject labelGO = new GameObject(labelName);
        labelGO.transform.SetParent(groupLabelsContainer, false);

        textComp = labelGO.AddComponent<RTLTextMeshPro>();
        textComp.text = defaultText;
        textComp.alignment = TextAlignmentOptions.Center;
        textComp.fontSize = 24;

        if (rtlFontAsset != null)
        {
            textComp.font = rtlFontAsset;
        }

        LayoutElement layoutElement = labelGO.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 200;

        return textComp;
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }
    }

    // MODIFIED: Changed method from 'async Task' to 'void' as it runs synchronously.
    private void CreateBar(Transform parentContainer, float value, float scaleFactor, Color barColor)
    {
        GameObject newBar = Instantiate(barPrefab, parentContainer, false);
        newBar.transform.localPosition = Vector3.zero;
        RectTransform barRect = newBar.GetComponent<RectTransform>();
        float barHeight = value * scaleFactor;
        barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, barHeight);
        Image barImage = newBar.GetComponent<Image>();
        if (barImage != null)
            barImage.color = barColor;

        TMP_Text barLabel = newBar.GetComponentInChildren<TMP_Text>();
        if (barLabel != null)
        {
            Canvas labelCanvas = barLabel.gameObject.GetComponent<Canvas>();
            if (labelCanvas == null)
            {
                labelCanvas = barLabel.gameObject.AddComponent<Canvas>();
            }
            labelCanvas.overrideSorting = true;
            labelCanvas.sortingOrder = 1;

            if (barLabel.gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                barLabel.gameObject.AddComponent<GraphicRaycaster>();
            }

            barLabel.text = FormatRTLNumber(value);
            RectTransform labelRect = barLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            float margin = 5f;
            labelRect.anchoredPosition = new Vector2(0f, barHeight + margin);
        }
    }

    private string FormatRTLNumber(float number)
    {
        string numStr = number.ToString("G", CultureInfo.InvariantCulture);
        string[] parts = numStr.Split('.');

        if (ShouldUseRTL())
        {
            if (parts.Length == 1)
                return parts[0];

            return $"{parts[1]}/{parts[0]}";
        }
        else
        {
            return numStr;
        }
    }

    private bool ShouldUseRTL()
    {
        return LocalizationSettings.SelectedLocale != null &&
               LocalizationSettings.SelectedLocale.Identifier.Code == "fa";
    }

    private async Task<string> GetLocalizedStringAsync(string tableName, string entryName)
    {
        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, entryName);
        await operation.Task;
        if (operation.IsDone && operation.Result != null)
        {
            return operation.Result;
        }
        return entryName;
    }
}