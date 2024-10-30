using System;

namespace AdifXsltLib
{
    /**
     * <summary>
     *   This implements a text log file in the Documents folder by providing a wrapper for the
     *   <see cref="AdifReleaseLib.Logger"/> class.
     * </summary>
     * 
     * <remarks>
     *   It was originally an independent logging class with its own unique Log method signatures.&#160;
     *   To avoid re-writing the Log calls from the <see cref="AdifXsltLib.AdifXslt"/> class, it was expedient
     *   to map the Log method calls in this class onto the Log methods in <see cref="AdifReleaseLib.Logger"/>.
     * </remarks>
     */
    internal static class Logger
    {
        internal static void Log(string message, params string[] args)
        {
            Log(null, false, message, args);
        }

        internal static void Log(Exception exc, string message, params string[] args)
        {
            Log(exc, false, message, args);
        }

        internal static void Log(Exception exc, bool stack, string message, params string[] args)
        {
            if (message == null)
            {
                message = string.Empty;
            }
            else
            {
                if (args != null)
                {
                    message = string.Format(message, args);
                }
            }

            if (exc != null)
            {
                message += $"\r\n{exc.GetType().Name}";

                if (exc.Message != null)
                {
                    message += $"\r\n{exc.Message}";
                }
            }

            if (stack)
            {
                message += $"\r\n{System.Environment.StackTrace}";
            }

            AdifReleaseLib.Logger.Log(message);
        }
    }
}
