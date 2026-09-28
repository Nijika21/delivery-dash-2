using UnityEngine;
using UnityEngine.InputSystem;

namespace DeliveryDash.Classic
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class ClassicDriver : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float steerSpeed = 130f;
        private Rigidbody2D body;
        private bool touchGas;
        private bool touchBrake;
        private bool touchLeft;
        private bool touchRight;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
        }

        private void FixedUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            bool keyGas = keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
            bool keyBrake = keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            bool keyLeft = keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            bool keyRight = keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
            float drive = (keyGas || touchGas ? 1f : 0f) - (keyBrake || touchBrake ? 1f : 0f);
            float steer = (keyLeft || touchLeft ? 1f : 0f) - (keyRight || touchRight ? 1f : 0f);
            body.MoveRotation(body.rotation + steer * steerSpeed * Time.fixedDeltaTime);
            Vector2 forward = Quaternion.Euler(0, 0, body.rotation) * Vector2.up;
            body.MovePosition(body.position + forward * (drive * moveSpeed * Time.fixedDeltaTime));
        }

        public void SetTouchControls(bool gas, bool brake, bool left, bool right)
        {
            touchGas = gas;
            touchBrake = brake;
            touchLeft = left;
            touchRight = right;
        }

        private void OnDisable() => SetTouchControls(false, false, false, false);
    }
}
