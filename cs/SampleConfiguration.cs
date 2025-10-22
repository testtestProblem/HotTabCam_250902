//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the Microsoft Public License.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using Windows.Media.Capture;
using Windows.UI.Core;
using Windows.Media.MediaProperties;
using System.Diagnostics;
namespace CameraManualControls
{
    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }

    public class MediaCapturePreviewer
    {
        CoreDispatcher _dispatcher;
        CaptureElement _previewControl;

        public MediaCapturePreviewer(CaptureElement previewControl, CoreDispatcher dispatcher)
        {
            _previewControl = previewControl;
            _dispatcher = dispatcher;
        }

        public bool IsPreviewing { get; set; }
        public bool IsRecording { get; set; }
        public MediaCapture MediaCapture { get; private set; }

        /// <summary>
        /// Sets encoding properties on a camera stream. Ensures CaptureElement and preview stream are stopped before setting properties.
        /// </summary>
        public async Task SetMediaStreamPropertiesAsync(MediaStreamType streamType, IMediaEncodingProperties encodingProperties)
        {
            // Stop preview and unlink the CaptureElement from the MediaCapture object
            await MediaCapture.StopPreviewAsync();
            _previewControl.Source = null;

            // Apply desired stream properties
            await MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, encodingProperties);

            // Recreate the CaptureElement pipeline and restart the preview
            _previewControl.Source = MediaCapture;
            await MediaCapture.StartPreviewAsync();
        }

        /// <summary>
        /// Initializes the MediaCapture, starts preview.
        /// </summary>
        public async Task InitializeCameraAsync(MediaCaptureInitializationSettings Setting)
        {
            MediaCapture = new MediaCapture();
            MediaCapture.Failed += MediaCapture_Failed;

            try
            {
                await MediaCapture.InitializeAsync(Setting);
                _previewControl.Source = MediaCapture;
                await MediaCapture.StartPreviewAsync();
                IsPreviewing = true;
            }
            catch (UnauthorizedAccessException)
            {
                // This can happen if access to the camera has been revoked.
                Debug.WriteLine("Fail MediaCapture.InitializeAsync() or MediaCapture.StartPreviewAsync()");
                
                await CleanupCameraAsync();
            }
        }

        public async Task CleanupCameraAsync()
        {
            if (IsRecording)
            {
                await MediaCapture.StopRecordAsync();
                IsRecording = false;
            }

            if (IsPreviewing)
            {
                await MediaCapture.StopPreviewAsync();
                IsPreviewing = false;
            }

            _previewControl.Source = null;

            if (MediaCapture != null)
            {
                MediaCapture.Dispose();
                MediaCapture = null;
            }
        }

        private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs e)
        {
            var task = _dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                IsRecording = false;
                IsPreviewing = false;
                await CleanupCameraAsync();
            });
        }
    }
}
