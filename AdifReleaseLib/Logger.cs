using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace AdifReleaseLib
{
    /**
     * <summary>
     *   This implements a text log file in the Documents folder.
     * </summary>
     */
    public static class Logger
    {
        private const string
            contentsDateTimeFormat = "yyyy-MM-dd HH:mm:ss ",
            fileNameDateTimeFormat = "yyyyMMddHHmmss";

        private static TextWriter LogWriter = null;
        private static TextWriterTraceListener LogListener = null;
        private static string
            ProductName = string.Empty,
            ProductVersion = string.Empty;

        /**
         * <summary>
         *   Creates the log file &amp; stream and logs a title message to it.
         * </summary>
         */
        private static void Initialize()
        {
            try
            {
                string
                    logTitle,
                    logPath;

                StringBuilder logName = new StringBuilder(32);

                logTitle = ProductName;

                if (logTitle.Length > 0) { logTitle += ' '; }

                logTitle += "Log";

                if (ProductVersion.Length > 0) { logTitle += $" {ProductVersion}"; }

                {
                    // Limit the file name to safe characters.

                    char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

                    foreach (char c in ProductName)
                    {
                        if (!invalidFileNameChars.Contains<char>(c))
                        {
                            logName.Append(c);
                        }
                    }
                }

                if (logName.Length > 0) { _ = logName.Append(' '); }

                logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"{logName}Log {DateTime.UtcNow.ToString(fileNameDateTimeFormat)}.txt");

                LogWriter = new StreamWriter(logPath, true, Encoding.UTF8);
                LogListener = new TextWriterTraceListener(LogWriter);
                _ = System.Diagnostics.Trace.Listeners.Add(LogListener);
                Trace.AutoFlush = true;
                Trace.WriteLine($"{DateTime.UtcNow.ToString(contentsDateTimeFormat)}: {logTitle}\r\n");
            }
            catch { }
        }

        /**
         * <summary>
         *   Sets the product name and product version that will be included in the log file's first record and file name.
         * </summary>
         * 
         * <param name="productName">The product name to be included in the log file's first record and file name.</param>
         * <param name="productVersion">The product version to be included in the log file's first record.</param>
         */
        public static void StartLog(string productName, string productVersion)
        {
            ProductName = productName.Trim();
            if (ProductName == null) { ProductName = string.Empty; }

            ProductVersion = productVersion.Trim();
            if (ProductVersion == null) { ProductVersion = string.Empty; }
        }

        /**
         * <summary>
         *   Closes the current log file.
         * </summary>
         * 
         * <remarks>
         *   Flushes any outstanding contents to the log file and removes the listener.
         * </remarks>
         */
        public static void Close()
        {
            if (LogListener != null)
            {
                try
                {
                    Trace.Flush();
                    LogListener.Flush();
                    System.Diagnostics.Trace.Listeners.Remove(LogListener);
                    LogListener.Close();
                    LogListener = null;
                }
                catch { }
            }

            try
            {
                if (LogWriter != null)
                {
                    LogWriter.Close();
                    LogWriter = null;
                }
            }
            catch { }

            try
            {
                GC.Collect();  // Force a garbage collection to ensure the file is released.
            }
            catch { }
        }

        /**
         * <summary>
         *   Adds a message to the log file preceded by the current date and time.
         * </summary>
         * 
         * <param name="message"></param>
         */
        public static void Log(string message)
        {
            try
            {
                if (LogWriter == null)
                {
                    Initialize();
                }
                Trace.WriteLine(
                    string.IsNullOrEmpty(message) ?
                        "\r\n" :
                        $"{DateTime.UtcNow.ToString(contentsDateTimeFormat)}: {message}");
            }
            catch { }
        }

        /**
         * <summary>
         *   Adds details of an Exception to the log file preceded by the current date and time.
         *   If the Exception contains an InnerException, additionally logs the InnerException's message.
         * </summary>
         * 
         * <param name="exc">An <see cref="Exception"/> object.</param>
         */
        public static void Log(Exception exc)
        {
            try
            {
                Log($"Exception: {exc.GetType().Name}");
                if (!string.IsNullOrEmpty(exc.Message))
                {
                    Log($"Message: {exc.Message}");
                }
                if (exc.StackTrace != null)
                {
                    Log($"Stack Trace:\r\n{exc.StackTrace}");
                }
                if (exc.InnerException != null)
                {
                    Log($"Inner Exception: {exc.InnerException.Message}");
                }
            }
            catch { }
        }
    }
}
