using System;
using System.IO;
using System.Xml;
using AdifReleaseLib;

namespace AdifTestFileCreator
{
    /**
     * <summary>
     *   Creates ADIF test QSOs files and an intermediate XML file containing extended
     *   DXCC entity information.<br/>
     *   <br/>
     *   Data input files:
     *   
     *   <list type="bullet">
     *      <item>
     *          <term>all.xml</term>
     *          <description>An XML file containing tables exported from an ADIF Specification.</description>
     *      </item>
     *      <item>
     *          <term>Entities.xml</term>
     *          <description>An XML file containing additional DXCC entity information.&#160;
     *          This file is supplied as part of this application.</description>
     *      </item>
     *      <item>
     *          <term><![CDATA[QSO_templates.xslt]]></term>
     *          <description>An XSLT file containing the templates for ADIF records and fields.&#160;
     *          These files are supplied as part of this application.</description>
     *      </item>
     *   </list>
     *   
     *   Data output files:
     *   
     *   <list type="bullet">
     *      <item>
     *          <term><![CDATA[Entities_<ijk>_<yyyy>_<mm>_<dd>.xml]]></term>
     *          <description>A file containing extended DXCC entity information
     *          created by merging information from all.xml and Entities.xml and included for reference.</description>
     *      </item>
     *      <item>
     *          <term><![CDATA[QSO_templates_<ijk>_<yyyy>_<mm>_<dd>.xslt]]></term>
     *          <description>An XSLT file containing the templates for ADIF records and fields included for reference.</description>
     *      </item>
     *      <item>
     *          <term><![CDATA[ADIF_<ijk>_test_QSOs_<yyyy>_<mm>_<dd>.adi]]></term>
     *          <description>An ADI test QSOs file.</description>
     *      </item>
     *      <item>
     *          <term><![CDATA[ADIF_<ijk>_test_QSOs_<yyyy>_<mm>_<dd>.adx]]></term>
     *          <description>An ADX test QSOs file.</description>
     *      </item>
     *   </list><![CDATA[where <ijk> is the ADIF version i.j.k, <yyyy> is the year, <mm> is the month and <dd> is the day.]]>
     * </summary>
     */
    public class FileCreator
    {
        /**
         * <summary>
         *   The ADIF file format, either ADI (.adi) or ADX (.adx).
         * </summary>
         */
        internal enum AdifFileType
        {
            /**
             * <summary>
             *   Indicates ADIF ADI (.adi) file format.
             * </summary>
             */
            ADI = 0,

            /**
             * <summary>
             *   Indicates ADIF ADX (.adx) file format.
             * </summary>
             */
            ADX = 1
        }

        /**
         * <summary>
         *   A delegate type for reporting progress back to calling code.
         * </summary>
         */
        public delegate void ProgressReporter(string message);

        /**
         * <value>
         *   A <see cref="ProgressReporter"/> delegate object.
         * </value>
         */
        private ProgressReporter ReportProgress { get; set; }

        /**
         * <summary>
         *   A delegate type for reporting a detailed message back to calling code.&#160;
         *   For example, the calling code could then use Windows.Forms.MessageBox.Show to display the message.
         * </summary>
         */
        public delegate void UserPrompter(string message);

        /**
         * <value>
         *  A <see cref="ProgressReporter"/> delegate object.
         * </value>
         */
        private UserPrompter PromptUser { get; set; }

        /**
         * <summary>
         *   This receives a callback from <see cref="AdifXsltLib.AdifXslt.PromptUser"/> and raises it with <see cref="PromptUser"/>.&#160;
         *   This eliminates the need for <see cref="CreateAdifTestFiles"/> to have a reference to <see cref="AdifXsltLib"/>.
         * </summary>
         */
        private void AdifXsltLibPromptUser(string message) => PromptUser?.Invoke(message);

        /**
         * <value>
         *   <![CDATA[The full path to the <version> directory.]]>
         * </value>
         */
        private string AdifDirectoryPath { get; set; }

        /**
         * <value>
         *   <![CDATA[The full path to the file <version>\exports\xml\all.xml]]>
         * </value>
         */
        private string AllFilePath { get; set; }

        /**
         * <value>
         *   <![CDATA[The full path to the directory <version>\tests]]>
         * </value>
         */
        private string TestsDirectoryPath { get; set; }

        /**
         * <value>
         *   The path to the directory that contains the excutable and DLL files.
         * </value>
         */
        private string SourceDirectoryPath { get; set; }

        /**
         * <value>
         *   The path to the directory that contains the source files used for diagnostics.
         * </value>
         */
        private string StartupPath { get; set; }

        /**
         * <value>
         *   The product name.
         * </value>
         */
        private string ProductName { get; set; }

        /**
         * <value>
         *   The product name of the program for including in the <![CDATA[Entities<ijk>.xml]]> file.
         * </value>
         */
        private string ProductVersion { get; set; }

        /**
         * <value>
         *   The product version of the program for including in the <![CDATA[Entities<ijk>]]>.xml file.
         * </value>
         */
        private XmlDocument AllDoc { get; set; }

        /**
         * <value>
         *   The ADIF version found in the all.xml file's <![CDATA[<adif>]]> element's version attribute as a 3-character <see cref="string"/>.
         * </value>
         */
        private string AdifVersion { get; set; }

        /**
         * <value>
         *   A list of the ADIF versions supported by this version of the application.
         * </value>
         */
        private readonly string[] SupportedVersions =
        {
            "314",
            "315",
        };

        /**
         * <summary>
         *   Creates a <see cref="FileCreator"/> object.
         * </summary>
         * 
         * <param name="adifDirectoryPath"><![CDATA[The ADIF directory's root path, which is normally <ijk> where the ADIF version is i.j.k, for example 315.]]></param>
         * <param name="startupPath">The path to the directory that contains the excutable and DLL files.</param>
         * <param name="productName">The product name of the program for including in the <![CDATA[Entities<ijk>.xml]]> file.</param>
         * <param name="productVersion">The product version of the program for including in the <![CDATA[Entities<ijk>]]>.xml file.</param>
         * <param name="reportProgress">A delegate object for reporting a short progress message for logging and / or status bar purposes.</param>
         * <param name="userPrompter">A delegate object for displaying a message to the user.</param>
         */
        public FileCreator(
            string adifDirectoryPath,
            string startupPath,
            string productName,
            string productVersion,
            ProgressReporter reportProgress,
            UserPrompter userPrompter)
        {
            /*
             * Directory and file paths used in this application:
             * 
             *  <ijk>                                                   (ADIF version number i.j.k)
             *      exports
             *          xml
             *              all.xml                                     (XML exported from the ADIF Specification)
             *      tests
             *          ADIF_<ijk>_test_QSOs_<yyyy>_<mm>_<dd>.adi       (ADI test QSOs file created by this application)
             *          ADIF_<ijk>_test_QSOs_<yyyy>_<mm>_<dd>.adx       (ADX test QSOs file created by this application)
             *          source
             *              Entities_<ijk>_<yyyy>_<mm>_<dd>.xml         (XML entity details created by this application)
             *              QSO_templates_<ijk>_<yyyy>_<mm>_<dd>.xslt   (XSLT templates used to create the test files)
             */

            ReportProgress = reportProgress;
            PromptUser = userPrompter;

            AdifDirectoryPath = Path.GetFullPath(adifDirectoryPath);  // Allow a relative or full path.
            AllFilePath = Path.Combine(AdifDirectoryPath, "exports", "xml", "all.xml");
            TestsDirectoryPath = Path.Combine(AdifDirectoryPath, "tests");
            SourceDirectoryPath = Path.Combine(TestsDirectoryPath, "source");
            StartupPath = startupPath;
            ProductName = productName;
            ProductVersion = productVersion;

            if (!Directory.Exists(TestsDirectoryPath))
            {
                Logger.Log($"Creating directory {TestsDirectoryPath} ...");
                _ = Directory.CreateDirectory(TestsDirectoryPath);
            }

            if (!Directory.Exists(SourceDirectoryPath))
            {
                Logger.Log($"Creating directory {SourceDirectoryPath} ...");
                _ = Directory.CreateDirectory(SourceDirectoryPath);
            }

            AllDoc = new XmlDocument();

            if (!File.Exists(AllFilePath))
            {
                throw new ApplicationException($"File does not exist: \"{AllFilePath}\"");
            }

            AllDoc.Load(AllFilePath);

            {
                // ADIF versions are three single digits separated by dots, e.g. 3.1.4

                string adifVersionStr = AllDoc.DocumentElement.GetAttribute("version");
                string[] adifVersionParts = adifVersionStr.Split('.');

                if (adifVersionParts.Length == 3 &&
                    int.TryParse(adifVersionParts[0], out int i) &&
                    int.TryParse(adifVersionParts[1], out int j) &&
                    int.TryParse(adifVersionParts[2], out int k) &&
                    i >= 0 && i <= 9 &&
                    j >= 0 && j <= 9 &&
                    k >= 0 && k <= 9)
                {
                    AdifVersion = (i * 100 + j * 10 + k).ToString();

                    bool versionSupported = false;

                    foreach (string supportedVersion in SupportedVersions)
                    {
                        if (AdifVersion == supportedVersion)
                        {
                            versionSupported = true;
                            break;
                        }
                    }

                    if (!versionSupported)
                    {
                        throw new ApplicationException($"This application version does not support ADIF version {AdifVersion[0]}.{AdifVersion[1]}.{AdifVersion[2]}");
                    }
                }
                else
                {
                    throw new ApplicationException($"The ADIF version \"{adifVersionStr}\" in {AllFilePath} is invalid");
                }
            }
        }

        /**
         * <summary>
         *   Copies an XSLT file then creates an intermediate XML file and two ADIF test QSOs files.
         * </summary>
         * 
         * <remarks>
         *   The <![CDATA[QSO_templates.xslt]]> file is copied to <![CDATA[QSO_templates_<ijk>_<yyyy>_<mm>_<dd>.xslt]]>
         *   then the <![CDATA[Entities_<ijk>_<yyyy>_<mm>_<dd>.xml]]> file is created and finally the ADI and ADX files are created.
         * </remarks>
         */
        public void CreateAdifTestFiles()
        {
            string dateStamp = $"{DateTime.UtcNow:yyy_MM_dd}";

            {
                string
                    partialFileName = $"QSO_templates",
                    oldFilePath = Path.Combine(StartupPath, $"{partialFileName}.xslt"),
                    newFilePath = Path.Combine(SourceDirectoryPath, $"{partialFileName}_{AdifVersion}_{DateTime.UtcNow:yyy_MM_dd}.xslt");

                File.Copy(oldFilePath, newFilePath, true);
            }

            new EntityFileCreator(
                AdifVersion,
                AllFilePath,
                SourceDirectoryPath,
                StartupPath,
                ProductName,
                ProductVersion,
                AllDoc,
                ReportProgress).CreateFile();

            // Because the AdifXsltLib.AdifXslt constructor is called from within the TestQSOs<ijk>.xslt file,
            // it's necessary to set static properties instead of using constructor arguments.
            //
            // Using these properties removes the need for this class or AdifXsltLib to reference System.Windows.Forms
            // and the calling code can output the message (for example) to System.Windows.Forms.MessageBox or a console.

            AdifXsltLib.AdifXslt.EntitiesXmlPath = Path.Combine(StartupPath, "Entities.xml");
            AdifXsltLib.AdifXslt.PromptUser = new AdifXsltLib.AdifXslt.UserPrompter(AdifXsltLibPromptUser);
            AdifXsltLib.AdifXslt.ApplicationName = ProductName;
            AdifXsltLib.AdifXslt.ApplicationVersion = ProductVersion;

            new TestQsoFileCreator(
                AdifVersion,
                AdifDirectoryPath,
                TestsDirectoryPath,
                StartupPath,
                AllDoc,
                ReportProgress).CreateFiles();

            ReportProgress?.Invoke("Completed");
        }
    }
}
