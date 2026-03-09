using FishNet.Managing;
using UnityEngine;

public class ConnectionUI : MonoBehaviour
{
    private NetworkManager _networkManager;
    private string _joinAddress = "localhost";

    private NetworkManager GetNetworkManager()
    {
        if (_networkManager == null)
            _networkManager = FindFirstObjectByType<NetworkManager>();
        return _networkManager;
    }

    private bool IsConnected()
    {
        var nm = GetNetworkManager();
        return nm != null && nm.ServerManager != null && nm.ServerManager.Started;
    }

    private void OnGUI()
    {
        if (IsConnected())
        {
            GUI.FocusControl(null);
            GUILayout.BeginArea(new Rect(10, 10, 200, 30));
            GUILayout.Label("Connected (Host)");
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));

        if (GUILayout.Button("Host", GUILayout.Height(30)))
        {
            var nm = GetNetworkManager();
            if (nm != null && nm.ServerManager != null)
            {
                nm.ServerManager.StartConnection();
                nm.ClientManager.StartConnection();
                Debug.Log("network: starting as host");
            }
            else
            {
                Debug.LogError("network: NetworkManager not ready");
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Join Address:");
        _joinAddress = GUILayout.TextField(_joinAddress);

        if (GUILayout.Button("Join", GUILayout.Height(30)))
        {
            var nm = GetNetworkManager();
            if (nm != null && nm.ClientManager != null)
            {
                nm.ClientManager.StartConnection(_joinAddress);
                Debug.Log($"network: joining {_joinAddress}");
            }
            else
            {
                Debug.LogError("network: NetworkManager not ready");
            }
        }

        GUILayout.EndArea();
    }
}
