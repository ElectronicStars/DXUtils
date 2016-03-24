    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Drawing;
    using System.IO;
    using System.Runtime.Remoting;
    using System.Security.Permissions;
    using System.Runtime.InteropServices;
using System.Diagnostics;


    namespace Capture.Interface
    {
        public class Modules : MarshalByRefObject, IDisposable
        {

            List<ProcessModule> _modules;
           
            public List<ProcessModule> ModulesCollection
            {
                get
                {
                    return _modules;
                }
            }

            private bool _disposed;

            public Modules(string name, ProcessModuleCollection modules)
            {
                //_modules = modules;
                _modules = new List<ProcessModule>();

                foreach (ProcessModule module in modules)
                {
                    _modules.Add(module);

                }
            }

            ~Modules()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposeManagedResources)
            {
                if (!_disposed)
                {
                    if (disposeManagedResources)
                    {
                        Disconnect();
                    }
                    _disposed = true;
                }
            }

            /// <summary>
            /// Disconnects the remoting channel(s) of this object and all nested objects.
            /// </summary>
            private void Disconnect()
            {
                RemotingServices.Disconnect(this);
            }

            [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.Infrastructure)]
            public override object InitializeLifetimeService()
            {
                // Returning null designates an infinite non-expiring lease.
                // We must therefore ensure that RemotingServices.Disconnect() is called when
                // it's no longer needed otherwise there will be a memory leak.
                return null;
            }
        }
    }
