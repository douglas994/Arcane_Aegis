using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Arcane_Aegis.Network;
using NetworkLibrary;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// In-world overlay shown when the connection to the zone drops (GM kick/ban, timeout, or network loss). It FREEZES the
    /// game (Time.timeScale = 0, so the local player stops walking) and shows a notice with a "back to login" button.
    /// Build the panel by hand and wire the serialized refs (the panel starts hidden). One per gameplay scene.
    /// </summary>
    public sealed class DisconnectScreen : MonoBehaviour
    {
        [Header("Refs (wire by hand)")]
        [SerializeField] private GameObject panelRoot;     // the overlay container (starts hidden)
        [SerializeField] private TMP_Text messageText;     // the notice line

        [Header("Config")]
        [SerializeField] private string loginScene = "Login";
        [SerializeField] private string message = "Você foi desconectado do servidor.";

        private bool _shown;

        private void Start()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (NetClient.Instance != null) NetClient.Instance.OnDisconnectedFromServer += OnDisconnected;
        }

        private void OnDestroy()
        {
            if (NetClient.Instance != null) NetClient.Instance.OnDisconnectedFromServer -= OnDisconnected;
        }

        private void OnDisconnected(DisconnectReason reason)
        {
            if (_shown) return;
            _shown = true;
            Time.timeScale = 0f; // stop the local player + the world while the notice is up
            if (messageText != null) messageText.text = message;
            if (panelRoot != null) panelRoot.SetActive(true);
            Debug.Log($"[DisconnectScreen] session ended: {reason}");
        }

        /// <summary>Hook this to the panel's button → restore time + go back to the login scene.</summary>
        public void BackToLogin()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(loginScene);
        }
    }
}
