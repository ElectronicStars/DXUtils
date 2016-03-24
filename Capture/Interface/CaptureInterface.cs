﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Diagnostics;

namespace Capture.Interface
{
    [Serializable]
    public delegate void RecordingStartedEvent(CaptureConfig config);
    [Serializable]
    public delegate void RecordingStoppedEvent();
    [Serializable]
    public delegate void MessageReceivedEvent(MessageReceivedEventArgs message);
    [Serializable]
    public delegate void ScreenshotReceivedEvent(ScreenshotReceivedEventArgs response);
    [Serializable]
    public delegate void ModuleInfoReceivedEvent(ModuleInfoReceivedEventArgs response);
    [Serializable]
    public delegate void CheatReportReceivedEvent(CheatReportReceivedEventArgs response);
    [Serializable]
    public delegate void DisconnectedEvent();
    [Serializable]
    public delegate void ScreenshotRequestedEvent(ScreenshotRequest request);
    [Serializable]
    public delegate void DisplayTextEvent(DisplayTextEventArgs args);

    [Serializable]
    public class CaptureInterface : MarshalByRefObject
    {
        /// <summary>
        /// The client process Id
        /// </summary>
        public int ProcessId { get; set; }

        #region Events

        #region Server-side Events
        
        /// <summary>
        /// Server event for sending debug and error information from the client to server
        /// </summary>
        public event MessageReceivedEvent RemoteMessage;
        
        /// <summary>
        /// Server event for receiving screenshot image data
        /// </summary>
        public event ScreenshotReceivedEvent ScreenshotReceived;
        /// <summary>
        /// Server event for receiving moduledata
        /// </summary>
        public event ModuleInfoReceivedEvent ModuleInfoReceived;
        /// <summary>
        /// Server event for receiving cheat report
        /// </summary>
        public event CheatReportReceivedEvent CheatReportReceived;

        #endregion

        #region Client-side Events
        
        /// <summary>
        /// Client event used to communicate to the client that it is time to start recording
        /// </summary>
        public event RecordingStartedEvent RecordingStarted;

        /// <summary>
        /// Client event used to communicate to the client that it is time to stop recording
        /// </summary>
        public event RecordingStoppedEvent RecordingStopped;

        /// <summary>
        /// Client event used to communicate to the client that it is time to create a screenshot
        /// </summary>
        public event ScreenshotRequestedEvent ScreenshotRequested;

        /// <summary>
        /// Client event used to notify the hook to exit
        /// </summary>
        public event DisconnectedEvent Disconnected;

        /// <summary>
        /// Client event used to display a piece of text in-game
        /// </summary>
        public event DisplayTextEvent DisplayText;
        
        #endregion

        #endregion

        public bool IsRecording { get; set; }

        #region Public Methods

        #region Video Capture

        /// <summary>
        /// If not <see cref="IsRecording"/> will invoke the <see cref="RecordingStarted"/> event, starting a new recording. 
        /// </summary>
        /// <param name="config">The configuration for the recording</param>
        /// <remarks>Handlers in the server and remote process will be be invoked.</remarks>
        public void StartRecording(CaptureConfig config)
        {
            if (IsRecording)
                return;
            SafeInvokeRecordingStarted(config);
            IsRecording = true;
        }

        /// <summary>
        /// If <see cref="IsRecording"/>, will invoke the <see cref="RecordingStopped"/> event, finalising any existing recording.
        /// </summary>
        /// <remarks>Handlers in the server and remote process will be be invoked.</remarks>
        public void StopRecording()
        {
            if (!IsRecording)
                return;
            SafeInvokeRecordingStopped();
            IsRecording = false;
        }

        #endregion

        #region Still image Capture

        object _lock = new object();
        Guid? _requestId = null;
        Action<Screenshot> _completeScreenshot = null;
        ManualResetEvent _wait = new ManualResetEvent(false);

        /// <summary>
        /// Get a fullscreen screenshot with the default timeout of 2 seconds
        /// </summary>
        public Screenshot GetScreenshot()
        {
            return GetScreenshot(Rectangle.Empty, new TimeSpan(0, 0, 2), null, ImageFormat.Bitmap);
        }

        /// <summary>
        /// Get a screenshot of the specified region
        /// </summary>
        /// <param name="region">the region to capture (x=0,y=0 is top left corner)</param>
        /// <param name="timeout">maximum time to wait for the screenshot</param>
        public Screenshot GetScreenshot(Rectangle region, TimeSpan timeout, Size? resize, ImageFormat format)
        {
            lock (_lock)
            {
                Screenshot result = null;
                _requestId = Guid.NewGuid();
                _wait.Reset();
                //this.Message(MessageType.Debug, "Entered lock");
                SafeInvokeScreenshotRequested(new ScreenshotRequest(_requestId.Value, region)
                {
                    Format = format,
                    Resize = resize,
                });
                //this.Message(MessageType.Debug, "GetScreenshot 2) Screenshot Requested");
                _completeScreenshot = (sc) =>
                {
                    //this.Message(MessageType.Debug, "GetScreenshot 4) Continuing!");

                    try
                    {
                        Interlocked.Exchange(ref result, sc);
                    }
                    catch
                    {
                    }
                    //This is called by the target process, once the screenshot is taken so this thread can continue
                    _wait.Set();
                        
                };
                //Stops current thread until it gets signal from from _completeScreenshot
                //this.Message(MessageType.Debug, "GetScreenshot 3) We wait");

                _wait.WaitOne(timeout);
                _completeScreenshot = null;

                return result;
            }
        }


        //For convenience
        public IAsyncResult BeginGetScreenshot_FullPng(Rectangle region, TimeSpan timeout, AsyncCallback callback)
        {
            return BeginGetScreenshot(region, timeout, callback, null, ImageFormat.Png);
        }

        public IAsyncResult BeginGetScreenshot(Rectangle region, TimeSpan timeout, AsyncCallback callback = null, Size? resize = null, ImageFormat format = ImageFormat.Bitmap)
        {
            Func<Rectangle, TimeSpan, Size?, ImageFormat, Screenshot> getScreenshot = GetScreenshot;
            
            return getScreenshot.BeginInvoke(region, timeout, resize, format, callback, getScreenshot);
        }

        ProcessModuleCollection GetModules(){
            this.Message(MessageType.Debug, Process.GetCurrentProcess().ProcessName);
            return Process.GetCurrentProcess().Modules;
        }

        public IAsyncResult BeginGetModules(AsyncCallback callback)
        {
            Func<ProcessModuleCollection> getModules = GetModules;
            return getModules.BeginInvoke(callback,getModules) ;

        }

        public ProcessModuleCollection EndGetModules(IAsyncResult result)
        {
            Func<ProcessModuleCollection> getModules = result.AsyncState as Func<ProcessModuleCollection>;
            if (getModules != null)
            {
                return getModules.EndInvoke(result);
            }
            else return null;
        }


        public Screenshot EndGetScreenshot(IAsyncResult result)
        {
            Func<Rectangle, TimeSpan, Size?, ImageFormat, Screenshot> getScreenshot = result.AsyncState as Func<Rectangle, TimeSpan, Size?, ImageFormat, Screenshot>;
            if (getScreenshot != null)
            {
                return getScreenshot.EndInvoke(result);
            }
            else
                return null;
        }

        public void SendScreenshotResponse(Screenshot screenshot)
        {
            if (_requestId != null && screenshot != null && screenshot.RequestId == _requestId.Value)
            {
                if (_completeScreenshot != null)
                {
                    _completeScreenshot(screenshot);
                }
            }
        }

        #endregion

        /// <summary>
        /// Tell the client process to disconnect
        /// </summary>
        public void Disconnect()
        {
            SafeInvokeDisconnected();
        }

        /// <summary>
        /// Send a message to all handlers of <see cref="CaptureInterface.RemoteMessage"/>.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void Message(MessageType messageType, string format, params object[] args)
        {
            Message(messageType, String.Format(format, args));
        }

        public void Message(MessageType messageType, string message)
        {
            SafeInvokeMessageRecevied(new MessageReceivedEventArgs(messageType, message));
        }

        public void SendScreenshotToClient(Screenshot s)
        {

            SafeInvokeScreenshotReceived(new ScreenshotReceivedEventArgs(0, s));
        }

        public void SendModulesToClient(Modules m)
        {
           
        }


        public void SendCheatReportToClient()
        {
             SafeInvokeCheatReportReceived(new CheatReportReceivedEventArgs());
        }


        public void SendModuleInfoToClient(string name, HashSet<ModuleInfo> modules)
        {

            SafeInvokeModuleInfoReceived(new ModuleInfoReceivedEventArgs(name, modules));
        }

        /// <summary>
        /// Display text in-game for the default duration of 5 seconds
        /// </summary>
        /// <param name="text"></param>
        public void DisplayInGameText(string text)
        {
            DisplayInGameText(text, new TimeSpan(0, 0, 5));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="duration"></param>
        public void DisplayInGameText(string text, TimeSpan duration)
        {
            if (duration.TotalMilliseconds <= 0)
                throw new ArgumentException("Duration must be larger than 0", "duration");
            SafeInvokeDisplayText(new DisplayTextEventArgs(text, duration));
        }

        #endregion

        #region Private: Invoke message handlers

        private void SafeInvokeRecordingStarted(CaptureConfig config)
        {
            if (RecordingStarted == null)
                return;         //No Listeners

            RecordingStartedEvent listener = null;
            Delegate[] dels = RecordingStarted.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (RecordingStartedEvent)del;
                    listener.Invoke(config);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    RecordingStarted -= listener;
                }
            }
        }

        private void SafeInvokeRecordingStopped()
        {
            if (RecordingStopped == null)
                return;         //No Listeners

            RecordingStoppedEvent listener = null;
            Delegate[] dels = RecordingStopped.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (RecordingStoppedEvent)del;
                    listener.Invoke();
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    RecordingStopped -= listener;
                }
            }
        }

        private void SafeInvokeMessageRecevied(MessageReceivedEventArgs eventArgs)
        {
            if (RemoteMessage == null)
                return;         //No Listeners

            MessageReceivedEvent listener = null;
            Delegate[] dels = RemoteMessage.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (MessageReceivedEvent)del;
                    listener.Invoke(eventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    RemoteMessage -= listener;
                }
            }
        }

        private void SafeInvokeScreenshotRequested(ScreenshotRequest eventArgs)
        {
            if (ScreenshotRequested == null)
                return;         //No Listeners

            ScreenshotRequestedEvent listener = null;
            Delegate[] dels = ScreenshotRequested.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (ScreenshotRequestedEvent)del;
                    listener.Invoke(eventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    ScreenshotRequested -= listener;
                }
            }
        }

        private void SafeInvokeScreenshotReceived(ScreenshotReceivedEventArgs eventArgs)
        {
            if (ScreenshotReceived == null)
                return;         //No Listeners

            ScreenshotReceivedEvent listener = null;
            Delegate[] dels = ScreenshotReceived.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (ScreenshotReceivedEvent)del;
                    listener.Invoke(eventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    ScreenshotReceived -= listener;
                }
            }
        }


        protected void SafeInvokeModuleInfoReceived(ModuleInfoReceivedEventArgs eventArgs)
        {
            if (ModuleInfoReceived == null)
            {
                //this.Message(MessageType.Information, "nolisteners");
                return;         //No Listeners
            }


            ModuleInfoReceivedEvent listener = null;
            Delegate[] dels = ModuleInfoReceived.GetInvocationList();
            this.Message(MessageType.Information, "invoke");
            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (ModuleInfoReceivedEvent)del;
                    listener.Invoke(eventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    ModuleInfoReceived -= listener;
                }
            }
        }



        private void SafeInvokeCheatReportReceived(CheatReportReceivedEventArgs eventArgs)
        {
            if (CheatReportReceived == null)
                return;         //No Listeners

            CheatReportReceivedEvent listener = null;
            Delegate[] dels = CheatReportReceived.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (CheatReportReceivedEvent)del;
                    listener.Invoke(eventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    CheatReportReceived -= listener;
                }
            }
        }


        private void SafeInvokeDisconnected()
        {
            if (Disconnected == null)
                return;         //No Listeners

            DisconnectedEvent listener = null;
            Delegate[] dels = Disconnected.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (DisconnectedEvent)del;
                    listener.Invoke();
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    Disconnected -= listener;
                }
            }
        }

        private void SafeInvokeDisplayText(DisplayTextEventArgs displayTextEventArgs)
        {
            if (DisplayText == null)
                return;         //No Listeners

            DisplayTextEvent listener = null;
            Delegate[] dels = DisplayText.GetInvocationList();

            foreach (Delegate del in dels)
            {
                try
                {
                    listener = (DisplayTextEvent)del;
                    listener.Invoke(displayTextEventArgs);
                }
                catch (Exception)
                {
                    //Could not reach the destination, so remove it
                    //from the list
                    DisplayText -= listener;
                }
            }
        }

        #endregion

        /// <summary>
        /// Used 
        /// </summary>
        public void Ping()
        {
            
        }
    }


    /// <summary>
    /// Client event proxy for marshalling event handlers
    /// </summary>
    public class ClientCaptureInterfaceEventProxy : MarshalByRefObject
    {
        #region Event Declarations

        /// <summary>
        /// Client event used to communicate to the client that it is time to start recording
        /// </summary>
        public event RecordingStartedEvent RecordingStarted;

        /// <summary>
        /// Client event used to communicate to the client that it is time to stop recording
        /// </summary>
        public event RecordingStoppedEvent RecordingStopped;

        /// <summary>
        /// Client event used to communicate to the client that it is time to create a screenshot
        /// </summary>
        public event ScreenshotRequestedEvent ScreenshotRequested;

        /// <summary>
        /// Client event used to notify the hook to exit
        /// </summary>
        public event DisconnectedEvent Disconnected;

        /// <summary>
        /// Client event used to display in-game text
        /// </summary>
        public event DisplayTextEvent DisplayText;

        #endregion

        #region Lifetime Services

        public override object InitializeLifetimeService()
        {
            //Returning null holds the object alive
            //until it is explicitly destroyed
            return null;
        }

        #endregion

        public void RecordingStartedProxyHandler(CaptureConfig config)
        {
            if (RecordingStarted != null)
                RecordingStarted(config);
        }

        public void RecordingStoppedProxyHandler()
        {
            if (RecordingStopped != null)
                RecordingStopped();
        }


        public void DisconnectedProxyHandler()
        {
            if (Disconnected != null)
                Disconnected();
        }

        public void ScreenshotRequestedProxyHandler(ScreenshotRequest request)
        {
            if (ScreenshotRequested != null)
                ScreenshotRequested(request);
        }

        public void DisplayTextProxyHandler(DisplayTextEventArgs args)
        {
            if (DisplayText != null)
                DisplayText(args);
        }
    }
}
