using System;
using System.Management; // Reference: System.Management.dll
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

/*
 * Project: AutoPause Monitor Guard
 * Author: [Your Name/Handle]
 * Description: 
 * A background utility that listens for low-level PnP (Plug and Play) 
 * monitor disconnection events via WMI. When a disconnection is detected, 
 * it automatically simulates an ESC key press to pause active applications/games.
 */

namespace AutoPauseTool
{
    public class Program
    {
        // --- WinAPI Imports for Input Simulation ---
        private const uint INPUT_KEYBOARD = 1;
        private const ushort KEYEVENTF_KEYDOWN = 0x0000;
        private const ushort KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_ESCAPE = 0x1B; // Virtual Key for ESC

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MouseInput mi;
            [FieldOffset(0)] public KeyboardInput ki;
            [FieldOffset(0)] public HardwareInput hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseInput
        {
            public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardInput
        {
            public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HardwareInput
        {
            public uint uMsg; public ushort wParamL; public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        // --- Application State ---
        private static readonly object _lockObject = new object();
        private static DateTime _lastTriggerTime = DateTime.MinValue;

        // Configuration
        private const int DEBOUNCE_SECONDS = 3;
        private const string MONITOR_CLASS_GUID = "{4d36e96e-e325-11ce-bfc1-08002be10318}";

        public static void Main(string[] args)
        {
            Console.Title = "AutoPause Monitor Guard";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== AutoPause Monitor Guard ===");
            Console.ResetColor();
            Console.WriteLine("Status: Running (Admin Privileges Active)");
            Console.WriteLine("Mode: Listening for PnP Monitor Disconnection Events...");
            Console.WriteLine("Action: Press ESC on detection.");
            Console.WriteLine("\nPress [Enter] to exit the application.\n");

            ManagementEventWatcher watcher = null;

            try
            {
                // WQL Query: Listen for deletion (disconnection) of entities with the Monitor GUID
                string query = "SELECT * FROM __InstanceDeletionEvent WITHIN 1 " +
                               "WHERE TargetInstance ISA 'Win32_PnPEntity' " +
                               $"AND TargetInstance.ClassGuid = '{MONITOR_CLASS_GUID}'";

                watcher = new ManagementEventWatcher(new WqlEventQuery(query));
                watcher.EventArrived += HandlePnPEvent;
                watcher.Start();

                // Keep the application running
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Critical Error: {ex.Message}");
                Console.ResetColor();
                Console.ReadLine();
            }
            finally
            {
                watcher?.Stop();
                watcher?.Dispose();
            }
        }

        /// <summary>
        /// Handles the WMI event when a device is removed.
        /// </summary>
        private static void HandlePnPEvent(object sender, EventArrivedEventArgs e)
        {
            // Run on a separate thread to release the WMI listener immediately
            Task.Run(() =>
            {
                lock (_lockObject)
                {
                    // Debounce check to prevent multiple triggers for the same physical event
                    if ((DateTime.Now - _lastTriggerTime).TotalSeconds < DEBOUNCE_SECONDS)
                    {
                        return;
                    }
                    _lastTriggerTime = DateTime.Now;

                    string deviceName = "Unknown Device";
                    try
                    {
                        var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                        deviceName = targetInstance["Caption"]?.ToString() ?? deviceName;
                    }
                    catch { /* Ignore parsing errors */ }

                    LogEvent($"Disconnection Detected: {deviceName}");

                    // Execute the action
                    PerformPauseAction();
                }
            });
        }

        /// <summary>
        /// Simulates the ESC key press.
        /// </summary>
        private static void PerformPauseAction()
        {
            Console.WriteLine(">> Sending ESC key...");

            INPUT keyDown = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KeyboardInput
                    {
                        wVk = VK_ESCAPE,
                        dwFlags = KEYEVENTF_KEYDOWN,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };

            INPUT keyUp = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KeyboardInput
                    {
                        wVk = VK_ESCAPE,
                        dwFlags = KEYEVENTF_KEYUP,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };

            INPUT[] inputs = { keyDown, keyUp };

            uint result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

            if (result == 0)
            {
                LogEvent($"Error: SendInput failed. Code: {Marshal.GetLastWin32Error()}");
            }
            else
            {
                LogEvent("Success: ESC command sent to the OS.");
            }
        }

        private static void LogEvent(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            Console.ResetColor();
        }
    }
}