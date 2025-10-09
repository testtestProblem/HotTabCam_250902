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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.ExtendedExecution.Foreground;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CameraManualControls
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        //static public Task MainPage.TakePhotoAsync();
        public App()
        {
            Debug.WriteLine("App init");
            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;

            this.Suspending += OnSuspending;  // App going to background → save state
            this.Resuming += OnResuming;    // App coming back from Suspended (not terminated) 

            this.EnteredBackground += OnEnteredBackground;
            this.LeavingBackground += OnLeavingBackground;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            Debug.WriteLine("OnLaunched abcd");/*
            var newSession = new ExtendedExecutionForegroundSession();
            newSession.Reason = ExtendedExecutionForegroundReason.Unconstrained;
            newSession.Description = "Long Running Processing";
            newSession.Revoked += SessionRevoked;
            ExtendedExecutionForegroundResult result = await newSession.RequestExtensionAsync();
            switch (result)
            {
                case ExtendedExecutionForegroundResult.Allowed:
                    Debug.WriteLine("Allowed Foreground Session");
                    break;

                default:
                case ExtendedExecutionForegroundResult.Denied:
                    Debug.WriteLine("Denied Foreground Session");
                    break;
            }*/

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }
            // Ensure the current window is active
            Window.Current.Activate();
        }

        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            //var deferral = e.SuspendingOperation.GetDeferral();
            Debug.WriteLine("OnSuspending abcd" );
            try
            {
            }
            finally
            {
            }
        }

        private async void OnResuming(object sender, object e)
        {
            Debug.WriteLine("OnResuming abcd");
            //MainPage test= new MainPage();

            //await test.CleanupMediaCaptureAsync();
            //await test.SetupUiAsync();
            //await test.InitializeCameraAsync(MainPage._groupSelectionIndex);
        }

        private void OnEnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            Debug.WriteLine("Entered Background");


        }

        private void OnLeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            Debug.WriteLine("Leaving Background");
        }

        private void SessionRevoked(object sender, ExtendedExecutionForegroundRevokedEventArgs args)
        {
            Debug.WriteLine("Session Revoked");
            throw new NotImplementedException();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {

            e.Handled = true;

        }
    }
}
