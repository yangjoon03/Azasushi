using UnityEngine;
using TMPro;

public class CustomerOrderManager : MonoBehaviour
{
    public TextMeshProUGUI orderText;
    public TextMeshProUGUI scoreText;

    public string currentOrder;
    private int score = 0;

    void Start()
    {
        NewOrder();
        UpdateScore();
    }

    public void NewOrder()
    {
        string[] orders = { "Cube", "Sphere", "Capsule" };

        currentOrder = orders[Random.Range(0, orders.Length)];

        orderText.text = "Order: " + currentOrder;
    }

    public void CheckSubmittedItem(string submittedItem)
    {
        if (submittedItem == currentOrder)
        {
            score++;
            orderText.text = "Correct! Submitted: " + submittedItem;

            UpdateScore();

            Invoke(nameof(NewOrder), 1.5f);
        }
        else
        {
            orderText.text = "Wrong! Order was: " + currentOrder;
        }
    }

    void UpdateScore()
    {
        scoreText.text = "Score: " + score;
    }
}