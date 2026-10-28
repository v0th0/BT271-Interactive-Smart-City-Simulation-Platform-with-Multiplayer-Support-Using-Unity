using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

public class PlayerNameTag : NetworkBehaviour
{
    [Header("Name Label")]
    [SerializeField] private TextMeshPro nameLabel;

    [SerializeField]
    private Vector3 labelOffset =
        new Vector3(0f, 2.2f, 0f);

    private NetworkVariable<FixedString32Bytes> playerName =
        new NetworkVariable<FixedString32Bytes>(
            "Player",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    public override void OnNetworkSpawn()
    {
        // Owner sends their name
        if (IsOwner)
        {
            playerName.Value =
                NetworkUIButtons.LocalPlayerName;
        }

        // Display current name
        UpdateNameLabel(playerName.Value);

        // Listen for name changes
        playerName.OnValueChanged += OnPlayerNameChanged;
    }

    private void OnPlayerNameChanged(
        FixedString32Bytes oldName,
        FixedString32Bytes newName)
    {
        UpdateNameLabel(newName);
    }

    private void UpdateNameLabel(FixedString32Bytes newName)
    {
        if (nameLabel != null)
        {
            nameLabel.text = newName.ToString();
        }
    }

    private void LateUpdate()
    {
        if (nameLabel == null)
            return;

        if (Camera.main == null)
            return;

        // Keep the name above the player
        nameLabel.transform.position =
            transform.position + labelOffset;

        // Make the name face the camera
        nameLabel.transform.rotation =
            Camera.main.transform.rotation;
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnPlayerNameChanged;
    }
}