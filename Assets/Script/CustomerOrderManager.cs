using UnityEngine;
using TMPro;

public class CustomerOrderManager : MonoBehaviour
{
    public TextMeshProUGUI orderText;
    public TextMeshProUGUI scoreText;

    public string currentOrder;
    private int score = 0;

    public bool hasActiveOrder = false;

    void Start()
    {
        orderText.text = "";
        UpdateScore();
    }

    public void NewOrder()
    {
        string[] orders = { "saba", "unagi", "tai", "salmon", "shrimp", "otoro" };

        currentOrder = orders[Random.Range(0, orders.Length)];
        hasActiveOrder = true;

        orderText.text = "Order: " + currentOrder;
    }

    public bool CheckSubmittedItem(string submittedItem)
    {
        if (!hasActiveOrder)
            return false;

        if (submittedItem == currentOrder)
        {
            score++;
            UpdateScore();

            orderText.text = "Correct!";
            hasActiveOrder = false;

            return true;
        }
        else
        {
            orderText.text = "Wrong! Order was: " + currentOrder;
            return false;
        }
    }

    public void ClearOrder()
    {
        currentOrder = "";
        hasActiveOrder = false;
        orderText.text = "";
    }

    void UpdateScore()
    {
        scoreText.text = "Score: " + score;
    }
}