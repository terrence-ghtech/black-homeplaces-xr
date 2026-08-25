using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Controller : MonoBehaviour
{
    public Camera _camera;

    public float rotateSensitivity = 0.2f;
    public float panSensitivity = 0.02f;
    public float zoomStep = 5f;

    public void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return; 

        Vector2 mouseDelta = mouse.delta.ReadValue();

        bool fire1Held = mouse.leftButton.isPressed || Keyboard.current.leftCtrlKey.isPressed;

        if (fire1Held)
        {
            this.transform.Rotate(0f, mouseDelta.x * rotateSensitivity, 0f);
        }

        if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
        {
            _camera.transform.Translate(mouseDelta.x * panSensitivity, mouseDelta.y * panSensitivity, 0f);
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll < 0f)
        {
            _camera.transform.Translate(0f, 0f, -zoomStep);
        }
        if (scroll > 0f)
        {
            _camera.transform.Translate(0f, 0f, zoomStep);
        }
    }
}
