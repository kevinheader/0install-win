﻿/*
 * Copyright 2010-2016 Bastian Eicher
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser Public License for more details.
 *
 * You should have received a copy of the GNU Lesser Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Serialization.Formatters;
using System.Security;
using System.Security.Principal;
using System.ServiceProcess;
using NanoByte.Common;
using NanoByte.Common.Storage;
using ZeroInstall.Store.Implementations;
using ZeroInstall.Store.Service.Properties;

namespace ZeroInstall.Store.Service
{
    /// <summary>
    /// Represents a Windows service.
    /// </summary>
    public partial class StoreService : ServiceBase
    {
        public StoreService()
        {
            InitializeComponent();
        }

        /// <summary>IPC channel for providing services to clients.</summary>
        private IChannelReceiver _serverChannel;

        /// <summary>IPC channel for launching callbacks in clients.</summary>
        private IChannelSender _clientChannel;

        /// <summary>The store to provide to clients as a service.</summary>
        private MarshalByRefObject _store;

        /// <summary>The IPC remoting reference for <see cref="_store"/>.</summary>
        private ObjRef _objRef;

        private const int IncorrectFunction = 1, AccessDenied = 5, InvalidHandle = 6, UnableToWriteToDevice = 29;

        protected override void OnStart(string[] args)
        {
            try
            {
                if (!EventLog.SourceExists(eventLog.Source))
                    EventLog.CreateEventSource(eventLog.Source, eventLog.Log);
                Log.Handler += LogHandler;
            }
                #region Sanity checks
            catch (SecurityException ex)
            {
                Log.Error(ex);
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex);
            }
            #endregion

            if (Locations.IsPortable)
            {
                Log.Error(Resources.NoPortableMode);
                ExitCode = IncorrectFunction;
                Stop();
                return;
            }

            try
            {
                _serverChannel = new IpcServerChannel(
                    new Hashtable
                    {
                        {"name", IpcStore.IpcPortName},
                        {"portName", IpcStore.IpcPortName},
                        {"secure", true}, {"impersonate", true} // Use identity of client in server threads
                    },
                    new BinaryServerFormatterSinkProvider {TypeFilterLevel = TypeFilterLevel.Full} // Allow deserialization of custom types
#if !__MonoCS__
                    , IpcStore.IpcAcl
#endif
                );
                _clientChannel = new IpcClientChannel(
                    new Hashtable
                    {
                        {"name", IpcStore.IpcPortName + ".Callback"}
                    },
                    new BinaryClientFormatterSinkProvider());

                ChannelServices.RegisterChannel(_serverChannel, ensureSecurity: false);
                ChannelServices.RegisterChannel(_clientChannel, ensureSecurity: false);
                _store = CreateStore();
                _objRef = RemotingServices.Marshal(_store, IpcStore.IpcObjectUri, typeof(IStore));

                // Prevent the service from expiring on Windows 10
                var lease = (ILease)RemotingServices.GetLifetimeService(_store);
                lease.Renew(TimeSpan.FromDays(365));
            }
                #region Error handling
            catch (IOException ex)
            {
                Log.Error("Failed to open cache directory:" + Environment.NewLine + ex);
                ExitCode = UnableToWriteToDevice;
                Stop();
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Error("Failed to open cache directory:" + Environment.NewLine + ex);
                ExitCode = AccessDenied;
                Stop();
            }
            catch (RemotingException ex)
            {
                Log.Error("Failed to open IPC connection:" + Environment.NewLine + ex);
                ExitCode = InvalidHandle;
                Stop();
            }
            catch (SecurityException ex)
            {
                Log.Error("Failed to open IPC connection:" + Environment.NewLine + ex);
                ExitCode = InvalidHandle;
                Stop();
            }
            #endregion
        }

        protected override void OnStop()
        {
            if (_objRef != null) RemotingServices.Unmarshal(_objRef);
            try
            {
                ChannelServices.UnregisterChannel(_clientChannel);
                ChannelServices.UnregisterChannel(_serverChannel);
            }
            catch (RemotingException)
            {}

            _serverChannel = null;
            _clientChannel = null;

            Log.Handler -= LogHandler;
        }

        /// <summary>
        /// Creates the store to provide to clients as a service.
        /// </summary>
        /// <exception cref="IOException">A cache directory could not be created or the underlying filesystem can not store file-changed times accurate to the second.</exception>
        /// <exception cref="UnauthorizedAccessException">Creating a cache directory is not permitted.</exception>
        private static MarshalByRefObject CreateStore()
        {
            var identity = WindowsIdentity.GetCurrent();
            Debug.Assert(identity != null);

            var paths = StoreConfig.GetImplementationDirs(serviceMode: true);
            var stores = paths.Select(path => new SecureStore(path, identity)).Cast<IStore>();
            return new CompositeStore(stores);
        }

        /// <summary>
        /// Writes <see cref="NanoByte.Common.Log"/> messages to the Windows Event Log.
        /// </summary>
        /// <param name="severity">The type/severity of the entry.</param>
        /// <param name="message">The message text of the entry.</param>
        private void LogHandler(LogSeverity severity, string message)
        {
            var entryType = GetEntryType(severity);
            if (!entryType.HasValue) return;

            const int maxMessageLength = 16000;
            if (message.Length > maxMessageLength)
                message = message.Substring(0, maxMessageLength) + Environment.NewLine + "[MESSAGE TRIMMED DUE TO LENGTH]";

            try
            {
                eventLog.WriteEntry(message, entryType.Value);
            }
            catch (Win32Exception)
            {}
            catch (InvalidOperationException)
            {}
        }

        private static EventLogEntryType? GetEntryType(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Info:
                    return EventLogEntryType.Information;
                case LogSeverity.Warn:
                    return EventLogEntryType.Warning;
                case LogSeverity.Error:
                    return EventLogEntryType.Error;
                default:
                    return null;
            }
        }
    }
}
