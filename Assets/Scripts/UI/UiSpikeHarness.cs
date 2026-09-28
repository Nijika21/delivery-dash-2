using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DeliveryDash.UI
{
    public sealed class UiSpikeHarness : MonoBehaviour
    {
        [SerializeField] private UiRoot uiRoot;
        [SerializeField] private VisualTreeAsset lobbyLandscape;
        [SerializeField] private VisualTreeAsset lobbyPortrait;
        [SerializeField] private VisualTreeAsset deliveryLandscape;
        [SerializeField] private VisualTreeAsset deliveryPortrait;

        private bool showingDelivery;

        public void Configure(
            UiRoot root,
            VisualTreeAsset landscapeLobby,
            VisualTreeAsset portraitLobby,
            VisualTreeAsset landscapeDelivery,
            VisualTreeAsset portraitDelivery)
        {
            uiRoot = root;
            lobbyLandscape = landscapeLobby;
            lobbyPortrait = portraitLobby;
            deliveryLandscape = landscapeDelivery;
            deliveryPortrait = portraitDelivery;
            ShowLobby();
        }

        private void OnEnable()
        {
            ShowLobby();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                ShowLobby();
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                ShowDelivery();
            }
        }

        public void ShowLobby()
        {
            showingDelivery = false;
            uiRoot?.ConfigureTemplates(lobbyLandscape, lobbyPortrait);
        }

        public void ShowDelivery()
        {
            showingDelivery = true;
            uiRoot?.ConfigureTemplates(deliveryLandscape, deliveryPortrait);
        }

        public void ToggleScreen()
        {
            if (showingDelivery)
            {
                ShowLobby();
            }
            else
            {
                ShowDelivery();
            }
        }
    }
}
