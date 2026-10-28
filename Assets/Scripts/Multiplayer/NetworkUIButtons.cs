using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;

public class NetworkUIButtons : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button serverButton;

    [Header("Player Name")]
    [SerializeField] private TMP_InputField nameInput;

    [Header("Connection")]
    [SerializeField] private LanDiscovery discovery;
    [SerializeField] private TMP_InputField ipInput;   // optional manual fallback
    [SerializeField] private TMP_Text hostIpLabel;     // optional
    [SerializeField] private TMP_Text statusLabel;     // optional: "Searching for host..."

    [Header("UI")]
    [SerializeField] private GameObject uiRoot;

    public static string LocalPlayerName = "Player";

    private const ushort Port = 7777;
    private const float SearchTimeout = 8f;
    private UnityTransport transport;
    private Coroutine searchRoutine;

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        clientButton.onClick.AddListener(OnClientClicked);
        serverButton.onClick.AddListener(OnServerClicked);

        if (NetworkManager.Singleton != null)
        {
            transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        if (hostIpLabel != null)
            hostIpLabel.text = "Your IP: " + GetLocalIPv4();
    }

    private void OnHostClicked()
    {
        CacheName();
        if (NetworkManager.Singleton == null || transport == null) return;

        transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
        NetworkManager.Singleton.StartHost();
        discovery.StartBroadcasting(Port);   // tell the LAN "I'm hosting"
        SetStatus("Hosting. Waiting for players...");
        SetButtonsInteractable(false);
    }

    private void OnServerClicked()
    {
        if (NetworkManager.Singleton == null || transport == null) return;

        transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
        NetworkManager.Singleton.StartServer();
        discovery.StartBroadcasting(Port);
        SetStatus("Server running.");
        SetButtonsInteractable(false);
    }

    private void OnClientClicked()
    {
        CacheName();
        if (NetworkManager.Singleton == null || transport == null) return;

        SetButtonsInteractable(false);

        // Manual IP typed? Use it. Otherwise search automatically.
        if (ipInput != null && !string.IsNullOrWhiteSpace(ipInput.text))
        {
            ConnectToHost(ipInput.text.Trim());
            return;
        }

        SetStatus("Searching for host...");
        discovery.OnHostFound += ConnectToHost;
        discovery.StartSearching();
        searchRoutine = StartCoroutine(SearchTimeoutRoutine());
    }

    private void ConnectToHost(string ip)
    {
        discovery.OnHostFound -= ConnectToHost;
        if (searchRoutine != null) { StopCoroutine(searchRoutine); searchRoutine = null; }

        SetStatus("Connecting to " + ip + "...");
        transport.SetConnectionData(ip, Port);
        NetworkManager.Singleton.StartClient();
    }

    private IEnumerator SearchTimeoutRoutine()
    {
        yield return new WaitForSeconds(SearchTimeout);

        discovery.OnHostFound -= ConnectToHost;
        discovery.StopAll();
        searchRoutine = null;
        SetStatus("No host found. Check you're on the same network, or type the Host IP.");
        SetButtonsInteractable(true);
    }

    private void CacheName()
    {
        if (nameInput != null && !string.IsNullOrWhiteSpace(nameInput.text))
            LocalPlayerName = nameInput.text.Trim();
    }

    private string GetLocalIPv4()
    {
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { }
        return "unknown";
    }

    private void SetStatus(string msg)
    {
        if (statusLabel != null) statusLabel.text = msg;
    }

    private void SetButtonsInteractable(bool value)
    {
        if (hostButton != null) hostButton.interactable = value;
        if (clientButton != null) clientButton.interactable = value;
        if (serverButton != null) serverButton.interactable = value;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.LocalClientId == clientId && uiRoot != null)
            uiRoot.SetActive(false);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.LocalClientId == clientId || !NetworkManager.Singleton.IsConnectedClient)
        {
            SetButtonsInteractable(true);
            SetStatus("Disconnected.");
            if (uiRoot != null) uiRoot.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}