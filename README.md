# Create ADIF Test Files
## Description
This Windows GUI application will take an all.xml file exported from an [ADIF Specification](https://adif.org.uk/)
XHTML file and create test files containing QSOs in these formats:
- [ADIF ADI](https://adif.org.uk/ADIF_Current#ADI_File_Format) (.adi)
- [ADIF ADX](https://adif.org.uk/ADIF_Current#ADX_File_Format) (.adx)

The generated files for each ADIF version are then provided on the website in a ZIP file (created outside this application).

## Projects
| Name  | Purpose |
| ----- | ------- |
| AdifReleaseLib  | Contains classes with some general-purpose methods and implements a log file |
| AdifTestFileCreator | Creates ADIF records |
| AdifXsltLib | Creates ADIF ADI and ADX file content using XSLT |
| CreateADIFTestFiles  | GUI and related code |

## Software Requirements
- Microsoft Visual Studio 2022 Community Edition

## Limitations
- The application version is kept in step with the ADIF Specification versions.  This is because of the potential need to add code and / or QSO templates to support new features in the ADIF Specification.

## See Also
[Create ADIF Test Files](https://github.com/g3zod/CreateADIFTestFiles)
