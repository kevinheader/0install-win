﻿namespace ZeroInstall.Model
{
    /// <summary>
    /// A retrieval method is a way of getting an <see cref="Implementation"/>.
    /// </summary>
    public abstract class RetrievalMethod : ISimplifyable
    {
        /// <summary>
        /// Sets missing default values, flattens the inheritance structure, etc.
        /// </summary>
        /// <remarks>This should be called to prepare an interface for launch.
        /// It should not be called if you plan on serializing the <see cref="Interface"/> again since it will may some of its structure.</remarks>
        public abstract void Simplify();
    }
}
