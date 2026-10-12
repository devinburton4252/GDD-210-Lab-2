using UnityEngine;

public class MovePlayer : MonoBehaviour
{
    public CharacterController controller;
    public float speed;
    public float mouseSens;
    public Transform camTransform;

    private float camxrot = 0f;

    void Update()
    {
        // Rotation
        transform.Rotate(new Vector3(0, Input.GetAxis("Mouse X") * mouseSens, 0));
        camTransform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * mouseSens, 0 , 0));

        // Movement
        Vector3 move = Vector3.zero;

        float forwardSpeed = Input.GetAxis("Vertical");
        float sideSpeed = Input.GetAxis("Horizontal");

        move = (transform.forward * forwardSpeed * speed) + (transform.right * sideSpeed * speed);

        controller.Move(move * Time.deltaTime);
    }
}
