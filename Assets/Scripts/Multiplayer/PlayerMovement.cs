using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Spawn")]
    [SerializeField] private float spawnSpacing = 3f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float spawnRayHeight = 100f;
    [SerializeField] private float spawnRayDistance = 200f;
    [SerializeField] private float groundClearance = 0.03f;

    private static readonly Color[] PlayerColors =
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        Color.cyan,
        Color.magenta,
        new Color(1f, 0.5f, 0f)
    };

    private NetworkVariable<Color> playerColor =
        new NetworkVariable<Color>(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Renderer rend;
    private CapsuleCollider capsule;
    private Rigidbody rb;

    // Mouse look state
    private Camera playerCam;
    private float yaw;
    private float pitch;
    private bool cursorLocked;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        capsule = GetComponent<CapsuleCollider>();
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            int colorIndex =
                (int)(OwnerClientId % (ulong)PlayerColors.Length);

            playerColor.Value = PlayerColors[colorIndex];

            Vector3 spawnPosition = GetSpawnPosition(OwnerClientId);

            rb.position = spawnPosition;
            transform.position = spawnPosition;
        }

        ApplyColor(playerColor.Value);
        playerColor.OnValueChanged += OnPlayerColorChanged;

        SetupCamera();
    }

    private Vector3 GetSpawnPosition(ulong clientId)
    {
        float offsetX = clientId * spawnSpacing;

        Vector3 rayOrigin = new Vector3(
            offsetX,
            spawnRayHeight,
            0f
        );

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            spawnRayDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            float halfHeight = capsule.bounds.extents.y;

            float targetY =
                hit.point.y + halfHeight + groundClearance;

            Vector3 spawnPosition = new Vector3(
                offsetX,
                targetY,
                0f
            );

            Debug.Log(
                $"SPAWN RAY HIT | Client: {clientId} | " +
                $"Ground: {hit.collider.name} | " +
                $"Ground Y: {hit.point.y:F3} | " +
                $"Spawn Y: {targetY:F3}"
            );

            return spawnPosition;
        }

        Debug.LogWarning(
            $"No Ground detected for Client {clientId} " +
            $"at X={offsetX}, Z=0. Check the Ground layer, " +
            "colliders, and spawn coordinates."
        );

        return new Vector3(offsetX, 2f, 0f);
    }

    private void ApplyColor(Color color)
    {
        if (rend != null)
        {
            rend.material.color = color;
        }
    }

    private void OnPlayerColorChanged(Color oldColor, Color newColor)
    {
        ApplyColor(newColor);
    }

    private void SetupCamera()
    {
        Camera playerCamera =
            GetComponentInChildren<Camera>(true);

        if (playerCamera == null)
        {
            Debug.LogWarning(
                "PlayerCamera was not found inside the Player prefab."
            );

            return;
        }

        if (!IsOwner)
        {
            playerCamera.gameObject.SetActive(false);
            return;
        }

        playerCamera.gameObject.SetActive(true);
        playerCamera.tag = "MainCamera";

        // Mouse look setup
        playerCam = playerCamera;
        yaw = transform.eulerAngles.y;

        pitch = playerCam.transform.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;   // convert 0..360 to -180..180

        SetCursorLocked(true);

        // Disable any other MainCamera in the scene
        Camera[] cameras = FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Camera cam in cameras)
        {
            if (cam != playerCamera &&
                cam.CompareTag("MainCamera"))
            {
                cam.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (!IsOwner || rb == null)
            return;

        // Esc toggles the mouse so you can click the Exit button
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(!cursorLocked);

        if (cursorLocked)
            HandleMouseLook();

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Move relative to where the player is facing
        Vector3 movement =
            (transform.right * moveX + transform.forward * moveZ).normalized;

        rb.MovePosition(
            rb.position +
            movement * moveSpeed * Time.deltaTime
        );
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Left/right turns the player body
        yaw += mouseX;
        rb.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Up/down tilts only the camera
        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
        if (playerCam != null)
            playerCam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void SetCursorLocked(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner) SetCursorLocked(false);

        playerColor.OnValueChanged -= OnPlayerColorChanged;
    }
}