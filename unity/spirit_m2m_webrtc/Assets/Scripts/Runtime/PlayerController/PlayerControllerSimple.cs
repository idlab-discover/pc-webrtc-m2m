using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[PlayerControllerRegister("simple")]

public class PlayerControllerSimple : PlayerControllerBase
{
    protected override string NAME => "PlayerControllerSimple";
    protected override Vector3 Position => transform.position;

    public override Transform CameraTransform => Camera.transform;

    public override Transform ObjectTransform => transform;

    public Camera Camera;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private bool lockCursor = true;

    // vertical movement keys
    [SerializeField] private KeyCode ascendKey = KeyCode.Space;
    [SerializeField] private KeyCode descendKey = KeyCode.LeftControl;

    // internal rotation state
    private float yaw;
    private float pitch;

    void Start()
    {
        if (Camera.main == null)
            gameObject.tag = "MainCamera";

        // Initialize rotation state from current transform
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDestroy()
    {
        // restore cursor when this object is destroyed
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

    }

    protected override void Update()
    {
        base.Update();

        // --- Mouse look ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY; // typical Y-inverted look; flip sign if you prefer non-inverted
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // --- Movement relative to camera ---
        float horizontal = Input.GetAxis("Horizontal");
        float forwardInput = Input.GetAxis("Vertical");

        Vector3 camForward;
        Vector3 camRight;
        if (Camera != null)
        {
            camForward = Camera.transform.forward;
            camRight = Camera.transform.right;
        }
        else
        {
            camForward = transform.forward;
            camRight = transform.right;
        }

        // Project onto XZ plane so movement doesn't include vertical camera tilt
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // Ascend / Descend input (keys)
        float ascend = Input.GetKey(ascendKey) ? 1f : 0f;
        float descend = Input.GetKey(descendKey) ? 1f : 0f;
        float verticalMovement = ascend - descend;

        Vector3 movement = (camForward * forwardInput + camRight * horizontal) * moveSpeed * Time.deltaTime;
        movement += Vector3.up * verticalMovement * moveSpeed * Time.deltaTime;
       
        transform.Translate(movement, Space.World);
    }

    protected override void updatePositionMatrix()
    {
        currentPositionUpdate.position[0] = Position.x;
        currentPositionUpdate.position[1] = Position.y;
        currentPositionUpdate.position[2] = Position.z;
        Matrix4x4 worldToCamera = Camera.worldToCameraMatrix;
        Matrix4x4 projection = Camera.projectionMatrix;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                currentPositionUpdate.worldToCameraMatrix[i, j] = worldToCamera[i, j];
                currentPositionUpdate.projectionMatrix[i, j] = projection[i, j];
            }
        }
    }
}