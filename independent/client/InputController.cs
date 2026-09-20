using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AmazonZLRemote
{
    internal static class InputController
    {
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void MoveNormalized(double nx, double ny)
        {
            nx = Math.Max(0, Math.Min(1, nx));
            ny = Math.Max(0, Math.Min(1, ny));
            var bounds = Screen.PrimaryScreen.Bounds;
            int x = bounds.Left + (int)Math.Round(nx * Math.Max(1, bounds.Width - 1));
            int y = bounds.Top + (int)Math.Round(ny * Math.Max(1, bounds.Height - 1));
            SetCursorPos(x, y);
        }

        public static void MouseButton(string button, bool down)
        {
            uint flag = 0;
            if (button == "left") flag = down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
            else if (button == "right") flag = down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
            else if (button == "middle") flag = down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;
            if (flag != 0) mouse_event(flag, 0, 0, 0, UIntPtr.Zero);
        }

        public static void Keyboard(int keyCode, bool down)
        {
            if (keyCode < 0 || keyCode > 255) return;
            keybd_event((byte)keyCode, 0, down ? 0u : KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
