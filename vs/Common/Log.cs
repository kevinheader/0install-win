/*
 * Copyright 2006-2010 Bastian Eicher
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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Common.Utils;

namespace Common
{
    /// <summary>
    /// Writes log messages to the <see cref="Console"/> and maintains copies in memory and in a plain text file.
    /// </summary>
    public static class Log
    {
        #region Events
        /// <summary>
        /// Occurs whenever a new entry has been added to the log.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        public static event SimpleEventHandler NewEntry;
        #endregion

        #region Variables
        private static readonly StringBuilder _sessionContent = new StringBuilder();
        private static readonly StreamWriter _fileWriter;
        #endregion

        #region Properties
        /// <summary>
        /// All data logged in this session so far as plain text.
        /// </summary>
        public static string Content
        {
            get
            {
                lock (_sessionContent) { return _sessionContent.ToString(); }
            }
        }
        #endregion

        #region Constructor
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "The static constructor is used to add an identification header to the log file")]
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Any kind of problems writing the log file should be ignored")]
        static Log()
        {
            var assmebly = Assembly.GetEntryAssembly();
            if (assmebly == null) return; // Don't log to disk when assembly is unknown
            var assemblyInfo = assmebly.GetName();

            // Try to determine assembly title, fall back to assembly name on failure
            var assemblyTitleAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
            string assemblyTitle = (assemblyTitleAttributes.Length > 0 ? ((AssemblyTitleAttribute)assemblyTitleAttributes[0]).Title : assemblyInfo.Name);
            
            string filePath;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    // ToDo: Write to sensible log directory

                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                default:
                    filePath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(assemblyInfo.Name) + " Log.txt");
                    break;
            }

            // Try to open the file for writing but give up right away if there are any problems
            FileStream file;
            try { file = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite); }
            catch { return; }

            // Check if the log file has exceeded 1MB
            if (file.Length > 1024 * 1024)
            {
                // In this case we just kill it and create a new one
                file.Close();
                file = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            }

            // When writing to a new file use UTF-8 with BOM, otherwise keep existing encoding
            _fileWriter = (file.Length == 0 ? new StreamWriter(file, Encoding.UTF8) : new StreamWriter(file));
            _fileWriter.AutoFlush = true;

            // Go to end of file
            _fileWriter.BaseStream.Seek(0, SeekOrigin.End);

            // Add session identification block to the file
            _fileWriter.WriteLine("");
            _fileWriter.WriteLine("/// " + assemblyTitle + " v" + assemblyInfo.Version);
            _fileWriter.WriteLine("/// Log session started at: " + DateTime.Now.ToString(CultureInfo.InvariantCulture));
            _fileWriter.WriteLine("");
        }
        #endregion

        //--------------------//

        #region Add entry
        private enum LogSeverity { Echo, Info, Warn, Error }

        /// <summary>
        /// Adds a new entry to the log.
        /// </summary>
        /// <param name="severity">The type/severity of the entry.</param>
        /// <param name="message">The actual message text of the entry.</param>
        private static void AddEntry(LogSeverity severity, string message)
        {
            #region Sanity checks
            if (message == null) throw new ArgumentNullException("message");
            #endregion

            // Create uniform line-breaks and indention
            string[] lines = StringUtils.SplitMultilineText(message.Trim());
            message = StringUtils.Concatenate(lines, "\r\n\t");

            // Thread-safety: Only one log message is handled at a time
            lock (_sessionContent)
            {
                try
                {
                    // Write the colored message to the console
                    switch (severity)
                    {
                        case LogSeverity.Warn: Console.ForegroundColor = ConsoleColor.DarkYellow; break;
                        case LogSeverity.Error: Console.ForegroundColor = ConsoleColor.Red; break;
                    }
                }
                catch (IOException) {}
                Console.WriteLine(message);
                try { Console.ResetColor(); }
                catch (IOException) {}

                // Prepend severity and current time to message
                string formatString;
                switch (severity)
                {
                    case LogSeverity.Info: formatString = "[{0:T}] {1};"; break;
                    case LogSeverity.Warn: formatString = "[{0:T}] WARN: {1}"; break;
                    case LogSeverity.Error: formatString = "[{0:T}] ERROR: {1}"; break;
                    default: formatString = "{1}"; break;
                }
                message = string.Format(CultureInfo.InvariantCulture, formatString, DateTime.Now, message);

                // Store message in RAM
                _sessionContent.AppendLine(message);

                // If possible write message to the log file
                if (_fileWriter != null) _fileWriter.WriteLine(message);

                // Raise event
                if (NewEntry != null) NewEntry();
            }
        }
        #endregion

        #region Access
        /// <summary>
        /// Writes non-critical information to the log.
        /// </summary>
        //[LuaGlobal(Name = "Info", Description = "Writes non-critical information to the log.")]
        public static void Info(string message)
        {
            AddEntry(LogSeverity.Info, message);
        }

        /// <summary>
        /// Writes non-critical warnings to the log.
        /// </summary>
        //[LuaGlobal(Name = "Warn", Description = "Writes non-critical warnings to the log.")]
        public static void Warn(string message)
        {
            AddEntry(LogSeverity.Warn, message);
        }

        /// <summary>
        /// Writes critical errors to the log.
        /// </summary>
        //[LuaGlobal(Name = "Error", Description = "Writes critical errors to the log.")]
        public static void Error(string message)
        {
            AddEntry(LogSeverity.Error, message);
        }

        /// <summary>
        /// Echos to the log without a timestamp or severity.
        /// </summary>
        public static void Echo(string message)
        {
            AddEntry(LogSeverity.Echo, message);
        }
        #endregion
    }
}