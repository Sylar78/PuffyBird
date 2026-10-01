using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PuffyBird
{
    /// <summary>
    /// Entrées de la spec (§15) : chaque nouveau doigt, clic gauche, Espace, Flèche haut, Entrée
    /// ou bouton A = un tap, à l'appui et sans répétition automatique. Échap / P / Start = pause,
    /// M = muet, B = pilote automatique (démo), C = capture d'écran (éditeur). Fonctionne avec le nouvel Input System ou
    /// l'ancien gestionnaire d'entrées.
    /// Les doigts et les clics gardent leur position à l'écran (<see cref="TapPosition"/>), pour
    /// les boutons de l'interface ; les touches et la manette n'en ont pas.
    /// </summary>
    public sealed class InputReader
    {
        public const int MaxTaps = 8;

        readonly Vector2[] _taps = new Vector2[MaxTaps];

        public struct Frame
        {
            /// <summary>Taps sans position (clavier, manette).</summary>
            public int Presses;
            /// <summary>Taps avec position (doigt, souris), voir <see cref="TapPosition"/>.</summary>
            public int Taps;
            public bool Pause;
            public bool Mute;
            public bool ToggleAutoPilot;
            public bool Screenshot;
        }

        public InputReader()
        {
#if !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            // Sinon un toucher compte deux fois (toucher + clic simulé).
            Input.simulateMouseWithTouches = false;
#endif
        }

        /// <summary>Position à l'écran (pixels, origine en bas à gauche) du tap <paramref name="index"/> de la dernière lecture.</summary>
        public Vector2 TapPosition(int index) => _taps[index];

        void AddTap(ref Frame f, Vector2 position)
        {
            if (f.Taps < MaxTaps) _taps[f.Taps++] = position;
        }

        public Frame Read()
        {
            var f = new Frame();
#if ENABLE_INPUT_SYSTEM
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (touch.press.wasPressedThisFrame) AddTap(ref f, touch.position.ReadValue());
                }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) AddTap(ref f, mouse.position.ReadValue());
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) f.Presses++;
                if (keyboard.upArrowKey.wasPressedThisFrame) f.Presses++;
                if (keyboard.enterKey.wasPressedThisFrame) f.Presses++;
                f.Pause |= keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame;
                f.Mute |= keyboard.mKey.wasPressedThisFrame;
                f.ToggleAutoPilot |= keyboard.bKey.wasPressedThisFrame;
                f.Screenshot |= keyboard.cKey.wasPressedThisFrame;
            }
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.buttonSouth.wasPressedThisFrame) f.Presses++;
                f.Pause |= gamepad.startButton.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began) AddTap(ref f, touch.position);
            }
            if (Input.GetMouseButtonDown(0)) AddTap(ref f, Input.mousePosition);
            if (Input.GetKeyDown(KeyCode.Space)) f.Presses++;
            if (Input.GetKeyDown(KeyCode.UpArrow)) f.Presses++;
            if (Input.GetKeyDown(KeyCode.Return)) f.Presses++;
            if (Input.GetKeyDown(KeyCode.JoystickButton0)) f.Presses++;
            f.Pause = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7);
            f.Mute = Input.GetKeyDown(KeyCode.M);
            f.ToggleAutoPilot = Input.GetKeyDown(KeyCode.B);
            f.Screenshot = Input.GetKeyDown(KeyCode.C);
#endif
            return f;
        }
    }
}
