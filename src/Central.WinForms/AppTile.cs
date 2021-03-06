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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Windows.Forms;
using JetBrains.Annotations;
using NanoByte.Common;
using NanoByte.Common.Tasks;
using ZeroInstall.Central.WinForms.Properties;
using ZeroInstall.Commands.CliCommands;
using ZeroInstall.DesktopIntegration;
using ZeroInstall.Store;
using ZeroInstall.Store.Icons;
using ZeroInstall.Store.Model;
using Icon = ZeroInstall.Store.Model.Icon;
using SharedResources = ZeroInstall.Central.Properties.Resources;

namespace ZeroInstall.Central.WinForms
{
    /// <summary>
    /// Represents an application as a tile with buttons for launching, managing, etc..
    /// </summary>
    public sealed partial class AppTile : UserControl, IAppTile
    {
        #region Variables
        // Static resource preload
        private static readonly string _runButtonText = SharedResources.Run;
        private static readonly Bitmap _addImage = Resources.AppAdd, _removeImage = Resources.AppRemove, _integrateImage = Resources.AppIntegrate, _modifyImage = Resources.AppModify;
        private static readonly string _addText = SharedResources.MyAppsAdd, _removeText = SharedResources.MyAppsRemove, _integrateText = SharedResources.Integrate, _modifyIntegrationText = SharedResources.ModifyIntegration;
        private static readonly string _runCommandText = SharedResources.RunCommand, _runVersionText = SharedResources.RunVersion, _updateText = SharedResources.Update;

        /// <summary>Apply operations machine-wide instead of just for the current user.</summary>
        private readonly bool _machineWide;

        private static readonly ITaskHandler _handler = new SilentTaskHandler();

        /// <summary>The icon cache used to retrieve icons specified in <see cref="Feed"/>; can be <c>null</c>.</summary>
        [CanBeNull]
        private readonly IIconCache _iconCache;
        #endregion

        #region Properties
        /// <inheritdoc/>
        public FeedUri InterfaceUri { get; }

        /// <inheritdoc/>
        public string AppName => labelName.Text;

        private AppStatus _status;

        /// <inheritdoc/>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AppStatus Status
        {
            get { return _status; }
            set
            {
                #region Sanity checks
                if (value < AppStatus.Candidate || value > AppStatus.Integrated) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(AppStatus));
                if (InvokeRequired) throw new InvalidOperationException("Property set from a non UI thread.");
                #endregion

                _status = value;

                UpdateButtons();
            }
        }

        private Feed _feed;

        /// <inheritdoc/>
        public Feed Feed
        {
            get { return _feed; }
            set
            {
                #region Sanity checks
                if (InvokeRequired) throw new InvalidOperationException("Method called from a non UI thread.");
                #endregion

                _feed = value;
                if (value == null)
                {
                    buttonRunCommand.Visible = false;
                    return;
                }
                else buttonRunCommand.Visible = true;

                // Get application summary from feed
                labelSummary.Text = value.Summaries.GetBestLanguage(CultureInfo.CurrentUICulture);

                if (_iconCache != null)
                {
                    // Load application icon in background
                    var icon = value.GetIcon(Icon.MimeTypePng);
                    if (icon != null) iconDownloadWorker.RunWorkerAsync(icon.Href);
                    else pictureBoxIcon.Image = Resources.AppIcon; // Fall back to default icon
                }
                else pictureBoxIcon.Image = Resources.AppIcon; // Fall back to default icon
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new application tile.
        /// </summary>
        /// <param name="interfaceUri">The interface URI of the application this tile represents.</param>
        /// <param name="appName">The name of the application this tile represents.</param>
        /// <param name="status">Describes whether the application is listed in the <see cref="AppList"/> and if so whether it is integrated.</param>
        /// <param name="iconCache">The icon cache used to retrieve icons specified in <see cref="Feed"/>; can be <c>null</c>.</param>
        /// <param name="machineWide">Apply operations machine-wide instead of just for the current user.</param>
        public AppTile([NotNull] FeedUri interfaceUri, [NotNull] string appName, AppStatus status, [CanBeNull] IIconCache iconCache = null, bool machineWide = false)
        {
            #region Sanity checks
            if (interfaceUri == null) throw new ArgumentNullException(nameof(interfaceUri));
            if (appName == null) throw new ArgumentNullException(nameof(appName));
            #endregion

            _machineWide = machineWide;
            _iconCache = iconCache;

            InitializeComponent();
            buttonRun.Text = buttonRun2.Text = _runButtonText;
            buttonRunCommand.Text = _runCommandText;
            buttonRunVersion.Text = _runVersionText;
            buttonUpdate.Text = _updateText;

            buttonAdd.Image = _addImage;
            buttonAdd.AccessibleName = _addText;
            toolTip.SetToolTip(buttonAdd, _addText);
            buttonRemove.Image = _removeImage;
            buttonRemove.Text = _removeText;
            buttonIntegrate.Image = _integrateImage;

            InterfaceUri = interfaceUri;
            labelName.Text = appName;
            labelSummary.Text = "";
            Status = status;


            CreateHandle();
        }
        #endregion

        //--------------------//

        #region Feed processing
        private void iconDownloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Download and load icon in background
            var uri = (Uri)e.Argument;
            try
            {
                Debug.Assert(_iconCache != null);
                string path = _iconCache.GetIcon(uri, _handler);
                using (var stream = File.OpenRead(path))
                    e.Result = Image.FromStream(stream);
            }
                #region Error handling
            catch (OperationCanceledException)
            {}
            catch (WebException ex)
            {
                Log.Warn(ex);
            }
            catch (IOException ex)
            {
                Log.Warn($"Failed to store icon from {uri}");
                Log.Warn(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warn($"Failed to store icon from {uri}");
                Log.Warn(ex);
            }
            catch (ArgumentException ex)
            {
                Log.Warn($"Failed to parse icon from {uri}");
                Log.Warn(ex);
            }
            #endregion
        }

        private void iconDownloadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Display icon in UI thread
            var image = e.Result as Image;
            if (image != null) pictureBoxIcon.Image = image;
        }
        #endregion

        #region Buttons
        /// <summary>
        /// Updates the visibility and icons of buttons based on the <see cref="Status"/>.
        /// </summary>
        private void UpdateButtons()
        {
            buttonAdd.Enabled = buttonAdd.Visible = (Status == AppStatus.Candidate);

            string integrateText = (Status == AppStatus.Integrated) ? _modifyIntegrationText : _integrateText;
            buttonIntegrate.AccessibleName = integrateText;
            toolTip.SetToolTip(buttonIntegrate, integrateText);
            buttonIntegrate.Image = (Status == AppStatus.Integrated) ? _modifyImage : _integrateImage;
            buttonIntegrate.Visible = (Status >= AppStatus.Added);
            buttonIntegrate.Enabled = true;
            buttonIntegrate2.Text = integrateText;
        }

        private void LinkClicked(object sender, EventArgs e)
        {
            if (InterfaceUri.IsFake) return;
            try
            {
                ProcessUtils.Start(InterfaceUri.OriginalString);
            }
                #region Error handling
            catch (OperationCanceledException)
            {}
            catch (IOException ex)
            {
                Msg.Inform(this, ex.Message, MsgSeverity.Error);
            }
            #endregion
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (InterfaceUri.IsFake) return;
            if (Feed != null && Feed.NeedsTerminal) new SelectCommandDialog(new FeedTarget(InterfaceUri, Feed)).Show(this);
            else Program.RunCommand(Run.Name, "--no-wait", InterfaceUri.ToStringRfc());
        }

        private void buttonRunVersion_Click(object sender, EventArgs e)
        {
            if (InterfaceUri.IsFake) return;
            Program.RunCommand(Run.Name, "--no-wait", "--customize", InterfaceUri.ToStringRfc());
        }

        private void buttonRunCommand_Click(object sender, EventArgs e)
        {
            if (InterfaceUri.IsFake || Feed == null) return;
            new SelectCommandDialog(new FeedTarget(InterfaceUri, Feed)).Show(this);
        }

        private void buttonUpdate_Click(object sender, EventArgs e)
        {
            if (InterfaceUri.IsFake) return;
            Program.RunCommand(Commands.CliCommands.Update.Name, InterfaceUri.ToStringRfc());
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            if (InterfaceUri.IsFake) return;

            // Disable button while operation is running
            buttonAdd.Enabled = false;

            Program.RunCommand(UpdateButtons, _machineWide, AddApp.Name, InterfaceUri.ToStringRfc());
        }

        private void buttonIntegrate_Click(object sender, EventArgs e)
        {
            if (InterfaceUri.IsFake) return;

            // Disable buttons while operation is running
            buttonIntegrate.Enabled = false;

            Program.RunCommand(UpdateButtons, _machineWide, IntegrateApp.Name, InterfaceUri.ToStringRfc());
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            if (InterfaceUri.IsFake) return;

            if (Status == AppStatus.Integrated)
                if (!Msg.YesNo(this, string.Format(SharedResources.AppRemoveConfirm, AppName), MsgSeverity.Warn)) return;

            // Disable buttons while operation is running
            buttonIntegrate.Enabled = false;

            Program.RunCommand(UpdateButtons, _machineWide, RemoveApp.Name, InterfaceUri.ToStringRfc());
        }
        #endregion
    }
}
