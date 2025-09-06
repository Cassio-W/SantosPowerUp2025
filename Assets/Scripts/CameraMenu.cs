using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMenu : MonoBehaviour
{
    GameObject objCamera;
    [SerializeField] float cameraRotationSpeed = 0f;
    void Start()
    {
        objCamera = GameObject.FindGameObjectWithTag("MainCamera");
    }

    void Update()
    {
        objCamera.transform.Rotate(new Vector3(0f, (0 + cameraRotationSpeed) * Time.deltaTime, 0f), Space.World);
    }
}
