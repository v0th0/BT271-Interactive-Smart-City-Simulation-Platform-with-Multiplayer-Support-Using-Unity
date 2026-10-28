using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class LanDiscovery : MonoBehaviour
{
    private const int DiscoveryPort = 47777;
    private const string Magic = "BT271_SMARTCITY";

    public event Action<string> OnHostFound;

    private UdpClient sender;
    private UdpClient receiver;
    private bool broadcasting;
    private bool searching;
    private volatile string foundIp;
    private float nextBroadcastTime;
    private ushort gamePort;

    // Host/Server calls this: announces itself every second
    public void StartBroadcasting(ushort port)
    {
        StopAll();
        gamePort = port;
        sender = new UdpClient { EnableBroadcast = true };
        broadcasting = true;
    }

    // Client calls this: listens for a Host announcement
    public void StartSearching()
    {
        StopAll();
        foundIp = null;

        receiver = new UdpClient();
        receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        receiver.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

        searching = true;
        receiver.BeginReceive(OnReceive, receiver);
    }

    private void OnReceive(IAsyncResult ar)
    {
        var client = (UdpClient)ar.AsyncState;
        try
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = client.EndReceive(ar, ref remote);

            if (Encoding.UTF8.GetString(data).StartsWith(Magic))
                foundIp = remote.Address.ToString();
            else
                client.BeginReceive(OnReceive, client); // not ours, keep listening
        }
        catch (ObjectDisposedException) { }   // socket closed on purpose
        catch (Exception e) { Debug.LogWarning("LAN discovery: " + e.Message); }
    }

    private void Update()
    {
        if (broadcasting && Time.unscaledTime >= nextBroadcastTime)
        {
            nextBroadcastTime = Time.unscaledTime + 1f;
            byte[] data = Encoding.UTF8.GetBytes(Magic + ":" + gamePort);
            try { sender.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort)); }
            catch (Exception) { }
        }

        if (searching && foundIp != null)
        {
            string ip = foundIp;
            foundIp = null;
            StopAll();
            OnHostFound?.Invoke(ip);
        }
    }

    public void StopAll()
    {
        broadcasting = false;
        searching = false;
        sender?.Close();
        receiver?.Close();
        sender = null;
        receiver = null;
    }

    private void OnDestroy() => StopAll();
}