﻿/*
 * Copyright 2010-2014 Bastian Eicher
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
using System.IO;
using NanoByte.Common.Utils;

namespace ZeroInstall.Publish.EntryPoints
{
    /// <summary>
    /// A script written in Ruby.
    /// </summary>
    public sealed class RubyScript : InterpretedScript
    {
        /// <inheritdoc/>
        internal override bool Analyze(FileInfo file)
        {
            #region Sanity checks
            if (file == null) throw new ArgumentNullException("file");
            #endregion

            if (!base.Analyze(file)) return false;
            return
                StringUtils.EqualsIgnoreCase(file.Extension, ".rb") ||
                HasShebang(file, "ruby");
        }

        /// <inheritdoc/>
        protected override string InterpreterInterface { get { return "http://0install.de/feeds/Ruby.xml"; } }
    }
}