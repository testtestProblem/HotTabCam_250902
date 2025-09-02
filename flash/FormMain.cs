using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using CameraFlashDLLTestAP;
using WMCamera;
using System.Management;

namespace flash
{
    public partial class FormMain : Form
    {
        public const int WM_APP = 0x8000;
        public const int Event_FlashOn = WM_APP + 0x300;
        public const int Event_FlashOff = WM_APP + 0x301;
        public const int Event_Close = WM_APP + 0x302;
        public static bool flagOnOff = false;
        [DllImport("user32.dll", EntryPoint = "#2507")]
        extern static bool SetAutoRotation(bool bEnable);
        private ConnectionOptions connectionOptions;
        private ManagementScope managementScope;
        string MBName = "";

        [DllImport(@"TorchControl.dll")]
        public static extern bool Torch_ON();

        [DllImport(@"TorchControl.dll")]
        public static extern bool Torch_OFF();
        public FormMain()
        {
            InitializeComponent();
            connectionOptions = new ConnectionOptions();
            connectionOptions.Impersonation = ImpersonationLevel.Impersonate;
            connectionOptions.Authentication = AuthenticationLevel.Default;
            connectionOptions.EnablePrivileges = true;

            managementScope = new ManagementScope();
            managementScope.Path = new ManagementPath(@"\\" + Environment.MachineName + @"\root\CIMV2");
            managementScope.Options = connectionOptions;

        }


        public String GetWMI_BIOSMainBoard()
        {
            String BIOSMainBoard = "";

            SelectQuery selectQuery = new SelectQuery("SELECT * FROM Win32_ComputerSystemProduct");
            ManagementObjectSearcher managementObjectSearch = new ManagementObjectSearcher(managementScope, selectQuery);
            ManagementObjectCollection managementObjectCollection = managementObjectSearch.Get();

            foreach (ManagementObject managementObject in managementObjectCollection)
            {
                BIOSMainBoard = (string)managementObject["Name"];
            }

            return BIOSMainBoard;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Event_FlashOn:
                    flagOnOff = !flagOnOff;
                    if (flagOnOff)
                    {
                    	Flash(1);
						BION_Flash(1);
                        Torch_ON();
                    }
                    else
                    { 
						Flash(0);
                        BION_Flash(0);
                        Torch_OFF();
                    }
                    break;
                case Event_FlashOff:
                    flagOnOff = false;
                    BION_Flash(0);
                    Flash(0);
                    Torch_OFF();
                    break;
                case Event_Close:
                    BION_Flash(0);
                    Flash(0);
                    Torch_OFF();
                    SetAutoRotation(true);
                    foreach (var process in Process.GetProcessesByName("flash"))
                    {
                        process.Kill();
                    }
                    break;
            }
            base.WndProc(ref m);
        }

            private void BION_Flash(byte on)//1:on, 0:off
            {
                try
                {
                    if (BISON8MDLL.UVC_OpenRGB(0))
                    {
                    if (on == 0)
                    {
                        BISON8MDLL.UVC_SetTorch(false, 0);

                        Torch_OFF();
                    }
                    else if (on == 1)
                    {
                        BISON8MDLL.UVC_SetTorch(true, 50);

                        Torch_ON();
                    }

                        BISON8MDLL.UVC_Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

        private void FlashOn_Click(object sender, EventArgs e)
            {
            BION_Flash(1);
                Flash(1);
            Torch_ON();
            }

            private void FlashOff_Click(object sender, EventArgs e)
            {
                Flash(0);
                BION_Flash(0);
            Torch_ON();
            }

        public static bool Flash(byte ucOnOff)//1:on, 0:off
        {
            if (ucOnOff == 1)
                CamFlashDll.CAM_SetLEDOn();
            else
                CamFlashDll.CAM_SetLEDOff();

            return true;
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            this.Opacity = 0;
            this.Visible = false;

            BION_Flash(0);
            Flash(0);
            Torch_OFF();

            MBName = GetWMI_BIOSMainBoard();

            SetAutoRotation(false);
            var orientation = SystemInformation.ScreenOrientation;
            if (MBName.Equals("M900P"))
            {
                switch (orientation.ToString())
                {
                    case "Angle0":
                        MonitorControl.RotationDisplayMode(0, MonitorControl.RotationDisplay.DMDO_90, false);
                        break;
                    case "Angle90":
                        MonitorControl.RotationDisplayMode(0, MonitorControl.RotationDisplay.DMDO_180, false);
                        break;
                    case "Angle180":
                        MonitorControl.RotationDisplayMode(0, MonitorControl.RotationDisplay.DMDO_270, false);
                        break;
                    case "Angle270":
                        break;
                }
            }
            else
                MonitorControl.RotationDisplayMode(0, MonitorControl.RotationDisplay.DMDO_DEFAULT, false);

            try {
                string[] ClassList = CameraFlashDLL.GetDeviceList();
                foreach (string s in ClassList)
                {
                    comboBox1.Items.Add(s.ToUpper());
                }
                if (comboBox1.Items.Count >= 1)
                    comboBox1.SelectedIndex = 0;
                comboBox1.SelectedIndex = comboBox1.FindString("VID_064E&PID_998D");
                        CameraFlashDLL.ALC_Initialization(comboBox1.SelectedIndex);
            }
            catch
            {
                ;
            }

        }
    }

    public class BISON8MDLL
        {
            #region BISON For 8M camera
            [DllImport(@"DllRTSCam.dll")]
            public static extern bool UVC_SetTorch(bool bEnable, int nStrength);

            [DllImport(@"DllRTSCam.dll")]
            public static extern bool UVC_OpenRGB(int nStrength);

            [DllImport(@"DllRTSCam.dll")]
            public static extern bool UVC_Close();
        #endregion


    }

}
