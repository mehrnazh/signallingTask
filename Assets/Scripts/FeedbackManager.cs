using UnityEngine;
using UnityEngine.UI;

public class FeedbackManager : MonoBehaviour {
    public Image feedbackImage;
    public Color successColor = Color.green;
    public Color failColor = Color.red;

    public void ShowFeedback(bool success) {
        feedbackImage.color = success ? successColor : failColor;
        feedbackImage.gameObject.SetActive(true);
        Invoke("HideFeedback", 1.0f); // Hide after 1 second
    }

    void HideFeedback() {
        feedbackImage.gameObject.SetActive(false);
    }
}
