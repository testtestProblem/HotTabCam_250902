using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace flash
{
    static class Program
    {
        [DllImport("user32.dll")]
        public static extern Int32 FindWindow(String lpClassName, String lpWindowName);
        [DllImport("user32.dll")]
        public static extern Int32 PostMessage(Int32 hWnd, Int32 wMsg, Int32 wParam, Int32 lParam);
        public const int WM_APP = 0x8000;
        public const int Event_FlashOn = WM_APP + 0x300;
        public const int Event_FlashOff = WM_APP + 0x301;
        public const int Event_Close = WM_APP + 0x302;

        /// <summary>
        /// 應用程式的主要進入點。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            bool AppRunningFlag = false;

            System.Threading.Mutex mutex = new System.Threading.Mutex(true, Application.ProductName, out AppRunningFlag);
            Int32 hwndMainForm;
            hwndMainForm = FindWindow(null, "flash");

            if (AppRunningFlag)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FormMain());
                mutex.ReleaseMutex();
                PostMessage(hwndMainForm, Event_FlashOn, 0, 0);
            }
            else
            {
                if (args.Length > 2)
                {
                    switch (args[2])
                    {
                        case "/CloseAPP":
                            PostMessage(hwndMainForm, Event_Close, 0, 0);
                            break;
                        case "/FlashOn":
                            PostMessage(hwndMainForm, Event_FlashOn, 0, 0);
                            break;
                        case "/FlashOff":
                            PostMessage(hwndMainForm, Event_FlashOff, 0, 0);
                            break;
                    }
                }
                Environment.Exit(1);
            }
        }
    }
}
