using System;

#pragma warning disable IDE1006 // Naming Styles - disable because the naming is then consistent with the names in the .xlst file.

namespace AdifTestFileCreator
{
    /**
     * <summary>
     *   This is used as an XSLT extension object.  It acts as an interface between the <![CDATA[Test_QSOs_<ijk>.xslt]]> file
     *   and AdifXslt.cs
     * </summary>
     * 
     * <remarks>
     *   The source here was originally in a script file accessed from the <![CDATA[QSO_templates.xslt]]> file.<br/>
     *   <br />
     *   Now that it is a compiled C# extension object rather than a script file, it could be removed and calls made<br />
     *   directly to AdifXslt.cs but I think it is more readable to use this file for the interface to the XSLT<br />
     *   and keep the implementation code in <see cref="AdifXsltLib.AdifXslt"/>.
     * </remarks>
     */
    public class AdifXsltExtension
    {
        private AdifXsltLib.AdifXslt adifXslt = null;

        public string initialize(
          string fileFormat,
          bool hasHeaderFields,
          System.Xml.XPath.XPathNavigator nav)
        {
            adifXslt = new AdifXsltLib.AdifXslt(fileFormat, hasHeaderFields, nav);
            return string.Empty;
        }

        private AdifXsltLib.AdifXslt lib
        {
            get
            {
                if (adifXslt == null)
                {
                    throw new ApplicationException("The initialize method has not been called successfully");
                }
                return adifXslt;
            }
        }

        public string setOptions(
          string fieldSeparator,
          string recordSeparator) => lib.SetOptions(fieldSeparator, recordSeparator);

        public string adifVersion() => lib.AdifVersion();

        public int adifVersionInt() => lib.AdifVersionInt();

        public string programId() => lib.ProgramId;

        public string programVersion() => lib.ProgramVersion;

        public string createdTimestamp() => lib.CreatedTimestamp;

        public string saveQsoStartEnd() => lib.SaveQsoStartEnd();

        public string restoreQsoStartEnd() => lib.RestoreQsoStartEnd();

        public string callForDxcc(int dxcc) => lib.CallForDxcc(dxcc);

        public string callForCont(string cont) => lib.CallForCont(cont);

        public string callForPrimaryAdministrativeSubdivision(
            int dxcc,
            string primaryAdministrativeSubdivision)
            => lib.CallForPrimaryAdministrativeSubdivision(dxcc, primaryAdministrativeSubdivision);

        public string bandForContest(string contest) => lib.BandForContest(contest);

        public string comment(string text) => lib.Comment(text);

        public string commentLine(string text) => lib.CommentLine(text);

        public string commentLine2(string text) => lib.CommentLine2(text);

        public string commentReport(bool full) => lib.CommentReport(full);

        public string untestedField(string name) => lib.UntestedField(name);

        public string field(string name, string value) => lib.Field(name, value);

        public string userDefNField(
          string name,
          string dataTypeIndicator,
          int number,
          string enumeration) => lib.UserDefNField(name, dataTypeIndicator, number, enumeration);

        public string userDefField(
          string name,
          string value) => lib.UserDefField(name, value);

        public string appField(
          string name,
          string value,
          string programId) => lib.AppField(name, value, programId);

        public string appField(
          string name,
          string value,
          string programId,
          string dataTypeIndicator) => lib.AppField(name, value, programId, dataTypeIndicator);

        public string eoh() => lib.Eoh();

        public string bof() => lib.Bof();

        public string eof() => lib.Eof();

        // XSLT does not support the use of parameter arrays.  So that the AdifXsltLib method Record(params string[] args)
        // can be used, instead provide a set of overloads that call Record(... with a varying number of pairs (name and value)
        // of parameters.

        public string record() => lib.Record();

        public string record(string name, string value)
            => lib.Record(name, value);

        public string record(string name1, string value1, string name2, string value2)
            => lib.Record(name1, value1, name2, value2);

        public string record(string name1, string value1, string name2, string value2, string name3, string value3)
            => lib.Record(name1, value1, name2, value2, name3, value3);

        public string record(string name1, string value1, string name2, string value2, string name3, string value3, string name4, string value4)
            => lib.Record(name1, value1, name2, value2, name3, value3, name4, value4);

        public string record(string name1, string value1, string name2, string value2, string name3, string value3, string name4, string value4, string name5, string value5)
            => lib.Record(name1, value1, name2, value2, name3, value3, name4, value4, name5, value5);

        public string record(string name1, string value1, string name2, string value2, string name3, string value3, string name4, string value4, string name5, string value5, string name6, string value6)
            => lib.Record(name1, value1, name2, value2, name3, value3, name4, value4, name5, value5, name6, value6);

        public string record(string name1, string value1, string name2, string value2, string name3, string value3, string name4, string value4, string name5, string value5, string name6, string value6, string name7, string value7)
            => lib.Record(name1, value1, name2, value2, name3, value3, name4, value4, name5, value5, name6, value6, name7, value7);

        //public XPathNodeIterator nodeset(string xml, string select)
        //{
        //    return lib.NodeSet(xml, select);
        //}

        //public string experiment(XPathNodeIterator iterator)
        //{
        //    return lib.Experiment(iterator);
        //}
    }
}
#pragma warning restore IDE1006 // Naming Styles
