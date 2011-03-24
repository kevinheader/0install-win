﻿/*
 * Copyright 2006-2010 Simon E. Silva Lauinger, Bastian Eicher
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;
using Common.Tasks;
using Common.Utils;

namespace Common.Controls
{
    /// <summary>
    /// A progress bar that automatically tracks the progress of an <see cref="ITask"/>.
    /// </summary>
    public class TrackingProgressBar : ProgressBar
    {
        #region Variables
        /// <summary>A barrier that blocks threads until the window handle is ready.</summary>
        private readonly EventWaitHandle _handleReady = new EventWaitHandle(false, EventResetMode.ManualReset);
        #endregion

        #region Properties
        private ITask _task;
        /// <summary>
        /// The <see cref="ITask"/> to track.
        /// </summary>
        /// <remarks>
        ///   <para>Setting this property will hook up event handlers to monitor the task.</para>
        ///   <para>Remember to set it back to <see langword="null"/> or to call <see cref="IDisposable.Dispose"/> when done, to remove the event handlers again.</para>
        ///   <para>The value must not be set from a background thread.</para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the value is set from a thread other than the UI thread.</exception>
        [DefaultValue(null), Description("The IProgress object to track.")]
        public ITask Task
        {
            set
            {
                if (InvokeRequired) throw new InvalidOperationException("Must set this from UI thread");

                // Remove all delegates from old _task
                HookOut();

                _task = value;

                // Only start tracking if the handle is available
                if (IsHandleCreated) HookIn();
            }
            get { return _task; }
        }

        /// <summary>
        /// Starts tracking the progress of <see cref="_task"/>.
        /// </summary>
        /// <remarks>This may only be called after <see cref="Control.HandleCreated"/> has been raised.</remarks>
        private void HookIn()
        {
            if (_task == null) return;

            // Get the initial values
            StateChanged(_task);
            ProgressChanged(_task);
                        
            _task.StateChanged += StateChanged;
            _task.ProgressChanged += ProgressChanged;
        }

        /// <summary>
        /// Stops tracking the progress of <see cref="_task"/>.
        /// </summary>
        private void HookOut()
        {
            if (_task == null) return;

            _task.StateChanged -= StateChanged;
            _task.ProgressChanged -= ProgressChanged;
        }

        /// <summary>
        /// Determines the handle of the <see cref="Form"/> containing this control.
        /// </summary>
        /// <returns>The handle of the parent <see cref="Form"/> or <see cref="IntPtr.Zero"/> if there is no parent.</returns>
        private IntPtr ParentHandle
        {
            get
            {
                Form parent = FindForm();
                return (parent == null ? IntPtr.Zero : parent.Handle);
            }
        }

        /// <summary>
        /// Show the progress in the Windows taskbar.
        /// </summary>
        /// <remarks>Use only once per window. Only works on Windows 7 or newer.</remarks>
        [Description("Show the progress in the Windows taskbar."), DefaultValue(false)]
        public bool UseTaskbar { set; get; }
        #endregion

        #region Constructor
        public TrackingProgressBar()
        {
            // Track when events can be passed to the WinForms code
            HandleCreated += delegate
            {
                _handleReady.Set();
                HookIn();
            };
            HandleDestroyed += delegate { _handleReady.Reset(); };
        }
        #endregion

        //--------------------//

        #region Event callbacks
        /// <summary>
        /// Changes the <see cref="ProgressBarStyle"/> and the taskbar based on the <see cref="TaskState"/> of <see cref="_task"/>.
        /// </summary>
        /// <param name="sender">Object that called this method.</param>
        /// <remarks>Taskbar only changes for Windows 7 or newer.</remarks>
        private void StateChanged(ITask sender)
        {
            // Copy value so it can be safely accessed from another thread
            TaskState state = sender.State;

            // Handle events coming from a non-UI thread, don't block caller
            _handleReady.WaitOne();
            BeginInvoke(new SimpleEventHandler(delegate
            {
                IntPtr formHandle = ParentHandle;
                switch (state)
                {
                    case TaskState.Ready:
                        if (UseTaskbar && formHandle != IntPtr.Zero) WindowsUtils.SetProgressState(TaskbarProgressBarState.Paused, formHandle);
                        Style = ProgressBarStyle.Continuous;
                        break;

                    case TaskState.Header:
                        Style = ProgressBarStyle.Marquee;
                        if (UseTaskbar && formHandle != IntPtr.Zero) WindowsUtils.SetProgressState(TaskbarProgressBarState.Indeterminate, formHandle);
                        break;

                    case TaskState.Data:
                        // Only track progress if the final size is known
                        if (sender.BytesTotal != -1)
                        {
                            _task.ProgressChanged += ProgressChanged;
                            Style = ProgressBarStyle.Continuous;
                            if (UseTaskbar && formHandle != IntPtr.Zero) WindowsUtils.SetProgressState(TaskbarProgressBarState.Normal, formHandle);
                        }
                        break;

                    case TaskState.IOError:
                    case TaskState.WebError:
                        if (UseTaskbar && formHandle != IntPtr.Zero) WindowsUtils.SetProgressState(TaskbarProgressBarState.Error, formHandle);
                        Style = ProgressBarStyle.Continuous;
                        break;

                    case TaskState.Complete:
                        if (UseTaskbar && formHandle != IntPtr.Zero) WindowsUtils.SetProgressState(TaskbarProgressBarState.NoProgress, formHandle);

                        // When the status is complete the bar should always be full
                        Style = ProgressBarStyle.Continuous;
                        Value = 100;
                        break;
                }
            }));
        }

        /// <summary>
        /// Changes the <see cref="ProgressBar.Value"/> and the taskbar depending on the number of already processed bytes.
        /// </summary>
        /// <param name="sender">Object that called this method.</param>
        /// <remarks>Taskbar only changes for Windows 7 or newer.</remarks>
        private void ProgressChanged(ITask sender)
        {
            // Clamp the progress to values between 0 and 1
            double progress = sender.Progress;
            if (progress < 0) progress = 0;
            else if (progress > 1) progress = 1;

            // Copy value so it can be safely accessed from another thread
            int currentValue = (int)(progress * 100);

            // When the status is complete the bar should always be full
            if (sender.State == TaskState.Complete) currentValue = 100;

            // Handle events coming from a non-UI thread, don't block caller
            _handleReady.WaitOne();
            BeginInvoke(new SimpleEventHandler(delegate
            {
                Value = currentValue;
                IntPtr formHandle = ParentHandle;
                if (UseTaskbar && formHandle != IntPtr.Zero) WindowsUtils.SetProgressValue(currentValue, 100, formHandle);
            }));
        }
        #endregion

        //--------------------//

        #region Dispose
        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Remove update hooks
                Task = null;
            }
            try { base.Dispose(disposing); }
            finally { _handleReady.Close(); }
        }
        #endregion
    }
}
