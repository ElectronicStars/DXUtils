using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Capture.Hook;
using Capture.Interface;
using System.Threading.Tasks;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.InteropServices;
using System.Threading;
using SharpDX;
using System.Diagnostics;

namespace Capture
{
    public class EntryPoint : EasyHook.IEntryPoint
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(System.Windows.Forms.Keys vKey);
        System.Media.SoundPlayer player = new System.Media.SoundPlayer(Properties.Resources.camera_click);
        System.Media.SoundPlayer player_beep = new System.Media.SoundPlayer(Properties.Resources.beep);

        List<IDXHook> _directXHooks = new List<IDXHook>();
        IDXHook _directXHook = null;
        private CaptureInterface _interface;
        private System.Threading.ManualResetEvent _runWait;
        ClientCaptureInterfaceEventProxy _clientEventProxy = new ClientCaptureInterfaceEventProxy();
        IpcServerChannel _clientServerChannel = null;

        public EntryPoint(
            EasyHook.RemoteHooking.IContext context,
            String channelName,
            CaptureConfig config)
        {
            // Get reference to IPC to host application
            // Note: any methods called or events triggered against _interface will execute in the host process.
            _interface = EasyHook.RemoteHooking.IpcConnectClient<CaptureInterface>(channelName);
            // We try to ping immediately, if it fails then injection fails
            _interface.Ping();

            #region Allow client event handlers (bi-directional IPC)
            
            // Attempt to create a IpcServerChannel so that any event handlers on the client will function correctly
            System.Collections.IDictionary properties = new System.Collections.Hashtable();
            properties["name"] = channelName;
            properties["portName"] = channelName + Guid.NewGuid().ToString("N"); // random portName so no conflict with existing channels of channelName

            System.Runtime.Remoting.Channels.BinaryServerFormatterSinkProvider binaryProv = new System.Runtime.Remoting.Channels.BinaryServerFormatterSinkProvider();
            binaryProv.TypeFilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Full;

            System.Runtime.Remoting.Channels.Ipc.IpcServerChannel _clientServerChannel = new System.Runtime.Remoting.Channels.Ipc.IpcServerChannel(properties, binaryProv);
            System.Runtime.Remoting.Channels.ChannelServices.RegisterChannel(_clientServerChannel, false);
            
            #endregion
        }

        public void Run(
            EasyHook.RemoteHooking.IContext context,
            String channelName,
            CaptureConfig config)
        {
            // When not using GAC there can be issues with remoting assemblies resolving correctly
            // this is a workaround that ensures that the current assembly is correctly associated
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += (sender, args) =>
            {
                return this.GetType().Assembly.FullName == args.Name ? this.GetType().Assembly : null;
            };

            // NOTE: This is running in the target process
            _interface.Message(MessageType.Information, "Injected into process Id:{0}.", EasyHook.RemoteHooking.GetCurrentProcessId());

            _runWait = new System.Threading.ManualResetEvent(false);
            _runWait.Reset();


            var loaded_modules = Process.GetCurrentProcess().Modules;
            foreach (var module in loaded_modules)
            {
                _interface.Message(MessageType.Debug,module.ToString());
            }
            try
            {
                // Initialise the Hook
                if (!InitialiseDirectXHook(config))
                {
                    return;
                }
                _interface.Disconnected += _clientEventProxy.DisconnectedProxyHandler;

                // Important Note:
                // accessing the _interface from within a _clientEventProxy event handler must always 
                // be done on a different thread otherwise it will cause a deadlock

                _clientEventProxy.Disconnected += () =>
                {
                    // We can now signal the exit of the Run method
                    _runWait.Set();
                };

                // We start a thread here to periodically check if the host is still running
                // If the host process stops then we will automatically uninstall the hooks
                StartCheckHostIsAliveThread();

                // Wait until signaled for exit either when a Disconnect message from the host 
                // or if the the check is alive has failed to Ping the host.
                _runWait.WaitOne();

                // we need to tell the check host thread to exit (if it hasn't already)
                StopCheckHostIsAliveThread();

                // Dispose of the DXHook so any installed hooks are removed correctly
                DisposeDirectXHook();
            }
            catch (Exception e)
            {
                _interface.Message(MessageType.Error, "An unexpected error occured: {0}", e.ToString());
            }
            finally
            {
                try
                {
                    _interface.Message(MessageType.Information, "Disconnecting from process {0}", EasyHook.RemoteHooking.GetCurrentProcessId());
                }
                catch
                {
                }

                // Remove the client server channel (that allows client event handlers)
                System.Runtime.Remoting.Channels.ChannelServices.UnregisterChannel(_clientServerChannel);

                // Always sleep long enough for any remaining messages to complete sending
                System.Threading.Thread.Sleep(100);
            }
        }

        private void DisposeDirectXHook()
        {
            if (_directXHooks != null)
            {
                try
                {
                    _interface.Message(MessageType.Debug, "Disposing of hooks...");
                }
                catch (System.Runtime.Remoting.RemotingException) { } // Ignore channel remoting errors

                // Dispose of the hooks so they are removed
                foreach (var dxHook in _directXHooks)
                    dxHook.Dispose();

                _directXHooks.Clear();
            }
        }

        private bool InitialiseDirectXHook(CaptureConfig config)
        {
            Direct3DVersion version = config.Direct3DVersion;

            //List<Direct3DVersion> loadedVersions = new List<Direct3DVersion>();

            bool isX64Process = EasyHook.RemoteHooking.IsX64Process(EasyHook.RemoteHooking.GetCurrentProcessId());
            _interface.Message(MessageType.Information, "Remote process is a {0}-bit process.", isX64Process ? "64" : "32");

            try
            {

                ///BB I want just Direct3DVersion.Direct3D9
                ///So I scrapped all the autodetect and other dx version stuff
               // loadedVersions.Add(version);

                System.Threading.Thread key = new Thread(new ParameterizedThreadStart(keyState));
                key.IsBackground = true;
                key.Start();


                System.Threading.Thread mods = new Thread(new ParameterizedThreadStart(monitorModules));
                mods.IsBackground = true;
                mods.Start();

                KeyboardHook._interface = _interface;

                System.Threading.Thread lowlevelkeyboard = new Thread(new ParameterizedThreadStart(KeyboardHook.Consumer));
                lowlevelkeyboard.IsBackground = true;
                lowlevelkeyboard.Start();

                KeyboardHook.SetHook();


                _directXHook = new DXHookD3D9(_interface);
                _directXHook.Config = config;
                _directXHook.Hook();
         
                _directXHooks.Add(_directXHook);
                return true;

            }
            catch (Exception e)
            {
                // Notify the host/server application about this error
                _interface.Message(MessageType.Error, "Error in InitialiseHook: {0}", e.ToString());
                return false;
            }
        }

        private void monitorModules(object device1)
        {
            while(true){
                try
                {

                    //_interface.Message(MessageType.Information, "Getting Modules");
                    var process = Process.GetCurrentProcess();
                    //_interface.Message(MessageType.Information, "Sending");
                    var modules = new HashSet<ModuleInfo>();
                    foreach (ProcessModule mod in process.Modules)
                    {
                        var m = new ModuleInfo();
                        m.name = mod.ModuleName;
                        m.path = mod.FileName;
                        modules.Add(m);
                    }
                    //_interface.Message(MessageType.Information, "Sending 2");
                    _interface.SendModuleInfoToClient(process.ProcessName,modules);
                System.Threading.Thread.Sleep(800);
                }
                catch (Exception e)
                {
                   // _interface.Message(MessageType.Error, "Error getting modules" + e.ToString(), e.ToString());

                }
            }
        }



        private void keyState(object device1)
        {
            while (true)
            {
                var minimized = false;
                try
                    {
                    Process currentProcess = Process.GetCurrentProcess();
                    IntPtr handle = currentProcess.MainWindowHandle;

                    if (!NativeMethods.IsWindowInForeground(handle) || NativeMethods.IsIconic(handle))
                    {
                        minimized = true;
                    }

                    if (!minimized)
                    {
                        //disable
                        //if (GetAsyncKeyState(_directXHook.Config.screenshotHotkey) != 0 )
                        //{
                        //    var s = _directXHook.Interface.GetScreenshot(new System.Drawing.Rectangle(0, 0, 0, 0), new TimeSpan(0, 0, 2), null, Capture.Interface.ImageFormat.Png);
                            
                        //    System.Threading.Tasks.Task.Factory.StartNew(() =>
                        //    {
                        //        player.Play();
                        //        _interface.SendScreenshotToClient(s);
                        //    });

                        //    System.Threading.Thread.Sleep(800);
                        //}

                        if (GetAsyncKeyState(_directXHook.Config.reportCheatHotkey) != 0)
                        {
                            System.Threading.Tasks.Task.Factory.StartNew(() =>
                            {
                                player_beep.Play();
                                _interface.SendCheatReportToClient();

                            });
                            System.Threading.Thread.Sleep(800);
                        }
                    }

                    System.Threading.Thread.Sleep(200);
                }
                catch (Exception e)
                {
                    //_interface.Message(MessageType.Error, "Error in Key Listener {0}", e.ToString());

                }
            }
        }

        #region Check Host Is Alive

        Task _checkAlive;
        long _stopCheckAlive = 0;
        
        /// <summary>
        /// Begin a background thread to check periodically that the host process is still accessible on its IPC channel
        /// </summary>
        private void StartCheckHostIsAliveThread()
        {
            _checkAlive = new Task(() =>
            {
                try
                {
                    while (System.Threading.Interlocked.Read(ref _stopCheckAlive) == 0)
                    {
                        System.Threading.Thread.Sleep(1000);

                        // .NET Remoting exceptions will throw RemotingException
                        _interface.Ping();
                    }
                }
                catch // We will assume that any exception means that the hooks need to be removed. 
                {
                    // Signal the Run method so that it can exit
                    _runWait.Set();
                }
            });

            _checkAlive.Start();
        }

        /// <summary>
        /// Tell the _checkAlive thread that it can exit if it hasn't already
        /// </summary>
        private void StopCheckHostIsAliveThread()
        {
            System.Threading.Interlocked.Increment(ref _stopCheckAlive);
        }

        #endregion
    }
}
