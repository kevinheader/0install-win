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
using NanoByte.Common;
using NanoByte.Common.Streams;
using ZeroInstall.Store;

namespace ZeroInstall.Bootstrap
{
    /// <summary>
    /// Represents configuration embedded into the exectuable itself.
    /// This is used to create customized bootstrappers that use Zero Install to run or integrate another application.
    /// </summary>
    public class EmbeddedConfig
    {
        /// <summary>
        /// The name of the target application to bootstrap.
        /// Only relevant if <see cref="AppMode"/> is not <see cref="BootstrapMode.None"/>.
        /// </summary>
        public string AppName { get; private set; }

        /// <summary>
        /// The feed URI of the target application to bootstrap.
        /// Only relevant if <see cref="AppMode"/> is not <see cref="BootstrapMode.None"/>.
        /// </summary>
        public FeedUri AppUri { get; private set; }

        /// <summary>
        /// The application bootstrapping mode to use.
        /// </summary>
        public BootstrapMode AppMode { get; private set; }

        /// <summary>
        /// Loads the embedded configuration.
        /// </summary>
        private EmbeddedConfig()
        {
            var lines = typeof(EmbeddedConfig).GetEmbeddedString("EmbeddedConfig.txt").SplitMultilineText();

            try
            {
                AppUri = new FeedUri(lines[0].TrimEnd());
                Log.Info("EmbeddedConfig: AppUri: " + AppUri);

                AppName = lines[1].TrimEnd();
                Log.Info("EmbeddedConfig: AppName: " + AppName);

                AppMode = GetAppMode(lines[2].TrimEnd());
                Log.Info("EmbeddedConfig: AppMode: " + AppMode);
            }
            catch (UriFormatException)
            {
                // No (valid) feed URI set
            }
        }

        private static BootstrapMode GetAppMode(string value)
        {
            switch (value)
            {
                case "run":
                    return BootstrapMode.Run;
                case "integrate":
                    return BootstrapMode.Integrate;
                default:
                    return BootstrapMode.None;
            }
        }

        /// <summary>
        /// The embedded config as a singleton.
        /// </summary>
        public static readonly EmbeddedConfig Instance = new EmbeddedConfig();
    }
}