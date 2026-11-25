using Unity.Netcode;
using UnityEngine;

namespace NetworkCourse.BalloonFight.Networking
{
    public sealed class BalloonNetworkHarness : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;

        private bool _startHostRequested;
        private bool _startClientRequested;
        private bool _shutdownRequested;

        private void Update()
        {
            if (_networkManager == null) return;

            if (_startHostRequested)
            {
                _startHostRequested = false;
                _networkManager.StartHost();
            }

            if (_startClientRequested)
            {
                _startClientRequested = false;
                _networkManager.StartClient();
            }

            if (_shutdownRequested)
            {
                _shutdownRequested = false;
                _networkManager.Shutdown();
            }
        }

        private void OnGUI()
        {
            if (_networkManager == null) return;

            GUILayout.BeginArea(new Rect(12f, 12f, 220f, 120f), GUI.skin.box);
            GUILayout.Label(_networkManager.IsListening
                ? (_networkManager.IsServer ? "Role: Host" : "Role: Client")
                : "Role: -");

            if (!_networkManager.IsListening)
            {
                if (GUILayout.Button("Start Host")) _startHostRequested = true;
                if (GUILayout.Button("Start Client")) _startClientRequested = true;
            }
            else if (GUILayout.Button("Stop"))
            {
                _shutdownRequested = true;
            }

            GUILayout.EndArea();
        }
    }
}