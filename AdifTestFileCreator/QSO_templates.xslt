<?xml version="1.0" encoding="UTF-8"?>

<!--
This XSLT file transforms the XML file exported from the ADIF Specification into test QSOs in ADI and ADX file format.

All fields and enumerations within the specification are included in QSO records except for "Deleted" and "Import-only"
(deprecated) items.

It is ADIF version-specific because each new version of the specification inttroduces changes to enumerations, fields,
etc.

It uses features in Microsoft .NET and XSLT version 1.0 along with Microsoft and custom extension functions.
The custom extension functions provide a library that generates ADI and ADX formatted output.  Their returned values
must be used to create all output or the ADI or ADX generated may be incorrect.

Parameters:
  adifStyle is an XSLT string that can have these values:
      
    Value  Description
    =====  ===========
    'adi'  Create ADI file format.
    'adx'  Create ADX file format.

Change History:
  2024-10-28: Created for ADIF Specification 3.1.5
-->
<xsl:stylesheet version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:ms="urn:schemas-microsoft-com:xslt"
  xmlns:dt="urn:schemas-microsoft-com:datatypes"
  xmlns:ex="http://adif.org.uk/adiftestexamples"
  xmlns:ae="urn:adifxsltextension">

  <xsl:output method="text" encoding="UTF-8"/>

  <xsl:param    name="adifStyle"      select="'adi'"/>
  <xsl:variable name="linePerRecord"  select="false()"/>

  <xsl:variable name="fieldSeparator">
    <xsl:choose>
      <xsl:when test="$linePerRecord">
        <xsl:text> </xsl:text>
      </xsl:when>
      <xsl:otherwise>
        <xsl:text>&#13;&#10;</xsl:text>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:variable>
  
  <xsl:variable name="recordSeparator">
    <xsl:choose>
      <xsl:when test="$linePerRecord">
        <xsl:text>&#13;&#10;</xsl:text>
      </xsl:when>
      <xsl:otherwise>
        <xsl:text>&#13;&#10;&#13;&#10;</xsl:text>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:variable>

  <xsl:template name="deletedOrReadOnly">
    <xsl:param name="name"/>
    <xsl:param name="value"/>
    <xsl:variable name="valueText">
      <xsl:choose>
        <xsl:when test="$value != ''">
          <xsl:value-of select="concat(' value ', $value)"/>
        </xsl:when>
      </xsl:choose>
    </xsl:variable>
    <xsl:value-of select="ae:commentLine2(concat(
      '==== Not including ',
      $name,
      $valueText,
      ' because it is Deleted or Import-only (deprecated)'))"/>
  </xsl:template>

  <xsl:template name="arrlSectFromDxccCsv">
    <!-- The DXCC Entity Code in the ARRL Section enumeration is comma-separated value list.
         This template creates a QSO record for each DXCC entity code in the list by calling itself recursively. -->
    <xsl:param name="fieldName" />
    <xsl:param name="abbreviation" />
    <xsl:param name="cnt" />
    <xsl:param name="txt" />    
    <xsl:element name="{concat('Field',$cnt)}">
      <xsl:variable name="dxcc" select="normalize-space(substring-before($txt,','))"/>
      <xsl:variable name="dxccRecord" select="/adif/enumerations/enumeration[@name='DXCC_Entity_Code']/record[value[@name='Entity Code'] = $dxcc]"/>
      <xsl:choose>
        <!-- The Microsoft documentation says that MSXML later than version 2 requires their node-set() function
             when using a variable as node set. -->
        <xsl:when test="ms:node-set($dxccRecord)/value[@name='Import-only' or @name='Deleted']">
          <xsl:call-template name="deletedOrReadOnly">
            <xsl:with-param name="name" select="$fieldName"/>
            <xsl:with-param name="value" select="concat($abbreviation, ' (DXCC ', $dxcc, ' deleted)')"/>
          </xsl:call-template>
        </xsl:when>
        <xsl:otherwise>
          <xsl:value-of select="ae:record($fieldName, value[@name='Abbreviation'], 'CALL', ae:callForDxcc($dxcc), 'DXCC', $dxcc)"/>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:element>
    <xsl:if test="normalize-space(substring-after($txt,',')) != ''">
      <xsl:call-template name="arrlSectFromDxccCsv">
        <xsl:with-param name="fieldName" select="$fieldName" />
        <xsl:with-param name="abbreviation" select="$abbreviation" />
        <xsl:with-param name="cnt" select="$cnt + 1" />
        <xsl:with-param name="txt" select="substring-after($txt,',')" />
      </xsl:call-template>
    </xsl:if>
  </xsl:template>

  <xsl:variable name="kosovoDxccNone" select=  "0"/>
  <xsl:variable name="canadaDxcc"     select=  "1"/>
  <xsl:variable name="fijiDxcc"       select="176"/>
  <xsl:variable name="englandDxcc"    select="223"/>
  <xsl:variable name="franceDxcc"     select="227"/>
  <xsl:variable name="germanyDxcc"    select="230"/>
  <xsl:variable name="scotlandDxcc"   select="279"/>
  <xsl:variable name="usaDxcc"        select="291"/>
  <xsl:variable name="walesDxcc"      select="294"/>
  <xsl:variable name="serbiaDxcc"     select="296"/>
  <xsl:variable name="kosovoDxcc"     select="522"/>  

  <xsl:variable name="booleanValues">
    <ex:values>
      <ex:value>Y</ex:value>
      <ex:value>N</ex:value>
      <ex:value>y</ex:value>  
      <ex:value>n</ex:value>
    </ex:values>
  </xsl:variable>
    
  <xsl:variable name="booleans" select="ms:node-set($booleanValues)/ex:values/ex:value"/>

  <xsl:template match="/">

    <!-- Experiment: <xsl:value-of select="system-property('ms:version')"/> -->

    <xsl:value-of select="ae:initialize($adifStyle, true(), /adif)"/>
    <xsl:value-of select="ae:setOptions($fieldSeparator, $recordSeparator)"/>
    <xsl:value-of select="ae:bof()"/>

    <xsl:variable name="programId"         select="ae:programId()"/>        
    <xsl:variable name="version306OrLater" select="ae:adifVersionInt() >= 306"/>
    
    <xsl:for-each select="adif/fields/record">
      <xsl:variable name="fieldName" select="value[@name]"/>

      <xsl:choose>
        <xsl:when test="$fieldName='ADIF_VER'">
          <xsl:value-of select="ae:field($fieldName, ae:adifVersion())"/>
        </xsl:when>

        <xsl:when test="$fieldName='CREATED_TIMESTAMP'">
          <xsl:value-of select="ae:field($fieldName, ae:createdTimestamp())"/>
        </xsl:when>

        <xsl:when test="$fieldName='PROGRAMID'">
          <xsl:value-of select="ae:field($fieldName, $programId)"/>
        </xsl:when>

        <xsl:when test="$fieldName='PROGRAMVERSION'">
          <xsl:value-of select="ae:field($fieldName, ae:programVersion())"/>
        </xsl:when>

        <xsl:when test="$fieldName='USERDEFn'">
          <xsl:value-of select="ae:userDefNField('RCVD_FROM_DX_CLUSTER',      'B',  1, '')"/>
          <xsl:value-of select="ae:userDefNField('MY_POWER_CATEGORY',         'E',  2, '{QRPP,QRP,QRO}')"/>
          <xsl:value-of select="ae:userDefNField('MY_TEMPERATURE_FAHRENHEIT', 'N',  3, '{-50:150}')"/>
          <xsl:value-of select="ae:userDefNField('MY_HEIGHT_ASL_FEET',        'N',  4, '')"/>
          <xsl:value-of select="ae:userDefNField('NEXT_QSL_POSTING_DATE',     'D',  5, '')"/>
          <xsl:value-of select="ae:userDefNField('NEXT_QSL_POSTING_TIME',     'T',  6, '')"/>
          <xsl:value-of select="ae:userDefNField('MY_AMP',                    'S',  7, '')"/>
          <xsl:value-of select="ae:userDefNField('MY_AMP_INTL',               'I',  8, '')"/>
          <xsl:value-of select="ae:userDefNField('QSO_TRANSCRIPT',            'M',  9, '')"/>
          <xsl:value-of select="ae:userDefNField('QSO_TRANSCRIPT_INTL',       'G', 10, '')"/>
          <xsl:value-of select="ae:userDefNField('REMOTE_STATION_LAT',        'L', 11, '')"/>
          <xsl:value-of select="ae:userDefNField('REMOTE_STATION_LON',        'L', 12, '')"/>
          <xsl:value-of select="ae:eoh()"/>

          <xsl:value-of select="ae:userDefField('REMOTE_STATION_LAT',        'S018 28.430')"/>
          <xsl:value-of select="ae:userDefField('REMOTE_STATION_LON',        'W021 22.892')"/>
          <xsl:value-of select="ae:userDefField('QSO_TRANSCRIPT_INTL',       'NAME FRED&#13;&#10;QTH ALENÇON&#13;&#10;73 ES GOOD DX')"/>
          <xsl:value-of select="ae:userDefField('QSO_TRANSCRIPT',            'NAME FRED&#13;&#10;QTH ALENCON&#13;&#10;73 ES GOOD DX')"/>
          <xsl:value-of select="ae:userDefField('MY_AMP_INTL',               '1 KW')"/>
          <xsl:value-of select="ae:userDefField('MY_AMP',                    '1 KW')"/>
          <xsl:value-of select="ae:userDefField('NEXT_QSL_POSTING_TIME',     '1500')"/>
          <xsl:value-of select="ae:userDefField('NEXT_QSL_POSTING_DATE',     '20231101')"/>
          <!-- Note that ADX user-defined field names must be uppercase; ae:userDefNfield and ae:userDefField automatically convert them when ADX is being generated. -->
          <xsl:value-of select="ae:userDefField('my_height_asl_feet',        '150.5')"/>
          <xsl:value-of select="ae:userDefField('My_Temperature_Fahrenheit', '66')"/>
          <xsl:value-of select="ae:userDefField('mY_poweR_categorY',         'QrpP')"/>
          <xsl:value-of select="ae:userDefField('RCVD_FROM_DX_CLUSTER',      'N')"/>
          <xsl:value-of select="ae:record()"/>

          <xsl:value-of select="ae:appField('USING_YAGI',         'Y',                  $programId, 'B')"/>
          <xsl:value-of select="ae:appField('WORDS_PER_MINUTE',   '18',                 $programId, 'N')"/>
          <xsl:value-of select="ae:appField('NEXT_SKED_DATE',     '20180101',           $programId, 'D')"/>
          <xsl:value-of select="ae:appField('NEXT_SKED_TIME_ON',  '2130',               $programId, 'T')"/>
          <xsl:value-of select="ae:appField('NEXT_SKED_TIME_OFF', '223000',             $programId, 'T')"/>
          <xsl:value-of select="ae:appField('MY_OS',              'Ubuntu 18.04.4 LTS', $programId, 'S')"/>
          <xsl:value-of select="ae:appField('MY_OS_INTL',         'Ubuntu 18.04.4 LTS', $programId, 'I')"/>
          <xsl:value-of select="ae:appField('WX',                 'Cloud scattered&#13;&#10;Light rain',
                                                                                        $programId, 'M')"/>
          <xsl:value-of select="ae:appField('WX_INTL',            'Cloud scattered&#13;&#10;Light rain',
                                                                                        $programId, 'G')"/>
          <xsl:value-of select="ae:appField('NEXT_LAT',           'S018 28.430',        $programId, 'L')"/>
          <xsl:value-of select="ae:appField('NEXT_LON',           'W021 22.892',        $programId, 'L')"/>
          <xsl:value-of select="ae:record(  'MODE',               'CW')"/>

          <!-- Application fields may be for another application, so the program ID can have different values in one file. -->
          <xsl:value-of select="ae:appField('SENT_TO_DX_CLUSTER', 'Y',             'Other',    'B')"/>
          <xsl:value-of select="ae:appField('WORDS_PER_MINUTE',   '14',            $programId, 'N')"/>
          <xsl:value-of select="ae:record(  'MODE',               'CW')"/>
        </xsl:when>

        <xsl:when test="$fieldName='ADDRESS'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            $fieldName,                  '123 My Street&#13;&#10;My Town&#13;&#10;My Post Code',
            concat($fieldName, '_INTL'), '123 My Street&#13;&#10;Ähtäri&#13;&#10;My Post Code')"/>
        </xsl:when>

        <!-- Tested along with ADDRESS -->
        <xsl:when test="$fieldName='ADDRESS_INTL'"/>

        <xsl:when test="$fieldName='AGE'">
          <xsl:value-of select="ae:record($fieldName,   '9')"/>
          <xsl:value-of select="ae:record($fieldName,  '57')"/>
          <xsl:value-of select="ae:record($fieldName, '102')"/>
        </xsl:when>

        <xsl:when test="$fieldName='ALTITUDE' or $fieldName='MY_ALTITUDE'">
          <xsl:value-of select="ae:record($fieldName, '-20')"/>
          <xsl:value-of select="ae:record($fieldName, '150.3')"/>
          <xsl:value-of select="ae:record($fieldName, '5000')"/>
        </xsl:when>        
        
        <xsl:when test="$fieldName='ANT_AZ'">
          <xsl:value-of select="ae:record($fieldName,   '0')"/>
          <xsl:value-of select="ae:record($fieldName, '108.3')"/>
          <xsl:value-of select="ae:record($fieldName, '360')"/>
        </xsl:when>

        <xsl:when test="$fieldName='ANT_EL'">
          <!-- This is an opportunity also to test handling of negative numbers. -->
          <xsl:value-of select="ae:record($fieldName, '-90')"/>
          <xsl:value-of select="ae:record($fieldName, '-35.5')"/>
          <xsl:value-of select="ae:record($fieldName,   '-.5')"/>
          <xsl:value-of select="ae:record($fieldName,  '-0.5')"/>
          <xsl:value-of select="ae:record($fieldName,   '0')"/>
          <xsl:value-of select="ae:record($fieldName,  '35.5')"/>
          <xsl:value-of select="ae:record($fieldName,  '90')"/>
        </xsl:when>

        <xsl:when test="$fieldName='ANT_PATH'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Ant_Path']/record">
            <xsl:value-of select="ae:record($fieldName, value[@name='Abbreviation'])"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='ARRL_SECT' or $fieldName='MY_ARRL_SECT'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='ARRL_Section']/record">
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted' or @name='Deleted Date']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="value[@name='Abbreviation']"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:call-template name="arrlSectFromDxccCsv">
                  <xsl:with-param name="fieldName" select="$fieldName" />
                  <xsl:with-param name="abbreviation" select="value[@name='Abbreviation']" />
                  <xsl:with-param name="cnt" select="1" />
                  <xsl:with-param name="txt" select="concat(value[@name='DXCC Entity Code'],',')" />
                </xsl:call-template>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
        </xsl:when>

        <!-- AWARD_SUBMITTED and AWARD_GRANTED cannot be tested at ADIF 3.0.5 because there are no sponsor values
             available.  At ADIF 3.0.6, it is proposed to add "ADIF_" to the sponsors so that authentic examples can be
             included in the specification and test files. -->
        <xsl:when test="$fieldName='AWARD_SUBMITTED'">
          <xsl:if test="$version306OrLater">
            <xsl:value-of select="ae:record($fieldName, 'ADIF_CENTURY_BASIC,ADIF_CENTURY_SILVER,ADIF_SPECTRUM_100-160m')"/>
          </xsl:if>
        </xsl:when>

        <xsl:when test="$fieldName='AWARD_GRANTED'">
          <xsl:if test="$version306OrLater">
            <xsl:value-of select="ae:record($fieldName, 'ADIF_CENTURY_BASIC,ADIF_CENTURY_SILVER,ADIF_SPECTRUM_100-160m')"/>
            <xsl:value-of select="ae:record($fieldName, 'ADIF_CENTURY_BASIC', 'AWARD_SUBMITTED', 'ADIF_CENTURY_BASIC,ADIF_CENTURY_SILVER,ADIF_SPECTRUM_100-160m')"/>
          </xsl:if>
        </xsl:when>

        <xsl:when test="$fieldName='A_INDEX'">
          <xsl:value-of select="ae:record($fieldName,   '0')"/>
          <xsl:value-of select="ae:record($fieldName, '250.5')"/>
          <xsl:value-of select="ae:record($fieldName, '400')"/>
        </xsl:when>        
        
        <xsl:when test="$fieldName='BAND' or $fieldName='BAND_RX'">
          <!-- BAND and BAND_RX are tested thoroughly as part of FREQ and FREQ_RX, so just test here that abbreviations with uppercase are okay. -->
          <xsl:value-of select="ae:record($fieldName, '20M')"/>
          <xsl:value-of select="ae:record($fieldName, '70Cm')"/>
          <xsl:value-of select="ae:record($fieldName, '23CM')"/>
          <xsl:value-of select="ae:record($fieldName, '6Mm')"/>
          <xsl:value-of select="ae:record($fieldName, '4MM')"/>
        </xsl:when>

        <xsl:when test="$fieldName='CALL'">
          <xsl:value-of select="ae:record($fieldName, 'g5o')"/>
          <xsl:value-of select="ae:record($fieldName, 'G5o')"/>
          <xsl:value-of select="ae:record($fieldName, 'G2AAA/P')"/>
          <xsl:value-of select="ae:record($fieldName, 'MM/K6DF/P')"/>
          <xsl:value-of select="ae:record($fieldName, 'G2BBB/M')"/>
          <xsl:value-of select="ae:record($fieldName, 'G2CCC/MM')"/>
          <xsl:value-of select="ae:record($fieldName, 'K6DF/1')"/>

          <xsl:value-of select="ae:commentLine('This is a real call - see http://www.southgatearc.org/news/february2013/romanian_special_event_station_yo2013eyowf.htm')"/>
          <xsl:value-of select="ae:record($fieldName, 'YO2013EYOWF')"/>
        </xsl:when>

        <xsl:when test="$fieldName='CHECK'">
          <xsl:value-of select="ae:commentLine('For ARRL sweep stakes, the year of birth')"/>
          <xsl:value-of select="ae:record($fieldName, '{YEAR_OF_BIRTH(37)}')"/>
        </xsl:when>

        <xsl:when test="$fieldName='CLASS'">
          <xsl:value-of select="ae:record($fieldName, 'High power unassisted')"/>
        </xsl:when>

        <!-- CLUBLOG_QSO_UPLOAD_DATE is only valid along with a CLUBLOG_QSO_UPLOAD_STATUS, so tested with that field. -->
        <xsl:when test="$fieldName='CLUBLOG_QSO_UPLOAD_DATE'"/>

        <xsl:when test="$fieldName='CLUBLOG_QSO_UPLOAD_STATUS' or $fieldName='HAMLOGEU_QSO_UPLOAD_STATUS' or $fieldName='HAMQTH_QSO_UPLOAD_STATUS' or $fieldName='HRDLOG_QSO_UPLOAD_STATUS' or $fieldName='QRZCOM_QSO_UPLOAD_STATUS'">
          <xsl:variable name="fieldPrefix" select="substring-before($fieldName, 'QSO_UPLOAD_STATUS')"/>
          <xsl:for-each select="/adif/enumerations/enumeration[@name='QSO_Upload_Status']/record">
            <xsl:value-of select="ae:record($fieldName, value[@name='Status'])"/>
            <xsl:if test="value[@name='Status']!='N'">
              <xsl:value-of select="ae:record($fieldName, value[@name='Status'], concat($fieldPrefix, 'QSO_UPLOAD_DATE'), '{QSO_DATE_OFF+6.3}')"/>
            </xsl:if>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='CNTY' or $fieldName='MY_CNTY'">
          <xsl:variable name="fieldPrefix" select="substring-before($fieldName, 'CNTY')"/>
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Secondary_Administrative_Subdivision']/record">
            <xsl:variable name="code" select="value[@name='Code']"/>
            <xsl:variable name="dxcc" select="value[@name='DXCC Entity Code']"/>
            <xsl:variable name="dxccRecord" select="/adif/enumerations/enumeration[@name='DXCC_Entity_Code']/record[value[@name='Entity Code'] = $dxcc]"/>
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="concat($code, ' deleted (DXCC ', $dxcc, ')')"/>
                </xsl:call-template>
              </xsl:when>
              <!-- The Microsoft documentation says that MSXML later than version 2 requires their node-set() function
                   when using a variable as node set. -->
              <xsl:when test="ms:node-set($dxccRecord)/value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="concat($code, ' (DXCC ', $dxcc, ' deleted)')"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="ae:record(
                    'CALL',                       ae:callForPrimaryAdministrativeSubdivision($dxcc, substring-before($code, ',')),
                    concat($fieldPrefix, 'DXCC'), $dxcc,
                    $fieldName,                   $code)"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>

          <xsl:value-of select="ae:commentLine('CNTY values except for U.S. Counties are held outside the ADIF specification')"/>          
          <xsl:value-of select="ae:record(
            'CALL',                       ae:callForDxcc($usaDxcc),
            concat($fieldPrefix, 'DXCC'), $usaDxcc,
            $fieldName,                   'MA,Middlesex')"/>
        </xsl:when>

        <xsl:when test="$fieldName='CNTY_ALT' or $fieldName='MY_CNTY_ALT'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Secondary_Administrative_Subdivision_Alt']/record">
            <xsl:variable name="code" select="value[@name='Code']"/>
            <xsl:variable name="dxcc" select="value[@name='DXCC Entity Code']"/>
            <xsl:variable name="dxccRecord" select="/adif/enumerations/enumeration[@name='DXCC_Entity_Code']/record[value[@name='Entity Code'] = $dxcc]"/>
            <xsl:value-of select="ae:record(
              'CALL',     ae:callForDxcc($dxcc),
              'DXCC',     $dxcc,
              $fieldName, $code)"/>
          </xsl:for-each>
        </xsl:when>
        
        <xsl:when test="$fieldName='COMMENT'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            $fieldName,                  concat('His power was &gt; 10 Watts &amp; &lt; 100 Watts.  Hope to meet up again in 3 days', &quot;'&quot;, ' time.'),
            concat($fieldName, '_INTL'), concat('Son puissance est &gt; 10 Watts et &lt; 100 Watts.  J', &quot;'&quot;, 'espère le rencontrer à nouveau dans 3 jours.'))"/>
        </xsl:when>

        <!-- Tested along with COMMENT -->
        <xsl:when test="$fieldName='COMMENT_INTL'"/>

        <xsl:when test="$fieldName='CONT'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Continent']/record">
            <xsl:variable name="abbreviation" select="value[@name='Abbreviation']"/>
            <xsl:value-of select="ae:record('CALL', ae:callForCont($abbreviation), $fieldName, $abbreviation)"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='CONTACTED_OP'">
          <!-- Ensure the two calls are not the same by using different DXCCs. -->
          <xsl:value-of select="ae:record($fieldName, ae:callForDxcc($canadaDxcc), 'CALL', ae:callForDxcc($usaDxcc))"/>
        </xsl:when>

        <xsl:when test="$fieldName='CONTEST_ID'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Contest_ID']/record">
            <xsl:variable name="contestId"  select="value[@name='Contest-ID']"/>
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="value[@name='Contest-ID']"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="ae:record($fieldName, $contestId, 'BAND', ae:bandForContest($contestId))"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='COUNTRY' or $fieldName='MY_COUNTRY'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <!-- There is some doubt about what exactly is allowed in the COUNTRY and COUNTRY_INTL fields, so the examples are erring on the side of caution. -->
          <xsl:value-of select="ae:record(
            'CALL',                      ae:callForDxcc($usaDxcc),
            $fieldName,                  'United States of America',
            concat($fieldName, '_INTL'), 'United States of America')"/>
        </xsl:when>

        <!-- Tested along with COUNTRY and MY_COUNTRY -->
        <xsl:when test="$fieldName='COUNTRY_INTL' or $fieldName='MY_COUNTRY_INTL'"/>

        <xsl:when test="$fieldName='CQZ'">
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($canadaDxcc), $fieldName, '{}')"/>
        </xsl:when>

        <xsl:when test="$fieldName='CREDIT_SUBMITTED'">
          <!-- CREDIT_SUBMITTED and CREDIT_GRANTED can have multiple Credit values each with an optional list of QSL Medium values.
               Another complication is that in real life, not all QSL Medium types are supported by all Awards,
               e.g. eQSLs cannot be used with ARRL awards.

               The approach taken below is enumerate through the Credit values, emitting a QSO for each value of Credit with
               one appropriate (as far as possible) QSL Medium value: LOTW for ARRL awards, EQSL for EQSL awards, and CARD for all others.

               Separate "manual" QSOs are emitted to give some testing of various combinations of Credit and QSL Medium values within one QSO.

               The problem with all this is that there is a chance that some QSOs may be invalid if (for example) they are on unsupported bands
               for a particular award or if the random QSO generates a duplicate Credit (e.g. if the same band appears for the same award).
               At the moment, let's see how it works out, then refine it if any importing software rejects any QSOs for valid reasons. -->

          <xsl:for-each select="/adif/enumerations/enumeration[@name='Credit']/record">
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="value[@name='Credit For']"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:variable name='qslmedium'>
                  <xsl:choose>
                    <xsl:when test="value[@name='Sponsor']='ARRL'">LOTW</xsl:when>
                    <xsl:when test="value[@name='Sponsor']='eQSL'">EQSL</xsl:when>
                    <xsl:otherwise>CARD</xsl:otherwise>
                  </xsl:choose>
                </xsl:variable>
                <xsl:value-of select="ae:record($fieldName, concat(value[@name='Credit For'], ':', $qslmedium))"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>

          <!-- Test with one Credit value alone. -->
          <xsl:value-of select="ae:record($fieldName, 'DXCC')"/>

          <!-- Test with one Credit value and one QSL Medium. -->
          <xsl:value-of select="ae:record($fieldName, 'DXCC:CARD')"/>

          <!-- Test with one Credit value and two QSL Medium. -->
          <xsl:value-of select="ae:record($fieldName, 'DXCC_MODE:CARD&amp;LOTW')"/>

          <!-- Test with two Credit values alone. -->
          <xsl:value-of select="ae:record($fieldName, 'CQDX,IOTA')"/>

          <!-- Test with two Credit values and one QSL Medium. -->
          <xsl:value-of select="ae:record($fieldName, 'CQDX_MODE:CARD,CQDX_BAND:CARD')"/>

          <!-- Test with two Credit values and two QSL Medium. -->
          <xsl:value-of select="ae:record($fieldName, 'CQDX_MOBILE:CARD,CQDX_BAND:EQSL&amp;CARD')"/>
        </xsl:when>

        <xsl:when test="$fieldName='CREDIT_GRANTED'">
          <!-- CREDIT_SUBMITTED and CREDIT_GRANTED can have multiple Credit values each with an optional list of QSL Medium values.
               Another complication is that in real life, not all QSL Medium types are supported by all Awards,
               e.g. eQSLs cannot be used with ARRL awards.

               The approach taken below is enumerate through the Credit values, emitting a QSO for each value of Credit with
               one appropriate (as far as possible) QSL Medium value: LOTW for ARRL awards, EQSL for EQSL awards, and CARD for all others.

               Separate "manual" QSOs are emitted to give some testing of various combinations of Credit and QSL Medium values within one QSO.

               The problem with all this is that there is a chance that some QSOs may be invalid if (for example) they are on unsupported bands
               for a particular award or if the random QSO generates a duplicate Credit (e.g. if the same band appears for the same award).
               At the moment, let's see how it works out, then refine it if any importing software rejects any QSOs for valid reasons. -->

          <xsl:for-each select="/adif/enumerations/enumeration[@name='Credit']/record">
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="value[@name='Credit For']"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:variable name='qslmedium'>
                  <xsl:choose>
                    <xsl:when test="value[@name='Sponsor']='ARRL'">LOTW</xsl:when>
                    <xsl:when test="value[@name='Sponsor']='eQSL'">EQSL</xsl:when>
                    <xsl:otherwise>CARD</xsl:otherwise>
                  </xsl:choose>
                </xsl:variable>
                <xsl:value-of select="ae:record(
                  'CREDIT_SUBMITTED', concat(value[@name='Credit For'], ':', $qslmedium),
                  $fieldName,         concat(value[@name='Credit For'], ':', $qslmedium))"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>

          <!-- Test with one Credit value alone. -->
          <xsl:value-of select="ae:record(
            'CREDIT_SUBMITTED', 'DXCC',
            $fieldName,         'DXCC')"/>

          <!-- Test with one Credit value and one QSL Medium. -->
          <xsl:value-of select="ae:record(
            'CREDIT_SUBMITTED', 'DXCC:CARD',
            $fieldName,         'DXCC:CARD')"/>

          <!-- Test with one Credit value and two QSL Medium. -->
          <xsl:value-of select="ae:record(
            'CREDIT_SUBMITTED', 'DXCC_MODE:CARD&amp;LOTW',
            $fieldName,         'DXCC_MODE:CARD&amp;LOTW')"/>

          <!-- Test with two Credit values alone. -->
          <xsl:value-of select="ae:record(
            'CREDIT_SUBMITTED', 'CQDX,IOTA',
            $fieldName,         'CQDX,IOTA')"/>

          <!-- Test with two Credit values and one QSL Medium. -->
          <xsl:value-of select="ae:record(
            'CREDIT_SUBMITTED', 'CQDX_MODE:CARD,CQDX_BAND:CARD',
            $fieldName,         'CQDX_MODE:CARD,CQDX_BAND:CARD')"/>

          <!-- Test with two Credit values and two QSL Medium. -->
          <xsl:value-of select="ae:record(
            'CREDIT_SUBMITTED', 'CQDX_MOBILE:CARD,CQDX_BAND:EQSL&amp;CARD',
            $fieldName,         'CQDX_MOBILE:CARD,CQDX_BAND:EQSL&amp;CARD')"/>
        </xsl:when>

        <xsl:when test="$fieldName='DARC_DOK' or $fieldName='MY_DARC_DOK'">
          <xsl:if test="$version306OrLater">
            <!-- Three examples are a standard one, a special one with all letters and the longest current special one. -->
            <xsl:value-of select="ae:record('CALL', ae:callForDxcc($germanyDxcc), $fieldName, 'A01')"/>
            <xsl:value-of select="ae:record('CALL', ae:callForDxcc($germanyDxcc), $fieldName, 'ILERA')"/>
            <xsl:value-of select="ae:record('CALL', ae:callForDxcc($germanyDxcc), $fieldName, '50ESKILSTUNA')"/>
          </xsl:if>
        </xsl:when>

        <xsl:when test="$fieldName='DISTANCE'">
          <xsl:value-of select="ae:record($fieldName,    '0.0')"/>
          <xsl:value-of select="ae:record($fieldName,  '210')"/>
          <xsl:value-of select="ae:record($fieldName, '3000.5')"/>
        </xsl:when>

        <xsl:when test="$fieldName='DXCC'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='DXCC_Entity_Code']/record">
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="value[@name='Entity Code']"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="ae:record(
                  $fieldName, value[@name='Entity Code'],
                  'CALL',     ae:callForDxcc(value[@name='Entity Code']))"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='EMAIL'">
          <xsl:value-of select="ae:record($fieldName, 'someone@somewhere.com')"/>
        </xsl:when>

        <xsl:when test="$fieldName='EQ_CALL'">
          <!-- Ensure the two calls are not the same by using difference DXCCs. -->
          <xsl:value-of select="ae:record($fieldName, ae:callForDxcc($canadaDxcc), 'CALL', ae:callForDxcc($usaDxcc))"/>
        </xsl:when>

        <!-- No test QSOs because these fields are covered by the test of xxxQSL_RCVD -->
        <xsl:when test="$fieldName='DCL_QSLRDATE' or $fieldName='EQSL_QSLRDATE' or $fieldName='LOTW_QSLRDATE'or $fieldName='QSLRDATE'"/>

        <!-- No test QSOs because these fields are covered by the test of xxxQSL_SENT -->
        <xsl:when test="$fieldName='DCL_QSLSDATE' or $fieldName='EQSL_QSLSDATE' or $fieldName='LOTW_QSLSDATE' or $fieldName='QSLSDATE'"/>

        <xsl:when test="$fieldName='DCL_QSL_RCVD' or $fieldName='EQSL_QSL_RCVD' or $fieldName='LOTW_QSL_RCVD' or $fieldName='QSL_RCVD'">
          <xsl:variable name="fieldPrefix" select="substring-before($fieldName, 'QSL_RCVD')"/>
          <xsl:for-each select="/adif/enumerations/enumeration[@name='QSL_Rcvd']/record">
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="value[@name='Status']"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:variable name="status" select="value[@name='Status']"/>
                <xsl:value-of select="ae:record($fieldName, value[@name='Status'])"/>
                <xsl:if test="$status='Y' or $status='I' or $status='V'">
                  <xsl:value-of select="ae:record($fieldName, value[@name='Status'], concat($fieldPrefix, 'QSLRDATE'),  '{QSO_DATE_OFF+3}')"/>
                </xsl:if>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='DCL_QSL_SENT' or $fieldName='EQSL_QSL_SENT' or $fieldName='LOTW_QSL_SENT' or $fieldName='QSL_SENT'">
          <xsl:variable name="fieldPrefix" select="substring-before($fieldName, 'QSL_SENT')"/>
          <xsl:for-each select="/adif/enumerations/enumeration[@name='QSL_Sent']/record">
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="value[@name='Status']"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:variable name="status" select="value[@name='Status']"/>
                <xsl:value-of select="ae:record($fieldName, value[@name='Status'])"/>
                <xsl:if test="$status='Y' or $status='Q' or $status='I'">
                  <xsl:value-of select="ae:record($fieldName, value[@name='Status'], concat($fieldPrefix, 'QSLSDATE'), '{QSO_DATE_OFF+3}')"/>
                </xsl:if>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='FISTS'">
          <xsl:value-of select="ae:record($fieldName,     '1', 'CALL', 'GX0IPX')"/>
          <xsl:value-of select="ae:record($fieldName,  '8385', 'CALL', 'G3ZOD')"/>
          <xsl:value-of select="ae:record($fieldName, '08385', 'CALL', 'G3ZOD')"/>
        </xsl:when>

        <xsl:when test="$fieldName='FISTS_CC'">
          <xsl:value-of select="ae:record($fieldName,  '9876', 'CALL', 'G0QQQ')"/>
          <xsl:value-of select="ae:record($fieldName, '10876', 'CALL', 'M0QQQ')"/>
        </xsl:when>

        <xsl:when test="$fieldName='FORCE_INIT'">
          <xsl:for-each select="ms:node-set($booleans)">
            <xsl:value-of select="ae:record($fieldName, ., 'BAND', '60m')"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='FREQ' or $fieldName='FREQ_RX'">
          <xsl:variable name="fieldSuffix" select="substring-after($fieldName, 'FREQ')"/>
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Band']/record">
            <xsl:variable name="freqLo" select="value[@name='Lower Freq (MHz)']"/>
            <xsl:variable name="freqHi" select="value[@name='Upper Freq (MHz)']"/>
            <xsl:value-of select="ae:record($fieldName, $freqLo)"/>
            <xsl:value-of select="ae:record($fieldName, $freqLo, concat('BAND', $fieldSuffix), '{}')"/>
            <xsl:value-of select="ae:record($fieldName, $freqHi, concat('BAND', $fieldSuffix), '{}')"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='GRIDSQUARE' or $fieldName='MY_GRIDSQUARE'">
          <xsl:variable name="call" select="ae:callForDxcc($englandDxcc)"/>
          <xsl:value-of select="ae:record($fieldName, 'IO',       'CALL', $call)"/>
          <xsl:value-of select="ae:record($fieldName, 'IO83',     'CALL', $call)"/>
          <xsl:value-of select="ae:record($fieldName, 'io83wj',   'CALL', $call)"/>
          <xsl:value-of select="ae:record($fieldName, 'IO83WJ',   'CALL', $call)"/>
          <xsl:value-of select="ae:record($fieldName, 'IO83WJ16', 'CALL', $call)"/>
          <xsl:value-of select="ae:record($fieldName, 'IO83WJ16', 'CALL', $call, concat($fieldName, '_EXT'), 'xa')"/>
          <xsl:value-of select="ae:record($fieldName, 'IO83WJ16', 'CALL', $call, concat($fieldName, '_EXT'), 'XA09')"/>
        </xsl:when>

        <!-- Tested along with GRIDSQUARE and MY_GRIDSQUARE -->
        <xsl:when test="$fieldName='GRIDSQUARE_EXT' or $fieldName='MY_GRIDSQUARE_EXT'"/>
        
        <xsl:when test="$fieldName='GUEST_OP'">
          <!-- Deprecated, so not included -->
          <xsl:call-template name="deletedOrReadOnly">
            <xsl:with-param name="name" select="$fieldName"/>
            <xsl:with-param name="value" select="''"/>
          </xsl:call-template>
        </xsl:when>
        
        <!-- HAMLOGEU_QSO_UPLOAD_DATE is only valid along with a HAMLOGEU_QSO_UPLOAD_STATUS, so tested with that field. -->
        <xsl:when test="$fieldName='HAMLOGEU_QSO_UPLOAD_DATE'"/>

        <!-- HAMQTH_QSO_UPLOAD_DATE is only valid along with a HAMQTH_QSO_UPLOAD_STATUS, so tested with that field. -->
        <xsl:when test="$fieldName='HAMQTH_QSO_UPLOAD_DATE'"/>
        
        <!-- HRDLOG_QSO_UPLOAD_DATE is only valid along with a HRDLOG_QSO_UPLOAD_STATUS, so tested with that field. -->
        <xsl:when test="$fieldName='HRDLOG_QSO_UPLOAD_DATE'"/>

        <xsl:when test="$fieldName='IOTA' or $fieldName='MY_IOTA'">
            <!-- IOTA test data:
            eu-005 00009292  G Great Britain
            EU-005 00009311 GW Anglesey
            EU-120 00011087  G Isle of Wight
            EU-123 00011164 GM Arran -->

          <xsl:variable name="fieldIotaIslandId" select="concat(substring-before($fieldName, 'IOTA'), 'IOTA_ISLAND_ID')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($englandDxcc),  $fieldName, 'EU-005')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($englandDxcc),  $fieldName, 'eu-005', $fieldIotaIslandId, '00009292')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($walesDxcc),    $fieldName, 'EU-005', $fieldIotaIslandId, '9311')"/>
        </xsl:when>

        <xsl:when test="$fieldName='IOTA_ISLAND_ID' or $fieldName='MY_IOTA_ISLAND_ID'">
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($scotlandDxcc), $fieldName, '00011164')"/>
        </xsl:when>

        <xsl:when test="$fieldName='ITUZ'">
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($canadaDxcc), $fieldName, '{}')"/>
        </xsl:when>

        <xsl:when test="$fieldName='K_INDEX'">
          <xsl:value-of select="ae:record($fieldName, '0')"/>
          <xsl:value-of select="ae:record($fieldName, '9')"/>
        </xsl:when>

        <xsl:when test="$fieldName='LAT' or $fieldName='MY_LAT'">
          <!-- Strictly speaking, the LAT/MY_LAT fields can appear without the LON/MY_LON fields and v.v.
               but in real life that is of little, if any, practical value, so just test them in pairs. -->
          <xsl:variable name="fieldPrefix" select="substring-before($fieldName, 'LAT')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($englandDxcc),                concat($fieldPrefix, 'LAT'), 'N052 43.432', concat($fieldPrefix, 'LON'), 'W002 07.500')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($englandDxcc),                concat($fieldPrefix, 'LON'), 'E000 47.345', concat($fieldPrefix, 'LAT'), 'N053 23.733')"/>
          <xsl:value-of select="ae:record('CALL', concat(ae:callForDxcc($englandDxcc), '/MM'), concat($fieldPrefix, 'LAT'), 'S037 09.337', concat($fieldPrefix, 'LON'), 'E028 54.085')"/>
          <xsl:value-of select="ae:record('CALL', concat(ae:callForDxcc($englandDxcc), '/MM'), concat($fieldPrefix, 'LAT'), 'n012 38.777', concat($fieldPrefix, 'LON'), 'w037 12.110')"/>
          <xsl:value-of select="ae:record('CALL', concat(ae:callForDxcc($englandDxcc), '/MM'), concat($fieldPrefix, 'LAT'), 's018 28.430', concat($fieldPrefix, 'LON'), 'w021 22.892')"/>
        </xsl:when>
        
        <!-- Covered by the above test, since (MY_)LAT and (MY_)LON are not useful unless both are present. -->
        <xsl:when test="$fieldName='LON' or $fieldName='MY_LON'"/>

        <xsl:when test="$fieldName='MAX_BURSTS'">
          <xsl:value-of select="ae:record($fieldName, '0')"/>
          <xsl:value-of select="ae:record($fieldName, '0.4')"/>
        </xsl:when>

        <xsl:when test="$fieldName='MODE'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Mode']/record">
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="value[@name='Mode']"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="ae:record($fieldName, value[@name='Mode'])"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='MORSE_KEY_TYPE' or $fieldName='MY_MORSE_KEY_TYPE' or $fieldName='MORSE_KEY_INFO' or $fieldName='MY_MORSE_KEY_INFO'">
          <!-- MORSE_KEY_INFO is normally going to be included only if MORSE_KEY_TYPE is included.
               MY_MORSE_KEY_INFO is normally going to be included only if MY_MORSE_KEY_TYPE is included. -->
          
          <!-- First create records for each of the 4 MORSE_KEY fields on its own. -->
          <xsl:choose>
            <xsl:when test="$fieldName='MORSE_KEY_TYPE' or $fieldName='MY_MORSE_KEY_TYPE'">
              <xsl:value-of select="ae:record($fieldName, 'Bug')"/>  <!-- Deliberate test of unusual capitalization. -->
            </xsl:when>
            <xsl:when test="$fieldName='MORSE_KEY_INFO' or $fieldName='MY_MORSE_KEY_INFO'">
              <xsl:value-of select="ae:record($fieldName, 'Homebrew sideswiper made from nail file.')"/>
            </xsl:when>
          </xsl:choose>
          
          <!-- Now create pairs of KEY_TYPE and KEY_INFO fields. -->
          <xsl:if test="$fieldName='MORSE_KEY_TYPE' or $fieldName='MY_MORSE_KEY_TYPE'">
            <xsl:for-each select="/adif/enumerations/enumeration[@name='Morse_Key_Type']/record">
              <xsl:variable name="abbreviation" select="value[@name='Abbreviation']"/>
              <xsl:variable name="info">
                <xsl:choose>
                  <xsl:when test="$abbreviation='SK'" >Old flame-proof key.</xsl:when>
                  <xsl:when test="$abbreviation='SS'" >Made using a micro-switch.</xsl:when>
                  <xsl:when test="$abbreviation='BUG'">Hi-Mound BK-100 "Coffin" semi-automatic key.</xsl:when>
                  <xsl:when test="$abbreviation='FAB'">GHD GN209FA fully-automatic bug.</xsl:when>
                  <xsl:when test="$abbreviation='SP'" >G4ZPY single-lever paddle key.</xsl:when>
                  <xsl:when test="$abbreviation='DP'" >N3ZN ZN-9+ with 3/4" OTO.</xsl:when>
                  <xsl:when test="$abbreviation='CPU'">Keyboard used with N1MM software and K1EL WinKeyer.</xsl:when>
                  <xsl:otherwise>ERROR</xsl:otherwise>
                </xsl:choose>
              </xsl:variable>
              <xsl:variable name="infoFieldName" select="concat(substring-before($fieldName, 'TYPE'), 'INFO')"/>
              <xsl:value-of select="ae:record(
                $fieldName,     $abbreviation,
                $infoFieldName, $info)"/>            
            </xsl:for-each>
          </xsl:if>
        </xsl:when>
        
        <xsl:when test="$fieldName='MS_SHOWER'">
          <!-- As the ADIF Specification doesn't include a list of meteor showers, the list below is for visual meteor showers,
               taken from http://www.imo.net/resources/calendar/ (International Meteor Organization website) -->
          
          <xsl:variable name="showerValues">
            <ex:showers>
              <ex:shower value="Quadrantids"/>
              <ex:shower value="Lyrids"/>
              <ex:shower value="eta Aquariids"/>
              <ex:shower value="Southern delta Aquariids"/>
              <ex:shower value="alpha Capricornids"/>
              <ex:shower value="Perseids"/>
              <ex:shower value="Southern Taurids"/>
              <ex:shower value="Orionids"/>
              <ex:shower value="Northern Taurids"/>
              <ex:shower value="Leonids"/>
              <ex:shower value="Geminids"/>
              <ex:shower value="Ursids"/>          
            </ex:showers>
          </xsl:variable>
          
          <xsl:for-each select="ms:node-set($showerValues)//ex:showers/ex:shower/@value">
            <xsl:value-of select="ae:record($fieldName, ., 'BAND', '6m')"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='MY_ANTENNA'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:if test="$version306OrLater">
            <xsl:value-of select="ae:record(
              $fieldName,                  'Dipole',
              concat($fieldName, '_INTL'), 'дыполь')"/>
          </xsl:if>
        </xsl:when>

        <!-- Tested along with MY_ANTENNA -->
        <xsl:when test="$fieldName='MY_ANTENNA_INTL'"/>

        <xsl:when test="$fieldName='MY_CITY'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            $fieldName,                  'Alencon',
            concat($fieldName, '_INTL'), 'Alençon')"/>
        </xsl:when>

        <!-- Tested along with MY_CITY -->
        <xsl:when test="$fieldName='MY_CITY_INTL'"/>

        <xsl:when test="$fieldName='MY_CQ_ZONE'">
          <xsl:value-of select="ae:record($fieldName, '5')"/>
        </xsl:when>

        <xsl:when test="$fieldName='MY_DXCC'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='DXCC_Entity_Code']/record">
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="value[@name='Entity Code']"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="ae:record($fieldName, value[@name='Entity Code'])"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='MY_FISTS'">
          <xsl:value-of select="ae:record($fieldName,     '1')"/>
          <xsl:value-of select="ae:record($fieldName,  '8385')"/>
          <xsl:value-of select="ae:record($fieldName, '08385')"/>
        </xsl:when>

        <xsl:when test="$fieldName='MY_ITU_ZONE'">
          <xsl:value-of select="ae:record($fieldName, '2')"/>
        </xsl:when>

        <xsl:when test="$fieldName='MY_NAME' or $fieldName='NAME'">
          <!-- Include both fields in one record because just the appropriate ones will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            $fieldName,                  'Francois',
            concat($fieldName, '_INTL'), 'François')"/>
        </xsl:when>

        <!-- Tested along with MY_NAME and NAME -->
        <xsl:when test="$fieldName='MY_NAME_INTL'or $fieldName='NAME_INTL'"/>

        <xsl:when test="$fieldName='MY_POSTAL_CODE'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
             $fieldName,                  'CA 94436-5348',
             concat($fieldName, '_INTL'), 'M12 5DR')"/>
        </xsl:when>

        <!-- Tested along with MY_POSTAL_CODE -->
        <xsl:when test="$fieldName='MY_POSTAL_CODE_INTL'"/>

        <xsl:when test="$fieldName='MY_POTA_REF' or $fieldName='POTA_REF'">
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc), $fieldName, 'K-0059')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc), $fieldName, 'K-10000' )"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc), $fieldName, 'K-0817,K-4578@US-WY,K-4566,K-4576,K-4573' )"/>
        </xsl:when>
        
        <xsl:when test="$fieldName='MY_RIG'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <!-- Note that the RIG and RIG_INTL fields are tested in a separate "when" because they are xxxxMultlineString -->
          <xsl:value-of select="ae:record(
            $fieldName,                  'FT-817 transmitter + 100 Watt amplifier',
            concat($fieldName, '_INTL'), 'FT-817 émetteur + 100 Watt amplificateur')"/>
        </xsl:when>

        <!-- Tested along with MY_RIG -->
        <xsl:when test="$fieldName='MY_RIG_INTL'"/>

        <xsl:when test="$fieldName='MY_SIG' or $fieldName='SIG'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            $fieldName,                  'SOTA',
            concat($fieldName, '_INTL'), 'SOTA')"/>
        </xsl:when>

        <!-- Tested along with MY_SIG and SIG -->
        <xsl:when test="$fieldName='MY_SIG_INTL' or $fieldName='SIG_INTL'"/>

        <xsl:when test="$fieldName='MY_SIG_INFO' or $fieldName='SIG_INFO'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            $fieldName,                  'Summits on the Air activator',
            concat($fieldName, '_INTL'), 'Summits on the Air activator')"/>
        </xsl:when>

        <!-- Tested along with MY_SIG_INFO and SIG_INFO -->
        <xsl:when test="$fieldName='MY_SIG_INFO_INTL' or $fieldName='SIG_INFO_INTL'"/>

        <xsl:when test="$fieldName='MY_SOTA_REF' or $fieldName='SOTA_REF'">
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc),     $fieldName, 'W2/WE-003')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($englandDxcc), $fieldName, 'G/LD-003' )"/>
        </xsl:when>
        
        <xsl:when test="$fieldName='MY_STATE' or $fieldName='STATE'">
          <xsl:variable name="fieldPrefix" select="substring-before($fieldName, 'STATE')"/>
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Primary_Administrative_Subdivision']/record">
            <xsl:variable name="code" select="value[@name='Code']"/>
            <xsl:variable name="dxcc" select="value[@name='DXCC Entity Code']"/>
            <xsl:variable name="dxccRecord" select="/adif/enumerations/enumeration[@name='DXCC_Entity_Code']/record[value[@name='Entity Code'] = $dxcc]"/>
            <xsl:choose>
              <xsl:when test="value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="concat($code, ' deleted (DXCC ', $dxcc, ')')"/>
                </xsl:call-template>
              </xsl:when>
              <!-- The Microsoft documentation says that MSXML later than version 2 requires their node-set() function
                   when using a variable as node set. -->
              <xsl:when test="ms:node-set($dxccRecord)/value[@name='Import-only' or @name='Deleted']">
                <xsl:call-template name="deletedOrReadOnly">
                  <xsl:with-param name="name" select="$fieldName"/>
                  <xsl:with-param name="value" select="concat($code, ' (DXCC ', $dxcc, ' deleted)')"/>
                </xsl:call-template>
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="ae:record(
                    'CALL',                       ae:callForPrimaryAdministrativeSubdivision($dxcc, $code),
                    concat($fieldPrefix, 'DXCC'), $dxcc,
                    $fieldName,                   $code)"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='MY_STREET'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            $fieldName,                  'Rue Falguiere',
            concat($fieldName, '_INTL'), 'Rue Falguière')"/>
        </xsl:when>

        <!-- Tested along with MY_STREET -->
        <xsl:when test="$fieldName='MY_STREET_INTL'"/>

        <xsl:when test="$fieldName='MY_USACA_COUNTIES' or $fieldName='USACA_COUNTIES'">
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc), $fieldName, 'MA,Franklin:MA,Hampshire')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc), $fieldName, 'ma,hampshire:ma,franklin')"/>
        </xsl:when>

        <xsl:when test="$fieldName='MY_VUCC_GRIDS' or $fieldName='VUCC_GRIDS'">
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc), $fieldName, 'EN98,FM08,EM97,FM07')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc), $fieldName, 'fm07,em97,fm08,en98')"/>
        </xsl:when>

        <xsl:when test="$fieldName='NOTES'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            $fieldName,                  concat('His power was &gt; 10 Watts &amp; &lt; 100 Watts.&#13;&#10;&#13;&#10;Hope to meet up again in 3 days', &quot;'&quot;, ' time.'),
            concat($fieldName, '_INTL'), concat('Son puissance est &gt; 10 Watts et &lt; 100 Watts.&#13;&#10;&#13;&#10;J', &quot;'&quot;, 'espère le rencontrer à nouveau dans 3 jours.'))"/>
        </xsl:when>

        <xsl:when test="$fieldName='NOTES_INTL'"/>
        <!-- Tested along with NOTES -->

        <xsl:when test="$fieldName='NR_BURSTS'">
          <xsl:value-of select="ae:record($fieldName,   '0')"/>
          <xsl:value-of select="ae:record($fieldName,   '3')"/>
          <xsl:value-of select="ae:record($fieldName, '003')"/>
        </xsl:when>

        <xsl:when test="$fieldName='NR_PINGS'">
          <xsl:value-of select="ae:record($fieldName,   '0')"/>
          <xsl:value-of select="ae:record($fieldName,   '5')"/>
          <xsl:value-of select="ae:record($fieldName, '005')"/>
        </xsl:when>

        <!-- Tested along with STATION_CALLSIGN -->
        <xsl:when test="$fieldName='OPERATOR' or $fieldName='OWNER_CALLSIGN'"/>

        <xsl:when test="$fieldName='PFX'">
          <xsl:value-of select="ae:record('CALL', 'K1AAI', $fieldName, 'K1')"/>
          <xsl:value-of select="ae:record('CALL', 'W1AAJ', $fieldName, 'w1')"/>
        </xsl:when>

        <xsl:when test="$fieldName='PRECEDENCE'">
          <!-- Examples are based on ARRL Sweepstakes Precedence http://www.arrl.org/sweepstakes
            4.2. Precedence;
            4.2.1. “Q” for Single Op QRP (5 Watts output or less);
            4.2.2. “A” for Single Op Low Power (up to 150 W output);
            4.2.3. “B” for Single Op High Power (greater than 150 W output);
            4.2.4. “U” for Single Op Unlimited Single-Op Unlimited High Power and Single-Op Unlimited Low Power both send "U")
            4.2.5. “M” for Multi-Op (Multiop High Power and Multiop Low Power both send "M")
            4.2.6. “S” for School Club; -->

          <xsl:variable name="precedenceValues">
            <ex:precedences>
              <ex:precedence value="Q"/>
              <ex:precedence value="A"/>
              <ex:precedence value="B"/>
              <ex:precedence value="U"/>
              <ex:precedence value="M"/>
              <ex:precedence value="S"/>
            </ex:precedences>
          </xsl:variable>            

          <xsl:value-of select="ae:commentLine('Examples (Q, A, B, U, M, S) are based on ARRL Sweepstakes Precedence http://www.arrl.org/sweepstakes')"/>
          <xsl:for-each select="ms:node-set($precedenceValues)//ex:precedences/ex:precedence/@value">
            <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc), $fieldName, .)"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='PROP_MODE'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Propagation_Mode']/record">
            <xsl:value-of select="ae:record($fieldName, value[@name='Enumeration'])"/>
          </xsl:for-each>
          <!-- Add a QSO with a value in lowercase. -->
          <xsl:value-of select="ae:record('BAND', '2m', $fieldName, 'aur')"/>
        </xsl:when>

        <xsl:when test="$fieldName='PUBLIC_KEY'">
          <!-- This is a real 4096 bit public key generated at http://travistidwell.com/jsencrypt/demo/ -->
          <xsl:value-of select="ae:record($fieldName, 'MIICITANBgkqhkiG9w0BAQEFAAOCAg4AMIICCQKCAgBjFAnULtnMBPTEe2MPTXMt/6rj0R96e5jCO3l5amVZsnqo/CZV0Uinq4cVtrWM+PKv/RHzMgqb8lOfWK8k82JKQdpNzcgkoNyl3B8+Ut9coVAAvl981ChshCtD7lCRgdoMFlwyAmKfZcBkS7+J5ijBNdnjIe2UTdVlDre1+f/st7uE/MeZLOrr27IBZAUA2VE5FDNeGfclxMWYzlrQhb4yoLWmLexv+9ylljVYETSDmyohrHHcF8hnvtaabKxwF7BswiVANI+AmaWNPL69bvfpBpAXAs6SjR3OpG/dF0Pdiom8OAXMf88D+lza5Xqb4KRr4M+Vslxg69o1pPXDvYRMrSztNUMDl/lF5uMV55LhZZTexh3Yh7JibzcHHsxjpkEcz9laZC7QDeIHemAFNrzz33urLXuCrp/p4Y2VGNr1fAkWdBp+hLyNu0TxH5nVq8WrYi0dSu/3XPrWqiz3rgcB2bejmkDtLqMn2zKE6j5/9t+hySEKczU2Qk1HLT3wCAa5/bLUgnHd2DVQeeb9IgCYKtW5FUuACatyDTaeCb0sWUesZlqP69D3xu+098z1OWCaF+7S0uxubqMCynuXRetV5a/SrEOVLRyrZVjcHBtyL6prOBwjh51w7OGVcYQM/Zv9btdXxGZgqV5TCo2b5kpR8LBVKDYy24BrGNlgklk2swIDAQAB')"/>
        </xsl:when>

        <!-- QRZCOM_QSO_DOWNLOAD_DATE is only valid along with a QRZCOM_QSO_DOWNLOAD_STATUS, so tested with that field. -->
        <xsl:when test="$fieldName='QRZCOM_QSO_DOWNLOAD_DATE'"/>
                
        <xsl:when test="$fieldName='QRZCOM_QSO_DOWNLOAD_STATUS'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='QSO_Download_Status']/record">
            <xsl:value-of select="ae:record(
               $fieldName, value[@name='Status'])"/>
            <xsl:if test="value[@name='Status']!='N'">
              <xsl:value-of select="ae:record(
                $fieldName,                 value[@name='Status'],
                'QRZCOM_QSO_DOWNLOAD_DATE', '{QSO_DATE_OFF+6.3}')"/>
            </xsl:if>
          </xsl:for-each>
        </xsl:when>       
        
        <!-- QRZCOM_QSO_UPLOAD_DATE is only valid along with a QRZCOM_QSO_UPLOAD_STATUS, so tested with that field. -->
        <xsl:when test="$fieldName='QRZCOM_QSO_UPLOAD_DATE'"/>

        <xsl:when test="$fieldName='QSLMSG'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            $fieldName,                  'Danke fur die QSO.&#13;&#10;&#13;&#10;GUD DX ES CUAGN.',
            concat($fieldName, '_INTL'), 'Danke für die QSO.&#13;&#10;&#13;&#10;GUD DX ES CUAGN.')"/>
        </xsl:when>

        <!-- Tested along with QSLMSG -->
        <xsl:when test="$fieldName='QSLMSG_INTL'"/>
        
        <!-- This is not combined with the code above for QSLMSG(_INTL) because QSLMSG_RCVD has no corresponding _INTL field. -->
        <xsl:when test="$fieldName='QSLMSG_RCVD'">
          <xsl:value-of select="ae:record($fieldName, 'Many thanks for the QSO and QSL.')"/>          
        </xsl:when>

        <xsl:when test="$fieldName='QSL_RCVD_VIA' or $fieldName='QSL_SENT_VIA'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='QSL_Via']/record">
            <xsl:choose>
            <xsl:when test="value[@name='Import-only' or @name='Deleted']">
              <xsl:call-template name="deletedOrReadOnly">
                <xsl:with-param name="name" select="$fieldName"/>
                <xsl:with-param name="value" select="value[@name='Via']"/>
              </xsl:call-template>
            </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="ae:record($fieldName, value[@name='Via'])"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
          <!-- Add a QSO with a value in lowercase. -->
          <xsl:value-of select="ae:record($fieldName, 'e')"/>
        </xsl:when>

        <xsl:when test="$fieldName='QSL_VIA'">
          <xsl:value-of select="ae:record($fieldName, 'ARRL QSL Bureau')"/>
        </xsl:when>

        <xsl:when test="$fieldName='QSO_COMPLETE'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='QSO_Complete']/record">
            <xsl:variable name="abbreviation" select="value[@name='Abbreviation']"/>
            <xsl:value-of select="ae:record($fieldName, $abbreviation)"/>
            <xsl:value-of select="ae:record($fieldName, translate($abbreviation, 'YNIL', 'ynil'))"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='QSO_DATE'">
          <!-- QSO_DATE, TIME_ON and TIME_OFF already appear in every QSO with numerous values,
               so this test deals with QSOs that go over midnight along with including QSO_DATE_OFF and 4-digit times.
               Tests of a mixture of 4 & 6 digit times within a single QSO are not included; this is allowable but not likely in real ADIF files.
          
               If the ae:record and  ae:field calls include any of the four date/time fields, they will update the default QSO's
               corresponding values too, so save copies of the the default QSO's end date & time values so they can be restored
               after this test and then the sequence of automatically incrementing dates and times can continue where it left off. -->
          <xsl:value-of select="ae:saveQsoStartEnd()"/>
          
          <xsl:value-of select="ae:record('QSO_DATE', '20230501',                             'TIME_ON', '2350', 'TIME_OFF', '0005')"/>
          <xsl:value-of select="ae:record('QSO_DATE', '20230502', 'QSO_DATE_OFF', '20230502', 'TIME_ON', '2350', 'TIME_OFF', '2355')"/>
          <xsl:value-of select="ae:record('QSO_DATE', '20230502', 'QSO_DATE_OFF', '20230503', 'TIME_ON', '2350', 'TIME_OFF', '0005')"/>

          <xsl:value-of select="ae:restoreQsoStartEnd()"/>
        </xsl:when>

        <!-- Tested along with QSO_DATE -->
        <xsl:when test="$fieldName='QSO_DATE_OFF'"/>

        <xsl:when test="$fieldName='QSO_RANDOM'">
          <xsl:for-each select="$booleans">
            <xsl:value-of select="ae:record($fieldName, .)"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='QTH'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <xsl:value-of select="ae:record(
            'CALL',                      ae:callForDxcc($franceDxcc),
            $fieldName,                  'Alencon',
            concat($fieldName, '_INTL'), 'Alençon')"/>
        </xsl:when>

        <!-- Tested along with QTH -->
        <xsl:when test="$fieldName='QTH_INTL'"/>

        <xsl:when test="$fieldName='RIG'">
          <!-- Include both _INTL and non-_INTL fields in one record because just the appropriate one will be output to the ADI or ADX file. -->
          <!-- Note that the M7_RIG and MY_RIG_INTL fields are tested in a separate "when" because they are xxxxString -->
          <xsl:value-of select="ae:record(
            $fieldName,                  'FT-817 transmitter&#13;&#10;100 Watt amplifier',
            concat($fieldName, '_INTL'), 'FT-817 émetteur&#13;&#10;100 Watt amplificateur')"/>
        </xsl:when>

        <!-- Tested along with RIG -->
        <xsl:when test="$fieldName='RIG_INTL'"/>

        <xsl:when test="$fieldName='RST_RCVD'">
          <!-- The specification doesn't give details of what is allowable as "signal report" in RST_RCVD and RST_SENT
               despite their names, so the test includes expected typical types of report used: RS(T), RS(T) with suffix, and SINPO code.
               
               Wikipedia says for RST suffixes:
               
                A: signal distorted by auroral propagation[8]
                C: "chirp" (frequency shift when keying)
                K: key clicks
                M: signal distorted by multipath propagation
                S: signal distorted by scatter propagation
                X: stable frequency (crystal control)
               
               I've not come across M or S before, so am not including them.
               The band used is 6m because it is one that Auroral propagation occurs. -->

          <xsl:variable name="qsoReportValues">
            <ex:qsoReports>
              <ex:qsoReport value="57"    mode="SSB"/>
              <ex:qsoReport value="57A"   mode="ssb"/>
              <ex:qsoReport value="57a"   mode="ssb"/>
              <ex:qsoReport value="579"   mode="CW"/>
              <ex:qsoReport value="579A"  mode="CW"/>
              <ex:qsoReport value="579a"  mode="CW"/>
              <ex:qsoReport value="579C"  mode="CW"/>
              <ex:qsoReport value="579c"  mode="CW"/>
              <ex:qsoReport value="579K"  mode="CW"/>
              <ex:qsoReport value="579k"  mode="CW"/>
              <ex:qsoReport value="579X"  mode="CW"/>
              <ex:qsoReport value="579x"  mode="CW"/>
              <ex:qsoReport value="54554" mode="FM"/>
            </ex:qsoReports>
          </xsl:variable>
          
          <xsl:for-each select="ms:node-set($qsoReportValues)//ex:qsoReports/ex:qsoReport">          
            <xsl:value-of select="ae:record('MODE', @mode, 'BAND', '6m', 'RST_RCVD', @value)"/>
            <xsl:value-of select="ae:record('MODE', @mode, 'BAND', '6m',                     'RST_SENT', @value)"/>
            <xsl:value-of select="ae:record('MODE', @mode, 'BAND', '6m', 'RST_RCVD', @value, 'RST_SENT', @value)"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='REGION'">
          <xsl:if test="$version306OrLater">
            <xsl:for-each select="/adif/enumerations/enumeration[@name='Region']/record">
              <xsl:variable name="region" select="value[@name='Region Entity Code']"/>
              <xsl:variable name="dxcc">
                <xsl:choose>
                  <xsl:when test="$region='NONE'"><xsl:value-of select="'0'"/></xsl:when>
                  <xsl:when test="$region='KO'"><xsl:value-of select="'0'"/></xsl:when>
                  <xsl:otherwise><xsl:value-of select="value[@name='DXCC Entity Code']"/></xsl:otherwise>
                </xsl:choose>
              </xsl:variable>              
              <xsl:variable name="call">
                <xsl:choose>
                  <xsl:when test="$region='NONE'">M0ZZA/MM</xsl:when>
                  <xsl:when test="$region='IV'"  >4U1V</xsl:when>
                  <xsl:when test="$region='AI'"  >IG9ZZB</xsl:when>
                  <xsl:when test="$region='SY'"  >IT9ZZC</xsl:when>
                  <xsl:when test="$region='BI'"  >JW0ZZD/B</xsl:when>
                  <xsl:when test="$region='SI'"  >GM0ZZE/S</xsl:when>
                  <xsl:when test="$region='KO'"  >Z6ZZF</xsl:when>
                  <xsl:when test="$region='ET'"  >TA1ZZG</xsl:when>
                  <xsl:otherwise                 ></xsl:otherwise>
                </xsl:choose>
              </xsl:variable>
              <xsl:choose>
                <xsl:when test="$region='KO'">
                  <!-- Up to ADIF 3.1.5, Kosovo is represented in the table as a single row.
                       That should change to three rows in ADIF 3.1.6.
                       For now, just ensure that the following three QSOs are not emitted for
                       ADIF 3.1.6 and later.
                  -->
                  <xsl:choose>
                    <xsl:when test="ae:adifVersionInt() &lt;= 315">
                      <xsl:value-of select="ae:saveQsoStartEnd()"/>
                      <xsl:value-of select="ae:record(
                        $fieldName,     $region,
                        'DXCC',         $serbiaDxcc,
                        'CALL',         'YU8ZZH',
                        'QSO_DATE',     '20120911',
                        'QSO_DATE_OFF', '20120911',
                        'TIME_ON',      '2350',
                        'TIME_OFF',     '2355')"/>                  

                      <xsl:value-of select="ae:record(
                        $fieldName,     $region,
                        'DXCC',         $kosovoDxccNone,
                        'CALL',         $call,
                        'QSO_DATE',     '20180120',
                        'QSO_DATE_OFF', '20180120',
                        'TIME_ON',      '2350',
                        'TIME_OFF',     '2355')"/>                  

                      <xsl:value-of select="ae:record(
                        $fieldName,     $region,
                        'DXCC',         $kosovoDxcc,
                        'CALL',         $call,
                        'QSO_DATE',     '20180121',
                        'QSO_DATE_OFF', '20180121',
                        'TIME_ON',      '2350',
                        'TIME_OFF',     '2355')"/>                  
                      </xsl:when>
                    
                      <xsl:otherwise>
                        <!-- TODO: Add a QSO for ADIF 3.1.6 and later. -->
                      </xsl:otherwise>
                    </xsl:choose>
                    <xsl:value-of select="ae:restoreQsoStartEnd()"/>
                  </xsl:when>
                <xsl:when test="$call=''">
                  <xsl:value-of select="ae:commentLine2(
                    concat('==== Not including region ',
                    $region,
                    ' because it is not recognised'))"/>
                </xsl:when>
                <xsl:otherwise>
                  <xsl:value-of select="ae:record(
                  $fieldName, $region,
                  'DXCC',     $dxcc,
                  'CALL',     $call)"/>
                </xsl:otherwise>
              </xsl:choose>
            </xsl:for-each>
          </xsl:if>
        </xsl:when>
        
        <!-- Tested along with RST_RCVD -->
        <xsl:when test="$fieldName='RST_SENT'"/>

        <xsl:when test="$fieldName='RX_PWR'">
          <!-- Milliwatts are represented by digits to the right of the decimal point, e.g. 100 milliwatts can be .1
               This is also a convenient for testing a variety of positive Number data type variations. -->

          <xsl:variable name="qsoPowerValues">
            <ex:qsoPowers>
              <ex:qsoPower value=    ".010"/>
              <ex:qsoPower value=   "0.015"/>
              <ex:qsoPower value=   "1"/>
              <ex:qsoPower value=   "5"/>
              <ex:qsoPower value=   "5.0"/>
              <ex:qsoPower value=   "5.5"/>
              <ex:qsoPower value= "100"/>
              <ex:qsoPower value="0100"/>
              <ex:qsoPower value="1000.000"/>
            </ex:qsoPowers>
          </xsl:variable>
          
          <xsl:for-each select="ms:node-set($qsoPowerValues)//ex:qsoPowers/ex:qsoPower">
            <xsl:value-of select="ae:record('RX_PWR', @value                  )"/>
            <xsl:value-of select="ae:record(                  'TX_PWR', @value)"/>
            <xsl:value-of select="ae:record('RX_PWR', @value, 'TX_PWR', @value)"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='SAT_MODE'">          
          <!-- At the time of writing, https://en.wikipedia.org/wiki/Amateur_radio_satellite gives satellite modes as:
          
           "Uplink and downlink designations use sets of paired letters following the structure X/Y where
            X is the uplink band and Y is the downlink band. Occasionally, the downlink letter is rendered in lower case (i.e., X/y)...
           
            Designator  H       A       V         U         L         S         S2        C       X       K       R
            Band        15m     10m     2m        70cm      23cm      13cm      9cm       5cm     3cm     1.2cm   6mm
            Frequency   21 MHz  29 MHz  145 MHz   435 MHz   1.2 GHz   2.4 GHz   3.4 GHz   5 GHz   10 GHz  24 GHz  47 GHz
            (General)
           
            ...While deprecated, these older mode designations are still widely used in casual conversation.

            Mode A: 2m   uplink / 10m  downlink
            Mode B: 70cm uplink / 2m   downlink
            Mode J: 2m   uplink / 70cm downlink" -->

          <xsl:value-of select="ae:record(
            $fieldName,  'U/V',
            'SAT_NAME',  'AO-85',
            'PROP_MODE', 'SAT',
            'MODE',      'FM',
            'BAND',      '2m',
            'FREQ',      '145')"/>
        </xsl:when>

        <!-- Tested along with SAT_MODE -->
        <xsl:when test="$fieldName='SAT_NAME'"/>

        <xsl:when test="$fieldName='SFI'">          
          <!-- ARRL https://www.arrl.org/files/file/Technology/tis/info/pdf/0209038.pdf says SFI varies from 50 to 300 but http://www.hamqsl.com/solar2.html says the minimum value is 62.5 -->
          <xsl:value-of select="ae:record($fieldName,  '63')"/>
          <xsl:value-of select="ae:record($fieldName, '123')"/>
          <xsl:value-of select="ae:record($fieldName, '300')"/>
        </xsl:when>

        <xsl:when test="$fieldName='SILENT_KEY'">
          <xsl:for-each select="$booleans">
            <xsl:value-of select="ae:record($fieldName, .)"/>
          </xsl:for-each>
        </xsl:when>

        <xsl:when test="$fieldName='SKCC'">
          <xsl:value-of select="ae:record($fieldName,     '1')"/>
          <xsl:value-of select="ae:record($fieldName, '98765')"/>
          <xsl:value-of select="ae:record($fieldName, '98766C')"/>
          <xsl:value-of select="ae:record($fieldName, '98767T')"/>
        </xsl:when>

        <xsl:when test="$fieldName='SRX'">
          <!-- As an example for SRX_STRING and STX_STRING, SK and WA are partial post codes exchanged in some RSGB VHF contests. -->
          <xsl:value-of select="ae:record('SRX',    '1')"/>
          <xsl:value-of select="ae:record('SRX',  '002')"/>
          <xsl:value-of select="ae:record('SRX', '1003')"/>
          <xsl:value-of select="ae:record(               'STX',    '1')"/>
          <xsl:value-of select="ae:record(               'STX',  '002')"/>
          <xsl:value-of select="ae:record(               'STX', '1003')"/>
          <xsl:value-of select="ae:record('SRX',  '034', 'STX',  '143', 'SRX_STRING', 'SK')"/>
          <xsl:value-of select="ae:record('SRX',  '034', 'STX',  '143', 'SRX_STRING', 'SK', 'STX_STRING', 'WA')"/>
        </xsl:when>

        <!-- Tested along with SRX -->
        <xsl:when test="$fieldName='SRX_STRING' or $fieldName='STX' or $fieldName='STX' or $fieldName='STX_STRING'"/>

        <xsl:when test="$fieldName='STATION_CALLSIGN'">
          <xsl:variable name="call1" select="ae:callForDxcc($englandDxcc)"/>
          <xsl:variable name="call2" select="ae:callForDxcc($canadaDxcc)"/>
          <xsl:variable name="call3" select="ae:callForDxcc($usaDxcc)"/>
          <xsl:value-of select="ae:record(                                        'OWNER_CALLSIGN', $call3)"/>
          <xsl:value-of select="ae:record(                    'OPERATOR', $call2)"/>
          <xsl:value-of select="ae:record(                    'OPERATOR', $call2, 'OWNER_CALLSIGN', $call3)"/>
          <xsl:value-of select="ae:record($fieldName, $call1)"/>
          <xsl:value-of select="ae:record($fieldName, $call1,                     'OWNER_CALLSIGN', $call3)"/>
          <xsl:value-of select="ae:record($fieldName, $call1, 'OPERATOR', $call2)"/>
          <xsl:value-of select="ae:record($fieldName, $call1, 'OPERATOR', $call2, 'OWNER_CALLSIGN', $call3)"/>
          <!-- More typically, all three fields will have the same value, so include a record with the same one. -->
          <xsl:value-of select="ae:record($fieldName, $call1, 'OPERATOR', $call1, 'OWNER_CALLSIGN', $call1)"/>
        </xsl:when>

        <xsl:when test="$fieldName='SUBMODE'">
          <xsl:for-each select="/adif/enumerations/enumeration[@name='Submode']/record">
            <!-- The JT9-nn modes are not supported by current software so it's reasonable not to include them with recently-dated QSOs -->
            <xsl:variable name="submode" select="value[@name='Submode']"/>
            <xsl:choose>
              <xsl:when test="starts-with($submode,'JT9-')">
                <xsl:value-of select="ae:commentLine2(
                  concat('==== Not including ',
                  $submode,
                  ' because it is not supported by current software'))"/> 
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="ae:record($fieldName, $submode, 'MODE', value[@name='Mode'])"/>
              </xsl:otherwise>
            </xsl:choose>
          </xsl:for-each>
          <!-- Include one with other than uppercase2 -->
          <xsl:value-of select="ae:record($fieldName, 'Usb', 'MODE', 'Ssb')"/>
          <!-- SUBMODE values do not have to be in the Submode enumeration -->
          <xsl:value-of select="ae:record($fieldName, 'DSB', 'MODE', 'AM')"/>
        </xsl:when>

        <xsl:when test="$fieldName='SWL'">
          <xsl:for-each select="$booleans">
            <xsl:value-of select="ae:record($fieldName, .)"/>
          </xsl:for-each>
        </xsl:when>

        <!-- Tested along with QSO_DATE -->
        <xsl:when test="$fieldName='TIME_OFF' or $fieldName='TIME_ON'"/>

        <xsl:when test="$fieldName='TEN_TEN'">
          <xsl:value-of select="ae:record($fieldName,     '1')"/>
          <xsl:value-of select="ae:record($fieldName, '00001')"/>
          <xsl:value-of select="ae:record($fieldName, '98765')"/>
        </xsl:when>

        <!-- Tested along with RX_PWR -->
        <xsl:when test="$fieldName='TX_PWR'"/>

        <xsl:when test="$fieldName='UKSMG'">
          <xsl:value-of select="ae:record($fieldName,     '1')"/>
          <xsl:value-of select="ae:record($fieldName, '00001')"/>
          <xsl:value-of select="ae:record($fieldName, '98765')"/>
        </xsl:when>

        <xsl:when test="$fieldName='VE_PROV'">
          <!-- Deprecated, so not included -->
          <xsl:call-template name="deletedOrReadOnly">
            <xsl:with-param name="name" select="$fieldName"/>
            <xsl:with-param name="value" select="''"/>
          </xsl:call-template>
        </xsl:when>

        <xsl:when test="$fieldName='WEB'">
          <xsl:value-of select="ae:record($fieldName, 'http://adif.org.uk')"/>
          <xsl:value-of select="ae:record($fieldName, 'http://adif.org.uk/')"/>
          <xsl:value-of select="ae:record($fieldName, 'http://adif.org.uk/305/ADIF_305.htm#QSO_Fields')"/>
        </xsl:when>

        <xsl:when test="$fieldName='WWFF_REF' or $fieldName='MY_WWFF_REF'">
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($usaDxcc),     $fieldName, 'KFF-0070')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($englandDxcc), $fieldName, 'GFF-0010')"/>
          <xsl:value-of select="ae:record('CALL', ae:callForDxcc($fijiDxcc),    $fieldName, '3D2FF-0006')"/>
        </xsl:when>

        <xsl:otherwise>
          <xsl:value-of select="ae:commentLine2(concat('**** Untested field name ', value[@name]))"/>
          <xsl:value-of select="ae:untestedField(value[@name])"/>
        </xsl:otherwise>

      </xsl:choose>
    </xsl:for-each>

    <xsl:value-of select="ae:commentReport(true())"/>
    <xsl:value-of select="ae:eof()"/>
  </xsl:template>
</xsl:stylesheet>