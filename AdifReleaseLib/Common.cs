using System;
using System.Text;
using System.IO;
using System.Xml;
using System.Globalization;

namespace AdifReleaseLib
{
    /**
     * <summary>
     *   This class contains commonly used methods and properties.
     * </summary>
     */
    public class Common
    {
        /**
         * <value>
         *   <para>
         *      An <see cref="Encoding"/> object for Windows-1252.
         *   </para>
         *   <para>
         *     In .NET 8, Encoding.GetEncoding does not support Windows-1252 and instead it's necessary to call<br/>
         *     &#160;&#160;CodePagesEncodingProvider.Instance.GetEncoding(1252);<br/>
         *     To cater for this in the future, all code that requires Windows-1252 will use this field so that
         *     several code changes will not be required.
         *   
         *     Note: NETFRAMEWORK is defined in the project's Properties / Build / "Conditional&#160;Compilation&#160;symbols:"
         *     i.e. it is not something declared automatically in Visual Studio projects.
         *   </para>
         * </value>
         */
        public static readonly Encoding Windows1252Encoding =
#if NETFRAMEWORK
            Encoding.GetEncoding("Windows-1252");
#else
            CodePagesEncodingProvider.Instance.GetEncoding(1252);
#endif

        /**
         * <summary>
         *   Converts the leftmost three parts of a version <see cref="string"/> into a <see cref="decimal"/> with 8 digits per part.
         * </summary>
         * 
         * <param name="version">A <see cref="string"/> containing the version number.</param>
         * 
         * <returns>A <see cref="decimal"/> containing the leftmost three parts of the version.</returns>
         * 
         * <exception cref="ArgumentException"/>
         */
        public static decimal VersionToDecimal(string version)
        {
            // Storing 3 parts of the version in a decimal allows a maximum of 8 digits per part, so a total of 24 digits,
            // which is within the 27/28 digit limit of a decimal.

            const int maxDigits = 8;

            string[] parts = version.Split('.');

            if (parts[0].Length > maxDigits ||
                parts[1].Length > maxDigits ||
                parts[2].Length > maxDigits)
            {
                throw new ArgumentException(
                    $"One or more of the first three parts of the version string exceeds {maxDigits} digits: \"{string.Join(".", parts)}\"");
            }
            return decimal.Parse(
                parts[0].PadLeft(maxDigits, '0') +
                parts[1].PadLeft(maxDigits, '0') +
                parts[2].PadLeft(maxDigits, '0'));
        }

        /**
         * <summary>
         *   Returns a <see cref="NumberFormatInfo"/> object that does not include a "+" sign and uses a dot "." as a decimal point.
         * </summary>
         * 
         * <remarks>
         *   ADIF numbers can optionally include a decimal point that is always a dot "." but cannot include a plus "+" sign.
         * </remarks>
         * 
         * <value>A <see cref="NumberFormatInfo"/> object that does not include a "+" sign and uses a dot "." as a decimal point.</value>
         */
        public readonly static NumberFormatInfo AdifNumberFormatInfo;

        //public readonly static NumberStyles     AdifNumberStyles;

        /**
         * <summary>
         *   Initialises the class's static variables.
         * </summary>
         */
        static Common()
        {
            AdifNumberFormatInfo = (NumberFormatInfo)CultureInfo.GetCultureInfo("en-US").NumberFormat.Clone();
            AdifNumberFormatInfo.PositiveSign = string.Empty;
            //AdifNumberStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        }

        /**
         * <summary>
         *   This sets a file's Date Created, Date Modified and Date Last Updated to the current UTC date and time.
         * </summary>
         * 
         * <remarks>
         *   File.Delete does not delete files synchronously, so that typically when the code creates a
         *   new file, it is actually an old one that has been overwritten.&#160; To compensate for the confusing
         *   "Date created" date and time, set all the file's dates manually to the current date and time.
         * </remarks>
         * 
         * <param name="filePath">The file's path</param>
         */
        public static void SetFileTimesToNow(string filePath)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                DateTime utcNow = DateTime.UtcNow;

                fileInfo.CreationTimeUtc = utcNow;
                fileInfo.LastWriteTimeUtc = utcNow;
                fileInfo.LastAccessTimeUtc = utcNow;
            }
            catch (Exception eX)
            {
                // Changing the file times is desirable but not essential.
                //
                // Norton will cause an exception because it does not approve of changing file attributes.

                Logger.Log($"Exception setting file times on {filePath}");
                Logger.Log(eX);
            }
        }

        /**
         * <summary>
         *   Get the current UTC date and time with a whole number of seconds in XML Schema format.
         * </summary>
         * 
         * <returns>The current UTC date and time in XML Schema format.</returns>
         */
        public static string GetXmlDateTimeNow()
        {
            DateTime now = DateTime.UtcNow;

            return XmlConvert.ToString(
                new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second),
                XmlDateTimeSerializationMode.Utc);
        }

        /**
         * <summary>
         *   Converts a date from a <see cref="DateTime"/> to an XML Schema date format.
         * </summary>
         * 
         * <param name="date">The date to be converted.</param>
         * <return>The date in XML Schema date format.</return>
         */
        public static string GetXmlDate(DateTime date)
        {
            return XmlConvert.ToString(date, "yyyy-MM-dd\\Z");
        }

        /**
         * <summary>
         *   Replaces contiguous sequences of whitespace by a single space character.
         * </summary>
         * 
         * <param name="text">The text to replace whitespace by a single space.</param>
         * <return>The text with whitespace replaced by a single space.</return>
         */
        public static string ReplaceWhiteSpace(string text)
        {
            StringBuilder output = new StringBuilder(1024);
            bool inWhiteSpace = false;

            foreach (char c in text)
            {
                if (inWhiteSpace)
                {
                    if (!char.IsWhiteSpace(c))
                    {
                        output.Append(c);
                        inWhiteSpace = false;
                    }
                }
                else
                {
                    if (char.IsWhiteSpace(c))
                    {
                        inWhiteSpace = true;
                        output.Append(' ');
                    }
                    else
                    {
                        output.Append(c);
                    }
                }
            }
            return output.ToString();
        }

        /**
         * <summary>
         *   Converts a <see cref="string"/> that contains multiple lines to a single line.<br />
         *   <br />
         *   This is used to ensure that messages put in a status bar are confined to one line.
         * </summary>
         * 
         * <param name="text">The text to be converted.</param>
         * 
         * <returns>The text converted into a single line.</returns>
         */
        public static string ToSingleLine(string text)
        {
            while (text.StartsWith("\r\n"))
            {
                text = text.Substring(2);
            }
            int indexOfCrLf = text.IndexOf("\r\n");

            if (indexOfCrLf > 0)
            {
                text = text.Substring(0, indexOfCrLf);
            }
            return text;
        }
    }
}
