using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

public class ConnectionUI : MonoBehaviour
{
    private NetworkManager _networkManager;
    private string _joinAddress = "localhost";
    private bool _connected;

    private void Awake()
    {
        _networkManager = InstanceFinder.NetworkManager;
    }

    private void OnEnable()
    {
        if (_networkManager != null)
        {
            _networkManager.ServerManager.OnServerConnectionState += OnServerState;
            _networkManager.ClientManager.OnClientConnectionState += OnClientState;
        }
    }

    private void OnDisable()
    {
        if (_networkManager != null)
        {
            _networkManager.ServerManager.OnServerConnectionState -= OnServerState;
            _networkManager.ClientManager.OnClientConnectionState -= OnClientState;
        }
    }

    private void OnServerState(ServerConnectionStateArgs args)
    {
        _connected = args.ConnectionState == LocalConnectionState.Started;
        Debug.Log($"network: server state changed to {args.ConnectionState}");
    }

    private void OnClientState(ClientConnectionStateArgs args)
    {
        _connected = args.ConnectionState == LocalConnectionState.Started;
        Debug.Log($"network: client state changed to {args.ConnectionState}");
    }

    private void OnGUI()
    {
        if (_connected)
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 30));
            GUILayout.Label($"Connected ({(_networkManager.IsServerStarted ? "Host" : "Client")})");
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));

        if (GUILayout.Button("Host", GUILayout.Height(30)))
        {
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
            Debug.Log("network: starting as host");
        }

        GUILayout.Space(10);
        GUILayout.Label("Join Address:");
        _joinAddress = GUILayout.TextField(_joinAddress);

        if (GUILayout.Button("Join", GUILayout.Height(30)))
        {
            _networkManager.ClientManager.StartConnection(_joinAddress);
            Debug.Log($"network: joining {_joinAddress}");
        }

        GUILayout.EndArea();
    }
}
