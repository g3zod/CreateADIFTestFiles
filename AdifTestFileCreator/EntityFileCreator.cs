using System;
using System.Text;
using System.Xml;
using System.IO;
using AdifReleaseLib;
using static AdifTestFileCreator.FileCreator;

namespace AdifTestFileCreator
{
    /**
     * <summary>
     *   Creates an ADIF version-specific XML file containing entity details with ADUF Primary Adminstrative Subdivision information.<br/>
     *   <br/>
     *   The new file name is saved with the ADIF version number as a 3-digit suffix.
     * </summary>
     */
    internal class EntityFileCreator
    {
        private readonly ProgressReporter ReportProgress = null;

        private readonly string
            AdifVersion,
            //AdifDirectoryPath,
            AllFilePath,
            SourceDirectoryPath,
            StartupPath,
            ProductName,
            ProductVersion;

        private readonly XmlDocument AllDoc;

        /**
         * <summary>
         *   Creates an object for creating an XML file containing entity details with ADIF Primary Adminstrative Subdivision information.
         * </summary>
         * 
         * <param name="adifVersion">The ADIF version number as a <see cref="string"/> containing 3 digits.</param>
         * <param name="adifDirectoryPath">The path to directory that contains the "exports" and "tests" sub-directories.</param>
         * <param name="allFilePath">The path to the all.xml file.</param>
         * <param name="allDoc">An <see cref="XmlDocument"/> object loaded with the contents of the all.xml file</param>
         * <param name="startupPath">The path to the directory that contains the excutable and DLL files.</param>
         * <param name="productName">The product name of the program for including in the <![CDATA[Entities<ijk>.xml]]> file.</param>
         * <param name="productVersion">The product version of the program for including in the <![CDATA[Entities<ijk>]]>.xml file.</param>
         * <param name="reportProgress">A delegate that used to report progress to the calling class.</param>
         */
        internal EntityFileCreator(
            string adifVersion,
            string allFilePath,
            string sourceDirectoryPath,
            string startupPath,
            string productName,
            string productVersion,
            XmlDocument allDoc, ProgressReporter reportProgress)
        {
            AdifVersion = adifVersion;
            AllFilePath = allFilePath;
            SourceDirectoryPath = sourceDirectoryPath;
            StartupPath = startupPath;
            ProductName = productName;
            ProductVersion = productVersion;
            AllDoc = allDoc;
            ReportProgress = reportProgress;
        }

        /**
         * <summary>
         *   Creates an ADIF version-specific XML file containing entity details with ADF Primary Adminstrative Subdivision information
         *   based on an existing XML file.<br/>
         *   <br/>
         *   The new file name is saved with the ADIF version number as a 3-digit suffix.
         * </summary>
         */
        internal void CreateFile()
        {
            int warnings = 0;

            string
                partialFileName = "Entities",
                oldFilePath = Path.Combine(StartupPath, $"{partialFileName}.xml"),
                newFilePath = Path.Combine(SourceDirectoryPath, $"{partialFileName}_{AdifVersion}_{DateTime.UtcNow:yyyy_MM_dd}.xml");

            ReportProgress?.Invoke($"Creating {newFilePath} ...");

            DateTime date = DateTime.UtcNow;

            date = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Month, date.Second);  // Remove milliseconds.

            XmlDocument
                newDoc = new XmlDocument(),
                oldDoc = new XmlDocument();

            using (XmlWriter xmlWriter = XmlWriter.Create(
                newFilePath,
                new XmlWriterSettings
                {
                    CheckCharacters = true,
                    ConformanceLevel = ConformanceLevel.Document,
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    IndentChars = "  ",
                    NewLineHandling = NewLineHandling.None,
                    OmitXmlDeclaration = false
                }))
            {
                oldDoc.Load(oldFilePath);

                XmlComment newComment = newDoc.CreateComment(string.Empty);  // Contents will be updated at the end of the method.

                newDoc.AppendChild(newComment);

                XmlElement newAdifEl = newDoc.CreateElement("adif");

                newAdifEl.SetAttribute("created", XmlConvert.ToString(date, XmlDateTimeSerializationMode.Utc));
                newAdifEl.SetAttribute("programName", ProductName);
                newAdifEl.SetAttribute("programVersion", ProductVersion);

                newDoc.AppendChild(newAdifEl);

                XmlElement newDxccEntities = newDoc.CreateElement("dxccEntities");

                newAdifEl.AppendChild(newDxccEntities);

                XmlElement allEnumerationEl = (XmlElement)AllDoc.DocumentElement.SelectSingleNode("enumerations/enumeration");

                XmlNodeList allPasEls = AllDoc.DocumentElement.SelectNodes("enumerations/enumeration[@name='Primary_Administrative_Subdivision']");

                // Use the list of DXCC entities in all.xml to iterate through the 500+ entities that aren't marked as "Deleted".

                XmlElement allDxccEntityCodeEnumerationEl = (XmlElement)AllDoc.DocumentElement.SelectSingleNode(
                    "enumerations/enumeration[@name='DXCC_Entity_Code']");

                XmlNodeList allDxccEntityCodeEnumerationEntityCodes = allDxccEntityCodeEnumerationEl.SelectNodes(
                    "record[value[@name='Entity Code']]");

                allDxccEntityCodeEnumerationEntityCodes = allDxccEntityCodeEnumerationEl.SelectNodes(
                    "record[value[@name='Entity Code'] and not(value[@name='Deleted']) and value[@name='Entity Code'] != '0']");

                foreach (XmlElement allDxccEntityCodeEnumerationEntityCodeValueEl in allDxccEntityCodeEnumerationEntityCodes)
                {
                    string dxccCode = allDxccEntityCodeEnumerationEntityCodeValueEl.SelectSingleNode(
                        "value[@name='Entity Code']").InnerText;

                    string dxccName = allDxccEntityCodeEnumerationEntityCodeValueEl.SelectSingleNode(
                        "value[@name='Entity Name']").InnerText;

                    Logger.Log($"DXCC {dxccCode} {dxccName}");

                    // Primary Administrative Subdivision elements should never be marked as "Deleted".

                    System.Diagnostics.Debug.Assert(
                        (XmlElement)allDxccEntityCodeEnumerationEntityCodeValueEl.SelectSingleNode(
                            "value[@name='Deleted']") == null);

                    // Copy any available DXCC entity attributes from Entities.xml to Entities<version>.xml

                    XmlElement oldDxccEntityEl = (XmlElement)oldDoc.DocumentElement.SelectSingleNode($"dxccEntities/dxccEntity[@code='{dxccCode}']");

                    if (oldDxccEntityEl == null)
                    {
                        warnings++;
                        Logger.Log($"  *** No <dxccEntity> record found");
                    }
                    else
                    {
                        const string
                            callTemplateAttrName = "callTemplate",
                            continentAttrName = "continent",
                            ituzAttrName = "ituz",
                            cqzAttrName = "cqz",
                            startDateAttrName = "startDate",
                            endDateAttrName = "endDate";

                        string
                            callTemplate = oldDxccEntityEl.GetAttribute(callTemplateAttrName),
                            continent = oldDxccEntityEl.GetAttribute(continentAttrName),
                            ituz = oldDxccEntityEl.GetAttribute(ituzAttrName),
                            cqz = oldDxccEntityEl.GetAttribute(cqzAttrName),
                            startDate = oldDxccEntityEl.GetAttribute(startDateAttrName),
                            endDate = oldDxccEntityEl.GetAttribute(endDateAttrName);

                        XmlElement newDxccEntity = newDoc.CreateElement("dxccEntity");

                        newDxccEntity.SetAttribute("code", dxccCode);
                        if (string.IsNullOrEmpty(callTemplate))
                        {
                            Logger.Log($"  Missing \"{callTemplateAttrName}\" attribute");
                        }
                        else
                        {
                            newDxccEntity.SetAttribute(callTemplateAttrName, callTemplate);
                        }

                        if (string.IsNullOrEmpty(dxccName))
                        {
                            Logger.Log($"  Missing \"name\" attribute");
                        }
                        else
                        {
                            newDxccEntity.SetAttribute("name", dxccName);
                        }

                        if (string.IsNullOrEmpty(continent))
                        {
                            Logger.Log($"  Missing \"{continentAttrName}\" attribute");
                        }
                        else
                        {
                            newDxccEntity.SetAttribute(continentAttrName, continent);
                        }

                        if (string.IsNullOrEmpty(ituz))
                        {
                            Logger.Log($"  Missing \"{ituzAttrName}\" attribute");
                        }
                        else
                        {
                            newDxccEntity.SetAttribute(ituzAttrName, ituz);
                        }

                        if (string.IsNullOrEmpty(cqz))
                        {
                            Logger.Log($"  Missing \"{cqzAttrName}\" attribute");
                        }
                        else
                        {
                            newDxccEntity.SetAttribute(cqzAttrName, cqz);
                        }

                        if (string.IsNullOrEmpty(startDate))
                        {
                            // This is an optional attribute.
                        }
                        else
                        {
                            newDxccEntity.SetAttribute(startDateAttrName, startDate);
                        }

                        if (string.IsNullOrEmpty(endDate))
                        {
                            // This is an optional attribute.
                        }
                        else
                        {
                            newDxccEntity.SetAttribute(endDateAttrName, endDate);
                        }

                        /*
                            Copy any available pas elements from Entities.xml to Entities<version>.xml

                            <adif created="2018-01-24T22:36:20Z">
                              <dxccEntities>
                                <dxccEntity code="1" callTemplate="VE#aaa" name="Canada" continent="NA" ituz="2" cqz="5">
                                  <pas code="NS" callTemplate="VE1aaa" />
                                  <pas code="QC" callTemplate="VE2aaa" />
                                  <pas code="ON" callTemplate="VE3aaa" />
                                  <pas code="MB" callTemplate="VE4aaa" />
                                  <pas code="SK" callTemplate="VE5aaa" />
                                  <pas code="AB" callTemplate="VE6aaa" />
                                  <pas code="BC" callTemplate="VE7aaa" />
                                  <pas code="NT" callTemplate="VE8aaa" />
                                  <pas code="NB" callTemplate="VE9aaa" />
                                  <pas code="NL" callTemplate="VO1aaa" />
                                  <pas code="YT" callTemplate="VY1aaa" />
                                  <pas code="PE" callTemplate="VY2aaa" />
                                  <pas code="NU" callTemplate="VY0aaa" />
                                </dxccEntity>
                         */

                        XmlNodeList allPrimaryAdminstrativeSubdivisionRecordEls = AllDoc.DocumentElement.SelectNodes(
                            $"enumerations/enumeration/record[value[@name='Primary Administrative Subdivision'] and value[@name='DXCC Entity Code'] = '{dxccCode}']");

                        int newPasFoundCount = 0,
                            oldPasCallTemplateCount = 0;

                        foreach (XmlElement allPrimaryAdminstrativeSubdivisionRecordEl in allPrimaryAdminstrativeSubdivisionRecordEls)
                        {
                            // Note: A Primary Administrative Subdivision code can be "import-only" but never "deleted".
                            // For the purposes of test QSOs, include the "import-only" codes.

                            newPasFoundCount++;

                            string
                                pasCode,
                                pasCqZone,
                                pasItuZone;

                            XmlElement el = (XmlElement)allPrimaryAdminstrativeSubdivisionRecordEl.SelectSingleNode(
                                "value[@name='Code']");

                            pasCode = el.InnerText;

                            el = (XmlElement)allPrimaryAdminstrativeSubdivisionRecordEl.SelectSingleNode(
                                "value[@name='CQ Zone']");

                            pasCqZone = el == null ? string.Empty : el.InnerText;

                            el = (XmlElement)allPrimaryAdminstrativeSubdivisionRecordEl.SelectSingleNode(
                                "value[@name='ITU Zone']");

                            pasItuZone = el == null ? string.Empty : el.InnerText;

                            XmlElement newPasEl = newDoc.CreateElement("pas");

                            newPasEl.SetAttribute("code", pasCode);

                            // Copy any available "callTemplate" attributes from the Entities.xml file to the Entities<version>.xml file.

                            {
                                XmlElement oldDxccEntityPas = (XmlElement)oldDxccEntityEl.SelectSingleNode(
                                    $"pas[@code='{pasCode}']");

                                if (oldDxccEntityPas != null)
                                {
                                    string oldPasCallTemplate = oldDxccEntityPas.GetAttribute("callTemplate");

                                    if (!string.IsNullOrEmpty(oldPasCallTemplate))
                                    {
                                        oldPasCallTemplateCount++;

                                        newPasEl.SetAttribute("callTemplate", oldPasCallTemplate);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(pasCqZone))
                            {
                                newPasEl.SetAttribute("cqz", pasCqZone);
                            }

                            if (!string.IsNullOrEmpty(pasItuZone))
                            {
                                newPasEl.SetAttribute("ituz", pasItuZone);
                            }

                            newDxccEntity.AppendChild(newPasEl);

                            if (dxccCode == "6")  // Alaska
                            {
                                // Update the list of Alaska county codes, implemented as <sas> elements contained in the one and only <pas> element.

                                /*                            
                                    Extract a list of current Alaskan counties.  This is an example of the format in the all.xml file:

                                    <adif version="3.1.4" status="Released" created="2024-08-27T16:08:24Z">
                                        <enumerations>
                                        <enumeration name="Secondary_Administrative_Subdivision">
                                            <header>
                                            <value>Enumeration Name</value>
                                            <value>Code</value>
                                            <value>Secondary Administrative Subdivision</value>
                                            <value>DXCC Entity Code</value>
                                            <value>Alaska Judicial District</value>
                                            <value>Deleted</value>
                                            <value>Import-only</value>
                                            <value>Comments</value>
                                            </header>
                                            <record>
                                            <value name="Enumeration Name">Secondary_Administrative_Subdivision</value>
                                            <value name="Code">AK,Aleutians East</value>
                                            <value name="Secondary Administrative Subdivision">Aleutians East</value>
                                            <value name="DXCC Entity Code">6</value>
                                            <value name="Alaska Judicial District">Alaska Third Judicial District</value>
                                            </record>
                                            <record>
                                            <value name="Enumeration Name">Secondary_Administrative_Subdivision</value>
                                            <value name="Code">AK,Aleutians Islands</value>
                                            <value name="Secondary Administrative Subdivision">Aleutians Islands</value>
                                            <value name="DXCC Entity Code">6</value>
                                            <value name="Alaska Judicial District">Alaska Third Judicial District</value>
                                            <value name="Deleted">true</value>
                                            </record>
                                */

                                XmlNodeList allAlaskaSasEls = AllDoc.DocumentElement.SelectNodes(
                                    "enumerations/enumeration[@name='Secondary_Administrative_Subdivision']/record[not(value[@name='Deleted'])]");

                                foreach (XmlElement allRecordEl in allAlaskaSasEls)
                                {
                                    XmlElement allValueCode = (XmlElement)allRecordEl.SelectSingleNode("value[@name='Code']");

                                    XmlElement newSasEl = newDoc.CreateElement("sas");

                                    newSasEl.SetAttribute("code", allValueCode.InnerText);
                                    newPasEl.AppendChild(newSasEl);
                                }
                            }  // if (dxccCode == "6")  // Alaska
                        }  // foreach (XmlElement allPrimaryAdminstrativeSubdivisionRecordEl in allPrimaryAdminstrativeSubdivisionRecordEls)

                        if (oldPasCallTemplateCount > 0)
                        {
                            if (oldPasCallTemplateCount != newPasFoundCount)
                            {
                                warnings++;
                                Logger.Log($"  *** Warning: Entities.xml contains fewer <pas> elements ({oldPasCallTemplateCount}) than all.xml ({newPasFoundCount})");
                            }
                        }
                        newDxccEntities.AppendChild(newDxccEntity);
                    }  // else for if (oldDxccEntityEl == null)
                }  // foreach (XmlElement allDxccEntityCodeEnumerationEntityCodeValueEl in allDxccEntityCodeEnumerationEntityCodes)

                string seeLogFileForWarnings = warnings == 0 ? string.Empty : " (see the log file in 'Documents' for details)";

                newComment.Data =
$@"
  This file is used to provide additional information required to create files containing ADIF test QSOs.

  It was generated by {ProductName} {ProductVersion} by merging DXCC,
  Primary Administrative Subdivison and (for Alaska) Secondary Administrative Subdivision
  data from the two XML input files.

  Input   : {oldFilePath}
  Input   : {AllFilePath}
  Output  : {newFilePath}

  Warnings: {warnings}{seeLogFileForWarnings}
";

                newDoc.WriteContentTo(xmlWriter);

                ReportProgress?.Invoke($"Completed creating {newFilePath} with {warnings} warnings");
            }  // using (XmlWriter xmlWriter = XmlWriter.Create(
        }  // internal void CreateFile()
    }
}
