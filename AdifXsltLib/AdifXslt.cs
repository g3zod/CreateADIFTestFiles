using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using System.Globalization;

namespace AdifXsltLib
{
    /**
     * <summary>
     *   A library of methods that are used as XSLT user functions for creating ADI and ADX files.&#160;
     *   These are called from the <![CDATA[QSO_templates.xslt]]> file.
     * </summary>
     */
    public partial class AdifXslt
    {
        private class BandEntry
        {
            internal string Name;
            internal float LowerLimit;
            internal float UpperLimit;

            internal BandEntry(string name, float lowerLimit, float upperLimit)
            {
                Name = name;
                LowerLimit = lowerLimit;
                UpperLimit = upperLimit;
            }

            internal bool IsInBand(float freq) => freq >= LowerLimit && freq <= UpperLimit;

            //internal static bool Band(float freq, Dictionary<string, BandEntry> bands, out BandEntry bandEntry)
            //{
            //    bandEntry = null;
            //    foreach (BandEntry be in bands.Values)
            //    {
            //        if (be.IsInBand(freq))
            //        {
            //            bandEntry = be;
            //            break;
            //        }
            //    }
            //    return bandEntry != null;
            //}

            internal static string RandomBand(Dictionary<string, BandEntry> bands, Random random)
            {
                int index = random.Next(bands.Count);
                int i = 0;
                string band = string.Empty;

                foreach (BandEntry bandEntry in bands.Values)
                {
                    if (i++ == index)
                    {
                        band = bandEntry.Name;
                        break;
                    }
                }
                return band;
            }
        }

        private class ModeEntry
        {
            private static readonly char[] commaSplitChar = [','];

            internal string Mode;
            internal string[] Submodes;  // Not currently used because the SUBMODE field exercises all the Submode enumeration values.

            internal ModeEntry(string mode, string submodes)
            {
                this.Mode = mode;
                this.Submodes = submodes.Split(commaSplitChar, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private class CallEntry
        {
            internal string Call;
            internal int Dxcc;
            internal int CqZone;
            internal int ItuZone;
            internal string Cont;
            internal DateTime QsoStartDateTime;
            internal bool QsoHasStartTime;

            private CallEntry()
            {
                System.Diagnostics.Debug.Assert(false);
            }

            internal CallEntry(
                string call)
            {
                System.Diagnostics.Debug.Assert(call != null);
                System.Diagnostics.Debug.Assert(!call.Contains('a'));

                Call = call;
                Dxcc = 0;
                CqZone = 0;
                ItuZone = 0;
                Cont = string.Empty;
                QsoStartDateTime = DateTime.MinValue;
                QsoHasStartTime = false;
            }

            internal CallEntry(
                string call,
                XmlElement dxccEl,
                DateTime qsoStartDateTime,
                bool qsoStartHasTime)
            {
                System.Diagnostics.Debug.Assert(call != null);
                System.Diagnostics.Debug.Assert(!call.Contains('a'));
                System.Diagnostics.Debug.Assert(dxccEl != null);
                System.Diagnostics.Debug.Assert(dxccEl.GetAttribute("callTemplate") != null);
                System.Diagnostics.Debug.Assert(dxccEl.GetAttribute("code") != null);
                System.Diagnostics.Debug.Assert(dxccEl.GetAttribute("cqz") != null);
                System.Diagnostics.Debug.Assert(dxccEl.GetAttribute("ituz") != null);

                Call = call;
                Dxcc = int.Parse(dxccEl.GetAttribute("code"));
                CqZone = int.Parse(dxccEl.GetAttribute("cqz"));
                ItuZone = int.Parse(dxccEl.GetAttribute("ituz"));
                Cont = dxccEl.GetAttribute("continent");
                QsoStartDateTime = qsoStartDateTime;
                QsoHasStartTime = qsoStartHasTime;
            }
        }

        private class Calls
        {
            internal SortedDictionary<string, int> CallCounts = [];
            internal int RepeatedCallsTotal = 0;
            internal int RepeatedCalls = 0;

            private readonly XmlElement testDxccsEl;
            private readonly Random random = new(1);  // Produce a fixed sequence each time to enable some repeatability when debugging.
            private readonly List<int> validDxccs = new(1000);
            private readonly Dictionary<string, CallEntry> callEntries = new(8192);

            internal Calls(string fileName)
            {
                XmlDocument testData = new();

                testData.Load(fileName);
                testDxccsEl = (XmlElement)testData.DocumentElement.SelectSingleNode("dxccEntities");

                foreach (XmlElement el in testDxccsEl.SelectNodes("dxccEntity"))
                {
                    validDxccs.Add(int.Parse(el.GetAttribute("code")));
                }
            }

            private class Callsequencer
            {
                // This cycles through 3 letters in alphabetical order: AAA, AAB, AAC, ... ZZZ.
                // When it reaches ZZZ, it goes back to AAA.

                private readonly char[] letters = ['A', 'A', 'A'];

                internal char[] Next()
                {
                    char[] next = (char[])letters.Clone();

                    if (letters[2] < 'Z')
                    {
                        letters[2]++;
                    }
                    else
                    {
                        letters[2] = 'A';
                        if (letters[1] < 'Z')
                        {
                            letters[1]++;
                        }
                        else
                        {
                            letters[1] = 'A';
                            if (letters[0] < 'Z')
                            {
                                letters[0]++;
                            }
                            else
                            {
                                letters[0] = 'A';
                            }
                        }
                    }
                    return next;
                }
            }

            private readonly Callsequencer
                cs1 = new(),
                cs2 = new(),
                cs3 = new();

            private static readonly char[]
                XmlTimeSeparator = ['t', 'T'],
                CallTemplateListSeparator = ['+'];

            /**
             * <summary>
             *   <para>Creates a callsign modelled on a template.</para>
             *   
             *   <para>Callsign templates can have one, two, or three lower case letter 'a's representing any letter,
             *   and a '#' representing a digit.  Examples: W#aaa G2aa</para>
             *    
             *   <para>The idea is that the lower case letter 'a's are replace by an alphabetical sequence
             *   of letters, e.g. K2aaa produces W2AAA, W2AAB, W2AAC, ... W2ZZZ.  The use of a sequence
             *   instead of random letters reduces the chances of a duplicate call in sequential QSOs.</para>
             *  
             *   <para>The sequence of characters is maintained in three CallSequencer objects.</para>
             *  
             *   <para>Since those templates with 1 or 2 letter 'a's have fewer callsigns available before a repeat
             *   occurs (e.g. G2aa has 26^2 callsigns and W1a has only 26 possibilec allsigns), a separate sequence
             *   is kept for 3, 2, and 1 letter 'a' templates so that available letters for (in partciular) the
             *   1 letter 'a' templates aren't "used" up by the 2 and 3 letter templates.</para>
             *  
             *   <para>It would reduce the time between repeats even more if the sequence were kept separately for each
             *   callsign template rather than the groups of templates with 1, 2, and 3 letter 'a's.  However, at the
             *   moment, I don't believe that level of complexity is necessary.</para>
             * </summary>
             * 
             * <param name="callTemplate">The callsign template <see cref="string"/> from the Entities.xml file.</param>
             * 
             * <returns>A callsign instantiated from the callTemplate <see cref="string"/>.</returns>
             */
            private string InstantiateCallTemplate(string callTemplate, out DateTime qsoStartDateTime, out bool qsoHasStartTime)
            {
                string[] callList = callTemplate.Split(CallTemplateListSeparator, StringSplitOptions.RemoveEmptyEntries);

                qsoStartDateTime = DateTime.MinValue;
                qsoHasStartTime = false;

                if (callList.Length > 1)
                {
                    // Choose between several callsign templates.

                    callTemplate = new(callList[random.Next(callList.Length)]);
                }

                if (callTemplate.Contains('#'))
                {
                    callTemplate = callTemplate.Replace('#', (char)random.Next('0', '9' + 1));
                }

                {
                    // The call entry can include a date and possibly the time for the QSO.  This is necessary where
                    // Club Log's country does not allow mapping by prefix and it is necessary to include a historical
                    // QSO.
                    //
                    // Examples:
                    //    WA#aaa=2021-12-03Z
                    //    MD#bbb=2019-04-12T23:15:00Z

                    string[] callTemplateParts = callTemplate.Split('=');

                    if (callTemplateParts.Length == 2)
                    {
                        // Call entry includes the date and possibly the time for the QSO.  This is necessary where
                        // Club Log's country list does not allow mapping by prefix.

                        callTemplate = callTemplateParts[0];

                        string qsoDateTimeString = callTemplateParts[1];

                        qsoStartDateTime = XmlConvert.ToDateTime(qsoDateTimeString, XmlDateTimeSerializationMode.Utc);
                        qsoHasStartTime = qsoDateTimeString.Split(XmlTimeSeparator).Length == 2;
                    }
                    //if (callTemplate.ToString() == "K1B")
                    //{

                    //}
                }

                StringBuilder call = new(callTemplate);

                int aPosn = callTemplate.IndexOf('a');

                if (aPosn >= 0)
                {
                    if (callTemplate.Contains("aaa"))
                    {
                        char[] letters = cs3.Next();

                        call[aPosn + 0] = letters[0];
                        call[aPosn + 1] = letters[1];
                        call[aPosn + 2] = letters[2];
                    }
                    else if (callTemplate.Contains("aa"))
                    {
                        char[] letters = cs2.Next();

                        call[aPosn + 0] = letters[1];
                        call[aPosn + 1] = letters[2];
                    }
                    else if (callTemplate.Contains('a'))
                    {
                        char[] letters = cs1.Next();

                        call[aPosn + 0] = letters[2];
                    }
                }

                string callString = call.ToString();

                if (CallCounts.ContainsKey(callString))
                {
                    if (++CallCounts[callString] == 2)
                    {
                        RepeatedCalls++;
                    }
                    RepeatedCallsTotal++;
                }
                else
                {
                    CallCounts.Add(callString, 1);
                }

                if (callString == "YS0CJW")
                {
                    // TODO:
                }

                return callString;
            }

            //internal CallEntry RandomCall()
            //{
            //    XmlElement dxccEl = null;

            //    while (dxccEl == null)
            //    {
            //        int index = random.Next(1, validDxccs.Count - 1);
            //        int dxcc = validDxccs[index];

            //        dxccEl = (XmlElement)testDxccsEl.SelectSingleNode(
            //            "dxccEntity[(@code='" + dxcc.ToString() + "') and not(@deleted)]");
            //    }
            //    string call = InstantiateCallTemplate(dxccEl.GetAttribute("callTemplate"), out DateTime qsoStartDateTime, out bool qsoStartHasTime);

            //    return SaveCallEntry(new CallEntry(call, dxccEl, qsoStartDateTime, qsoStartHasTime));
            //}


            /**
             * <summary>
             *   Creates a random callsign and returns it in a <see cref="CallEntry"/> object.
             *   These must not have an associated specific QSO start date because the XSLT templates that
             *   invoke this method are not expecting to have to emit a DXCC field and a historical start date;
             *   without those, the QSO will almost certainly fail to be acceptable to Club Log.
             * </summary>
             * 
             * <remarks>
             *   TODO: Avoiding callsigns with a specific QSO is done here by looping until one is found.  It would
             *   be "cleaner" instead to change <see cref="InstantiateCallTemplate(string, out DateTime, out bool)"/>
             *   to exclude those but for now, the code here works.
             * </remarks>
             */
            internal CallEntry RandomCall()
            {
                const int maxSkips = 100;

                int skipCount = 0;

                XmlElement dxccEl = null;
                string call = string.Empty;
                DateTime qsoStartDateTime = DateTime.MinValue;
                bool qsoHasStartTime = false;

                while (dxccEl == null)
                {
                    int index = random.Next(1, validDxccs.Count - 1);
                    int dxcc = validDxccs[index];

                    dxccEl = (XmlElement)testDxccsEl.SelectSingleNode(
                        "dxccEntity[(@code='" + dxcc.ToString() + "') and not(@deleted)]");

                    if (dxccEl != null)
                    {
                        call = InstantiateCallTemplate(dxccEl.GetAttribute("callTemplate"), out qsoStartDateTime, out qsoHasStartTime);
                        if (qsoStartDateTime != DateTime.MinValue)
                        {
                            // For random calls, the template is not expecting to have calls returned that have a
                            // specified QSO date/time, so try again.

                            if (skipCount++ == maxSkips)
                            {
                                // Avoid an infinite loop.

                                throw new AdifException($"Unable to select a random call after {skipCount} attempts");
                            }

                            dxccEl = null;
                        }
                    }
                }

                System.Diagnostics.Debug.Assert(qsoStartDateTime == DateTime.MinValue);

                return SaveCallEntry(new CallEntry(call, dxccEl, qsoStartDateTime, qsoHasStartTime));
            }

            internal CallEntry CallForDxcc(int dxcc, out DateTime qsoStartDateTime, out bool qsoHasStartTime)
            {
                string callTemplate;
                CallEntry callEntry;
                if (dxcc == 0)
                {
                    callEntry = new CallEntry(InstantiateCallTemplate("M0aaa/MM", out _, out _));
                    qsoStartDateTime = DateTime.MinValue;
                    qsoHasStartTime = false;
                }
                else
                {
                    XmlElement dxccEl = (XmlElement)testDxccsEl.SelectSingleNode(
                        "dxccEntity[(@code='" + dxcc.ToString() + "') and not(@deleted)]") ?? throw new Exception(string.Format(
                            "Internal error: Calls.CallForDxcc({0}): DXCC entity code not found or is deleted",
                            dxcc.ToString()));
                    callTemplate = dxccEl.GetAttribute("callTemplate");
                    //callEntry = new CallEntry(InstantiateCallTemplate(callTemplate), dxccEl);
                    string call = InstantiateCallTemplate(callTemplate, out qsoStartDateTime, out qsoHasStartTime);
                    callEntry = new CallEntry(
                        call,
                        dxccEl,
                        qsoStartDateTime,
                        qsoHasStartTime);
                }
                return SaveCallEntry(callEntry);
            }

            internal CallEntry CallForCont(string cont)
            {
                string callTemplate;

                XmlElement dxccEl = null;

                while (dxccEl == null)
                {
                    dxccEl = (XmlElement)testDxccsEl.SelectSingleNode(
                        "dxccEntity[(@continent='" + cont + "') and not(@deleted)]");

                    if (dxccEl == null)
                    {
                        throw new Exception($"Internal error: Calls.CallForCont(\"{StringToNullOrString(cont)}\"): Continent not found");
                    }
                }
                callTemplate = dxccEl.GetAttribute("callTemplate");
                string call = InstantiateCallTemplate(callTemplate, out DateTime qsoStartDateTime, out bool qsoHasStartTime);
                return SaveCallEntry(new CallEntry(
                    call,
                    dxccEl,
                    qsoStartDateTime, qsoHasStartTime));
            }

            internal CallEntry CallForPrimaryAdministrativeSubdivision(
                int dxcc,
                string primaryAdministrativeSubdivision,
                out DateTime qsoStartDateTime,
                out bool qsoHasStartTime)
            {
                string callTemplate;

                if (dxcc == 0)
                {
                    throw new Exception(string.Format(
                        "Calls.CallForPrimaryAdministrativeSubdivision({0}, \"{1}\"): Invalid DXCC",
                        StringToNullOrString(dxcc.ToString()),
                        primaryAdministrativeSubdivision));
                }

                XmlElement dxccEl = (XmlElement)testDxccsEl.SelectSingleNode(
                    "dxccEntity[(@code='" + dxcc.ToString() + "') and not(@deleted)]") ?? throw new Exception(string.Format(
                        "Calls.CallForPrimaryAdministrativeSubdivision({0}, \"{1}\"): DXCC entity not found or is deleted",
                        StringToNullOrString(dxcc.ToString()),
                        primaryAdministrativeSubdivision));

                // See if there is a template for the specific Primary Administrative Subdivision.
                // E.g. <pas code="NS" callTemplate="VE1aaa" />
                // Otherwise use the <dxccEntity> element's template.

                XmlElement pasEl = (XmlElement)dxccEl.SelectSingleNode(
                    "pas[(@code='" + primaryAdministrativeSubdivision + "') and not(@deleted)]");
                callTemplate = (pasEl ?? dxccEl).GetAttribute("callTemplate");
                string call = InstantiateCallTemplate(callTemplate, out qsoStartDateTime, out qsoHasStartTime);
                return SaveCallEntry(new CallEntry(
                    call,
                    dxccEl,
                    qsoStartDateTime,
                    qsoHasStartTime));
            }

            private CallEntry SaveCallEntry(CallEntry callEntry)
            {
                callEntries.TryAdd(callEntry.Call, callEntry);
                return callEntry;
            }

            internal CallEntry Previous(string call)
            {
                _ = callEntries.TryGetValue(call, out CallEntry callEntry);
                return callEntry;
            }
        }

        private partial class DataTypeEntry
        {
#pragma warning disable format, IDE0055
            private const string
                AdifVerFieldName =                                  "ADIF_VER",
                CreatedTimeStampFieldName =                         "CREATED_TIMESTAMP",

                CharacterDataType =                                 "CHARACTER",
                StringDataType =                                    "STRING",
                IntlStringDataType =                                "INTLSTRING",
                MultilineStringDataType =                           "MULTILINESTRING",
                IntlMultilineStringDataType =                       "INTLMULTILINESTRING",
                AwardListImportOnlyDataType =                       "AWARDLIST IMPORT-ONLY",
                CreditListDataType =                                "CREDITLIST",
                SponsoredAwardListDataType =                        "SPONSOREDAWARDLIST",
                BooleanDataType =                                   "BOOLEAN",
                DigitDataType =                                     "DIGIT",
                IntegerDataType =                                   "INTEGER",
                NumberDataType =                                    "NUMBER",
                PositiveIntegerDataType =                           "POSITIVEINTEGER",
                DateDataType =                                      "DATE",
                TimeDataType =                                      "TIME",
                IotaRefNoDataType =                                 "IOTAREFNO",
                EnumerationDataType =                               "ENUMERATION",
                GridSquareDataType =                                "GRIDSQUARE",
                GridSquareExtDataType =                             "GRIDSQUAREEXT",
                GridSquareListDataType =                            "GRIDSQUARELIST",
                LocationDataType =                                  "LOCATION",
                PotaRefListDataType =                               "POTAREFLIST",
                SecondarySubdivisionListDataType =                  "SECONDARYSUBDIVISIONLIST",
                SecondaryAdministrativeSubdivisionListAltDataType = "SECONDARYADMINISTRATIVESUBDIVISIONLISTALT",
                SotaRefDataType =                                   "SOTAREF",
                WwffRefDataType =                                   "WWFFREF",

                SecondaryAdministrativeSubdivisionAltEnumeration =  "SECONDARY_ADMINISTRATIVE_SUBDIVISION_ALT";
#pragma warning restore format, IDE0055

            private static readonly char[]
                ampersandSplitChar = ['&'],
                colonSplitChar = [':'],
                commaSplitChar = [','],
                nullSplitChar = ['\0'];

            internal AdifXslt AdifXslt;  // Needed to access the enumerations dictionary.

            internal string Name;
            internal char DataTypeIndicator;
            internal double MinimumValue;
            internal double MaximumValue;

            private EnumerationEntry
                continentEnumeration = null,
                creditEnumeration = null,
                qslMediumEnumeration = null,
                awardSponsorEnumeration = null;

            internal DataTypeEntry(AdifXslt adifXslt, string name, char dataTypeIndicator, double minimumValue, double maximumValue)
            {
                AdifXslt = adifXslt;
                Name = name.ToUpper();
                DataTypeIndicator = dataTypeIndicator;
                MinimumValue = minimumValue;
                MaximumValue = maximumValue;
            }

            internal static DataTypeEntry GetDataTypeEntry(Dictionary<string, DataTypeEntry> dataTypes, char dataTypeIndicator)
            {
                DataTypeEntry dataTypeEntry = null;

                if (dataTypeIndicator == 'S')
                {
                    // "S" will always return the "CHARACTER" data type due to it sharing "S" with "STRING",
                    // so always treat "S" as "STRING" rather than "CHARACTER".

                    dataTypes.TryGetValue(StringDataType, out dataTypeEntry);
                }
                else if (dataTypeIndicator == 'I')
                {
                    // "I" will always return the "INTLCHARACTER" data type due to it sharing "I" with "INTLSTRING",
                    // so always treat "I" as "INTLSTRING" rather than "INTLCHARACTER".

                    dataTypes.TryGetValue(IntlStringDataType, out dataTypeEntry);
                }
                else if (dataTypeIndicator == 'N')
                {
                    // "N" will always return the "DIGIT" data type due to it sharing "I" with "NUMBER",
                    // so always treat "N" as "NUMBER" rather than "INTLCHARACTER".

                    dataTypes.TryGetValue(NumberDataType, out dataTypeEntry);
                }
                else
                {
                    foreach (DataTypeEntry dte in dataTypes.Values)
                    {
                        if (dataTypeIndicator == dte.DataTypeIndicator)
                        {
                            dataTypeEntry = dte;
                            break;
                        }
                    }
                }
                if (dataTypeIndicator == (char)0 || dataTypeEntry == null)
                {
                    throw new Exception(string.Format(
                        "Data Type Indicator '{0}' does not exist",
                        dataTypeIndicator));
                }
                return dataTypeEntry;
            }

            private bool IsValidContinent(string continent)
            {
                continentEnumeration ??= AdifXslt.enumerations["CONTINENT"];
                continentEnumeration.Validate(continent);  // This throws an exception if the Validate() fails.
                return true;
            }

            private static readonly Regex
                AdifVerRegex =
                    _AdifVerRegex(),
                PotaRefRegex =
                    _PotaRefRegex(),
                CreatedTimestampRegex =
                    _CreatedTimestampRegex();

            /**
             * <summary>
             *   Checks whether a field contains a value allowed by the field's data type.
             * </summary>
             * 
             * <param name="fieldName">The field's name.</param>
             * <param name="value">The field's value.</param>
             * <param name="enumeration">The enumeration containing the field's allowed values.</param>
             * <param name="adiStyle">Whether it is an ADI or ADX file being created.</param>
             */
            internal void ValidateValue(
                string fieldName,
                string value,
                string enumeration,
                bool adiStyle)
            {
                // This is called from the FieldEntry class only.

                string error = string.Empty;

                switch (Name)
                {
                    case CharacterDataType:
                    case StringDataType:
                    case IntlStringDataType:
                    case MultilineStringDataType:
                    case IntlMultilineStringDataType:
                        bool
                            character = Name == CharacterDataType,
                            _string = Name == StringDataType,
                            intlString = Name == IntlStringDataType,
                            multlineString = Name == MultilineStringDataType,
                            intlMultlineString = Name == IntlMultilineStringDataType;

                        bool valid = false;

                        if ((adiStyle) &&
                            (intlString || intlMultlineString))
                        {
                            error = string.Format(
                                "data type '{0}' is not allowed in an ADI (.adi) file",
                                Name);
                        }
                        else if (character || _string || intlString || multlineString || intlMultlineString)
                        {
                            foreach (char c in value)
                            {
                                if (character)
                                {
                                    valid = c >= 32 && c <= 126 && value.Length == 1;
                                }
                                else if (_string)
                                {
                                    valid = c >= 32 && c <= 126;
                                }
                                else if (intlString)
                                {
                                    valid = c != '\r' && c != '\n';
                                }
                                else if (multlineString)
                                {
                                    valid = (c >= 32 && c <= 126) || c == '\r' || c == '\n';
                                }
                                else if (intlMultlineString)
                                {
                                    valid = true;
                                }
                                else
                                {
                                    error = string.Format(
                                        "Internal error in DataTypeEntry.ValidateValue() validating character data: unexpected data type '{0}'",
                                        Name);
                                }
                                if (!valid)
                                {
                                    error = string.Format(
                                        "Character '{0}' (decimal {1}, hex {2} is not allowed in a field of type {3}",
                                        c,
                                        ((int)c).ToString(),
                                        ((int)c).ToString("x"),
                                        Name);
                                }
                            }
                        }
                        switch(fieldName)
                        {
                            case AdifVerFieldName:
                                if (!AdifVerRegex.IsMatch(value))
                                {
                                    throw new AdifValidationException(string.Format(
                                        "{0} contains an invalid ADIF version in a field of type {1}",
                                        value,
                                        Name));
                                }
                                break;

                            case CreatedTimeStampFieldName:
                                if (!CreatedTimestampRegex.IsMatch(value))
                                {
                                    throw new AdifValidationException(string.Format(
                                        "{0} contains an invalid date and time in a field of type {1}",
                                        value,
                                        Name));
                                }
                                break;

                            default:
                                break;
                        }
                        break;

                    case AwardListImportOnlyDataType:
                        break;

                    case CreditListDataType:
                        {
                            List<string> creditItemsChecklist = new(32);
                            string[] creditItems = value.Split(commaSplitChar);

                            if (creditEnumeration == null || qslMediumEnumeration == null)
                            {
                                creditEnumeration = AdifXslt.enumerations["CREDIT"];
                                qslMediumEnumeration = AdifXslt.enumerations["QSL_MEDIUM"];
                            }

                            foreach (string creditItem in creditItems)
                            {
                                List<string> mediumItemsChecklist = new(3);
                                string[] creditPair = creditItem.Split(colonSplitChar);

                                if (creditPair.Length < 1 || creditPair.Length > 2)
                                {
                                    throw new  AdifValidationException(string.Format(
                                        "Value '{0}' is not a valid in a {1} field",
                                        StringToNullOrString(value),
                                        Name));
                                }

                                creditEnumeration.Validate(creditPair[0].ToUpper());

                                if (creditItemsChecklist.Contains(creditPair[0]))
                                {
                                    throw new AdifValidationException(string.Format(
                                        "Value '{0}' contains more than one occurrence of Credit {1} in a {2} field",
                                        StringToNullOrString(value),
                                        StringToNullOrString(creditPair[0]),
                                        Name));
                                }
                                else
                                {
                                    creditItemsChecklist.Add(creditPair[0].ToUpper());
                                }

                                if (creditPair.Length == 2)
                                {
                                    string[] qslMedia = creditPair[1].Split(ampersandSplitChar);

                                    foreach (string qslMedium in qslMedia)
                                    {
                                        qslMediumEnumeration.Validate(qslMedium);

                                        if (mediumItemsChecklist.Contains(qslMedium.ToUpper()))
                                        {
                                            throw new AdifValidationException(string.Format(
                                                "Value '{0}' contains more than one occurrence of QSL_Medium {1} in a {2} field",
                                                StringToNullOrString(value),
                                                StringToNullOrString(qslMedium),
                                                Name));
                                        }
                                        else
                                        {
                                            mediumItemsChecklist.Add(qslMedium.ToUpper());
                                        }
                                    }
                                }
                            }
                        }
                        break;

                    case SponsoredAwardListDataType:
                        {
                            string[] awards = value.Split(commaSplitChar);

                            awardSponsorEnumeration ??= AdifXslt.enumerations["AWARD_SPONSOR"];

                            foreach (string award in awards)
                            {
                                StringBuilder nullDelimitedAward = new(128);
                                int partNo = 0;

                                foreach (char c in award)
                                {
                                    nullDelimitedAward.Append(c == '_' && partNo++ < 2 ?
                                        '\0' :
                                        char.ToUpper(c));
                                }

                                string[] parts = nullDelimitedAward.ToString().Split(nullSplitChar);

                                if (parts.Length < 3)
                                {
                                    ReportError(nullDelimitedAward.ToString());
                                    error = string.Format(
                                        "Award '{0}' does not have 3 parts separated by '_' characters so is not allowed in a field of type {1}",
                                        StringToNullOrString(award),
                                        Name);
                                }
                                else
                                {
                                    foreach (string part in parts)
                                    {
                                        if (part.Length == 0)
                                        {
                                            error = string.Format(
                                                "Award '{0}' with an empty part is not allowed in a field of type {1}",
                                                StringToNullOrString(award),
                                                Name);
                                            break;
                                        }
                                    }

                                    if (error.Length == 0)
                                    {
                                        awardSponsorEnumeration.Validate(parts[0].ToUpper() + "_");
                                    }
                                }
                            }
                        }
                        break;

                    case BooleanDataType:
                        {
                            char c = value.Length == 1 ?
                                char.ToUpper(value[0]) :
                                '\0';

                            if (c != 'Y' && c != 'N')
                            {
                                error = string.Format(
                                    "'{0}' is not allowed in a field of type {1}",
                                    StringToNullOrString(value),
                                    Name);
                            }
                            break;
                        }

                    case DigitDataType:
                        if (value.Length != 1 ||
                            (value[0] < '0' || value[0] > '9'))
                        {
                            error = string.Format(
                                "'{0}' is not allowed in a field of type {1}",
                                StringToNullOrString(value),
                                Name);
                        }
                        break;

                    case IntegerDataType:
                        try
                        {
                            int.Parse(value, adifNumberStyles, adifNumberFormatInfo);
                        }
                        catch
                        {
                            error = string.Format(
                                "'{0}' is not allowed in a field of type {1}",
                                value,
                                Name);
                        }
                        break;

                    case NumberDataType:
                        try
                        {
                            float.Parse(value, adifNumberStyles, adifNumberFormatInfo);
                        }
                        catch
                        {
                            error = string.Format(
                                "'{0}' is not allowed in a field of type {1}",
                                value,
                                Name);
                        }
                        break;

                    case PositiveIntegerDataType:
                        try
                        {
                            int iValue = int.Parse(value, adifNumberStyles, adifNumberFormatInfo);

                            if (MinimumValue != double.MinValue && iValue < (int)MinimumValue)
                            {
                                throw new AdifValidationException(string.Format(
                                    "Value {0} is < {1}",
                                    value,
                                    MinimumValue.ToString()));
                            }
                            if (MaximumValue != double.MaxValue && iValue > (int)MaximumValue)
                            {
                                throw new AdifValidationException(string.Format(
                                    "Value {0} is > {1}",
                                    value,
                                    MaximumValue.ToString()));
                            }
                        }
                        catch (Exception exc)
                        {
                            error = string.Format(
                                "'{0}' is not allowed in a field of type {1} ({2})",
                                value,
                                Name,
                                exc.Message ?? string.Empty);
                        }
                        break;

                    case DateDataType:
                        try
                        {
                            _ = new DateTime(
                                int.Parse(value[..4]),
                                int.Parse(value.Substring(4, 2)),
                                int.Parse(value.Substring(6, 2)));
                        }
                        catch
                        {
                            error = string.Format(
                                "'{0}' is not allowed in a field of type {1}",
                                value,
                                Name);
                        }
                        break;

                    case TimeDataType:
                        try
                        {
                            _ = new DateTime(
                                1900,
                                01,
                                01,
                                int.Parse(value[..2]),
                                int.Parse(value.Substring(2, 2)),
                                value.Length == 4 ?
                                    00 :
                                    int.Parse(value.Substring(4, 2)));
                        }
                        catch
                        {
                            error = string.Format(
                                "'{0}' is not allowed in a field of type {1}",
                                value,
                                Name);
                        }
                        break;


                    case IotaRefNoDataType:
                        {
                            if (value.Length != 6 ||
                                (!IsValidContinent(value[..2])) ||
                                value[2] != '-' ||
                                (!int.TryParse(value[3..], out _)))
                            {
                                error = string.Format(
                                    "'{0}' is not allowed in a field of type {1}",
                                    value,
                                    Name);
                            }
                        }
                        break;

                    case EnumerationDataType:
                        if ((!string.IsNullOrEmpty(enumeration)) &&
                             enumeration[0] != '{' &&
                             enumeration != "PRIMARY_ADMINISTRATIVE_SUBDIVISION")
                        {
                            if (!AdifXslt.enumerations.TryGetValue(enumeration, out EnumerationEntry enumerationEntry))
                            {
                                throw new AdifValidationException(string.Format(
                                    "Internal error: enumeration '{0}' does not exist",
                                    enumeration));
                            }
                            enumerationEntry.Validate(value);
                        }
                        break;

                    case GridSquareDataType:
                        if (!IsValidLocator(value))
                        {
                            error = string.Format(
                                "'{0}' is not valid for a field of type {1}",
                                value,
                                Name);
                        }
                        break;

                    case GridSquareExtDataType:
                        if (!IsValidLocatorExt(value))
                        {
                            error = string.Format(
                                "'{0}' is not valid for a field of type {1}",
                                value,
                                Name);
                        }
                        break;

                    case GridSquareListDataType:
                        {
                            string[] locators = value.Split(commaSplitChar, StringSplitOptions.None);

                            foreach (string locator in locators)
                            {
                                if (!IsValidLocator(locator))
                                {
                                    error = string.Format(
                                    "value '{0}' in '{1}' is not valid for a field of type {2}",
                                    locator,
                                    value,
                                    Name);
                                    break;
                                }
                            }
                        }
                        break;

                    case LocationDataType:
                        if (value.Length != 11)
                        {
                            throw new AdifValidationException(string.Format(
                                "'{0}' must be 11 characters long in a field of type {1}",
                                value,
                                Name));
                        }
                        {
                            char cardinalPoint = char.ToUpperInvariant(value[0]);
                            string ddd = value.Substring(1, 3);
                            string mmmmmm = value.Substring(5, 6);
                            char space = value[4];
                            char dot = value[7];

                            //double asNumber;
                            //bool   latitude;                            

                            if (space != ' ')
                            {
                                throw new AdifValidationException(string.Format(
                                    "'{0}' does not have a space (' ') in the 5th character position in a field of type {1}",
                                    value,
                                    Name));
                            }
                            if (dot != '.')
                            {
                                throw new AdifValidationException(string.Format(
                                    "'{0}' does not have a full stop ('.') in the 8th character position in a field of type {1}",
                                    value,
                                    Name));
                            }
                            if ((!int.TryParse(
                                    ddd,
                                    NumberStyles.None,
                                    NumberFormatInfo.InvariantInfo,
                                    out int degrees)) ||
                                (!double.TryParse(
                                    mmmmmm, NumberStyles.AllowDecimalPoint,
                                    NumberFormatInfo.InvariantInfo,
                                    out double minutes)))
                            {
                                throw new AdifValidationException(string.Format(
                                    "'{0}' does not have a valid unsigned decimal number in the character positions 2-7 in a field of type {1}",
                                    value,
                                    Name));
                            }
                            {
                                switch (cardinalPoint)
                                {
                                    case 'N':
                                    case 'S':
                                        //latitude = true;
                                        if (degrees <= 90 &&
                                            ((degrees == 90 && minutes == 0d) ||
                                              minutes < 60d))
                                        {
                                            //asNumber = degrees + minutes / 60d;
                                            //if (cardinalPoint == 'S')
                                            //{
                                            //    asNumber = -asNumber;
                                            //}
                                        }
                                        else
                                        {
                                            throw new AdifValidationException(string.Format(
                                                "'{0}' does not have a valid unsigned decimal number for latitude in the character positions 2-7 in a field of type {1}",
                                                value,
                                                Name));
                                        }
                                        break;
                                    case 'E':
                                    case 'W':
                                        //latitude = false;
                                        if (degrees <= 180 &&
                                            ((degrees == 180 && minutes == 0d) ||
                                              minutes < 60d))
                                        {
                                            //asNumber = degrees + minutes / 60d;

                                            //if (cardinalPoint == 'W')
                                            //{
                                            //    asNumber = -asNumber;
                                            //}
                                            //if (asNumber == -180d)
                                            //{
                                            //    asNumber = 180d;
                                            //}
                                        }
                                        else
                                        {
                                            throw new AdifValidationException(string.Format(
                                                "'{0}' does not have a valid unsigned decimal number for longitude in the character positions 2-7 in a field of type {1}",
                                                value,
                                                Name));
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        break;

                    case PotaRefListDataType:
                        {
                            /*
                                PotaRefList:
                                    a comma-delimited list of one or more POTARef items.
                             
                                PotaRef:
                                    a sequence of case-insensitive Characters representing a Parks on the Air park reference in the form xxxx-nnnnn[@yyyyyy] comprising 6 to 17 characters where: 
                                    •xxxx is the POTA national program and is 1 to 4 characters in length, typically the default callsign prefix of the national program (rather than the DX entity)
                                    •nnnnn represents the unique number within the national program and is either 4 or 5 characters in length (use the exact format listed on the POTA website)
                                    •yyyyyy **Optional** is the 4 to 6 character ISO 3166-2 code to differentiate which state/province/prefecture/primary administration location the contact represents, in the case that the park reference spans more than one location (such as a trail). 
                             */

                            string[] potaRefs = value.Split([','], StringSplitOptions.RemoveEmptyEntries);

                            foreach (string potaRef in potaRefs)
                            {
                                if (!PotaRefRegex.IsMatch(potaRef))
                                {
                                    throw new AdifValidationException(string.Format(
                                        "{0} contains an invalid POTA reference \"{2}\" in a field of type {1}",
                                        value,
                                        Name,
                                        potaRef));
                                }
                            }
                        }
                        break;

                    case SecondarySubdivisionListDataType:
                        break;

                    case SecondaryAdministrativeSubdivisionListAltDataType:
                        /*
                            Subdivision codes are in the form: 
                                enumeration-name1:enumeration-code1;enumeration-name2:enumeration-code2 ...

                            At the time of writing (October 2024), the only enumeration-name allowed is NZ_Regions.

                            Here is a list of QSO records that can be incorporated in the XSLT file to test the
                            error-checking.  Only the first one is valid:

                            <xsl:value-of select="ae:record('CNTY_ALT', 'NZ_Regions:Northland/Far North')"/>
                            <xsl:value-of select="ae:record('CNTY_ALT', 'NZ_Regions:Northland/Nowhere')"/>
                            <xsl:value-of select="ae:record('CNTY_ALT', 'NZ_Regions:Northland/Far North;NZ_Regions:Hawkes Bay/Wairoa')"/>
                            <xsl:value-of select="ae:record('CNTY_ALT', 'NZ_Regions:Northland')"/>
                            <xsl:value-of select="ae:record('CNTY_ALT', 'NZ_Regions:abc/def')"/>
                            <xsl:value-of select="ae:record('CNTY_ALT', 'NZ_Islands:North Island')"/>
                         */

                        // The dictionary is used to check that each enumeration-name in a record is unique.

                        Dictionary<string, string> includedEnumerationNames = new(
                            16,
                            StringComparer.OrdinalIgnoreCase);
                        string[] subdivisionCodes = value.Split(';');

                        foreach (string subdivisionCode in subdivisionCodes)
                        {
                            // E.g. subdivisionCode "NZ_Regions:Northland/Far North"

                            string[] subdivisionCodeParts = subdivisionCode.Split(':');

                            if (subdivisionCodeParts.Length != 2 ||
                                subdivisionCodeParts[0].Length == 0 ||
                                subdivisionCodeParts[1].Length == 0)
                            {
                                throw new AdifValidationException(
                                    $"{value} contains an invalid subdivision code \"{subdivisionCode}\" in a field of type {Name}");
                            }

                            string
                                enumerationName = subdivisionCodeParts[0],  // E.g. "NZ_Regions"
                                enumerationCode = subdivisionCodeParts[1];  // E.g. "Northland/Far North"

                            if (!AdifXslt.enumerations.TryGetValue(
                                SecondaryAdministrativeSubdivisionAltEnumeration,
                                out EnumerationEntry enumerationEntry))
                            {
                                throw new AdifValidationException(
                                    $"Enumeration \"{SecondaryAdministrativeSubdivisionAltEnumeration}\" not found for a field of type {Name}");
                            }

                            if (!enumerationEntry.Values.TryGetValue(subdivisionCode.ToUpper(), out _))
                            {
                                throw new AdifValidationException(
                                    $"{value} contains an invalid {enumerationName} subdivision code \"{subdivisionCode}\" in a field of type {Name}");
                            }

                            if (includedEnumerationNames.TryGetValue(enumerationName, out _))
                            {
                                throw new AdifValidationException(
                                    $"{value} contains more than one enumeration-name \"{enumerationName}\" in a field of type {Name}");
                            }

                            includedEnumerationNames.Add(enumerationName, enumerationName);
                        }
                        break;

                    case SotaRefDataType:
                        {
                            // This would be a lot shorter and tidier using a single regular expression, although that would be slower
                            // and the exception messages would be less specific.

                            int slashPosition = value.IndexOf('/');

                            if (slashPosition < 1)
                            {
                                throw new AdifValidationException(string.Format(
                                    "{0} does not have a forward slash (/) in the the 2nd or later character position in a field of type {1}",
                                    value,
                                    Name));
                            }
                            if (slashPosition == value.Length - 1)
                            {
                                throw new AdifValidationException(string.Format(
                                    "{0} cannot have a forward slash (/) in the last character position in a field of type {1}",
                                    value,
                                    Name));
                            }
                            string referenceNumber = value[(slashPosition + 1)..];

                            if (referenceNumber.Length != 6 ||
                                referenceNumber[2] != '-' ||
                                (!char.IsLetter(referenceNumber[0])) ||
                                (!char.IsLetter(referenceNumber[1])) ||
                                (!int.TryParse(referenceNumber[3..], out int _)))
                            {
                                throw new AdifValidationException(string.Format(
                                    "{0} does not contain a valid SOTA Reference Number in the righthand 6 characters in a field of type {1}",
                                    referenceNumber,
                                    Name));
                            }
                        }
                        break;

                    case WwffRefDataType:
                        {
                            /*
                                a sequence of case-insensitive Characters representing an International WWFF (World Wildlife Flora & Fauna) reference in the form xxFF-nnnn comprising 8 to 11 characters where:

                                    xx is the WWFF national program and is 1 to 4 characters in length.
                                    FF- is two F characters followed by a dash character.
                                    nnnn represents the unique number within the national program and is 4 characters in length with leading zeros.
                            */

                            // This would be a lot shorter and tidier using a single regular expression, although that would be slower
                            // and the exception messages would be less specific.

                            string[] parts = value.Split(['-'], StringSplitOptions.RemoveEmptyEntries);

                            if (parts.Length != 2)
                            {
                                throw new AdifValidationException(string.Format(
                                    "{0} does not have two parts separated by a dash (-) in a field of type {1}",
                                    value,
                                    Name));
                            }
                            else if (parts[0].Length < 3 || parts[0].Length > 6)
                            {
                                throw new AdifValidationException(string.Format(
                                    "{0} length to the left of the dash (-) is not within the range 3 to 6 characters in a field of type {1}",
                                    value,
                                    Name));
                            }
                            else if (!(parts[0].Substring(parts[0].Length - 2, 2).Equals("FF", StringComparison.OrdinalIgnoreCase)))
                            {
                                throw new AdifValidationException(string.Format(
                                    "{0} final two characters to the left of the dash (-) are not FF in a field of type {1}",
                                    value,
                                    Name));
                            }
                            else if (parts[1].Length != 4)
                            {
                                throw new AdifValidationException(string.Format(
                                    "{0} there are not four characters the right of the dash (-) in a field of type {1}",
                                    value,
                                    Name));
                            }
                            else
                            {
                                foreach (char c in parts[1])
                                {
                                    if (c < '0' || c > '9')
                                    {
                                        throw new AdifValidationException(string.Format(
                                            "{0} the characters to the right of the dash (-) are not all digits (0-9) in a field of type {1}",
                                            value,
                                            Name));
                                    }
                                }
                            }
                        }
                        break;

                    default:
                        error = string.Format(
                            "Internal error in DataTypeEntry.ValidateValue(): unexpected data type '{0}'",
                            Name);
                        break;
                }
                if (error.Length > 0)
                {
                    throw new AdifException(error);
                }
            }

            private static bool IsValidLocator(string locator)
            {
                // Checks whether a locator appears to be valid

                bool valid = true;

                locator = locator.ToUpper();

                if (locator.Length == 0 || locator.Length % 2 != 0 || locator.Length > 8)
                {
                    // Locators must have an even number of characters between 2 and 8
                    // LLDDLLDD where:
                    //     first pair of letters are A-R and second pair of letters are A-X
                    //     digits are all 0-9
                    valid = false;
                }
                else
                {
                    int position = 0;

                    char a = locator[position++],
                         b = locator[position++];

                    if (a < 'A' || a > 'R' || b < 'A' || b > 'R')
                    {
                        valid = false;
                    }
                    else if (locator.Length > position)
                    {
                        a = locator[position++];
                        b = locator[position++];

                        if (a < '0' || a > '9' || b < '0' || b > '9')
                        {
                            valid = false;
                        }
                        else if (locator.Length > position)
                        {
                            a = locator[position++];
                            b = locator[position++];

                            if (a < 'A' || a > 'X' || b < 'A' || b > 'X')
                            {
                                valid = false;
                            }
                            else if (locator.Length > position)
                            {
                                a = locator[position++];
                                b = locator[position++];

                                if (a < '0' || a > '9' || b < '0' || b > '9')
                                {
                                    valid = false;
                                }
                            }
                        }
                    }
                }

                return valid;
            }

            private static bool IsValidLocatorExt(string locatorExt)
            {
                // Checks whether a locator extension appears to be valid

                bool valid = true;

                locatorExt = locatorExt.ToUpper();

                if (locatorExt.Length != 2 && locatorExt.Length != 4)
                {
                    // Locator extensions must have 2 or 4 characters
                    // LLDD where:
                    //     first  pair are A-X
                    //     second pair are 0-9
                    valid = false;
                }
                else
                {
                    int position = 0;

                    char a = locatorExt[position++],
                         b = locatorExt[position++];

                    if (a < 'A' || a > 'X' || b < 'A' || b > 'X')
                    {
                        valid = false;
                    }
                    else if (locatorExt.Length > position)
                    {
                        a = locatorExt[position++];
                        b = locatorExt[position++];

                        if (a < '0' || a > '9' || b < '0' || b > '9')
                        {
                            valid = false;
                        }
                    }
                }

                return valid;
            }


            [GeneratedRegex(@"3\.[0-9]\.[0-9]")]
            private static partial Regex _AdifVerRegex();


            [GeneratedRegex(@"[a-zA-Z0-9]{1,4}\-[0-9]{4,5}(@[a-zA-Z]{2}\-[a-zA-Z0-9]{1,3})?(,[a-zA-Z0-9]{1,4}\-[0-9]{4,5}(@[a-zA-Z]{2}\-[a-zA-Z0-9]{1,3})?)*")]
            private static partial Regex _PotaRefRegex();


            [GeneratedRegex(@"(19[3-9][0-9]|[2-9][0-9]{3})(0[1-9]|1[0-2])(0[1-9]|[1-2][0-9]|[3][0-1]) ([0-1][0-9]|2[0-3])([0-5][0-9]){2}")]
            private static partial Regex _CreatedTimestampRegex();
        }

        private class FieldEntry
        {
            internal enum FieldVariant
            {
                Adif,   // A header or record field defined in the ADIF specification, including USERDEFn fields.
                App,    // An APP_ field.
                User,   // A USERDEF field.
            }

            private static readonly char[]
                commaSplitChar = [','],
                colonSplitChar = [':'];

            internal string Name;
            internal bool Header;
            internal DataTypeEntry DataType;
            internal FieldVariant Variant;
            internal int UserDefNumber;
            internal string Enumeration;
            internal List<string> EnumStrings;
            internal float EnumMin;
            internal float EnumMax;
            internal double MinimumValue;
            internal double MaximumValue;
            internal int Occurrences;
            internal string LastValue;

            // Called for USERDEFn fields.
            internal FieldEntry(
                Dictionary<string, DataTypeEntry> dataTypes,
                string name,
                bool header,
                char dataTypeIndicator,
                int userDefNumber,
                string enumerationOrRange)
            {
                this.Name = name;
                this.Header = header;
                this.DataType = DataTypeEntry.GetDataTypeEntry(dataTypes, dataTypeIndicator);
                this.Variant = FieldVariant.User;
                this.UserDefNumber = userDefNumber;
                this.Enumeration = enumerationOrRange.ToUpper();
                this.EnumStrings = [];
                this.EnumMin = float.MinValue;
                this.EnumMax = float.MaxValue;
                this.MinimumValue = double.MinValue;
                this.MaximumValue = double.MaxValue;
                this.Occurrences = 0;
                this.LastValue = string.Empty;

                if (enumerationOrRange.Length > 0)
                {
                    //if (DataType.Name != "ENUMERATION")
                    //{
                    //    throw new Exception("USERDEFn field has an enumeration without Data Type Indicator E");
                    //}
                    enumerationOrRange = enumerationOrRange.ToUpper().Substring(1, enumerationOrRange.Length - 2);

                    if (enumerationOrRange.Contains(','))
                    {
                        if (DataType.Name != "ENUMERATION")
                        {
                            throw new Exception("USERDEFn field has an enumeration without Data Type Indicator E");
                        }

                        EnumStrings = [.. enumerationOrRange.Split(commaSplitChar, StringSplitOptions.RemoveEmptyEntries)];
                    }
                    else
                    {
                        if (DataType.Name != "NUMBER")
                        {
                            throw new Exception("USERDEFn field has a range without Data Type Indicator E");
                        }

                        string[] values = enumerationOrRange.Split(colonSplitChar, StringSplitOptions.RemoveEmptyEntries);

                        EnumMin = float.Parse(values[0], adifNumberStyles, adifNumberFormatInfo);
                        EnumMax = float.Parse(values[1], adifNumberStyles, adifNumberFormatInfo);
                    }
                }
                else
                {
                    if (DataType.Name == "ENUMERATION")
                    {
                        throw new Exception("USERDEFn Data Type Indicator cannot be 'E' without an enumeration");
                    }
                }
            }

            // Called for APP_ fields.
            internal FieldEntry(
                Dictionary<string, DataTypeEntry> dataTypes,
                string name,
                bool header,
                char dataTypeIndicator)
            {
                this.Name = name;
                this.Header = header;
                this.DataType = DataTypeEntry.GetDataTypeEntry(dataTypes, dataTypeIndicator);
                this.Variant = FieldVariant.App;
                this.UserDefNumber = 0;
                this.Enumeration = string.Empty;
                this.EnumStrings = [];
                this.EnumMin = float.MinValue;
                this.EnumMax = float.MaxValue;
                this.MinimumValue = double.MinValue;
                this.MaximumValue = double.MaxValue;
                this.Occurrences = 0;
                this.LastValue = string.Empty;
            }

            // Called by AdifXslt constructor for ADIF-defined fields
            internal FieldEntry(
                Dictionary<string, DataTypeEntry> dataTypes,
                string name,
                bool header,
                string dataType,
                string enumeration,
                double minimumValue,
                double maximumValue)
            {
                this.Name = name;
                this.Header = header;
                this.DataType = null;
                this.Variant = FieldVariant.Adif;
                this.UserDefNumber = 0;
                this.Enumeration = enumeration;
                this.EnumStrings = [];
                this.EnumMin = float.MinValue;
                this.EnumMax = float.MaxValue;
                this.MinimumValue = minimumValue;
                this.MaximumValue = maximumValue;
                this.Occurrences = 0;
                this.LastValue = string.Empty;

                string[] dataTypeParts = dataType.Split(commaSplitChar, StringSplitOptions.RemoveEmptyEntries);

                if (dataTypeParts.Length == 0 || dataTypeParts[0].Length == 0)
                {
                    throw new Exception(string.Format(
                        "field '{0}' has a data type {1}, which does not exist",
                        StringToNullOrString(name),
                        StringToNullOrString(dataType)));
                }

                if (!dataTypes.TryGetValue(dataTypeParts[0], out this.DataType))
                {
                    throw new Exception(string.Format(
                        "field '{0}' has an unrecognized data type '{1}'",
                        StringToNullOrString(name),
                        StringToNullOrString(dataType)));
                }
            }

            internal bool CheckValidUserDefEnumerationValue(string value)
            {
                bool valid = true;

                if (Enumeration.Length > 0)
                {
                    if (EnumStrings.Count > 0)
                    {
                        if (!EnumStrings.Contains(value.ToUpper()))
                        {
                            throw new Exception(string.Format(
                                "USERDEFn field {0} does not allow the value {1} in its enumeration {2}",
                                Name,
                                value,
                                Enumeration));
                        }
                    }
                    else
                    {
                        if ((!float.TryParse(value, out float valueFloat)) ||
                            valueFloat < EnumMin || valueFloat > EnumMax)
                        {
                            throw new Exception(string.Format(
                                "USERDEFn field {0} does not allow the value {1} in its enumeration {2}",
                                Name,
                                value,
                                Enumeration));
                        }
                    }
                }
                return valid;
            }

            internal void ValidateValue(string value, bool adiStyle)
            {
                // Firstly, validate the value according to the Data Type, then check for any field-specific validation
                // (e.g. for the Lat field, check that the cardinal point letter is 'N' or 'S').

                DataType.ValidateValue(Name, value, Enumeration, adiStyle);

                if (MinimumValue != double.MinValue || MaximumValue != double.MaxValue)
                {
                    if (!double.TryParse(value, adifNumberStyles, adifNumberFormatInfo, out double dValue))
                    {
                        throw new AdifValidationException(string.Format(
                            "Value '{0}' is not allowed in a {1} field that has a defined minimum and / or maximum value",
                            value,
                            Name));
                    }
                    else if (MinimumValue != double.MinValue && dValue < MinimumValue)
                    {
                        throw new AdifValidationException(string.Format(
                            "Value {0} is not allowed because the minimum value in a {1} field is {2}",
                            value,
                            Name,
                            MinimumValue.ToString()));
                    }
                    else if (MaximumValue != double.MaxValue && dValue > (int)MaximumValue)
                    {
                        throw new AdifValidationException(string.Format(
                            "Value {0} is not allowed because the maximum value in a {1} field is {2}",
                            value,
                            Name,
                            MaximumValue.ToString()));
                    }
                }

                switch (Name)
                {
                    case "LAT":
                    case "MY_LAT":
                        {
                            char cardinalPoint = char.ToUpper(value[0]);

                            if (cardinalPoint != 'N' && cardinalPoint != 'S')
                            {
                                throw new Exception(string.Format(
                                    "a {0} field must have a first character of 'S' or 'N'",
                                    Name));
                            }
                        }
                        break;

                    case "LON":
                    case "MY_LON":
                        {
                            char cardinalPoint = char.ToUpper(value[0]);

                            if (cardinalPoint != 'E' && cardinalPoint != 'W')
                            {
                                throw new Exception(string.Format(
                                    "a {0} field must have a first character of 'E' or 'W'",
                                    Name));
                            }
                        }
                        break;
                }
            }

            internal static bool ContainsUserDefNumber(Dictionary<string, FieldEntry> fields, int userDefNumber)
            {
                bool found = false;

                foreach (FieldEntry fieldEntry in fields.Values)
                {
                    if (userDefNumber == fieldEntry.UserDefNumber)
                    {
                        found = true;
                        break;
                    }
                }
                return found;
            }
        }

        private class EnumerationEntry
        {
            internal string Name;
            internal Dictionary<string, string> Values;

            internal EnumerationEntry(string name, Dictionary<string, string> values)
            {
                this.Name = name;
                this.Values = values;
            }

            internal void Validate(string value)
            {
                bool found = Values.ContainsKey(value.ToUpper()); ;

                if (!found)
                {
                    // The REGION enumeration contains compound keys that comprise the region code followed by \t followed by the DXCC.
                    // So, instead of looking up the key, see if the collection includes the value.

                    foreach (string enumerationValue in Values.Values)
                    {
                        if (value.Equals(enumerationValue, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    Logger.Log(null, true, $"Enumeration '{Name}' does not include the value '{value}'");

                    foreach (string key in Values.Keys)
                    {
                        Logger.Log($"Enumeration '{Name}', Key = '{key}', Value='{Values[key]}'");
                    }

                    throw new Exception($"Enumeration '{Name}' does not include the value '{value}'");
                }
            }
        }

        private readonly bool ClubLogBandsOnly = false;

        private class Qso
        {
            private const string defaultBand = "20m";
            private const float defaultFreq = 14.050f;
            private static readonly char[] plusSplitChar = ['+'];

            internal AdifXslt adifXslt;
            internal Random random = new(1);  // Produce a fixed sequence each time to enable some repeatability when debugging.
            internal DateTime Start;
            internal DateTime End;
            internal string Call = "VE3AAA";
            internal string Band = defaultBand;
            internal float Freq = defaultFreq;
            internal string BandRx = defaultBand;
            internal float FreqRx = defaultFreq;
            internal int Dxcc = 1;
            internal string Mode = "SSB";
            internal int Ituz = 2;
            internal int Cqz = 5;
            internal string Cont = "NA";

            private DateTime SavedStart;
            private DateTime SavedEnd;
            private CallEntry callEntry;

            internal Qso(AdifXslt adifXslt, DateTime start, TimeSpan duration)
            {
                this.adifXslt = adifXslt;
                this.Start = start;
                this.End = start.Add(duration);
                this.SavedStart = this.Start;
                this.SavedEnd = this.End;
            }

            internal void Next(string lastCall)
            {
                this.Start = this.End.Add(adifXslt.qsoInterval);
                this.End = this.Start.Add(adifXslt.qsoDuration);
                {
                    if (callEntry == null || lastCall == callEntry.Call)
                    {
                        callEntry = adifXslt.calls.RandomCall();
                    }
                    this.Dxcc = callEntry.Dxcc;
                    this.Call = callEntry.Call;
                    this.Cqz = callEntry.CqZone;
                    this.Ituz = callEntry.ItuZone;
                    this.Cont = callEntry.Cont;
                }
                {
                    this.Band = BandEntry.RandomBand(
                        adifXslt.ClubLogBandsOnly ?
                            adifXslt.clubLogBands :
                            adifXslt.bands,
                        random);
                    this.Freq = float.Parse(adifXslt.Freq(this.Band), adifNumberStyles, adifNumberFormatInfo);
                }
                {
                    this.BandRx = BandEntry.RandomBand(
                        adifXslt.ClubLogBandsOnly ?
                            adifXslt.clubLogBands :
                            adifXslt.bands,
                        random);
                    this.FreqRx = float.Parse(adifXslt.Freq(this.BandRx), adifNumberStyles, adifNumberFormatInfo);

                    //if (++adifXslt.messages < MaxMessages)
                    //{
                    //    ReportError("Next() has set qso.BandRx to " + this.BandRx + " and " +
                    //                               "qsp.FreqRx to " + this.FreqRx.ToString(adifNumberFormatInfo));
                    //}
                }
                {
                    ModeEntry modeEntry = adifXslt.modesByEntry[random.Next(adifXslt.modesByEntry.Count)];

                    this.Mode = modeEntry.Mode;
                }
            }

            internal string SaveStartEnd()
            {
                if (Start.Date == new DateTime(1997,12,01))
                {
                    //
                }
                System.Diagnostics.Debug.Assert(Start.Year >= 2024);
                SavedStart = Start;
                SavedEnd = End;
                return string.Empty;
            }

            internal string RestoreStartEnd()
            {
                if (Start.Date == new DateTime(1997, 12, 01))
                {
                    //
                }
                System.Diagnostics.Debug.Assert(SavedStart.Year >= 2024);
                Start = SavedStart;
                End = SavedEnd;
                return string.Empty;
            }

            internal string Substitute(string nameUpper, string value)
            {
                // This provides a macro-like feature that will substitute values for use with the
                // record(...) method.  For example
                //      record("TIME_OFF", "{QSO_TIME_ON}");
                // will emit the QSO_TIME_OFF field set to the value of the default QSO's TIME_ON value.
                //
                // The reason for doing it this way rather than exposing equivalent C# methods to XSLT
                // is that when a call is made to the record(...) method, parameters that cause required
                // side-effects are evaluated BEFORE the method call, which is too early.  Using this
                // macro-like scheme instead defers evaulation to within the record(...) method itself.
                //
                // Note that C# parameter evaluation is guaranteed to be performed from left to right.
                //
                // Supported values:
                //      {}                  equivalent to calling Substitute with the value set to "{" + nameUpper + "}"
                //      {QSO_DATE}
                //      {QSO_DATE+n}        where n is fixed point number of days
                //      {QSO_DATE_OFF}
                //      {QSO_DATE_OFF+n}    where n is fixed point number of days
                //      {TIME_ON}
                //      {TIME_OFF}
                //      {CALL}
                //      {BAND}
                //      {FREQ}
                //      {BAND_RX}
                //      {FREQ_RX}
                //      {DXCC}
                //      {YEAR_OF_BIRTH(n)} where n is an integer representing age in years
                //      {CQZ}
                //      {ITUZ}
                //      {CONT}
                //      {BAND_FOR_CONTEST(xx)}  where xx is a contest

                if (value.Contains('{', StringComparison.Ordinal))
                {
                    float addDays = 0;
                    string param = string.Empty;

                    if (value.Length < 2 || value[0] != '{' || value[^1] != '}')
                    {
                        throw new Exception("Substitution value does not start and end with '{' and '}'");
                    }
                    if (value == "{}")
                    {
                        value = nameUpper;
                    }
                    else
                    {
                        value = value[1..^1];
                    }
                    {
                        string[] parts = value.Split(plusSplitChar, StringSplitOptions.RemoveEmptyEntries);

                        switch (parts.Length)
                        {
                            case 1:
                                // No action
                                break;

                            case 2:
                                if (parts[0] != "QSO_DATE" && parts[0] != "QSO_DATE_OFF")
                                {
                                    throw new Exception("Cannot use the '+' in a substituion with this field");
                                }
                                else
                                {
                                    if (!float.TryParse(parts[1], out addDays))
                                    {
                                        throw new Exception("Item following '+' is not fixed point or integer");
                                    }
                                    value = parts[0];
                                }
                                break;

                            default:
                                throw new Exception("More than one '+' found in substitution");
                        }

                        int paramStart = value.IndexOf('(');

                        if (paramStart > 0)
                        {
                            if (value[^1] != ')')
                            {
                                throw new Exception("Parameter must end with a ')' character");
                            }
                            param = value.Substring(paramStart + 1, value.Length - paramStart - 2);
                            value = value[..paramStart];

                            if (value != "YEAR_OF_BIRTH")
                            {
                                throw new Exception($"A parameter cannot be used with {value}");
                            }
                        }
                    }

                    switch (value)
                    {
                        case "QSO_DATE":
                            {
                                value = Start.AddDays(addDays).ToString("yyyyMMdd");
                            }
                            break;

                        case "QSO_DATE_OFF":
                            {
                                value = End.AddDays(addDays).ToString("yyyyMMdd");
                            }
                            break;

                        case "TIME_ON":
                            {
                                value = Start.ToString("HHmmss");
                            }
                            break;

                        case "TIME_OFF":
                            {
                                value = End.ToString("HHmmss");
                            }
                            break;

                        case "CALL":
                            {
                                value = Call;
                            }
                            break;

                        case "BAND":
                            {
                                value = Band;
                            }
                            break;

                        case "FREQ":
                            {
                                value = Freq.ToString();  //TODO: Formatting!
                            }
                            break;

                        case "BAND_RX":
                            {
                                value = BandRx;
                            }
                            break;

                        case "FREQ_RX":
                            {
                                value = FreqRx.ToString();  //TODO: Formatting!
                            }
                            break;

                        case "DXCC":
                            {
                                value = Dxcc.ToString();
                            }
                            break;

                        case "YEAR_OF_BIRTH":
                            {
                                if (!int.TryParse(param, out int age))
                                {
                                    throw new Exception(string.Format(
                                        "YEAR_OF_BIRTH({0}): Invalid age parameter",
                                        param));
                                }

                                string yearOfBirth;
                                try
                                {
                                    yearOfBirth = Start.AddYears(-age).Year.ToString();
                                }
                                catch (Exception exc)
                                {
                                    ReportError(string.Format(
                                        "YEAR_OF_BIRTH({0}) Exception: {1}",
                                        age.ToString(),
                                        exc.Message));
                                    throw;
                                }
                                value = yearOfBirth;
                            }
                            break;

                        case "CQZ":
                            value = Cqz.ToString();
                            break;

                        case "ITUZ":
                            value = Ituz.ToString();
                            break;

                        case "CONT":
                            value = Cont;
                            break;

                        default:
                            {
                                throw new Exception("Invalid substitution");
                            }
                    }
                }
                return value;
            }
        }

        // Ensure dot is used as decimal point
        private readonly static NumberFormatInfo adifNumberFormatInfo;
        private readonly static NumberStyles adifNumberStyles;

        public static string EntitiesXmlPath { get; set; }

        /**
         * <summary>
         *   A delegate type for sending a prompt back to a user.
         * </summary>
         */
        public delegate void UserPrompter(string message);

        /**
         * <value>
         *   A <see cref="UserPrompter"/> delegate object.
         * </value>
         */
        public static UserPrompter PromptUser { get; set; }

        /**
         * <value>
         *   The name of this program, e.g. "CreateADIFTestFiles".<br  />
         *   <br />
         *   This is static so that it can be accessed without the need for a reference.&#160;
         *   It is read by the <see cref="ProgramId"/> property.
         * </value>
         */
        public static string ApplicationName { get; set; }

        /**
         * <value>
         *   The version of this program as a string, e.g. "1.2.3.4".<br />
         *   <br />
         *   This is static so that it can be accessed without the need for a reference.&#160;
         *   It is read by the <see cref="ProgramVersion"/> property.
         * </value>
         */
        public static string ApplicationVersion { get; set; }

        /**
         * <value>
         *   Whether or not errors were experienced applying the XSLT file.
         * </value>
         */
        public static bool Success { get; set; }

        public Random random = new(1);  // Produce a fixed sequence each time to enable some repeatability when debugging.

        private readonly bool
            adiStyle = true,
            hasHeaderFields = false;
#pragma warning disable format
        private bool inHeader = true;
        private string fieldSeparator   = "\r\n";
        private string recordSeparator  = "\r\n\r\n";

        private readonly TimeSpan qsoDuration;  // Length of time of a QSO.
        private readonly TimeSpan qsoInterval;  // The length of time after the previous QSO ends that the next on starts.

        private readonly Qso qso;

        private readonly Dictionary<string, BandEntry>          clubLogBands            = new(64);
        private readonly Dictionary<string, BandEntry>          bands                   = new(64);
        private readonly List      <ModeEntry>                  modesByEntry            = new(512);
        private readonly Dictionary<string, string>             recordFieldsEmitted     = new(32);
        private readonly Dictionary<string, string>             fieldNamesWithIntlField = new(1024);
        private readonly Dictionary<string, string>             headerFieldNames        = new(16);
        private readonly Dictionary<string, DataTypeEntry>      dataTypes               = new(64);
        private readonly Dictionary<string, FieldEntry>         fields                  = new(1024);
        private readonly Dictionary<string, EnumerationEntry>   enumerations            = new(128);

        private readonly Calls calls;

        int totalFields  = 0,
            totalRecords = 0;

        private readonly string
            adifVersion =   string.Empty,
            adifStatus =    string.Empty;

        readonly DateTime
            adifDate = DateTime.MinValue;
#pragma warning restore format

        private readonly int adifVersionInt = -1;

        private static void ReportError(string message)
        {
            // Delegate the prompting the user back to the caller so that this class is usuable from
            // a Windows GUI or a command line progarm.

            //Success = false;

            if (PromptUser == null)
            {
                const string
                    error = "AdifXslt.UserPrompter property has not been initialised",
                    logError = "*** " + error;

                Logger.Log(logError);
                throw new ApplicationException(error);
            }
            else
            {
                PromptUser.Invoke(message);
            }
        }

        static AdifXslt()
        {
            adifNumberFormatInfo = (System.Globalization.NumberFormatInfo)System.Globalization.CultureInfo.GetCultureInfo("en-US").NumberFormat.Clone();
            adifNumberFormatInfo.PositiveSign = string.Empty;
            //adifNumberFormatInfo.NumberDecimalDigits = 3;
            adifNumberStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        }

        private AdifXslt()
        {
            System.Diagnostics.StackFrame fr = new(1, true);
            string stack = new System.Diagnostics.StackTrace(fr).ToString();

            throw new Exception($"A call to the AdifXslt parameterless constructor is not allowed\r\n\r\n{stack}");
        }

        public AdifXslt(
#pragma warning disable format
            string          adifStyle,
            bool            hasHeaderFields,
            bool            clubLogBandsOnly,
            XPathNavigator  nav)
#pragma warning restore format
        {
            try
            {
                Success = false;  // Guilty until proven innocent!

                ClubLogBandsOnly = clubLogBandsOnly;

                qsoDuration = new TimeSpan(00, 04, 37);
                qsoInterval = new TimeSpan(00, 01, 06);

                qso = new Qso(
                    this,
                    DateTime.UtcNow.Date.AddMonths(-2),  // Set start time to 00:00:00 so that separate runs produce the same dates & times during a day.
                    qsoDuration);

                adiStyle = adifStyle.ToUpperInvariant() switch
                {
                    "ADI" => true,
                    "ADX" => false,
                    _ => throw new Exception("Invalid adifStyle parameter"),
                };
                this.hasHeaderFields = hasHeaderFields;

                bands.Clear();
                clubLogBands.Clear();

                nav = nav.SelectSingleNode("/adif/enumerations/enumeration[@name='Band']/record");
                do
                {
                    string band = nav.SelectSingleNode("value[@name='Band']").Value;
                    float lowerLimit = float.Parse(nav.SelectSingleNode("value[@name='Lower Freq (MHz)']").Value, adifNumberStyles, adifNumberFormatInfo);
                    float upperLimit = float.Parse(nav.SelectSingleNode("value[@name='Upper Freq (MHz)']").Value, adifNumberStyles, adifNumberFormatInfo);

                    BandEntry bandEntry = new(band, lowerLimit, upperLimit);

                    bands.Add(band, bandEntry);

                    switch (band.ToLowerInvariant())
                    {
                        case "160m":
                        case "80m":
                        case "60m":
                        case "40m":
                        case "30m":
                        case "20m":
                        case "17m":
                        case "15m":
                        case "12m":
                        case "10m":
                        case "6m":
                        case "4m":
                        case "2m":
                        case "70cm":
                        case "23cm":
                        case "13cm":
                            clubLogBands.Add(band, bandEntry);
                            break;

                        default:
                            break;
                    }
                }
                while (nav.MoveToNext("record", string.Empty));

                modesByEntry.Clear();
                nav = nav.SelectSingleNode("/adif/enumerations/enumeration[@name='Mode']/record");
                do
                {
                    if (nav.SelectSingleNode("value[@name='Import-only']") == null)
                    {
                        string mode = nav.SelectSingleNode("value[@name='Mode']").Value;
                        XPathNavigator submodesNav = nav.SelectSingleNode("value[@name='Submodes']");
                        ModeEntry modeEntry = new(mode, submodesNav == null ? string.Empty : submodesNav.Value);

                        modesByEntry.Add(modeEntry);
                    }
                }
                while (nav.MoveToNext("record", string.Empty));

                dataTypes.Clear();
                nav = nav.SelectSingleNode("/adif/dataTypes/record");
                do
                {
                    string dataTypeName = nav.SelectSingleNode("value[.]").Value.ToUpper();

                    XPathNavigator dtiNav = nav.SelectSingleNode("value[@name='Data Type Indicator']");
                    string dtiString = dtiNav?.Value;
                    char dataTypeIndicator = string.IsNullOrEmpty(dtiString) ? (char)0 : dtiString[0];

                    XPathNavigator minNav = nav.SelectSingleNode("value[@name='Minimum Value']");
                    double minimumValue = minNav == null ?
                        double.MinValue :
                        double.Parse(minNav.Value);

                    XPathNavigator maxNav = nav.SelectSingleNode("value[@name='Maximum Value']");
                    double maximumValue = maxNav == null ?
                        double.MaxValue :
                        double.Parse(maxNav.Value);

                    dataTypes.Add(dataTypeName, new DataTypeEntry(this, dataTypeName, dataTypeIndicator, minimumValue, maximumValue));
                }
                while (nav.MoveToNext("record", string.Empty));

                fields.Clear();
                fieldNamesWithIntlField.Clear();
                headerFieldNames.Clear();
                nav = nav.SelectSingleNode("/adif/fields/record");
                do
                {
                    string field = nav.SelectSingleNode("value[.]").Value.ToUpper();
                    bool header = nav.SelectSingleNode("value[@name='Header Field']") != null;

                    XPathNavigator dtNav = nav.SelectSingleNode("value[@name='Data Type']");
                    string dataType = dtNav != null ?
                        dtNav.Value.ToUpper() :
                        string.Empty;

                    XPathNavigator enNav = nav.SelectSingleNode("value[@name='Enumeration']");
                    string enumeration = enNav != null ?
                        enNav.Value.ToUpper() :
                        string.Empty;

                    {
                        int indexOfSquareBracket = enumeration.IndexOf('[');

                        if (indexOfSquareBracket > 0)
                        {
                            enumeration = enumeration[..indexOfSquareBracket];

                            // Logger.Log($"Field '{field}' Enumeration '{enumeration}' is a function");
                        }
                    }

                    XPathNavigator minNav = nav.SelectSingleNode("value[@name='Minimum Value']");
                    double minimumValue = minNav == null ?
                        double.MinValue :
                        double.Parse(minNav.Value);

                    XPathNavigator maxNav = nav.SelectSingleNode("value[@name='Maximum Value']");
                    double maximumValue = maxNav == null ?
                        double.MaxValue :
                        double.Parse(maxNav.Value);

                    if (field != "USERDEFN")  // Ignore USERDEFN as it is a model for a Field and not an actual field.
                    {
                        if (field.EndsWith("_INTL"))
                        {
                            fieldNamesWithIntlField.Add(field[..^"_INTL".Length], field);
                        }

                        if (header)
                        {
                            headerFieldNames.Add(field, field);
                        }
                        fields.Add(field, new FieldEntry(dataTypes, field, header, dataType, enumeration, minimumValue, maximumValue));
                    }
                }
                while (nav.MoveToNext("record", string.Empty));

                enumerations.Clear();
                nav = nav.SelectSingleNode("/adif/enumerations/enumeration");
                do
                {
                    string enumerationName = nav.SelectSingleNode("@name").Value.ToUpper();
                    Dictionary<string, string> values = new(2048);
                    XPathNavigator valueNav = nav.SelectSingleNode("record");

                    do
                    {
                        string value = valueNav.SelectSingleNode("value[2]").Value.ToUpper();
                        string key = value;

                        if (enumerationName.Equals("Primary_Administrative_Subdivision", StringComparison.OrdinalIgnoreCase))
                        {
                            // Each value has to have a 2-part key comprising value and DXCC entity code.
                            // Additionally, some entries need a 3-part key because the values are 'Deleted'.

                            string dxcc = valueNav.SelectSingleNode("value[@name='DXCC Entity Code']").Value;
                            bool deleted = valueNav.SelectSingleNode("value[@name='Deleted']") != null;

                            key += '\t' + dxcc;

                            if (deleted)
                            {
                                key += "\tDeleted";
                            }
                        }
                        else if (enumerationName.Equals("Region", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                XPathNavigator dxccNode = valueNav.SelectSingleNode("value[@name='DXCC Entity Code']");

                                if (dxccNode != null)
                                {
                                    string dxcc = dxccNode.Value;
                                    //string dxcc = valueNav.SelectSingleNode("value[@name='DXCC Entity Code']").Value;

                                    key += '\t' + dxcc;
                                }
                            }
                            catch { }
                        }

                        if (values.ContainsKey(key))
                        {
                            ReportError(string.Format(
                                "Internal error: enumeration {0} already contains the key {1}",
                                enumerationName,
                                key));

                            continue;
                        }

                        values.Add(key, value);  // The second value element always contains the enumeration value.
                    }
                    while (valueNav.MoveToNext("record", string.Empty));

                    enumerations.Add(enumerationName, new EnumerationEntry(enumerationName, values));
                }
                while (nav.MoveToNext("enumeration", string.Empty));

                {
                    string sas = "SECONDARY_ADMINISTRATIVE_SUBDIVISION";
                    Dictionary<string, string> sasDictionary = [];

                    sasDictionary = enumerations[sas].Values;

                    sasDictionary.Add("MA,MIDDLESEX", "MA,MIDDLESEX");
                    //sasDictionary.Add("AK,ALEUTIANS EAST", "AK,ALEUTIANS EAST");
                    //enumerations[sas].Values = sasDictionary;
                }

                adifVersion = nav.SelectSingleNode("/adif/@version").Value;
                if (adifVersion == null ||
                    adifVersion.Length != 5 ||
                    (!char.IsDigit(adifVersion[0])) ||
                    (!char.IsDigit(adifVersion[2])) ||
                    (!char.IsDigit(adifVersion[4])) ||
                    adifVersion[1] != '.' ||
                    adifVersion[3] != '.')
                {
                    throw new Exception(string.Format(
                        "ADIF Version '{0}' is not in the required format of i.j.k",
                        StringToNullOrString(adifVersion)));
                }

                adifVersionInt = ((adifVersion[0] - '0') * 100) + ((adifVersion[2] - '0') * 10) + (adifVersion[4] - '0');

                if (adifVersionInt < 305)
                {
                    throw new Exception(string.Format(
                        "ADIF Version '{0}' ({1}) is not supported",
                        StringToNullOrString(adifVersion),
                        adifVersionInt));
                }

                adifStatus = nav.SelectSingleNode("/adif/@status").Value;
                switch (adifStatus)
                {
                    case "Draft":
                    case "Proposed":
                    case "Released":
                        break;

                    default:
                        throw new Exception($"ADIF Status '{StringToNullOrString(adifStatus)}' is not one of 'Draft', 'Proposed', or 'Released'");
                }

                {
                    XPathNavigator datePathNavigator = nav.SelectSingleNode("/adif/@date");

                    if (datePathNavigator != null &&
                        !string.IsNullOrEmpty(datePathNavigator.Value))
                    {
                        adifDate = XmlConvert.ToDateTime(datePathNavigator.Value, XmlDateTimeSerializationMode.Utc);
                    }
                    else
                    {
                        Logger.Log("The document element does not contain a valid \"date\" attribute.");
                    }
                }

                calls = new Calls(EntitiesXmlPath);

                // throw new ApplicationException("Oops it broke");  // For testing.

                //Dictionary<string, BandEntry>.ValueCollection.Enumerator e = bands.Values.GetEnumerator();

                //BandEntry bandEntry = null;
                //int max = 0;

                //while(e.MoveNext())
                //{
                //    bandEntry = e.Current;
                //    if (max++ == 4) break;
                //}

                //max = 0;
                //e.Dispose(); e = bands.Values.GetEnumerator();

                //while (e.MoveNext())
                //{
                //    bandEntry = e.Current;
                //    if (max++ == 4) break;
                //}
                //e.Dispose();
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.AdifXslt({0}, {1}, {2}) Exception: {3}",
                    StringToNullOrString(adifStyle),
                    hasHeaderFields.ToString(),
                    nav == null ? "[null]" : "[XPathNavigator]",
                    exc.Message));
                throw;
            }
            Success = true;
        }

        public string SetOptions(
            string fieldSeparator,
            string recordSeparator)
        {
            try
            {
#pragma warning disable format
                this.fieldSeparator  = fieldSeparator;
                this.recordSeparator = recordSeparator;
#pragma warning restore format
            }
            catch (Exception exc)
            {
                ReportError($"AdifXslt.SetOptions({StringToNullOrString(fieldSeparator)}, {StringToNullOrString(recordSeparator)}) Exception: {exc.Message}");
                throw;
            }
            return string.Empty;
        }

        //public string Property(
        //    string name)
        //{
        //    string value = string.Empty;

        //    try
        //    {
        //        switch (name.ToLower())
        //        {
        //            case "adifversion":
        //                value = adifVersion;
        //                break;

        //            case "adifstatus":
        //                value = adifStatus;
        //                break;

        //            case "programid":
        //                value = "AdifRelease";
        //                break;

        //            default:
        //                throw new Exception("Unrecognized property");
        //        }
        //    }
        //    catch (Exception exc)
        //    {
        //        reportError(string.Format(
        //            "AdifXslt.Property({0}) Exception: {1}",
        //            StringToNullOrString(name),
        //            exc.Message));
        //        throw;
        //    }
        //    return value;
        //}

        public string AdifVersion() => adifVersion;

        public int AdifVersionInt() => adifVersionInt;

        /**
         * <value>
         *   The name of this program, e.g. "CreateADIFTestFiles".
         * </value>
         */
        public static string ProgramId => ApplicationName;

        /**
         * <value>
         *   The version of this program as a string, e.g. "1.2.3.4".
         * </value>
         */
        public static string ProgramVersion => ApplicationVersion;

        /**
         * <summary>
         *   Obtains the current UTC date and time in the format required by the ADIF CREATED_TIMESTAMP field.
         * </summary>
         * 
         * <returns>the current UTC date and time in the format required by the ADIF CREATED_TIMESTAMP field.</returns>
         */
        public static string CreatedTimestamp => DateTime.UtcNow.ToString("yyyyMMdd HHmmss");

        private static readonly char[] xmlEscapeChars = ['&', '<', '>', '\'', '"'];

        /**
         * <summary>
         *   Encodes any characters in a <see cref="string"/> that are not allowed unencoded in XML. 
         * </summary>
         * 
         * <param name="text">The <see cref="string"/> to be encoded.</param>
         * 
         * <returns>The input <see cref="string"/> with characters encoded.</returns>
         */
        private string Encode(string text)
        {
            if (!adiStyle)
            {
                if (text.IndexOfAny(xmlEscapeChars) >= 0)  // Optimisation to avoid unnecessary multiple calls to string.Replace.
                {
#pragma warning disable format
                    text = text.
                        Replace("&" , "&amp;").  // Must change ampersand first or else it itself will get replaced.
                        Replace("<" , "&lt;").
                        Replace(">" , "&gt;").
                        Replace("'" , "&apos;").
                        Replace("\"", "&quot;");
#pragma warning restore format
                }
            }
            return text;
        }

        // While the XmlEncode method below is more "puristic" than using string.Replace() per the Encode method
        // above, the XmlWriter's WriteString method is designed to encode element values and hence does not
        // encode apostrophes (') or double-quotes (") because they are valid.  This means that it is no use for
        // encoding attribute values where they *do* needed to be encoded.
        //
        // While WriteAttributeString would probably work, it has to be called in the context of writing an
        // element and then the wanted encoded string would need to be extracted from the resulting XML;
        // it's not worth the effort when string.Replace() works fine.

        //public static string XmlEncode(string value)
        //{
        //    StringBuilder builder = new StringBuilder();
        //    XmlWriterSettings settings = new System.Xml.XmlWriterSettings();

        //    settings.ConformanceLevel = ConformanceLevel.Fragment;

        //    using (XmlWriter writer = XmlWriter.Create(builder, settings))
        //    {
        //        writer.WriteString(value);
        //    }
        //    return builder.ToString();
        //}

        //public static string XmlDecode(string xmlEncodedValue)
        //{
        //    XmlReaderSettings settings = new System.Xml.XmlReaderSettings();

        //    settings.ConformanceLevel = ConformanceLevel.Fragment;

        //    System.IO.StringReader stringReader = new System.IO.StringReader(xmlEncodedValue);

        //    using (XmlReader xmlReader = XmlReader.Create(stringReader, settings))
        //    {
        //        xmlReader.Read();
        //        return xmlReader.Value;
        //    }
        //}

        /**
         * <summary>
         *   Accepts a <see cref="string"/> value and converts it to a value for incorporating in
         *   error messages and exceptions.<br />
         *   <br />
         *   If the string is null, "[null]" is returned.<br />
         *   If the string contains \r, \n, or \t, these are converted to "\\r", "\\n" and "\\t" respectively.
         * </summary>
         * 
         * <param name="value">The <see cref="string"/> to be converted.</param>
         * 
         * <returns>The converted <see cref="string"/> value.</returns>
         */
        internal static string StringToNullOrString(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
#pragma warning disable format
                value = value.
                    Replace("\r", "\\r").
                    Replace("\n", "\\n").
                    Replace("\t", "\\t");
#pragma warning restore format
            }

            return value == null ?
                "[null]" :
                $"\"{value}\"";
        }

        /**
         * <summary>
         *   Saves the last used QSO start date and time prior to a QSO being output with a
         *   pre-defined date and time.  This is so that after the QSO has been output,
         *   the last used QSO start date and time can be restored.
         * </summary>
         * 
         * <returns>An empty <see cref="string"/> because no output is being included in the ADIF file.</returns>
         */
        public string SaveQsoStartEnd() => qso.SaveStartEnd();

        /**
         * <summary>
         *   Restores the saved QSO start date and time back to the last used QSO data and time after a QSO
         *   has been output with a pre-defined date and time.
         * </summary>
         * 
         * <returns>An empty <see cref="string"/> because no output is being included in the ADIF file.</returns>
         */
        public string RestoreQsoStartEnd() => qso.RestoreStartEnd();

        private string QsoDate() => qso.Start.ToString("yyyyMMdd");

        // Not curently required - QSO_DATE_OFF fields are nevertheless generated by QSO_templates.xslt
        //
        //private string qsoDateOff()
        //{
        //    return qso.End.ToString("yyyyMMdd");
        //}

        // Not currently required; however 4-digit TIME_ON fields are nevertheless generated by QSO_templates.xslt
        //
        //private string timeOn4()
        //{
        //    return qso.Start.ToString("HHmm");
        //}

        private string TimeOn6() => qso.Start.ToString("HHmmss");

        // Not currently required; however 4-digit TIME_OFF fields are nevertheless generated by QSO_templates.xslt
        //
        //private string timeOff4()
        //{
        //    return qso.End.ToString("HHmm");
        //}

        private string TimeOff6() => qso.End.ToString("HHmmss");

        private string Band() => qso.Band;

        private string Band(float freq)
        {
            string band = string.Empty;

            try
            {
                if (float.IsNaN(freq) || float.IsInfinity(freq))
                {
                    throw new Exception("freq parameter is Nan or Infinity");
                }
                else
                {

                    foreach (BandEntry bandEntry in bands.Values)
                    {
                        if (freq >= bandEntry.LowerLimit && freq <= bandEntry.UpperLimit)
                        {
                            band = bandEntry.Name;
                            break;
                        }
                    }
                    if (band.Length == 0)
                    {
                        throw new Exception("the freq parameter is not within an amateur radio band");
                    }
                }
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.Band({0}) Exception: {1}",
                    freq.ToString(),
                    exc.Message));
                throw;
            }
            return band;
        }

        private const string frequencyFormat = "0.######";

        private string Freq()
        {
            string freq;

            try
            {
                freq = qso.Freq.ToString(frequencyFormat, adifNumberFormatInfo);
            }
            catch (Exception exc)
            {
                ReportError("AdifXslt.Freq() Exception: " + exc.Message);
                throw;
            }
            return freq;
        }

        private string Freq(string band)
        {
            string freq;

            try
            {
                if (!bands.TryGetValue(band.ToLower(), out BandEntry bandEntry))
                {
                    throw new Exception("band parameter is not a band in the ADIF specification");
                }

                float increment = (float)random.NextDouble() * (bandEntry.UpperLimit - bandEntry.LowerLimit);

                freq = (bandEntry.LowerLimit + increment).ToString(frequencyFormat, adifNumberFormatInfo);
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.Freq({0}) Exception:  {1}",
                    StringToNullOrString(band),
                    exc.Message));
                throw;
            }
            return freq;
        }

        //private string bandRx()
        //{
        //    if (++messages < MaxMessages)
        //    {
        //        reportError("BandRx() returning " + qso.BandRx);
        //    }

        //    return qso.BandRx;
        //}

        //private string freqRx()
        //{
        //    return qso.FreqRx.ToString();
        //}

        public static string BandForContest(string contest)
        {
            string band;

            contest = contest.ToUpper();
            if (contest.Contains("VHF"))
            {
                band = "2m";
            }
            else if (contest.Contains("UHF"))
            {
                band = "70cm";
            }
            else if (contest.Contains("UKSMG"))
            {
                band = "6m";
            }
            else if (contest.Contains("160"))
            {
                band = "160m";
            }
#pragma warning disable format
            else if (contest.Contains("80M")                ||
                     contest.Contains("RSGB-AFS")           ||
                     contest.Contains("RSGB-CLUB-CALLS")    ||
                     contest.Contains("RSGB-ROPOCO"))
#pragma warning restore format
            {
                band = "80m";
            }
            else if (contest.Contains("40M"))
            {
                band = "40m";
            }
            else if (contest.Contains("10") ||
                contest.Contains("28") ||
                contest.Contains("TEN"))
            {
                band = "10m";
            }
            else
            {
                band = "20m";
            }
            return band;
        }

        public bool IncludeBand(string band) => !ClubLogBandsOnly || clubLogBands.ContainsKey(band);

        private DateTime DxccQsoStartDateTime;

        private bool DxccQsoHasStartTime;

        internal string CallForDxccStartDate() => DxccQsoStartDateTime != DateTime.MinValue ?
                DxccQsoStartDateTime.ToString("yyyyMMdd") :
                string.Empty;

        internal string CallForDxccStartTime() => DxccQsoHasStartTime ?
                DxccQsoStartDateTime.ToString("HHmmss") :
                string.Empty;

        private string Call() => qso.Call;

        public string CallForDxcc(int dxccEntity) => calls.CallForDxcc(dxccEntity, out DxccQsoStartDateTime, out DxccQsoHasStartTime).Call;

        public string CallForCont(string cont)
        {
            CallEntry callEntry;

            try
            {
                if (string.IsNullOrEmpty(cont))
                {
                    throw new Exception("Continent is [null] or empty");
                }
                callEntry = calls.CallForCont(cont);
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.CallForCont({0}) Exception: {1}",
                    StringToNullOrString(cont),
                    exc.Message));
                throw;
            }
            return callEntry.Call;
        }

        public string CallForPrimaryAdministrativeSubdivision(int dxcc, string primaryAdministrativeSubdivision)
        {
            CallEntry callEntry;

            try
            {
                if (string.IsNullOrEmpty(primaryAdministrativeSubdivision))
                {
                    throw new Exception("primaryAdministrativeSubdivision is [null] or empty");
                }
                callEntry = calls.CallForPrimaryAdministrativeSubdivision(dxcc, primaryAdministrativeSubdivision, out DxccQsoStartDateTime, out DxccQsoHasStartTime);
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.CallForPrimaryAdministrativeSubdivision({0}, \"{1}\" Exception: {2}",
                    dxcc.ToString(),
                    StringToNullOrString(primaryAdministrativeSubdivision),
                    exc.Message));
                throw;
            }
            return callEntry.Call;
        }

        //public int Dxcc()
        //{
        //    //reportError("Returning Dxcc " + qso.Dxcc.ToString());

        //    return qso.Dxcc;
        //}

        private string Mode() => qso.Mode;

        private bool IgnoreField(string nameUpper) =>
            adiStyle ?
                nameUpper.EndsWith("_INTL") :
                fieldNamesWithIntlField.ContainsKey(nameUpper);

        private readonly List<string> UntestedFields = [];

        public string UntestedField(string name)
        {
            UntestedFields.Add(name);
            return string.Empty;
        }

        public string Comment(string text)
        {
            try
            {
                if (adiStyle)
                {
                    if (text.Contains('<', StringComparison.Ordinal))
                    {
                        throw new Exception("Comments in ADI files cannot include a '<'");
                    }
                }
                else
                {
                    if (text.Contains("--", StringComparison.Ordinal))
                    {
                        throw new Exception("Comments in ADX files cannot include '--'");
                    }
                    text = "<!--" + Encode(text) + "-->";
                }
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.Comment({0}) Exception: {1}",
                    StringToNullOrString(text),
                    exc.Message));
                throw;
            }
            return text;
        }

        public string CommentLine(string text) => Comment(text) + "\r\n";

        public string CommentLine2(string text) => CommentLine(text) + "\r\n";

        public string CommentReport(bool full)
        {
            StringBuilder message = new(65536);

            _ = message.Append("Report\r\n\r\n");

            _ = message.Append($"ADIF Specification: {adifStatus} {adifVersion}");

            if (adifDate != DateTime.MinValue)
            {
                _ = message.Append($" {adifDate.ToString("yyyy-MM-dd")}");
            }

            _ = message.Append("\r\n\r\n");

            if (UntestedFields.Count > 0)
            {
                _ = message.Append($"Untested fields: {UntestedFields.Count,6}\r\n");
            }

            _ = message.Append($"Fields emitted: {totalFields,7}\r\nRecords emitted: {totalRecords,6}\r\n");

            if (UntestedFields.Count > 0)
            {
                _ = message.Append("\r\nUntested fields\r\n\r\n");

                foreach (string untestedField in UntestedFields)
                {
                    _ = message.Append($"{untestedField}\r\n");
                }
            }

            if (full)
            {
                const string
                    format = "Occurrences: {0,5}, Name: {1,-34}, Header: {2,-5}, Variant: {3}\r\n",
                    userDefFormat = "Occurrences: {0,5}, Name: {1,-34}, Header: {2,-5}, Variant: {3}, USERDEFn number: {4,3}\r\n";

                message.Append("\r\nField details\r\n\r\n");
                foreach (string name in fields.Keys)
                {
                    FieldEntry fieldEntry = fields[name];

                    if (adiStyle && fieldEntry.Variant == FieldEntry.FieldVariant.Adif && fieldEntry.Name.EndsWith("_INTL"))
                    {
                        // ADIF-defined fields ending in "_INTL" are not allowed in ADI files because of the extended character set.
                    }
                    else
                    {
                        message.AppendFormat(
                            fieldEntry.Variant == FieldEntry.FieldVariant.User ?
                                userDefFormat :
                                format,
                            fieldEntry.Occurrences,
                            fieldEntry.Name,
                            fieldEntry.Header.ToString(),
                            fieldEntry.Variant.ToString(),
                            fieldEntry.UserDefNumber.ToString());
                    }
                }
#pragma warning disable format
                message
                    .AppendFormat("\r\nCalls created:        {0,5}\r\n",     calls.CallCounts.Count.ToString())
                    .AppendFormat(    "Repeating calls:      {0,5}\r\n",     calls.RepeatedCalls.ToString())
                    .AppendFormat(    "Total repeated calls: {0,5}\r\n\r\n", calls.RepeatedCallsTotal.ToString());
#pragma warning restore format

                foreach (string call in calls.CallCounts.Keys)
                {
                    int count = calls.CallCounts[call];

                    if (count > 1)
                    {
                        message.AppendFormat(
                            "Call: {0,-7}, Count: {1,4}\r\n",
                            call,
                            count.ToString());
                    }
                }
            }
            return CommentLine(message.ToString());
        }

        /**
         * <summary>
         *   Converts a date in the format "yyyy-MM-dd" to the ADIF date format "yyyyMMdd".
         * </summary>
         * 
         * <param name="date">The date to be converted.</param>
         * 
         * <returns>The date in the ADIF date format.</returns>
         */
        public string DateToAdifDate(string date) => date.Replace("-", string.Empty);

        public string Field(
            string name,
            string value) => Field(name, value, string.Empty);

        private const int MaxMessages = 6;

        private int messages = 0;

        public string Field(
            string name,
            string value,
            string dataTypeIndicator)
        {
            // TBS should check value against ADIF spec data types.

            StringBuilder field = new(2048);

            try
            {
                bool isAdifDefinedQsoField = false;

                totalFields++;
                if (string.IsNullOrEmpty(name))
                {
                    throw new Exception("name parameter is zero-length or null");
                }
                if (string.IsNullOrEmpty(value))
                {
                    //System.Diagnostics.Debug.Assert(false);
                    throw new Exception("value parameter is zero-length or null");
                }

                string nameUpper = name.ToUpper();

                value = qso.Substitute(nameUpper, value);
                if ((!hasHeaderFields) && headerFieldNames.ContainsKey(nameUpper))
                {
                    throw new Exception("Trying to add a header field after the Initialize method specified hasHeaderFields = false");
                }
                else if ((!string.IsNullOrEmpty(dataTypeIndicator)) &&
                         dataTypeIndicator.Length != 1)
                {
                    throw new Exception("Invalid data type indicator");
                }
                else if (recordFieldsEmitted.ContainsKey(nameUpper))
                {
                    throw new Exception("Field name already included in record");
                }
                else if (IgnoreField(nameUpper))
                {
                    // Skip the field because either:
                    //      _INTL fields are not output to ADI files.
                    //      non-_INTL fields are not output to ADX files
                }
                else
                {
                    if (!fields.ContainsKey(nameUpper))
                    {
                        throw new Exception("Unknown ADIF field or undeclared USERDEF field");
                    }
                    else
                    {
                        FieldEntry fieldEntry = fields[nameUpper];

                        if (fieldEntry.Header && !inHeader)
                        {
                            throw new Exception("Header field not allowed in record section");
                        }
                        switch (fieldEntry.Variant)
                        {
                            case FieldEntry.FieldVariant.Adif:
                                if (dataTypeIndicator.Length > 0)
                                {
                                    DataTypeEntry dataTypeEntry = DataTypeEntry.GetDataTypeEntry(dataTypes, dataTypeIndicator[0]);

                                    if (fieldEntry.DataType != dataTypeEntry)
                                    {
                                        throw new Exception("if a Data Type Indicator is supplied, it must match the one for the field in the ADIF specification");
                                    }
                                }
                                isAdifDefinedQsoField = true;
                                break;

                            case FieldEntry.FieldVariant.App:
                                throw new Exception("APP_ field must be output using the AppField method");

                            case FieldEntry.FieldVariant.User:
                                throw new Exception("USERDEF field must be output using the UserDefField method");

                            default:
                                throw new Exception(string.Format(
                                    "Internal error: unexpected FieldVariant: {0}",
                                    ((int)fieldEntry.Variant).ToString()));
                        }
                        fieldEntry.Occurrences++;
                        fieldEntry.ValidateValue(value, adiStyle);
                    }

                    {
                        StringBuilder newValue = new(2 * value.Length);
                        bool expectLf = false;

                        foreach (char c in value)
                        {
                            switch (c)
                            {
                                case '\n':
                                    if (expectLf)
                                    {
                                        newValue.Append('\n');
                                        expectLf = false;
                                    }
                                    else
                                    {
                                        // Oops - found a \n without a preceding \r
                                        newValue.Append("\r\n");
                                    }
                                    break;

                                case '\r':
                                    expectLf = true;
                                    newValue.Append('\r');
                                    break;

                                default:
                                    if (expectLf)
                                    {
                                        // Oops - last character was \r but the next one wasn't \n
                                        newValue.Append('\n');
                                        expectLf = false;
                                    }
                                    newValue.Append(c);
                                    break;
                            }
                        }
                        value = newValue.ToString();
                    }
                    field.Append(Bor());
                    recordFieldsEmitted.Add(nameUpper, value);

                    switch (nameUpper)
                    {
                        case "QSO_DATE":
                            {
                                qso.Start = new DateTime(
                                    int.Parse(value[..4]),
                                    int.Parse(value.Substring(4, 2)),
                                    int.Parse(value.Substring(6, 2)),
                                    qso.Start.Hour,
                                    qso.Start.Minute,
                                    qso.Start.Second);

                                if (qso.End < qso.Start)
                                {
                                    qso.End = qso.Start;
                                }
                            }
                            break;

                        case "QSO_DATE_OFF":
                            {
                                qso.End = new DateTime(
                                    int.Parse(value[..4]),
                                    int.Parse(value.Substring(4, 2)),
                                    int.Parse(value.Substring(6, 2)),
                                    qso.End.Hour,
                                    qso.End.Minute,
                                    qso.End.Second);

                                if (qso.Start > qso.End)
                                {
                                    qso.Start = qso.End;
                                }
                            }
                            break;

                        case "TIME_ON":
                            {
                                string fullValue = value;

                                if (fullValue.Length == 4)
                                {
                                    // Pad out to a six character time string.

                                    fullValue += "00";
                                }
                                qso.Start = new DateTime(
                                    qso.Start.Year,
                                    qso.Start.Month,
                                    qso.Start.Day,
                                    int.Parse(fullValue[..2]),
                                    int.Parse(fullValue.Substring(2, 2)),
                                    int.Parse(fullValue.Substring(4, 2)));

                                if (qso.End < qso.Start)
                                {
                                    qso.End = qso.Start;
                                }
                            }
                            break;

                        case "TIME_OFF":
                            {
                                string fullValue = value;

                                if (fullValue.Length == 4)
                                {
                                    // Pad out to a six character time string.

                                    fullValue += "00";
                                }
                                qso.End = new DateTime(
                                    qso.End.Year,
                                    qso.End.Month,
                                    qso.End.Day,
                                    int.Parse(fullValue[..2]),
                                    int.Parse(fullValue.Substring(2, 2)),
                                    int.Parse(fullValue.Substring(4, 2)));

                                if (qso.Start > qso.End)
                                {
                                    qso.Start = qso.End;
                                }
                            }
                            break;

                        case "CALL":
                            {
                                qso.Call = value;

                                // If the callsign was generated by this library, then its DXCC, CQ Zone, ITU Zone, and Continent
                                // are available for saving in the default QSOs fields.  However, it if is not a generated one,
                                // then these fields are left blank as the library doesn't have the capability of looking these
                                // items up; in that case, the source XSLT file will need to specify the values if they are wanted.

                                CallEntry callEntry = calls.Previous(value);
                                if (callEntry != null)
                                {
                                    qso.Dxcc = callEntry.Dxcc;
                                    qso.Cqz = callEntry.CqZone;
                                    qso.Ituz = callEntry.ItuZone;
                                    qso.Cont = callEntry.Cont;
                                }
                                else
                                {
                                    qso.Dxcc = -1;
                                    qso.Cqz = 0;
                                    qso.Ituz = 0;
                                    qso.Cont = string.Empty;
                                }
                            }
                            break;

                        case "BAND":
                            {
                                if (!bands.TryGetValue(value.ToLower(), out BandEntry bandEntry))
                                {
                                    throw new Exception($"value parameter {value} is not a band in the ADIF specification");
                                }
                                qso.Band = value;
                                if (!bandEntry.IsInBand(qso.Freq))
                                {
                                    qso.Freq = float.Parse(Freq(value), adifNumberStyles, adifNumberFormatInfo);
                                }
                            }
                            break;

                        case "FREQ":
                            {
                                qso.Freq = float.Parse(value, adifNumberStyles, adifNumberFormatInfo);
                                qso.Band = Band(qso.Freq);
                            }
                            break;

                        case "BAND_RX":
                            {
                                if (!bands.TryGetValue(value.ToLower(), out BandEntry bandEntry))
                                {
                                    throw new Exception("value parameter is not a band in the ADIF specification");
                                }
                                qso.BandRx = value;
                                if (!bandEntry.IsInBand(qso.FreqRx))
                                {
                                    qso.FreqRx = float.Parse(Freq(value), adifNumberStyles, adifNumberFormatInfo);
                                }
                                //if (++messages < MaxMessages)
                                //{
                                //    ReportError("Field('" + name + "', '" + value + "') has set qso.FreqRx to " + qso.FreqRx.ToString() +
                                //                                                          " and qso.BandRx to " + qso.BandRx);
                                //}
                            }
                            break;

                        case "FREQ_RX":
                            {
                                qso.FreqRx = float.Parse(value, adifNumberStyles, adifNumberFormatInfo);
                                qso.BandRx = Band(qso.FreqRx);

                                //if (++messages < MaxMessages)
                                //{
                                //    ReportError("Field('" + name + "', '" + value + "') has set qso.FreqRx to " + qso.FreqRx.ToString() +
                                //                                                          " and qso.BandRx to " + qso.BandRx);
                                //}
                            }
                            break;

                        case "DXCC":
                            {
                                //reportError("Setting qso.DXCC to " + value);
                                qso.Dxcc = int.Parse(value);
                                // tbs note that for now, call must be set afterwards and be consistent.
                            }
                            break;

                        case "CQZ":
                            {
                                _ = int.TryParse(value, out int valueInt);
                                if (valueInt <= 0)
                                {
                                    throw new Exception(string.Format(
                                        "{0} is not a valid value for the {1} field",
                                        value,
                                        name));
                                }
                                qso.Cqz = valueInt;
                            }
                            break;

                        //case "IOTA":
                        //case "MY_IOTA":
                        //    {

                        //    }
                        //    break;

                        case "ITUZ":
                            {
                                _ = int.TryParse(value, out int valueInt);
                                if (valueInt <= 0)
                                {
                                    throw new Exception(string.Format(
                                        "{0} is not a valid value for the {1} field",
                                        value,
                                        name));
                                }
                                qso.Ituz = valueInt;
                            }
                            break;

                        case "CONT":
                            if (value.Length != 2 || (!char.IsLetter(value[0])) || (!char.IsLetter(value[1])))
                            {
                                throw new Exception(string.Format(
                                    "{0} is not a valid value for the {1} field",
                                    value,
                                    name));
                            }
                            qso.Cont = value;
                            break;
                    }

                    if (adiStyle)
                    {
                        _ = field.AppendFormat(
                            "<{0}:{1}{2}>{3}",
                            name,
                            value.Length.ToString(),
                            string.IsNullOrEmpty(dataTypeIndicator) ?
                                string.Empty :
                                ":" + dataTypeIndicator,
                            value).
                              Append(fieldSeparator);
                    }
                    else
                    {
                        _ = field.AppendFormat(
                            "<{0}{1}>{2}</{0}>",
                            Encode(name.ToUpper()),
                            string.IsNullOrEmpty(dataTypeIndicator) || isAdifDefinedQsoField  // Data Type Indicator is not allowed in ADX ADIF-defined QSO fields.
                                ? string.Empty
                                : " DATATYPEINDICATOR=\"" + dataTypeIndicator + "\"",
                            Encode(value)).
                              Append(fieldSeparator);
                    }
                }
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.Field({0}, {1}, {2}) Exception: {3}",
                    StringToNullOrString(name),
                    StringToNullOrString(value),
                    StringToNullOrString(dataTypeIndicator),
                    exc.Message));
                throw;
            }
            return field.ToString();
        }

        public string UserDefNField(
            string name,
            string dataTypeIndicator,
            int number,
            string enumeration)
        {
            StringBuilder field = new(2048);

            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new Exception("name parameter is zero-length or null");
                }
                if (string.IsNullOrEmpty(dataTypeIndicator))
                {
                    throw new Exception("dataTypeIndicator parameter is zero-length or null");
                }
                if (number < 1)
                {
                    throw new Exception("number parameter is less than 1");
                }

                string nameUpper = name.ToUpper();

                if (!hasHeaderFields)
                {
                    throw new Exception("Trying to add a USERDEFn header field after the Initialize method specified hasHeaderFields = false");
                }
                else if (string.IsNullOrEmpty(dataTypeIndicator) ||
                         dataTypeIndicator.Length != 1)
                {
                    throw new Exception("Invalid data type indicator");
                }
                else if (recordFieldsEmitted.ContainsKey(nameUpper))
                {
                    throw new Exception("USERDEFn field name already included in record");
                }
                else
                {
                    {
                        FieldEntry fieldEntry;

                        if (fields.TryGetValue(nameUpper, out FieldEntry value))
                        {
                            fieldEntry = value;
                            string message = fieldEntry.Variant switch
                            {
                                FieldEntry.FieldVariant.Adif => "USERDEF field cannot have the same name as an ADIF-defined field",
                                FieldEntry.FieldVariant.User => "USERDEF field has already been declared in a USERDEFn field",
                                _ => "Internal error: USERDEF field name is already defined as an APP or unexpected field variant",
                            };
                            throw new Exception(message);
                        }
                        if (FieldEntry.ContainsUserDefNumber(fields, number))
                        {
                            throw new Exception("USERDEFn field number has already been used in a USERDEFn field");
                        }
                        fieldEntry = new FieldEntry(dataTypes, nameUpper, false, dataTypeIndicator[0], number, enumeration);
                        fields.Add(nameUpper, fieldEntry);
                    }
                    // tbs doesn't quite work ... need to validate name validateCharacters(nameUpper, value);

                    field.Append(Bor());
                    recordFieldsEmitted.Add(nameUpper, number.ToString());

                    if (adiStyle)
                    {
                        string _enum = enumeration.Length > 0 ?
                            "," + enumeration :
                            string.Empty;

                        field.AppendFormat(
                            "<USERDEF{0}:{1}:{2}>{3}{4}",
                            number.ToString(),                          // 0
                            (name.Length + _enum.Length).ToString(),    // 1
                            dataTypeIndicator,                          // 2
                            name,                                       // 3
                            _enum).                                     // 4
                              Append(fieldSeparator);
                    }
                    else
                    {
                        string range = enumeration.Contains(':') ?
                            string.Format(" RANGE=\"{0}\"", Encode(enumeration)) :
                            string.Empty;

                        string _enum = enumeration.Contains(',') ?
                            string.Format(" ENUM=\"{0}\"", Encode(enumeration)) :
                            string.Empty;

                        field.AppendFormat(
                            "<USERDEF FIELDID=\"{0}\" TYPE=\"{1}\"{2}{3}>{4}</USERDEF>",
                            number.ToString(),  // 0
                            dataTypeIndicator,  // 1
                            range,              // 2
                            _enum,              // 3
                            Encode(nameUpper)). // 4   In ADX, only uppercase names are allowed.
                              Append(fieldSeparator);
                    }
                }
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.UserDefNField(string, string, int, string) ({0}, {1}, {2}, {3}) Exception: {4}",
                    StringToNullOrString(name),
                    StringToNullOrString(dataTypeIndicator),
                    number.ToString(),
                    StringToNullOrString(enumeration),
                    exc.Message));
                throw;
            }
            return field.ToString();
        }

        public string UserDefField(
            string name,
            string value)
        {
            // TBS should check value against ADIF spec data types.

            StringBuilder field = new(2048);

            try
            {
                FieldEntry fieldEntry;

                if (string.IsNullOrEmpty(name))
                {
                    throw new Exception("name parameter is zero-length or null");
                }
                if (string.IsNullOrEmpty(value))
                {
                    throw new Exception("value parameter is zero-length or null");
                }

                string nameUpper = name.ToUpper();
                if (!fields.ContainsKey(nameUpper))
                {
                    throw new Exception("Undeclared USERDEF field");
                }
                else
                {
                    fieldEntry = fields[nameUpper];

                    if (fieldEntry.Header && !inHeader)
                    {
                        throw new Exception("Header field not allowed in record section");
                    }
                    switch (fieldEntry.Variant)
                    {
                        case FieldEntry.FieldVariant.Adif:
                            throw new Exception("ADIF-defined fields must be emitted using the Field or Record methods");

                        case FieldEntry.FieldVariant.App:
                            throw new Exception("APP_ field must be emitted using the AppField method");

                        case FieldEntry.FieldVariant.User:
                            break;

                        default:
                            throw new Exception(string.Format(
                                "Internal error: unexpected FieldVariant: {0}",
                                ((int)fieldEntry.Variant).ToString()));
                    }
                    fieldEntry.CheckValidUserDefEnumerationValue(value);
                }

                if (!hasHeaderFields)
                {
                    throw new Exception("Trying to add a USERDEF field after the Initialize method specified hasHeaderFields = false");
                }
                else if (recordFieldsEmitted.ContainsKey(nameUpper))
                {
                    throw new Exception("USERDEF field already included in record");
                }
                else
                {
                    bool intlChars = fieldEntry.DataType.DataTypeIndicator == 'I' || fieldEntry.DataType.DataTypeIndicator == 'G';
                    bool chars = fieldEntry.DataType.DataTypeIndicator == 'S' || fieldEntry.DataType.DataTypeIndicator == 'M';

                    if ((intlChars && adiStyle) ||
                        (chars && !adiStyle))
                    {
                        // Skip international character fields in ADI files and skip non-international character fields in ADX files.
                    }
                    else
                    {
                        field.Append(Bor());
                        recordFieldsEmitted.Add(nameUpper, value);
                        fieldEntry.ValidateValue(value, adiStyle);
                        fieldEntry.Occurrences++;

                        if (adiStyle)
                        {
                            field.AppendFormat(
                                "<{0}:{1}>{2}",
                                name,                       // 0
                                value.Length.ToString(),    // 1
                                value).                     // 2 
                                 Append(fieldSeparator);
                        }
                        else
                        {
                            field.AppendFormat(
                                "<USERDEF FIELDNAME=\"{0}\">{1}</USERDEF>",
                                Encode(nameUpper),  // 0  Only uppercase names are allowed in ADX
                                Encode(value)).     // 1
                                  Append(fieldSeparator);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.UserDefField({0}, {1}) Exception: {2}",
                    StringToNullOrString(name),
                    StringToNullOrString(value),
                    exc.Message));
                throw;
            }
            return field.ToString();
        }

        public string AppField(
            string name,
            string value,
            string programId) => AppField(name, value, programId, string.Empty);

        public string AppField(
            string name,
            string value,
            string programId,
            string dataTypeIndicator)
        {
            // TBS should check value against ADIF spec data types.

            StringBuilder field = new(2048);

            try
            {
                totalFields++;
                if (string.IsNullOrEmpty(name))
                {
                    throw new Exception("name parameter is zero-length or null");
                }
                if (string.IsNullOrEmpty(value))
                {
                    throw new Exception("value parameter is zero-length or null");
                }
                if (string.IsNullOrEmpty(programId))
                {
                    throw new Exception("programId parameter is zero-length or null");
                }

                string fullyQualifiedUpperName = string.Format(
                    "APP_{0}_{1}",
                    programId.ToUpper(),
                    name.ToUpper());

                if ((!string.IsNullOrEmpty(dataTypeIndicator)) &&
                    dataTypeIndicator.Length != 1)
                {
                    throw new Exception("Data type indicators must be a single character");
                }
                else
                {
                    FieldEntry fieldEntry;

                    if (!fields.ContainsKey(fullyQualifiedUpperName))
                    {
                        if (string.IsNullOrEmpty(dataTypeIndicator))
                        {
                            // This is the first time this APP_ field has appeared in the output and a
                            // data type indicator has not been provided, so default to the widest possible data type,
                            // which is either M or G.

                            dataTypeIndicator = adiStyle ?
                                "M" :   // Multiline string is the widest possible data type for ADI.
                                "G";    // Multiline string is the widest possible data type for ADX.
                        }
                        fieldEntry = new FieldEntry(dataTypes, fullyQualifiedUpperName, false, dataTypeIndicator[0]);
                        fields.Add(fullyQualifiedUpperName, fieldEntry);
                    }
                    else
                    {
                        fieldEntry = fields[fullyQualifiedUpperName];

                        if (fieldEntry.Header && !inHeader)
                        {
                            throw new Exception("Header field not allowed in record section");
                        }
                        switch (fieldEntry.Variant)
                        {
                            case FieldEntry.FieldVariant.Adif:
                                throw new Exception(string.Format(
                                    "Internal error: unexpected FieldVariant: {0}",
                                    ((int)fieldEntry.Variant).ToString()));

                            case FieldEntry.FieldVariant.App:
                                break;

                            case FieldEntry.FieldVariant.User:
                                throw new Exception("USERDEF field must be output using the UserDefField method");

                            default:
                                throw new Exception(string.Format(
                                    "Internal error: unexpected FieldVariant: {0}",
                                    ((int)fieldEntry.Variant).ToString()));
                        }

                        if ((!string.IsNullOrEmpty(dataTypeIndicator)) &&
                            fieldEntry.DataType.DataTypeIndicator != dataTypeIndicator[0])
                        {
                            throw new Exception(string.Format(
                                "the '{0}' field has already been included in a QSO with a different Data Type Indicator of '{1}'",
                                fullyQualifiedUpperName,
                                fieldEntry.DataType.DataTypeIndicator));
                        }
                    }

                    if (recordFieldsEmitted.ContainsKey(fullyQualifiedUpperName))
                    {
                        throw new Exception(string.Format(
                            "APP_ field name {0} has already been included in this record",
                            fullyQualifiedUpperName));
                    }

                    bool intlChars = fieldEntry.DataType.DataTypeIndicator == 'I' || fieldEntry.DataType.DataTypeIndicator == 'G';
                    bool chars = fieldEntry.DataType.DataTypeIndicator == 'S' || fieldEntry.DataType.DataTypeIndicator == 'M';

                    if ((intlChars && adiStyle) ||
                        (chars && !adiStyle))
                    {
                        // Skip international character fields in ADI files and skip non-international character fields in ADX files.
                    }
                    else
                    {
                        field.Append(Bor());
                        recordFieldsEmitted.Add(fullyQualifiedUpperName, value);
                        fieldEntry.ValidateValue(value, adiStyle);
                        fieldEntry.Occurrences++;

                        if (adiStyle)
                        {
                            field.AppendFormat(
                                "<APP_{0}_{1}:{2}:{3}>{4}",
                                programId,                  // 0
                                name,                       // 1
                                value.Length.ToString(),    // 2
                                dataTypeIndicator,          // 3
                                value).                     // 4
                                  Append(fieldSeparator);
                        }
                        else
                        {
                            field.AppendFormat(
                                "<APP PROGRAMID=\"{0}\" FIELDNAME=\"{1}\" TYPE=\"{2}\">{3}</APP>",
                                Encode(programId),      // 0
                                Encode(name.ToUpper()), // 1
                                dataTypeIndicator,      // 2
                                Encode(value)).         // 3
                                  Append(fieldSeparator);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                ReportError(string.Format(
                    "AdifXslt.AppField({0}, {1}, {2}, {3}) Exception: {4}",
                    StringToNullOrString(name),
                    StringToNullOrString(value),
                    StringToNullOrString(dataTypeIndicator),
                    StringToNullOrString(programId),
                    exc.Message));

                ReportError(string.Format("Stack trace:\r\n\r\n{0}", exc.StackTrace));
                throw;
            }
            return field.ToString();
        }

        private string Bor()
        {
            StringBuilder text = new(32);

            if (adiStyle)
            {
                if (hasHeaderFields && inHeader && recordFieldsEmitted.Count == 0)
                {
                    // If an ADI file has a header, the file must not start with an opening chevron

                    text.Append(' ');
                }
            }
            else
            {
                if (recordFieldsEmitted.Count == 0 && !inHeader)
                {
                    // Bor() is called for every header and record field.
                    //
                    // In ADX files, the <HEADER> tag exists even if there are no header fields.
                    // For this reason, <HEADER> is emitted by a previous call Bof() and Bor() never emits it.

                    text.Append("<RECORD>");
                    text.Append(fieldSeparator);
                }
            }
            return text.ToString();
        }

        public string Eoh()
        {
            StringBuilder text = new(32);

            if (adiStyle)
            {
                if (hasHeaderFields)
                {
                    text.Append("<EOH>").
                         Append(recordSeparator);
                }
            }
            else
            {
                text.Append("</HEADER>").
                     Append(recordSeparator).
                     Append("<RECORDS>").
                     Append(fieldSeparator);
            }
            recordFieldsEmitted.Clear();
            inHeader = false;
            return text.ToString();
        }

        private string Eor()
        {
            StringBuilder field = new(32);

            totalRecords++;

            qso.Next(recordFieldsEmitted.TryGetValue("CALL", out string value) ? value : string.Empty);
            if (adiStyle)
            {
                field.Append("<EOR>").
                      Append(recordSeparator);
            }
            else
            {
                field.Append("</RECORD>").
                      Append(recordSeparator);
            }
            recordFieldsEmitted.Clear();
            return field.ToString();
        }

        public string Bof()
        {
            StringBuilder text = new(32);

            if (!adiStyle)
            {
                text.Append("<?xml version=\"1.0\" encoding=\"utf-8\" ?>").
                     Append(fieldSeparator).
                     Append("<ADX>").
                     Append(fieldSeparator).
                     Append("<HEADER>").
                     Append(fieldSeparator);
            }
            return text.ToString();
        }

        public string Eof()
        {
            StringBuilder text = new(32);

            if (!adiStyle)
            {
                text.Append("</RECORDS>").
                     Append(fieldSeparator).
                     Append("</ADX>");
            }
            return text.ToString();
        }

        public string Record(params string[] args)
        {
            //if (++messages < MaxMessages)
            //{
            //    ReportError("Record() starting");
            //}

            StringBuilder record = new(2048);

            try
            {
                if (args.Length % 2 != 0)
                {
                    throw new Exception("Number of parameters supplied is not an even number");
                }
                else
                {
                    string emptyField = null;

                    for (int i = 0; i < args.Length; i += 2)
                    {
                        string
                            name = args[i],
                            value = args[i + 1];

                        if (value.Length == 0)
                        {
                            if (name == "QSO_DATE" || name == "TIME_ON")
                            {
                                // Ignore zero-length QSO_DATE and TIME_ON fields because they are emitted for many QSOs that involve
                                // (MY_)DXCC fields depending on whether or not the QSO is for a specified historic date and time
                                // (i.e. usually DXpeditions).
                            }
                            else
                            {
                                // Defer outputting the error message so that all fields can be seen in the error message.
                                // If there is more than one empty field, this will just report the last one in the record.

                                emptyField = name;
                            }
                        }
                        else
                        {
                            record.Append(Field(name, value));
                        }
                    }

                    if (emptyField != null && ++messages < MaxMessages)
                    {
                        ReportError($"An empty {emptyField} field should never be generated by the XSLT template file\r\n\r\n{record}");
                    }

                    //if (++messages < MaxMessages)
                    //{
                    //    ReportError($"Record() has generated {record}");
                    //}
                }
                if (!recordFieldsEmitted.ContainsKey("QSO_DATE"))
                {
                    record.Append(Field("QSO_DATE", QsoDate()));
                }
                if (!recordFieldsEmitted.ContainsKey("TIME_ON"))
                {
                    record.Append(Field("TIME_ON", TimeOn6()));
                }
                if (!recordFieldsEmitted.ContainsKey("TIME_OFF"))
                {
                    record.Append(Field("TIME_OFF", TimeOff6()));
                }
                if (!recordFieldsEmitted.ContainsKey("CALL"))
                {
                    record.Append(Field("CALL", Call()));
                }
                if (!recordFieldsEmitted.ContainsKey("BAND"))
                {
                    record.Append(Field("BAND", Band()));
                }
                if (!recordFieldsEmitted.ContainsKey("FREQ"))
                {
                    record.Append(Field("FREQ", Freq()));
                }
                if (!recordFieldsEmitted.ContainsKey("MODE"))
                {
                    record.Append(Field("MODE", Mode()));
                }
                record.Append(Eor());
            }
            catch (Exception exc)
            {
                StringBuilder argList = new(64);

                foreach (string arg in args)
                {
                    if (argList.Length > 0)
                    {
                        argList.Append(", ");
                    }
                    argList.Append(StringToNullOrString(arg));
                }
                ReportError(string.Format(
                    "AdifXslt.Record({0}) Exception: {1}",
                    argList.ToString(),
                    exc.Message));
                throw;
            }
            return record.ToString();
        }

        /**
         * <summary>
         *   This logs a message and is for diagnostic purposes; as such, it will not be included in released
         *   versions of the XSLT templates file.
         * </summary>
         * 
         * <param name="message">The message to be logged.</param>
         */
        public static string Log(string message)
        {
            Logger.Log(message);
            return string.Empty;
        }

        /*
            various approaches to incorporating XML test data in the XSLT file.
            Originally the document() method was used a number of times but Visual Studio 2022 showed
            a warning for each one in its "Error List" window although it worked fine a runtime
                "Execution of the 'document()' function was prohibited. Use the XsltSettings.EnableDocumentFunction property to enable it."
            
            The deluge of these useless warnings were making real warnings hard to see and apparently
            there is no way of configuring Visual Studio 2022 to ignore this error.
                      
            [1] Originally the document() method was used a number of times, causing the useless warnings:
               
                ...
                xmlns:ex="http://adif.org.uk/adiftestexamples"
                ...
                <ex:booleanValues>
                  <ex:boolean value="Y"/>
                  <ex:boolean value="N"/>
                  <ex:boolean value="y"/>
                  <ex:boolean value="n"/>
                </ex:booleanValues> 
                ...
                <xsl:when test="$fieldName='FORCE_INIT'">
                <xsl:for-each select="document('')//ex:booleanValues/ex:boolean/@value">
                  <xsl:value-of select="ae:record($fieldName, ., 'BAND', '2m')"/>
                  </xsl:for-each>
                </xsl:when>
                ...

            [2] One improvement to the above was to assign a variable using the document('') function so that
                there was only one call to document('') and hence only one warning message from Visual Studio:

                ...
                xmlns:ms="urn:schemas-microsoft-com:xslt"                ...
                xmlns:ex="http://adif.org.uk/adiftestexamples"
                ...
                <ex:booleanValues>
                  <ex:boolean value="Y"/>
                  <ex:boolean value="N"/>
                  <ex:boolean value="y"/>
                  <ex:boolean value="n"/>
                </ex:booleanValues> 
                ...
                <xsl:variable name="doc" select="document('')"/>   
                <xsl:variable name="booleans" select="$doc//ex:booleanValues/ex:boolean/@value"/>
                ...
                <xsl:when test="$fieldName='FORCE_INIT'">
                <xsl:for-each select="ms:node-set($booleans)">
                  <xsl:value-of select="ae:record($fieldName, ., 'BAND', '2m')"/>
                  </xsl:for-each>
                </xsl:when>
                ...

            [3] To avoid the use of document('') altogether, a different method was to pass XML
                as a string to an extension function (see the NodeSet method above).  This worked
                but to make the XML in the XSLT file readable, needed substituting for the
                "<", ">", and "'" characters because they are not allowed in XML element attributes:
                
                ...
                xmlns:ms="urn:schemas-microsoft-com:xslt"                ...
                xmlns:ex="http://adif.org.uk/adiftestexamples"
                xmlns:ae="urn:adifxsltextension">
                ...
                <xsl:variable name="booleans" select="ae:nodeset('
                  {booleanValues}
                      {boolean value=!Y!/}
                      {boolean value=!N!/}
                      {boolean value=!y!/}
                      {boolean value=!n!/}
                  {/booleanValues}',
                  'booleanValues/boolean/@value')"/>
                ...                
                <xsl:when test="$fieldName='FORCE_INIT'">
                <xsl:for-each select="ms:node-set($booleans)">
                  <xsl:value-of select="ae:record($fieldName, ., 'BAND', '2m')"/>
                  </xsl:for-each>
                </xsl:when>
                ...

            [4] Finally, a variable was set to an XML fragment and then a second variable was used
                to select a node set from it.  This provided better readability than [3] above and
                the only drawback was the requirement for two variables (as far as I can see, it is
                not possible using a single variable):

                ...
                xmlns:ms="urn:schemas-microsoft-com:xslt"                ...
                xmlns:ae="urn:adifxsltextension">
                ...
                <xsl:variable name="booleanValues">
                    <values>
                    <value>Y</value>
                    <value>N</value>
                    <value>y</value>
                    <value>n</value>
                    </values>
                </xsl:variable>
                <xsl:variable name="booleans" select="ms:node-set($booleanValues)/values/value"/>
                ...                
                <xsl:when test="$fieldName='FORCE_INIT'">
                <xsl:for-each select="ms:node-set($booleans)">
                  <xsl:value-of select="ae:record($fieldName, ., 'BAND', '2m')"/>
                  </xsl:for-each>
                </xsl:when>
                ...
         */

        /*
         * <summary>
         *   Creates a node set from a <see cref="string"/> containing an XML document and a <see cref="string"/> containing an XPATH statement.
         * </summary>
         * 
         * <param name="xml">An XML document.&#160; The "&lt;", "&gt;", and "'" characters are represented by "{", "}", and "!".</param>
         * <param name="select">An XPATH statement.</param>
         * 
         * <returns>An <see cref="XPathNodeIterator"/> object for the node set.</returns>
         */
        //public XPathNodeIterator NodeSet(string xml, string select)
        //{
        //    XmlDocument doc = new XmlDocument
        //    {
        //        PreserveWhitespace = true
        //    };

        //    xml = xml.Replace('{', '<').Replace('}', '>').Replace('!', '\"');

        //    doc.LoadXml(xml);

        //    return doc.CreateNavigator().Select(select);
        //}

        //public string Experiment(XPathNodeIterator iterator)
        //{
        //    foreach (object obj in iterator)
        //    {
        //        Logger.Log(obj.GetType().ToString());
        //    }

        //    Logger.Log(iterator.GetType().ToString());
        //    Logger.Log(iterator.Count.ToString());

        //    return iterator.GetType().ToString();
        //}
    }
}
