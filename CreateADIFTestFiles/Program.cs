using System;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics.CodeAnalysis;

namespace CreateADIFTestFiles
{
    /**
     * <summary>
     *   This class instantiates the application's user interface Form.
     * </summary>
     */
#pragma warning disable IDE0079 // Remove unnecessary suppression
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This is the Windows UI")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
    internal static class Program
    {
        /**
         * <summary>
         *   The main entry point for the application.
         * </summary>
         */
        [STAThread]
        static void Main()
        {
            // A mutex is used to prevent more than one instance of the program running.

            Mutex mutex = new Mutex(
                true,
                $"adif.org.uk.{Application.ProductName}",
                out bool created);

            if (created)
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new CreateFiles());
                }
                finally
                {
                    if (created)
                    {
                        // The mutex must only be released if it is owned (held) by the thread.
                        mutex.ReleaseMutex();
                    }
                }
            }
            else
            {
                _ = MessageBox.Show($"Another instance of {Application.ProductName} is already running", $"{Application.ProductName} already running");
            }
        }
    }
}
