
using System.Runtime.InteropServices;
using System.Threading;
using System;
using System.Collections.Generic;
using DirectShowLib;


namespace CameraFlashDLLTestAP
{
    class CameraFlashDLL
    {

        public static int ALC_Initialization(int idx)
        {
            int iRet = 0;
            iRet = CamFlashDll.CAM_Initialization(idx);
            return iRet;
        }

        public static int ALC_UnInitialization()
        {
            return CamFlashDll.CAM_UnInitialization();
        }

        public static bool Flash(byte ucOnOff)//1:on, 0:off
        {
            if (ucOnOff == 1)
                CamFlashDll.CAM_SetLEDOn();
            else
                CamFlashDll.CAM_SetLEDOff();

            return true;
        }

        public static string[] GetDeviceList()
        {
            //string[] HardwareList = hwh.GetAllVideoInput();
            string[] HardwareList = GetListCamera();

            return HardwareList;
        }

        public static string[] GetListCamera()
        {
            DsDevice[] capDevices;
            List<String> list = new List<String>();

            // Get the collection of video devices
            capDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);

            if (capDevices.Length != 0)
            {
                for (int i = 0; i < capDevices.Length; i++)
                {
                    DsDevice dev = capDevices[i];
                    string str = dev.DevicePath;

                    int first = str.IndexOf("#");
                    if (first != -1)
                    {
                        str = str.Substring(first + 1);
                        int second = str.IndexOf("#");
                        if (second != -1)
                        {
                            str = str.Substring(0, second);
                        }
                    }
                    list.Add(str);
                }

                //DsDevice dd = capDevices[iVidoeInputIndex];

                //iDeviceId = CameraDeviceIdCompare(iVidoeInputIndex, dd.DevicePath);

            }

            return list.ToArray(); ;
        }
    }

    class CamFlashDll
    {
        #region CamFlashDll DLL Declare 5M camera

        [DllImport("CamFlashDll.dll")]
        public static extern int CAM_Initialization(int idx);

        [DllImport("CamFlashDll.dll")]
        public static extern int CAM_UnInitialization();

        [DllImport("CamFlashDll.dll")]
        public static extern int CAM_SetLEDOn();

        [DllImport("CamFlashDll.dll")]
        public static extern int CAM_SetLEDOff();

        #endregion

    }
}
