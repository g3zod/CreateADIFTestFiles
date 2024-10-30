using AdifReleaseLib;
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Xsl;
using static AdifTestFileCreator.FileCreator;

namespace AdifTestFileCreator
{
    /**
     * <summary>
     *   Creates ADIF ADI and ADX test QSOs files using the XML data file exported from the ADIF Specification.
     * </summary>
     */
    internal class TestQsoFileCreator
    {
        private readonly FileCreator.ProgressReporter ReportProgress = null;
        private readonly string
            AdifVersion,
            AdifDirectoryPath,
            TestsDirectoryPath,
            StartupPath;

        private readonly XmlDocument AllDoc;

        /**
         * <summary>
         *   Returns an object for creating ADIF ADI and ADX test QSOs files using the XML data file exported from the ADIF Specification.
         * </summary>
         * 
         * <param name="adifVersion">The ADIF version number as a <see cref="string"/> containing 3 digits.</param>
         * <param name="adifDirectoryPath"></param>
         * <param name="testsDirectoryPath">The path to the tests directory.</param>
         * <param name="startupPath">The path to the directory that contains the excutable and DLL files.</param>
         * <param name="allDoc">An <see cref="XmlDocument"/> object loaded with the contents of the all.xml file</param>
         */
        /// <param name="reportProgress">A delegate that used to report progress to the calling class.</param>
        internal TestQsoFileCreator(
            string adifVersion,
            string adifDirectoryPath,
            string testsDirectoryPath,
            string startupPath,
            XmlDocument allDoc, ProgressReporter reportProgress)
        {
            AdifVersion = adifVersion;
            AdifDirectoryPath = adifDirectoryPath;
            TestsDirectoryPath = testsDirectoryPath;
            StartupPath = startupPath;
            AllDoc = allDoc;
            ReportProgress = reportProgress;
        }

        /**
         * <summary>
         *   Creates ADIF ADI and ADX test QSO files using the XML data file exported from the ADIF Specification.
         * </summary>
         */
        internal void CreateFiles()
        {
            CreateFile(AdifFileType.ADI);
            CreateFile(AdifFileType.ADX);
        }

        /**
         * <summary>
         *   Creates an ADIF ADI or ADX test QSO file using the XML data file exported from the ADIF Specification.
         * </summary>
         * 
         * <param name="adifFileType">Sets the output file to be either an ADI (.adi) file or an ADX (.adx) file.</param>
         */
        internal void CreateFile(AdifFileType adifFileType)
        {
            try
            {
                string testQsosFileNameWithoutExtension = $"ADIF_{AdifVersion}_test_QSOs_{DateTime.UtcNow:yyyy_MM_dd}";
                string adifTestQsosFilePath = TestsDirectoryPath;
                Encoding encoding;

                switch (adifFileType)
                {
                    case AdifFileType.ADI:
                        adifTestQsosFilePath = Path.Combine(adifTestQsosFilePath, $"{testQsosFileNameWithoutExtension}.adi");
                        encoding = Encoding.ASCII;
                        break;

                    case AdifFileType.ADX:
                        adifTestQsosFilePath = Path.Combine(adifTestQsosFilePath, $"{testQsosFileNameWithoutExtension}.adx");
                        encoding = Encoding.UTF8;
                        break;

                    default:
                        throw new ApplicationException($"Unrecognised ADIF file type: {adifFileType}");
                }

                ReportProgress?.Invoke($"Creating {adifTestQsosFilePath} ...");

                using (XmlReader xmlDocReader = new XmlNodeReader(AllDoc))
                {
                    AppContext.SetSwitch("Switch.System.Xml.AllowDefaultResolver", true);  // Needed for .NET 8

                    XslCompiledTransform xslt = new XslCompiledTransform();

                    xslt.Load(
                        Path.Combine(StartupPath, $"QSO_templates.xslt"),
                        XsltSettings.TrustedXslt,
                        new XmlUrlResolver());

                    XsltArgumentList xslArgs = new XsltArgumentList();

                    xslArgs.AddExtensionObject(
                        "urn:adifxsltextension",
                        new AdifXsltExtension());

                    xslArgs.AddParam("adifStyle", "", adifFileType == AdifFileType.ADI ? "ADI" : "ADX");

                    string contents = string.Empty;

                    using (StringWriter stringWriter = new StringWriter())
                    {
                        xslt.Transform(xmlDocReader, xslArgs, stringWriter);
                        contents = stringWriter.ToString();
                    }

                    string resultMessage;

                    if (AdifXsltLib.AdifXslt.Success)
                    {
                        if (adifFileType == AdifFileType.ADX)
                        {
                            // It is posisble to validate the ADX using both XML schemas since no
                            // deprecated (import-only) items are included in the ADX.
                            //
                            // It is also a good test of the XML schemas themselves.

                            foreach (string schemaFileName in new string[] {
                                $"adx{AdifVersion}.xsd",
                                $"adx{AdifVersion}generic.xsd" })
                            {
                                if (!ValidateAdx(
                                        contents,
                                        Path.Combine(AdifDirectoryPath, schemaFileName)))
                                {
                                    Logger.Log($"ADX file has failed validation\r\n\r\n{AdixValidationResults}");
                                    throw new AdifXsltLib.AdifException("ADX file has failed validation - see log file for details");
                                }
                            }
                        }

                        using (StreamWriter streamWriter = new StreamWriter(adifTestQsosFilePath, false, encoding))
                        {
                            streamWriter.Write(contents);
                        }
                        resultMessage = $"Completed creating {adifTestQsosFilePath}";
                    }
                    else
                    {
                        resultMessage = $"*** Error creating {adifTestQsosFilePath}";
                    }
                    ReportProgress?.Invoke(resultMessage);
                }
            }
            catch (XsltException exc)
            {
                StringBuilder message = new StringBuilder(1024);

                _ = message.Append(string.IsNullOrEmpty(exc.Message) ?
                                exc.GetType().Name :
                                exc.Message).
                            Append("\r\n");

                if (!string.IsNullOrEmpty(exc.Source))
                {
                    _ = message.Append("Source ").
                                Append(exc.Source).
                                Append("\r\n");
                }
                if (exc.LineNumber > 0)
                {
                    _ = message.Append("Line ").
                                Append(exc.LineNumber).
                                Append("\r\n");
                }
                if (exc.LinePosition > 0)
                {
                    _ = message.Append("Line Position ").
                                Append(exc.LinePosition).
                                Append("\r\n");
                }
                if (!string.IsNullOrEmpty(exc.SourceUri))
                {
                    _ = message.Append("Source URI ").
                                Append(exc.SourceUri).
                                Append("\r\n");
                }
                if (exc.InnerException != null && !string.IsNullOrEmpty(exc.InnerException.Message))
                {
                    _ = message.Append("\r\n\r\n").
                                Append(exc.InnerException.Message);
                }
                ReportProgress?.Invoke(message.ToString());
                throw;
            }
            catch (Exception exc)
            {
                StringBuilder message = new StringBuilder(1024);

                message.Append(string.IsNullOrEmpty(exc.Message) ?
                    exc.GetType().Name :
                    exc.Message);
                if (exc.InnerException != null && !string.IsNullOrEmpty(exc.InnerException.Message))
                {
                    _ = message.Append("\r\n\r\n").
                        Append(exc.InnerException.Message);
                }
                ReportProgress?.Invoke(message.ToString());
                throw;
            }
        }

        /**
         * <summary>
         *   Buffers the messages from <see cref="AdxValidationCallBack"/>.
         * </summary>
         */
        private readonly StringBuilder AdixValidationResults = new StringBuilder(65536);


        /**
         * <summary>
         *   Validates ADX XML against an ADX XML schema.
         * </summary>
         * 
         * <param name="adx">The ADX to be validated. </param>
         * <param name="schemaFilePath">The path of the XML schema to be used to validate the ADX XML.</param>
         * 
         * <returns>true if the validation succeeded, otherwise false.</returns>
         */
        private bool ValidateAdx(string adx, string schemaFilePath)
        {
            // Simulate an error that causes XmlReader to throw an exception.
            //adx = "oops" + adx;

            // Simulate an error that causes XmlReader to invoke AdxValidationCallBack().
            //adx = adx.Replace("<DXCC>166</DXCC>", "<DXCC>oops</DXCC>");

            // Set the validation settings.

            ReportProgress?.Invoke($"Validating ADX using schema {Path.GetFileName(schemaFilePath)} ...");

            XmlReaderSettings settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
            };

            _ = settings.Schemas.Add("", Path.Combine(AdifDirectoryPath, $"adx{AdifVersion}.xsd"));
            settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
            settings.ValidationEventHandler += new ValidationEventHandler(AdxValidationCallBack);

            using (StringReader stringReader = new StringReader(adx))
            using (XmlReader xmlReader = XmlReader.Create(stringReader, settings))
            {
                while (xmlReader.Read());
            }
            
            return AdixValidationResults.Length == 0;
        }

        /**
         * <summary>
         *   This is a callback invoked by <see cref="XmlReader"/>.
         * </summary>
         * 
         * <param name="sender">A reference to the object in error.</param>
         * <param name="args">Details of the validation error.</param>
         */
        // Buffer any warnings or errors.
        private void AdxValidationCallBack(object _, ValidationEventArgs args)
        {
            _ = AdixValidationResults.Append(
                $"Line {args.Exception.LineNumber} {(args.Severity == XmlSeverityType.Error ? "Error" : "Warning")}: {args.Message}\r\n\r\n");
        }
    }
}
