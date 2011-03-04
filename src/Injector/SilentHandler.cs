﻿/*
 * Copyright 2010-2011 Bastian Eicher
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

using Common;

namespace ZeroInstall.Injector
{
    /// <summary>
    /// Ignores progress reports and silently answer all questions with "No".
    /// </summary>
    public class SilentHandler : SilentIOHandler, IHandler
    {
        /// <inheritdoc />
        public virtual void Output(string title, string information)
        {}

        /// <inheritdoc />
        public bool AcceptNewKey(string information)
        {
            return false;
        }

        /// <inheritdoc/>
        public void CloseAsync()
        {
            // No GUI, so nothing to do
        }
    }
}
