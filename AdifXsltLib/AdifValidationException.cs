using System;

namespace AdifXsltLib
{
    /**
     * <summary>
     *   Represents an error where an ADIF field value does not conform to its data type.
     * </summary>
     */
    public class AdifValidationException : AdifException
    {
        /**
         * <value>
         *   The name of the field that caused the error.
         * </value>
         */
        public string Name { get; }

        /**
         * <value>
         *   The data type of the field that caused the error.
         * </value>
         */
        public string DataType { get; }

        /**
         * <value>
         *   The value of the field that caused the error.
         * </value>
         */
        public string Value { get; }

        /**
         * <summary>
         *   Represents an error where an ADIF field value does not conform to its data type.
         * </summary>
         */
        public AdifValidationException()
            : base()
        {
        }

        /**
         * <summary>
         *   Represents an error where an ADIF field value does not conform to its data type.
         * </summary>
         * 
         * <param name="message">The error message that explains the reason for the exception, or an empty string ("").</param>
         */
        public AdifValidationException(string message)
            : base(message)
        {
        }

        /**
         * <summary>
         *   Represents an error where an ADIF field value does not conform to its data type.
         * </summary>
         * 
         * <param name="message">The error message that explains the reason for the exception, or an empty string ("").</param>
         * <param name="innerException">The <see cref="Exception"/> instance that caused the current exception.</param>
         */
        public AdifValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /**
         * <summary>
         *   Represents an error where an ADIF field value does not conform to its data type.
         * </summary>
         * 
         * <param name="message">The error message that explains the reason for the exception, or an empty string ("").</param>
         * <param name="innerException">The <see cref="Exception"/> instance that caused the current exception.</param>
         * <param name="name">The name of the field that caused the error.</param>
         * <param name="dataType">The data type of the field that caused the error.</param>
         * <param name="value">The value of the field that caused the error.</param>
         */
        public AdifValidationException(string message, Exception innerException, string name, string dataType, string value)
            : base(message, innerException)
        {
            Name = name;
            DataType = dataType;
            Value = value;
        }
    }
}
