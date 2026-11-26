using Arena.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Arena.Characters
{
    /// <summary>
    /// Switches the player's weapon (ammo type): keys 1 / 2 / 3, mouse wheel, gamepad D-pad left / right.
    /// </summary>
    [RequireComponent(typeof(Player))]
    public class WeaponSwitcher : MonoBehaviour
    {
        [Tooltip("Minimum time between two switches with the mouse wheel, in seconds.")]
        [SerializeField, Min(0f)] private float _wheelCooldown = 0.12f;

        private Player _player;
        private float _nextWheelTime;

        void Awake()
        {
            _player = GetComponent<Player>();
        }

        void Update()
        {
            ProjectileLauncher launcher = _player.Launcher;
            if (!launcher || _player.Health.IsDead)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) launcher.SelectWeapon(0);
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) launcher.SelectWeapon(1);
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) launcher.SelectWeapon(2);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && Time.time >= _nextWheelTime)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    launcher.CycleWeapon(scroll > 0f ? -1 : 1);
                    _nextWheelTime = Time.time + _wheelCooldown;
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.dpad.right.wasPressedThisFrame) launcher.CycleWeapon(1);
                if (gamepad.dpad.left.wasPressedThisFrame) launcher.CycleWeapon(-1);
            }
        }
    }
}
