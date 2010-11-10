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

using System.Collections.Generic;

namespace Common.Undo
{
    /// <summary>
    /// An undo command that adds an element to a collection.
    /// </summary>
    /// <typeparam name="T">The type of elements the collection contains.</typeparam>
    public sealed class AddToCollection<T> : CollectionCommand<T>
    {
        #region Constructor
        /// <summary>
        /// Creates a new add to collection command.
        /// </summary>
        /// <param name="collection">The collection to be modified.</param>
        /// <param name="element">The element to be added to <paramref name="collection"/>.</param>
        public AddToCollection(ICollection<T> collection, T element) : base(collection, element)
        {}
        #endregion

        //--------------------//

        #region Execute
        /// <summary>
        /// Adds the <see cref="CollectionCommand{T}.Element"/> to the <see cref="CollectionCommand{T}.Collection"/>.
        /// </summary>
        protected override void OnExecute()
        {
            Collection.Add(Element);
        }
        #endregion

        #region Undo
        /// <summary>
        /// Removes the <see cref="CollectionCommand{T}.Element"/> from the <see cref="CollectionCommand{T}.Collection"/>.
        /// </summary>
        protected override void OnUndo()
        {
            Collection.Remove(Element);
        }
        #endregion
    }
}
