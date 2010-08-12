﻿/*
 * Copyright 2010 Bastian Eicher
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

namespace ZeroInstall.Store.Feed
{
    /// <summary>
    /// Callback methods to be used if the the user needs to be asked any questions while retrieving feeds.
    /// </summary>
    /// <remarks>
    /// All callbacks are called from the original thread.
    /// Thread-safety messures are needed only if the process was started on a background thread and is intended to update a UI.
    /// </remarks>
    public interface IFeedHandler
    {
        /// <summary>
        /// Called to ask the user whether he wishes to trust a new GPG key.
        /// </summary>
        /// <param name="information">Comprehensive information about the new key, to help the user make an informed decision.</param>
        /// <returns><see langword="true"/> if the user accepted the new key; <see langword="false"/> if he rejected it.</returns>
        bool AcceptNewKey(string information);
    }
}
