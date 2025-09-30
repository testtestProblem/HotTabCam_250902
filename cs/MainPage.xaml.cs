//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;
using Windows.UI.Popups;

namespace CameraManualControls
{

    public sealed partial class MainPage : Page
    {
        // Receive notifications about rotation of the device and UI and apply any necessary rotation to the preview stream and UI controls       
        private readonly DisplayInformation _displayInformation = DisplayInformation.GetForCurrentView();
        private readonly SimpleOrientationSensor _orientationSensor = SimpleOrientationSensor.GetDefault();
        //private SimpleOrientation _deviceOrientation = SimpleOrientation.NotRotated;
        //private DisplayOrientations _displayOrientation = DisplayOrientations.Portrait;
        private SimpleOrientation _deviceOrientation = SimpleOrientation.Rotated270DegreesCounterclockwise;
        private DisplayOrientations _displayOrientation = DisplayOrientations.Landscape;

        // Rotation metadata to apply to the preview stream and recorded videos (MF_MT_VIDEO_ROTATION)
        // Reference: http://msdn.microsoft.com/en-us/library/windows/apps/xaml/hh868174.aspx
        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        // Folder in which the captures will be stored (initialized in SetupUiAsync)
        private StorageFolder _captureFolder = null;

        // Prevent the screen from sleeping while the camera is running
        private readonly DisplayRequest _displayRequest = new DisplayRequest();

        // For listening to media property changes
        private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

        // Access to the Back button
        private readonly SystemNavigationManager _systemNavigationManager = SystemNavigationManager.GetForCurrentView();

        // MediaCapture and its state variables
        private MediaFrameSource _source;
        private bool _isInitialized;
        private bool _isPreviewing;
        private bool _isRecording;
        private bool _isFocused;

        // Information about the camera device
        private bool _mirroringPreview = true;
        private bool _externalCamera;

        // UI stat variable: whether the user has chosen a single control to manipulate, or showing buttons for all controls
        private bool _singleControlMode;

        // A flag that signals when the UI controls (especially sliders) are being set up, to prevent them from triggering callbacks and making API calls
        private bool _settingUpUi;

        private List<MediaFrameReader> _sourceReaders = new List<MediaFrameReader>();
        private IReadOnlyDictionary<MediaFrameSourceKind, FrameRenderer> _frameRenderers;
        public static int _groupSelectionIndex = 0;

        // Object to manage access to camera devices
        private MediaCapturePreviewer _previewer = null;
        private bool _FirstTimeInitial = false;
        private bool _frontCamFirstTimeInitial = false;
        private bool _frontCamFirstTimeInitialWorkaround = false;
        private bool _backCamFirstTimeInitial = false;
        private string _backVideoIndex = "3264x2448";
        private string _backPicIndex = "3264x2448";
        public int CountDown_Seconds=0;

        public int noSouceGroup_fage = 0;

        DispatcherTimer Timer = new DispatcherTimer();
        #region Constructor, lifecycle and navigation
        public Action<object, SuspendingEventArgs> Suspending { get; }


        public MainPage()
        {
            this.InitializeComponent();

            // Do not cache the state of the UI when navigating
            NavigationCacheMode = NavigationCacheMode.Disabled;

            // Useful to know when to initialize/clean up the camera
            Application.Current.Suspending += Application_Suspending;
            Application.Current.Resuming += Application_Resuming;

        }

        private void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            Debug.WriteLine("Application_Suspending");

            //// Handle global application events only if this page is active
            //if (Frame.CurrentSourcePageType == typeof(MainPage))
            //{
            //    try
            //    {
            //        if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            //        {
            //            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("SwitchBtn");
            //        }
            //    }
            //    catch
            //    {
            //        ;
            //    }

            //    var deferral = e.SuspendingOperation.GetDeferral();

            //    //await CleanupCameraAsync();

            //    await CleanupUiAsync();

            //    deferral.Complete();

            //    _frontCamFirstTimeInitial = false;
            //    _backCamFirstTimeInitial = false;
            //    await CleanupMediaCaptureAsync();
            //}

        }

        private async void Application_Resuming(object sender, object o)
        {
            Debug.WriteLine("Application_Resuming");
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                // Ensure the current window is active
                Window.Current.Activate();

                await CleanupMediaCaptureAsync();

                await SetupUiAsync();

                // Ensure the current window is active
                Window.Current.Activate();

                await InitializeCameraAsync(_groupSelectionIndex);
                while (noSouceGroup_fage == 1) await InitializeCameraAsync(_groupSelectionIndex);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            //Debug.WriteLine("OnNavigatedTo");
            await SetupUiAsync();

            await InitializeCameraAsync(_groupSelectionIndex);

        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Handling of this event is included for completenes, as it will only fire when navigating between pages and this sample only includes one page
            //Debug.WriteLine("OnNavigatingFrom");

            await CleanupCameraAsync();

            await CleanupUiAsync();

        }

        #endregion Constructor, lifecycle and navigation


        #region Event handlers

        /// <summary>
        /// In the event of the app being minimized this method handles media property change events. If the app receives a mute
        /// notification, it is no longer in the foregroud.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void SystemMediaControls_PropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                // Only handle this event if this page is currently being displayed
                if (args.Property == SystemMediaTransportControlsProperty.SoundLevel && Frame.CurrentSourcePageType == typeof(MainPage))
                {
                    // Check to see if the app is being muted. If so, it is being minimized.
                    // Otherwise if it is not initialized, it is being brought into focus.
                    if (sender.SoundLevel == SoundLevel.Muted)
                    {
                        Debug.WriteLine("SystemMediaControls_PropertyChanged_CleanupCameraAsync");
                        //await CleanupCameraAsync();
                    }
                    else if (!_isInitialized)
                    {
                        Debug.WriteLine("SystemMediaControls_PropertyChanged_InitializeCameraAsync");
                        //await InitializeCameraAsync(_groupSelectionIndex);
                    }
                }
            });
        }

        /// <summary>
        /// Occurs each time the simple orientation sensor reports a new sensor reading.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="args">The event data.</param>
        private async void OrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        {
            if (args.Orientation != SimpleOrientation.Faceup && args.Orientation != SimpleOrientation.Facedown)
            {
                // Only update the current orientation if the device is not parallel to the ground. This allows users to take pictures of documents (FaceUp)
                // or the ceiling (FaceDown) in portrait or landscape, by first holding the device in the desired orientation, and then pointing the camera
                // either up or down, at the desired subject.
                //Note: This assumes that the camera is either facing the same way as the screen, or the opposite way. For devices with cameras mounted
                //      on other panels, this logic should be adjusted.
                _deviceOrientation = args.Orientation;

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateButtonOrientation());
            }
        }

        /// <summary>
        /// This event will fire when the page is rotated
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="args">The event data.</param>
        private async void DisplayInformation_OrientationChanged(DisplayInformation sender, object args)
        {
            //_displayOrientation = sender.CurrentOrientation;
            _displayOrientation = DisplayOrientations.Landscape;

            if (_isPreviewing)
            {
                await SetPreviewRotationAsync();
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateButtonOrientation());
        }

        private async void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            if (Splitter.IsPaneOpen)
                Splitter.IsPaneOpen = !Splitter.IsPaneOpen;

            PhotoButton.IsEnabled = false;
            await TakePhotoAsync();
        }


        private void Timer_Tick(object sender, object e)
        {

            TimeSpan duration = new TimeSpan(0, 0, CountDown_Seconds - 1);
            timerText.Text = duration.ToString().Substring(3,5);
            CountDown_Seconds = CountDown_Seconds + 1;
            if (CountDown_Seconds>1)
                timerText.Visibility = Visibility.Visible;
        }

        private async void VideoButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchBtn.IsEnabled = false;
            CountDown_Seconds = 0;
            timerText.Text = "";
            timerText.IsEnabled = true;
            //< start Timer >

            Timer.Start();

            //</ start Timer >

            if (Splitter.IsPaneOpen)
                Splitter.IsPaneOpen = !Splitter.IsPaneOpen;

            if (!_isRecording)
            {
                await StartRecordingAsync();
                _displayRequest.RequestActive();
            }
            else
            {
                await StopRecordingAsync();
                //_displayRequest.RequestRelease();
            }

            // After starting or stopping video recording, update the UI to reflect the MediaCapture state
            UpdateCaptureControls();
        }

        private async void HardwareButtons_CameraPressed(object sender, CameraEventArgs e)
        {
            await TakePhotoAsync();
        }

        private async void PreviewControl_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (!_isPreviewing || (TapFocusRadioButton.IsChecked != true)) return;

            if (!_isFocused && _previewer.MediaCapture.VideoDeviceController.FocusControl.FocusState != MediaCaptureFocusState.Searching)
            {
                var smallEdge = Math.Min(Window.Current.Bounds.Width, Window.Current.Bounds.Height);

                // Choose to make the focus rectangle 1/4th the length of the shortest edge of the window
                var size = new Size(smallEdge / 4, smallEdge / 4);
                var position = e.GetPosition(sender as UIElement);

                // Note that at this point, a rect at "position" with size "size" could extend beyond the preview area. The following method will reposition the rect if that is the case
                await TapToFocus(position, size);
            }
            else
            {
                await TapUnfocus();
            }
        }

        private void PreviewControl_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // This event handler implements pinch-to-zoom, which should only happen if preview is running
            if (!_isPreviewing) return;

            // Pinch gestures are a delta, so apply them to the current zoom value
            var zoomFactor = ZoomSlider.Value * e.Delta.Scale;

            // Set the value back on the slider, which will make the call to the ZoomControl
            ZoomSlider.Value = zoomFactor;
        }

        private async void MediaCapture_RecordLimitationExceeded(MediaCapture sender)
        {
            // This is a notification that recording has to stop, and the app is expected to finalize the recording

            await StopRecordingAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }

        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            //Debug.WriteLine("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message);

            await CleanupCameraAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }

        #endregion Event handlers


        #region MediaCapture methods

        /// <summary>
        /// Initializes the MediaCapture, registers events, gets camera device information for mirroring and rotating, starts preview and unlocks the UI
        /// </summary>
        /// <returns></returns>
        public async Task InitializeCameraAsync(int deviceID)
        {
            Debug.WriteLine("WM_ InitializeCameraAsync start");

            var allGroups = await MediaFrameSourceGroup.FindAllAsync();
            if (allGroups.Count == 0)
            {
                Debug.WriteLine("No source groups(camera) found.");
                noSouceGroup_fage = 1;
                return ;
            }
            if (allGroups.Count == 1)
            {
                Debug.WriteLine("success found source groups(camera).");
                noSouceGroup_fage = 0;

                SwitchBtn.IsEnabled = false;
                SwitchBtn.Visibility = Visibility.Collapsed;
            }

            // Pick next group in the array after each time the Next button is clicked.
            //_groupSelectionIndex = (_groupSelectionIndex + 1) % allGroups.Count;
            _groupSelectionIndex = deviceID;
            var selectedGroup = allGroups[_groupSelectionIndex];

            if (_FirstTimeInitial == false)
            {
                _mirroringPreview = true;
                if (selectedGroup.DisplayName.Contains("Rear") && allGroups.Count > 1) //Initial Back camera at first time
                {
                    _groupSelectionIndex = 1;
                    selectedGroup = allGroups[_groupSelectionIndex];
                }
                else
                {
                    _groupSelectionIndex = 0;
                    selectedGroup = allGroups[_groupSelectionIndex];
                }
            }

            //Debug.WriteLine($"Found {allGroups.Count} groups and selecting index [{_groupSelectionIndex}]: {selectedGroup.DisplayName}");
            PreviewControl.FlowDirection = _mirroringPreview ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            // The Canvas containing the FocusRectangle should be mirrored if the CaptureElement is, so LeftToRighttaps are shown in the correct position
            Debug.WriteLine("_mirroringPreview " + _mirroringPreview);
            //if (_mirroringPreview == false) noSouceGroup_fage = 1;

            _previewer = new MediaCapturePreviewer(PreviewControl, Dispatcher);
            var settings = new MediaCaptureInitializationSettings
            {
                SourceGroup = selectedGroup,

                // This media capture can share streaming with other apps.
                //SharingMode = MediaCaptureSharingMode.SharedReadOnly,

                // Only stream video and don't initialize audio capture devices.
                //StreamingCaptureMode = StreamingCaptureMode.Video,

                // Set to CPU to ensure frames always contain CPU SoftwareBitmap images
                // instead of preferring GPU D3DSurface images.
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };
            try
            {
                await _previewer.InitializeCameraAsync(settings);
                _isInitialized = true;
                _isPreviewing = _previewer.IsPreviewing;
                // Initialize the preview to the current orientation
                await SetPreviewRotationAsync();

                // Register for a notification when video recording has reached the maximum time and when something goes wrong
                _previewer.MediaCapture.RecordLimitationExceeded += MediaCapture_RecordLimitationExceeded;

                Debug.WriteLine("inital camera success");
                Debug.WriteLine(_groupSelectionIndex + " if 1 is rear camera"); 
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("The app was denied access to the camera");
            }

            // If initialization succeeded, start the preview
            if (_isInitialized)
            {
                // Figure out where the camera is located
                //if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                //{
                //    // No information on the location of the camera, assume it's an external camera, not integrated on the device
                //    _externalCamera = true;
                //}
                //else
                //{
                // Camera is fixed on the device
                _externalCamera = false;

                //    // Only mirror the preview if the camera is on the front panel
                //    _mirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                //}
                //_mirroringPreview = false;

                //await StartPreviewAsync();

                System.Collections.Generic.IReadOnlyList<IMediaEncodingProperties> res;
                res = _previewer.MediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);
                uint maxResolution = 0;
                int indexMaxResolution = 0;

                if (res.Count >= 1)
                {
                    for (int i = 0; i < res.Count; i++)
                    {
                        VideoEncodingProperties vp = (VideoEncodingProperties)res[i];

                        Debug.WriteLine("enmu resolution");
                        if (vp.Width > maxResolution)
                        {
                            indexMaxResolution = i;
                            maxResolution = vp.Width;
                            Debug.WriteLine(indexMaxResolution + "   ----------  over max width");
                            Debug.WriteLine("Resolution: " + vp.Width + "X" + vp.Height);
                            Debug.WriteLine("FrameRate: " + vp.FrameRate.Numerator.ToString());
                            Debug.WriteLine("Type: " + vp.Subtype.ToString());
                        }
                    }
                    if ((((VideoEncodingProperties)res[indexMaxResolution]).Width > 2592)) //5M
                    {
                        Debug.WriteLine("_mirroringPreview error over max resolution");

                        _mirroringPreview = false;
                        if (!_backCamFirstTimeInitial)
                        {
                            PopulateComboBox(MediaStreamType.Photo, FormatComboBox, false);
                            PopulateComboBox(MediaStreamType.VideoRecord, BackVideoFormatComboBox);

                            _backCamFirstTimeInitial = true;
                        }

                        foreach (ComboBoxItem cbi in FormatComboBox.Items)
                        {
                            Debug.WriteLine("FormatComboBox0: " + cbi.Content.ToString());
                            if (cbi.Content as string == _backPicIndex)
                            {
                                FormatComboBox.PlaceholderText = cbi.Content.ToString();
                                var encodingProperties = (cbi.Tag as StreamResolution).EncodingProperties;
                                await _previewer.MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.Photo, encodingProperties);
                            }
                        }
                        
                        foreach (ComboBoxItem cbi in BackVideoFormatComboBox.Items)
                        {
                            Debug.WriteLine("FormatComboBox1: " + cbi.Content.ToString());
                            if (cbi.Content as string == _backVideoIndex)
                            {
                                BackVideoFormatComboBox.PlaceholderText = cbi.Content.ToString();
                                var encodingProperties = (cbi.Tag as StreamResolution).EncodingProperties;
                                await _previewer.MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoRecord, encodingProperties);
                            }
                        }


                        FlashBtn.IsEnabled = true;
                        FrontVideoFormatComboBox.IsEnabled = false;
                        FrontVideoFormatComboBox.Visibility = Visibility.Collapsed;
                        BackVideoFormatComboBox.IsEnabled = true;
                        BackVideoFormatComboBox.Visibility = Visibility.Visible;
                        FlashBtn.Visibility = Visibility.Visible;
                        SettingBtn.Visibility = Visibility.Visible;
                        timerText.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        _mirroringPreview = true;
                        if (!_frontCamFirstTimeInitial)
                        {
                            PopulateComboBox(MediaStreamType.VideoRecord, FrontVideoFormatComboBox);

                            _frontCamFirstTimeInitial = true;
                        }

                        foreach (ComboBoxItem cbi in FrontVideoFormatComboBox.Items)
                        {
                            Debug.WriteLine("FormatComboBox2: " + cbi.Content.ToString());
                            if ((cbi.Content as string).Equals("1920x1080"))
                            {
                                FrontVideoFormatComboBox.PlaceholderText = cbi.Content.ToString();
                                var encodingProperties = (cbi.Tag as StreamResolution).EncodingProperties;
                                await _previewer.MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoRecord, encodingProperties);
                                Debug.WriteLine("FormatComboBox1: " + cbi.Content.ToString());
                                break;
                            }
                            if (cbi.Content as string == "1280x720")
                            {
                                FrontVideoFormatComboBox.PlaceholderText = cbi.Content.ToString();
                                var encodingProperties = (cbi.Tag as StreamResolution).EncodingProperties;
                                await _previewer.MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoRecord, encodingProperties);
                                Debug.WriteLine("FormatComboBox3: " + cbi.Content.ToString());
                            }
                        }

                        _displayRequest.RequestActive();


                        FlashBtn.IsEnabled = false;
                        FrontVideoFormatComboBox.IsEnabled = true;
                        FrontVideoFormatComboBox.Visibility = Visibility.Visible;
                        BackVideoFormatComboBox.IsEnabled = false;
                        BackVideoFormatComboBox.Visibility = Visibility.Collapsed;
                        FlashBtn.Visibility = Visibility.Collapsed;
                        SettingBtn.Visibility = Visibility.Collapsed;
                        timerText.Visibility = Visibility.Collapsed;
                    }

                    //Debug.WriteLine("WM_ InitializeCameraAsync Done");
                    //await _previewer.MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, res[indexMaxResolution]);
                }

                UpdateCaptureControls();
                UpdateManualControlCapabilities();

                CheckIfStreamsAreIdentical();

                //Winmate workaround back-end camera first
                if (_FirstTimeInitial == false && allGroups.Count > 1)
                {
                    this.Button_Click(null, null);
                    _FirstTimeInitial = true;
                    _frontCamFirstTimeInitialWorkaround = false;

                    Timer.Tick += Timer_Tick;

                    Timer.Interval = new TimeSpan(0, 0, 1);
                }
            }
        }

        /// <summary>
        /// Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on
        /// </summary>
        /// <returns></returns>
        private async Task StartPreviewAsync()
        {
            // Prevent the device from sleeping while the preview is running
            _displayRequest.RequestActive();

            // Set the preview source in the UI and mirror it if necessary
            PreviewControl.Source = _previewer.MediaCapture;
            PreviewControl.FlowDirection = _mirroringPreview ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            // The Canvas containing the FocusRectangle should be mirrored if the CaptureElement is, so taps are shown in the correct position
            FocusCanvas.FlowDirection = PreviewControl.FlowDirection;

            // Start the preview
            await _previewer.MediaCapture.StartPreviewAsync();
            _isPreviewing = true;

            // Initialize the preview to the current orientation
            // await SetPreviewRotationAsync();
        }

        /// <summary>
        /// Gets the current orientation of the UI in relation to the device and applies a corrective rotation to the preview
        /// </summary>
        private async Task SetPreviewRotationAsync()
        {
            // Only need to update the orientation if the camera is mounted on the device
            if (_externalCamera) return;

            // Calculate which way and how far to rotate the preview
            int rotationDegrees = ConvertDisplayOrientationToDegrees(_displayOrientation);

            // The rotation direction needs to be inverted if the preview is being mirrored
            if (_mirroringPreview)
            {
                rotationDegrees = (360 - rotationDegrees) % 360;
            }

            Debug.WriteLine("SetPreviewRotationAsync and rotationDegrees: " + rotationDegrees);

            // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
            var props = _previewer.MediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
            props.Properties.Add(RotationKey, rotationDegrees);
            await _previewer.MediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
        }

        /// <summary>
        /// Stops the preview and deactivates a display request, to allow the screen to go into power saving modes
        /// </summary>
        /// <returns></returns>
        private async Task StopPreviewAsync()
        {
            // Stop the preview
            _isPreviewing = false;
            await _previewer.MediaCapture.StopPreviewAsync();

            // Use the dispatcher because this method is sometimes called from non-UI threads
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Cleanup the UI
                PreviewControl.Source = null;

                // Allow the device screen to sleep now that the preview is stopped
               // _displayRequest.RequestRelease();
            });
        }

        /// <summary>
        /// Takes a photo to a StorageFile and adds rotation metadata to it
        /// </summary>
        /// <returns></returns>
        public async Task TakePhotoAsync()
        {
            // While taking a photo, keep the video button enabled only if the camera supports simultaneously taking pictures and recording video
            VideoButton.IsEnabled = _previewer.MediaCapture.MediaCaptureSettings.ConcurrentRecordAndPhotoSupported;

            // Make the button invisible if it's disabled, so it's obvious it cannot be interacted with
            VideoButton.Opacity = VideoButton.IsEnabled ? 1 : 0;

            var stream = new InMemoryRandomAccessStream();

            Debug.WriteLine("Taking photo...");
            await _previewer.MediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

            string _fileName = DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString().PadLeft(2, '0') + DateTime.Now.Day.ToString().PadLeft(2, '0') + DateTime.Now.Hour.ToString().PadLeft(2, '0') + DateTime.Now.Minute.ToString().PadLeft(2, '0') + DateTime.Now.Second.ToString().PadLeft(2, '0') + DateTime.Now.Millisecond.ToString().PadLeft(2, '0') + ".jpg";
            try
            {
                var file = await _captureFolder.CreateFileAsync(_fileName, CreationCollisionOption.GenerateUniqueName);

                //Debug.WriteLine("Photo taken! Saving to " + file.Path);

                var photoOrientation = ConvertOrientationToPhotoOrientation(GetCameraOrientation());

                await ReencodeAndSavePhotoAsync(stream, file, photoOrientation);

                //Debug.WriteLine("Photo saved!");
            }
            catch (Exception ex)
            {
                // File I/O errors are reported as exceptions
                //Debug.WriteLine("Exception when taking a photo: " + ex.ToString());
            }

            // Done taking a photo, so re-enable the button
            VideoButton.IsEnabled = true;
            VideoButton.Opacity = 1;
            PhotoButton.IsEnabled = true;
        }

        /// <summary>
        /// Records an MP4 video to a StorageFile and adds rotation metadata to it
        /// </summary>
        /// <returns></returns>
        private async Task StartRecordingAsync()
        {
            string _fileName = DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString().PadLeft(2, '0') + DateTime.Now.Day.ToString().PadLeft(2, '0') + DateTime.Now.Hour.ToString().PadLeft(2, '0') + DateTime.Now.Minute.ToString().PadLeft(2, '0') + DateTime.Now.Second.ToString().PadLeft(2, '0') + DateTime.Now.Millisecond.ToString().PadLeft(2, '0') + ".mp4";
            try
            {
                // Create storage file for the capture
                var videoFile = await _captureFolder.CreateFileAsync(_fileName, CreationCollisionOption.GenerateUniqueName);

                var encodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);

                // Calculate rotation angle, taking mirroring into account if necessary
                var rotationAngle = 360 - ConvertDeviceOrientationToDegrees(GetCameraOrientation());
                encodingProfile.Video.Properties.Add(RotationKey, PropertyValue.CreateInt32(rotationAngle));

                //Debug.WriteLine("Starting recording to " + videoFile.Path);

                await _previewer.MediaCapture.StartRecordToStorageFileAsync(encodingProfile, videoFile);
                _isRecording = true;

                //Debug.WriteLine("Started recording!");
            }
            catch (Exception ex)
            {
                // File I/O errors are reported as exceptions
                //Debug.WriteLine("Exception when starting video recording: " + ex.ToString());
            }
        }

        /// <summary>
        /// Stops recording a video
        /// </summary>
        /// <returns></returns>
        private async Task StopRecordingAsync()
        {
            Debug.WriteLine("Stopping recording...");

            _isRecording = false;
            Timer.Stop();
            timerText.IsEnabled = false;
            timerText.Visibility = Visibility.Collapsed;
            SwitchBtn.IsEnabled = true;
            await _previewer.MediaCapture.StopRecordAsync();

            //Debug.WriteLine("Stopped recording!");
        }

        /// <summary>
        /// Cleans up the camera resources (after stopping any video recording and/or preview if necessary) and unregisters from MediaCapture events
        /// </summary>
        /// <returns></returns>
        private async Task CleanupCameraAsync()
        {
            Debug.WriteLine("CleanupCameraAsync");

            if (_isInitialized)
            {
                // If a recording is in progress during cleanup, stop it to save the recording
                if (_isRecording)
                {
                    //await StopRecordingAsync();
                }

                if (_isPreviewing)
                {
                    // The call to stop the preview is included here for completeness, but can be
                    // safely removed if a call to MediaCapture.Dispose() is being made later,
                    // as the preview will be automatically stopped at that point
                    //await StopPreviewAsync();
                }

                _isInitialized = false;
            }

            //if (_previewer.MediaCapture != null)
            //{
            //    _previewer.MediaCapture.RecordLimitationExceeded -= MediaCapture_RecordLimitationExceeded;
            //    _previewer.MediaCapture.Failed -= MediaCapture_Failed;
            //    _previewer.MediaCapture.Dispose();
            //    //_previewer.MediaCapture = null;
            //}
        }

        #endregion MediaCapture methods


        #region Helper functions

        /// <summary>
        /// Attempts to lock the page orientation, hide the StatusBar (on Phone) and registers event handlers for hardware buttons and orientation sensors
        /// </summary>
        /// <returns></returns>
        public async Task SetupUiAsync()
        {
            // Hide the status bar
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().HideAsync();
            }

            // Populate orientation variables with the current state
            //_displayOrientation = _displayInformation.CurrentOrientation;
            _displayOrientation = DisplayOrientations.Landscape;
            if (_orientationSensor != null)
            {
                _deviceOrientation = _orientationSensor.GetCurrentOrientation();
            }

            RegisterEventHandlers();

            var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
            // Fall back to the local app storage if the Pictures Library is not available
            _captureFolder = picturesLibrary.SaveFolder ?? ApplicationData.Current.LocalFolder;
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors, allows the StatusBar (on Phone) to show, and removes the page orientation lock
        /// </summary>
        /// <returns></returns>
        private async Task CleanupUiAsync()
        {
            UnregisterEventHandlers();

            // Show the status bar
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().ShowAsync();
            }
        }

        /// <summary>
        /// This method will update the icons, enable/disable and show/hide the photo/video buttons depending on the current state of the app and the capabilities of the device
        /// </summary>
        private void UpdateCaptureControls()
        {
            // The buttons should only be enabled if the preview started sucessfully
            PhotoButton.IsEnabled = _isPreviewing;
            VideoButton.IsEnabled = _isPreviewing;

            // Depending on the preview, hide or show the controls grid which houses the individual control buttons and settings
            CameraControlsGrid.Visibility = _isPreviewing ? Visibility.Visible : Visibility.Collapsed;

            // Update recording button to show "Stop" icon instead of red "Record" icon
            StartRecordingIcon.Visibility = _isRecording ? Visibility.Collapsed : Visibility.Visible;
            StopRecordingIcon.Visibility = _isRecording ? Visibility.Visible : Visibility.Collapsed;

            // If the camera doesn't support simultaneosly taking pictures and recording video, disable the photo button on record
            if (_isInitialized && !_previewer.MediaCapture.MediaCaptureSettings.ConcurrentRecordAndPhotoSupported)
            {
                PhotoButton.IsEnabled = !_isRecording;

                // Make the button invisible if it's disabled, so it's obvious it cannot be interacted with
                PhotoButton.Opacity = PhotoButton.IsEnabled ? 1 : 0;
            }
        }

        /// <summary>
        /// Registers event handlers for hardware buttons and orientation sensors, and performs an initial update of the UI rotation
        /// </summary>
        private void RegisterEventHandlers()
        {
            if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
            {
                HardwareButtons.CameraPressed += HardwareButtons_CameraPressed;
            }

            // If there is an orientation sensor present on the device, register for notifications
            if (_orientationSensor != null)
            {
                _orientationSensor.OrientationChanged += OrientationSensor_OrientationChanged;

                // Update orientation of buttons with the current orientation
                UpdateButtonOrientation();
            }

            _displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;
            //_systemMediaControls.PropertyChanged += SystemMediaControls_PropertyChanged;
            _systemNavigationManager.BackRequested += SystemNavigationManager_BackRequested;
        }

        private void SystemNavigationManager_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (_singleControlMode)
            {
                // Exit single control mode
                SetSingleControl(null);

                e.Handled = true;
            }
        }


        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors
        /// </summary>
        private void UnregisterEventHandlers()
        {
            if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
            {
                HardwareButtons.CameraPressed -= HardwareButtons_CameraPressed;
            }

            if (_orientationSensor != null)
            {
                _orientationSensor.OrientationChanged -= OrientationSensor_OrientationChanged;
            }

            _displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;
            //_systemMediaControls.PropertyChanged -= SystemMediaControls_PropertyChanged;
            _systemNavigationManager.BackRequested -= SystemNavigationManager_BackRequested;
        }

        /// <summary>
        /// Attempts to find and return a device mounted on the panel specified, and on failure to find one it will return the first device listed
        /// </summary>
        /// <param name="desiredPanel">The desired panel on which the returned device should be mounted, if available</param>
        /// <returns></returns>
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        /// <summary>
        /// Applies the given orientation to a photo stream and saves it as a StorageFile
        /// </summary>
        /// <param name="stream">The photo stream</param>
        /// <param name="file">The StorageFile in which the photo stream will be saved</param>
        /// <param name="photoOrientation">The orientation metadata to apply to the photo</param>
        /// <returns></returns>
        private static async Task ReencodeAndSavePhotoAsync(IRandomAccessStream stream, StorageFile file, PhotoOrientation photoOrientation)
        {
            using (var inputStream = stream)
            {
                var decoder = await BitmapDecoder.CreateAsync(inputStream);

                using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                    var properties = new BitmapPropertySet { { "System.Photo.Orientation", new BitmapTypedValue(photoOrientation, PropertyType.UInt16) } };

                    await encoder.BitmapProperties.SetPropertiesAsync(properties);
                    await encoder.FlushAsync();
                }
            }
        }

        /// <summary>
        /// Calculates the size and location of the rectangle that contains the preview stream within the preview control, when the scaling mode is Uniform
        /// </summary>
        /// <param name="previewResolution">The resolution at which the preview is running</param>
        /// <param name="previewControl">The control that is displaying the preview using Uniform as the scaling mode</param>
        /// <param name="displayOrientation">The orientation of the display, to account for device rotation and changing of the CaptureElement display ratio compared to the camera stream</param>
        /// <returns></returns>
        public static Rect GetPreviewStreamRectInControl(VideoEncodingProperties previewResolution, CaptureElement previewControl, DisplayOrientations displayOrientation)
        {
            var result = new Rect();

            // In case this function is called before everything is initialized correctly, return an empty result
            if (previewControl == null || previewControl.ActualHeight < 1 || previewControl.ActualWidth < 1 ||
                previewResolution == null || previewResolution.Height == 0 || previewResolution.Width == 0)
            {
                return result;
            }

            var streamWidth = previewResolution.Width;
            var streamHeight = previewResolution.Height;

            // For portrait orientations, the width and height need to be swapped
            if (displayOrientation == DisplayOrientations.Portrait || displayOrientation == DisplayOrientations.PortraitFlipped)
            {
                streamWidth = previewResolution.Height;
                streamHeight = previewResolution.Width;
            }

            // Start by assuming the preview display area in the control spans the entire width and height both (this is corrected in the next if for the necessary dimension)
            result.Width = previewControl.ActualWidth;
            result.Height = previewControl.ActualHeight;

            // If UI is "wider" than preview, letterboxing will be on the sides
            if ((previewControl.ActualWidth / previewControl.ActualHeight > streamWidth / (double)streamHeight))
            {
                var scale = previewControl.ActualHeight / streamHeight;
                var scaledWidth = streamWidth * scale;

                result.X = (previewControl.ActualWidth - scaledWidth) / 2.0;
                result.Width = scaledWidth;
            }
            else // Preview stream is "wider" than UI, so letterboxing will be on the top+bottom
            {
                var scale = previewControl.ActualWidth / streamWidth;
                var scaledHeight = streamHeight * scale;

                result.Y = (previewControl.ActualHeight - scaledHeight) / 2.0;
                result.Height = scaledHeight;
            }

            return result;
        }

        #endregion Helper functions


        #region Rotation helpers

        /// <summary>
        /// Calculates the current camera orientation from the device orientation by taking into account whether the camera is external or facing the user
        /// </summary>
        /// <returns>The camera orientation in space, with an inverted rotation in the case the camera is mounted on the device and is facing the user</returns>
        private SimpleOrientation GetCameraOrientation()
        {
            //if (_externalCamera)
            {
                // Cameras that are not attached to the device do not rotate along with it, so apply no rotation
                return SimpleOrientation.NotRotated;
            }

            var result = _deviceOrientation;

            // Account for the fact that, on portrait-first devices, the camera sensor is mounted at a 90 degree offset to the native orientation
            if (_displayInformation.NativeOrientation == DisplayOrientations.Portrait)
            {
                switch (result)
                {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        result = SimpleOrientation.NotRotated;
                        break;
                    case SimpleOrientation.Rotated180DegreesCounterclockwise:
                        result = SimpleOrientation.Rotated90DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        result = SimpleOrientation.Rotated180DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.NotRotated:
                        result = SimpleOrientation.Rotated270DegreesCounterclockwise;
                        break;
                }
            }

            // If the preview is being mirrored for a front-facing camera, then the rotation should be inverted
            if (_mirroringPreview)
            {
                // This only affects the 90 and 270 degree cases, because rotating 0 and 180 degrees is the same clockwise and counter-clockwise
                switch (result)
                {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        return SimpleOrientation.Rotated270DegreesCounterclockwise;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        return SimpleOrientation.Rotated90DegreesCounterclockwise;
                }
            }

            return result;
        }

        /// <summary>
        /// Converts the given orientation of the device in space to the corresponding rotation in degrees
        /// </summary>
        /// <param name="orientation">The orientation of the device in space</param>
        /// <returns>An orientation in degrees</returns>
        private static int ConvertDeviceOrientationToDegrees(SimpleOrientation orientation)
        {
            switch (orientation)
            {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    return 90;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    return 180;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    return 270;
                case SimpleOrientation.NotRotated:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Converts the given orientation of the app on the screen to the corresponding rotation in degrees
        /// </summary>
        /// <param name="orientation">The orientation of the app on the screen</param>
        /// <returns>An orientation in degrees</returns>
        private static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:
                    return 90;
                case DisplayOrientations.LandscapeFlipped:
                    return 180;
                case DisplayOrientations.PortraitFlipped:
                    return 270;
                case DisplayOrientations.Landscape:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Converts the given orientation of the device in space to the metadata that can be added to captured photos
        /// </summary>
        /// <param name="orientation">The orientation of the device in space</param>
        /// <returns></returns>
        private static PhotoOrientation ConvertOrientationToPhotoOrientation(SimpleOrientation orientation)
        {
            switch (orientation)
            {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    return PhotoOrientation.Rotate90;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    return PhotoOrientation.Rotate180;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    return PhotoOrientation.Rotate270;
                case SimpleOrientation.NotRotated:
                default:
                    return PhotoOrientation.Normal;
            }
        }

        /// <summary>
        /// Uses the current device orientation in space and page orientation on the screen to calculate the rotation
        /// transformation to apply to the controls
        /// </summary>
        private void UpdateButtonOrientation()
        {
            int device = ConvertDeviceOrientationToDegrees(_deviceOrientation);
            int display = ConvertDisplayOrientationToDegrees(_displayOrientation);

            if (_displayInformation.NativeOrientation == DisplayOrientations.Portrait)
            {
                device -= 90;
            }

            // Combine both rotations and make sure that 0 <= result < 360
            var angle = (360 + display + device) % 360;

            // Rotate the buttons in the UI to match the rotation of the device
            var transform = new RotateTransform { Angle = angle };

            // The RenderTransform is safe to use (i.e. it won't cause layout issues) in this case, because these buttons have a 1:1 aspect ratio
            //PhotoButton.RenderTransform = transform;
            //VideoButton.RenderTransform = transform;
        }

        #endregion Rotation helpers


        #region Manual controls setup

        // If activeButton = null, then exit single control mode.
        private void SetSingleControl(object activeButton)
        {
            _singleControlMode = (activeButton != null);

            // If in single control mode, hide all manual control buttons (except for the active button).
            // if not in single control mode, then show all the buttons which are supported.
            foreach (var button in ScenarioControlStackPanel.Children.OfType<Button>())
            {
                if (button != activeButton)
                {
                    // The Tag property of each button stores whether that button is supported.
                    // The value is set in the Update___ControlCapabilities method of each control.
                    button.Visibility = _singleControlMode ? Visibility.Collapsed : (Visibility)button.Tag;
                }
            }

            // Show the container control for manual configuration only when in single control mode
            ManualControlsGrid.Visibility = _singleControlMode ? Visibility.Visible : Visibility.Collapsed;

            // Show the Back button only when in single control mode
            _systemNavigationManager.AppViewBackButtonVisibility = _singleControlMode ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;

        }

        private void ManualControlButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle single control mode
            SetSingleControl(_singleControlMode ? null : sender);
        }

        /// <summary>
        /// Reflect the capabilities of the device in the UI, and set the initial state for each control
        /// </summary>
        private void UpdateManualControlCapabilities()
        {
            // Prevent the setup from triggering API calls
            _settingUpUi = true;

            // The implementation of these methods is in the partial classes named MainPage.Control.xaml.cs, where "Control" is the name of the control
            UpdateFlashControlCapabilities();
            UpdateZoomControlCapabilities();
            UpdateFocusControlCapabilities();
            UpdateWbControlCapabilities();
            UpdateIsoControlCapabilities();
            UpdateExposureControlCapabilities();
            UpdateEvControlCapabilities();

            _settingUpUi = false;
            SwitchBtn.IsEnabled = true;
            PhotoButton.IsEnabled = true;
            VideoButton.IsEnabled = true;
            FlashBtn.IsEnabled = true;
            SettingBtn.IsEnabled = true;

        }

        #endregion

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("FlashBtnOff");

                        FlashOnIcon.Visibility = Visibility.Collapsed;
                        FlashOffIcon.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                ;
            }
            SwitchBtn.IsEnabled = false;
            PhotoButton.IsEnabled = false;
            VideoButton.IsEnabled = false;
            FlashBtn.IsEnabled = false;
            SettingBtn.IsEnabled = false;
            await CleanupMediaCaptureAsync();
            //Debug.WriteLine("CleanupMediaCaptureAsync");
            if (_groupSelectionIndex > 0)
                _groupSelectionIndex = 0;
            else
                _groupSelectionIndex = 1;
            await InitializeCameraAsync(_groupSelectionIndex);

            if (Splitter.IsPaneOpen)
                Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
        }

        /// <summary>
        /// Unregisters FrameArrived event handlers, stops and disposes frame readers
        /// and disposes the MediaCapture object.
        /// </summary>
        public async Task CleanupMediaCaptureAsync()
        {
            if (_previewer.MediaCapture != null)
            {
                using (var mediaCapture = _previewer.MediaCapture)
                {
                    //_previewer.MediaCapture = null;

                    foreach (var reader in _sourceReaders)
                    {
                        if (reader != null)
                        {
                            reader.FrameArrived -= FrameReader_FrameArrived;
                            await reader.StopAsync();
                            reader.Dispose();
                        }
                    }
                    _sourceReaders.Clear();
                }
            }
        }

        /// <summary>
        /// Handles a frame arrived event and renders the frame to the screen.
        /// </summary>
        private void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            // TryAcquireLatestFrame will return the latest frame that has not yet been acquired.
            // This can return null if there is no such frame, or if the reader is not in the
            // "Started" state. The latter can occur if a FrameArrived event was in flight
            // when the reader was stopped.
            using (var frame = sender.TryAcquireLatestFrame())
            {
                if (frame != null)
                {
                    var renderer = _frameRenderers[frame.SourceKind];
                    renderer.ProcessFrame(frame);
                }
            }
        }

        private void SettingBtn_Click(object sender, RoutedEventArgs e)
        {
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
            Windows.Graphics.Display.DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
        }

        private async void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPreviewing)
            {

                var selectedItem = (sender as ComboBox).SelectedItem as ComboBoxItem;
                var encodingProperties = (selectedItem.Tag as StreamResolution).EncodingProperties;

                _backPicIndex = selectedItem.Content.ToString();

                await _previewer.MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.Photo, encodingProperties);

            }
        }


        private async void VideoFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPreviewing)
            {

                var selectedItem = (sender as ComboBox).SelectedItem as ComboBoxItem;
                var encodingProperties = (selectedItem.Tag as StreamResolution).EncodingProperties;

                _backVideoIndex = selectedItem.Content.ToString();

                await _previewer.MediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoRecord, encodingProperties);

            }
        }

        /// <summary>
        /// Populates the given combo box with all possible combinations of the given stream type settings returned by the camera driver
        /// </summary>
        /// <param name="streamType"></param>
        /// <param name="comboBox"></param>
        private void PopulateComboBox(MediaStreamType streamType, ComboBox comboBox, bool showFrameRate = true)
        {
            // Query all preview properties of the device 
            IEnumerable<StreamResolution> allStreamProperties = _previewer.MediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(streamType).Select(x => new StreamResolution(x));
            // Order them by resolution then frame rate
            allStreamProperties = allStreamProperties.OrderByDescending(x => x.Height * x.Width).ThenByDescending(x => x.FrameRate);

            // Populate the combo box with the entries
            foreach (var property in allStreamProperties)
            {
                ComboBoxItem comboBoxItem = new ComboBoxItem();

                if (property.GetFriendlyName(showFrameRate).Contains("NV12"))
                {
                    //filter string
                    String[] _resoulution = property.GetFriendlyName(showFrameRate).Split(' ');
                    comboBoxItem.Content = _resoulution[0];
                    //Debug.WriteLine($"WM_  {property.GetFriendlyName(showFrameRate)}");
                    comboBoxItem.Tag = property;
                    comboBox.Items.Add(comboBoxItem);
                }
            }
        }

        /// <summary>
        /// On some devices there may not be seperate streams for preview/photo/video. In this case, changing any of them
        /// will change all of them since they are the same stream
        /// </summary>
        public void CheckIfStreamsAreIdentical()
        {
            if (_previewer.MediaCapture.MediaCaptureSettings.VideoDeviceCharacteristic == VideoDeviceCharacteristic.AllStreamsIdentical ||
                _previewer.MediaCapture.MediaCaptureSettings.VideoDeviceCharacteristic == VideoDeviceCharacteristic.PreviewPhotoStreamsIdentical)
            {
                //rootPage.NotifyUser("Warning: Preview and photo streams for this device are identical, changing one will affect the other", NotifyType.ErrorMessage);
                FormatComboBox.Visibility = Visibility.Collapsed;
                PhotoText.Visibility = Visibility.Collapsed;
            }
            else
            {
                //rootPage.NotifyUser("Warning: Preview and photo streams for this device are identical, changing one will affect the other", NotifyType.ErrorMessage);
                FormatComboBox.Visibility = Visibility.Visible;
                PhotoText.Visibility = Visibility.Visible;
            }
        }

        private async void FlashBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("FlashBtn");

                    if (FlashOnIcon.Visibility == Visibility.Visible)
                        FlashOnIcon.Visibility = Visibility.Collapsed;
                    else
                        FlashOnIcon.Visibility = Visibility.Visible;
                    if (FlashOffIcon.Visibility == Visibility.Collapsed)
                        FlashOffIcon.Visibility = Visibility.Visible;
                    else
                        FlashOffIcon.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                ;
            }
        }

        private async void Splitter_Loading(FrameworkElement sender, object args)
        {
            try
            {
                if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("FlashBtnOff");
                }
            }
            catch
            {
                ;
            }
        }

        private async void PicFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder1 = KnownFolders.PicturesLibrary;
            await Windows.System.Launcher.LaunchFolderAsync(folder1);
        }
    }

    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return !(bool)value;
        }
    }

    public class RoundingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Math.Round((double)value, 2);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}