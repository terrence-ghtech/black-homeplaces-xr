using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class HideShowButtons : MonoBehaviour
{
    public GameObject buttons;
    private bool areVisible;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return; 

        if (keyboard.tabKey.wasPressedThisFrame)
        {
            areVisible = !areVisible;
            if (areVisible)
                buttons.SetActive(false);
            else
                buttons.SetActive(true);
        }
    }
}
