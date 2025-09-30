using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Capture.Frames;
using Windows.UI.Xaml;

namespace CameraManualControls
{
    static class Camera
    {


        /// <summary>
        /// return 0    -> No device    
        /// return 1    -> success  
        /// </summary>
        /// <param name="deviceID"></param>
        /// <returns></returns>
        /*public static async Task InitializeCameraAsync(int deviceID)
        {
            Debug.WriteLine("Initialize Camera 0");

            var allGroups = await MediaFrameSourceGroup.FindAllAsync();
            if (allGroups.Count == 0)
            {
                Debug.WriteLine("No found any Camera");
                return ;
            }
            else if (allGroups.Count == 1)
            {
                Debug.WriteLine("Have Camera device");
            }

            PreviewControl.FlowDirection = _mirroringPreview ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;


            return;
        }*/
    }
}
