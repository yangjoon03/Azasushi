using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CustomerOrderManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText;

    public Image sushiImage;
    public Image timerFillImage;

    public Sprite sabaSprite;
    public Sprite taiSprite;
    public Sprite salmonSprite;
    public Sprite otoroSprite;

    public GameObject clearEndingPanel;
    public TextMeshProUGUI clearTimeText;
    public TextMeshProUGUI wrongCountText;

    public string currentOrder;

    private int score = 0;
    private int wrongCount = 0;
    private float startTime;

    public bool hasActiveOrder = false;
    private bool isCleared = false;

    void Start()
    {
        startTime = Time.time;

        if (clearEndingPanel != null)
            clearEndingPanel.SetActive(false);

        ClearOrder();
        UpdateScore();
    }

    public void NewOrder()
    {
        if (isCleared)
            return;

        string[] orders = { "saba", "tai", "salmon", "otoro" };

        currentOrder = orders[Random.Range(0, orders.Length)];
        hasActiveOrder = true;

        if (sushiImage != null)
            sushiImage.gameObject.SetActive(true);

        if (timerFillImage != null)
        {
            timerFillImage.gameObject.SetActive(true);
            timerFillImage.fillAmount = 0f;
        }

        switch (currentOrder)
        {
            case "saba":
                sushiImage.sprite = sabaSprite;
                break;
            case "tai":
                sushiImage.sprite = taiSprite;
                break;
            case "salmon":
                sushiImage.sprite = salmonSprite;
                break;
            case "otoro":
                sushiImage.sprite = otoroSprite;
                break;
        }

        Debug.Log("New Order: " + currentOrder);
    }

    public bool CheckSubmittedTag(string submittedTag)
    {
        if (!hasActiveOrder || isCleared)
            return false;

        if (submittedTag == currentOrder)
        {
            score++;
            UpdateScore();
            hasActiveOrder = false;

            Debug.Log("Correct! " + submittedTag);

            if (score >= 5)
                ShowClearEnding();

            return true;
        }

        wrongCount++;
        Debug.Log("Wrong! Submitted: " + submittedTag + " / Order: " + currentOrder);
        return false;
    }

    void ShowClearEnding()
    {
        isCleared = true;
        hasActiveOrder = false;

        float clearTime = Time.time - startTime;
        int minutes = Mathf.FloorToInt(clearTime / 60f);
        int seconds = Mathf.FloorToInt(clearTime % 60f);

        if (sushiImage != null)
            sushiImage.gameObject.SetActive(false);

        if (timerFillImage != null)
            timerFillImage.gameObject.SetActive(false);

        if (clearEndingPanel != null)
            clearEndingPanel.SetActive(true);

        if (clearTimeText != null)
            clearTimeText.text = "Clear Time: " + minutes + "m " + seconds + "s";

        if (wrongCountText != null)
            wrongCountText.text = "Wrong Sushi: " + wrongCount;

        Debug.Log("CLEAR ENDING!");
    }

    public bool IsCleared()
    {
        return isCleared;
    }

    public void UpdateTimer(float value)
    {
        if (timerFillImage != null)
            timerFillImage.fillAmount = value;
    }

    public void ClearOrder()
    {
        if (isCleared)
            return;

        currentOrder = "";
        hasActiveOrder = false;

        if (sushiImage != null)
            sushiImage.gameObject.SetActive(false);

        if (timerFillImage != null)
        {
            timerFillImage.fillAmount = 0f;
            timerFillImage.gameObject.SetActive(false);
        }
    }

    void UpdateScore()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score;
    }
}