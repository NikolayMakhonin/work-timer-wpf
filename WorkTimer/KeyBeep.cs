using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace WorkTimer
{
    public class KeyBeep : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public KeyBeep()
        {
            _hookID = SetHook(_proc);
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
            }
        }

        private static HashSet<int> keysPressed = new HashSet<int>();

        private static int beepQueueCount = 0;
        private static Task beepTask = null;
        private static System.Media.SoundPlayer player = new System.Media.SoundPlayer(Properties.Resources.click);
        private static void Beep()
        {
            beepQueueCount++;
            if (beepTask == null)
            {
                beepTask = Task.Run(async () =>
                {
                    while (beepQueueCount > 0)
                    {
                        beepQueueCount = Math.Min(3, beepQueueCount) - 1;
                        player.Stop();
                        player.Play();
                        await Task.Delay(60);
                    }
                    beepTask = null;
                });
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Task.Run(() => Console.Beep(1000, 100));
                // beep on the first key down only before key up
                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    var key = Marshal.ReadInt32(lParam);
                    var isPressed = keysPressed.Contains(key);
                    if (!isPressed)
                    {
                        keysPressed.Add(key);
                        Beep();
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    var key = Marshal.ReadInt32(lParam);
                    keysPressed.Remove(key);
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
