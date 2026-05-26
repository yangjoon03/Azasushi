using UnityEngine;
using UnityEngine.InputSystem;

public class FridgeDrawerClick : MonoBehaviour
{
    public FridgeDrawer fridgeDrawer;

    void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    Debug.Log("¼­¶ø Å¬¸¯µÊ: " + gameObject.name);
                    fridgeDrawer.ToggleDrawer();
                }
            }
        }
    }
}