using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CustomerOrderManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText;

    public Image sushiImage;       // 손님 머리 위 초밥 이미지
    public Image timerFillImage;   // 빨갛게 차오르는 제한시간 이미지

    public Sprite sabaSprite;
    public Sprite unagiSprite;
    public Sprite taiSprite;
    public Sprite salmonSprite;
    public Sprite shrimpSprite;
    public Sprite otoroSprite;

    public string currentOrder;
    private int score = 0;

    public bool hasActiveOrder = false;

    void Start()
    {
        ClearOrder();
        UpdateScore();
    }

    public void NewOrder()
    {
        string[] orders = { "saba", "unagi", "tai", "salmon", "shrimp", "otoro" };

        currentOrder = orders[Random.Range(0, orders.Length)];
        hasActiveOrder = true;

        sushiImage.gameObject.SetActive(true);
        timerFillImage.gameObject.SetActive(true);

        timerFillImage.fillAmount = 0f;

        switch (currentOrder)
        {
            case "saba":
                sushiImage.sprite = sabaSprite;
                break;
            case "unagi":
                sushiImage.sprite = unagiSprite;
                break;
            case "tai":
                sushiImage.sprite = taiSprite;
                break;
            case "salmon":
                sushiImage.sprite = salmonSprite;
                break;
            case "shrimp":
                sushiImage.sprite = shrimpSprite;
                break;
            case "otoro":
                sushiImage.sprite = otoroSprite;
                break;
        }
    }

    public bool CheckSubmittedItem(string submittedItem)
    {
        if (!hasActiveOrder)
            return false;

        if (submittedItem == currentOrder)
        {
            score++;
            UpdateScore();

            hasActiveOrder = false;
            return true;
        }

        return false;
    }

    public void UpdateTimer(float value)
    {
        if (timerFillImage != null)
            timerFillImage.fillAmount = value;
    }

    public void ClearOrder()
    {
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
        scoreText.text = "Score: " + score;
    }
}