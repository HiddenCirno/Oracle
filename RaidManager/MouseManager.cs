using EFT.UI;
using Oracle.Tools;
using UnityEngine;

namespace Oracle.RaidManager
{
    public static class MouseManager
    {
        private static GameObject _inputManager;

        public static void ToggleCursor()
        {
            var unlock = RaidManagerGUI._isMenuOpen|| ItemManagerGUI._isMenuOpen;
            if (_inputManager == null)
                _inputManager = GameObject.Find("___Input");

            Cursor.visible = unlock;

            if (unlock)
            {
                Cursor.lockState = CursorLockMode.None;
                CursorSettings.SetCursor(ECursorType.Idle);
                Comfort.Common.Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.MenuContextMenu);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                CursorSettings.SetCursor(ECursorType.Invisible);
                Comfort.Common.Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.MenuDropdown);
            }

            if (_inputManager != null)
                _inputManager.SetActive(!unlock);
        }
    }
}