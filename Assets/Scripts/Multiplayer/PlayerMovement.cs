using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

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

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(
            moveX,
            0f,
            moveZ
        ).normalized;

        rb.MovePosition(
            rb.position +
            movement * moveSpeed * Time.deltaTime
        );
    }

    public override void OnNetworkDespawn()
    {
        playerColor.OnValueChanged -= OnPlayerColorChanged;
    }
}