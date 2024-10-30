using System;

namespace AdifXsltLib
{
    /**
    * <summary>
    *   This is a general purpose exception for any exceptions the program wishes to raise.
    * </summary>
    */
    public class AdifException : ApplicationException
    {
        /**
        * <summary>
        *   Represents errors that occur during application execution.
        * </summary>
        */
        public AdifException()
            : base("An exception occurred.")
        {
        }

        /**
        * <summary>
        *   Represents errors that occur during application execution.
        * </summary>
        * 
        * <param name="message">The error message that explains the reason for the exception, or an empty string ("").</param>
        */
        public AdifException(string message)
            : base(message)
        {
        }

        /**
        * <summary>
        *   Represents errors that occur during application execution.
        * </summary>
        *
        * <param name="message">The error message that explains the reason for the exception, or an empty string ("").</param>
        * <param name="innerException">The <see cref="Exception"/> instance that caused the current exception.</param>
        */
        public AdifException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
