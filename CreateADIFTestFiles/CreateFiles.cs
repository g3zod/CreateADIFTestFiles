using CreateADIFTestFiles.Properties;
using System;
using System.IO;
using System.Windows.Forms;
using AdifReleaseLib;
using AdifTestFileCreator;
using System.Diagnostics.CodeAnalysis;

namespace CreateADIFTestFiles
{
    /**
     * <summary>
     *   Provides the user interface for the CreateADIFTestFiles application that creates
     *   QSO files for test purposes based on data files exported from the ADIF Specification.<br/>
     *   <br/>
     *   The file formats are ADI (.adi) and ADX (.adx).
     * </summary>
     */
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This is the Windows UI")]
    public partial class CreateFiles : Form
    {
        private const string MessageBoxTitle = "Create ADIF Test Files";
        private readonly Settings settings = Settings.Default;
        private bool
            cancel = false,
            exporting = false;

        /**
         * <summary>
         *   Starts a log file in the Documents folder and upgrades the settings if required.<br/>
         *   If the program version is more recent than the one in the Settings.Default file, it upgrades the file.<br/>
         *   Initializes the form.
         * </summary>
         */
        public CreateFiles()
        {
            try
            {
                Logger.StartLog(Application.ProductName, Application.ProductVersion);
            }
            catch (Exception exc)
            {
                // No point in logging this if the Logger did not start up.
                _ = MessageBox.Show($"Unable to start log file.\r\n\r\n{exc.Message}", MessageBoxTitle);
                // Continue because failure to create a log is not disastrous.
            }

            try
            {
                string
                    settingsVersion = settings.Version,
                    thisVersion = Application.ProductVersion;

                decimal
                    settingsVersionAsDecimal = Common.VersionToDecimal(settingsVersion),
                    thisVersionAsDecimal = Common.VersionToDecimal(thisVersion);

                if (settingsVersionAsDecimal != thisVersionAsDecimal)
                {
                    if (settingsVersionAsDecimal < thisVersionAsDecimal)
                    {
                        Logger.Log($"Upgrading Settings from version {settingsVersion} to {thisVersion}");
                        settings.Upgrade();
                    }
                    else
                    {
                        Logger.Log($"Resetting the version in settings from {settingsVersion} to {thisVersion} because it is more recent");
                    }
                    settings.Version = thisVersion;
                    settings.Save();
                }
            }
            catch (Exception exc)
            {
                Logger.Log(exc);
                _ = MessageBox.Show($"Error while upgrading version.\r\n\r\n{exc.Message}", MessageBoxTitle);
                Logger.Close();
                Environment.Exit(1);
            }

            InitializeComponent();
        }

        /**
         * <summary>
         *   Enables or disables the Form's controls and sets the current directory.
         * </summary>
         */
        private void CreateFiles_Load(object sender, EventArgs e)
        {
            try
            {
                EnableControls(true);
            }
            catch (Exception exc)
            {
                Logger.Log(exc);
                _ = MessageBox.Show($"Error while loading.\r\n\r\n{exc.Message}", MessageBoxTitle);
                Logger.Close();
                Environment.Exit(1);
            }

            try
            {
                // This application's XSLT files have static file paths in, which makes it impractical to use full paths.
                //
                // To deal with this, CD to the directory containing the executable so that the XSLT files can contain
                // file names without full paths.

                Directory.SetCurrentDirectory(Application.StartupPath);
            }
            catch (Exception exc)
            {
                Logger.Log(exc);
                ShowError($"Error while changing directory to {Application.StartupPath}.\r\n\r\n{exc.Message}");
                Logger.Close();
                Environment.Exit(1);
            }
        }

        /**
         * <summary>
         *   Calls <see cref="MessageBox.Show(string)"/> in response to a callback from <see cref="AdifTestFileCreator.FileCreator"/>
         *   to report an error to the user.
         * </summary>
         * 
         * <remarks>
         *   This is used to to avoid adding a dependency
         *   on <see cref="System.Windows.Forms"/> in <see cref="AdifTestFileCreator.FileCreator"/>.
         * </remarks>
         */
        private void PromptUser(string message) => MessageBox.Show(message, $"{Application.ProductName} Error");

        /**
          * <summary>
          *   Shows a FolderBrowserDialog in response to the Form's "..." button.
          * </summary>
          */
        private void BtnChooseDirectory_Click(object sender, EventArgs e)
        {
            try
            {
                string directoryPath = string.Empty;

                try
                {
                    directoryPath = Path.GetFullPath(TxtAdifPath.Text);
                }
                catch
                {
                    directoryPath = string.Empty;
                }

                if ((!string.IsNullOrEmpty(directoryPath)) &&
                    Directory.Exists(directoryPath))
                {
                    FbdAdif.SelectedPath = directoryPath;
                }

                if (FbdAdif.ShowDialog(this) == DialogResult.OK)
                { 
                    TxtAdifPath.Text = FbdAdif.SelectedPath;
                    settings.Save();
                }
            }
            catch (Exception exc)
            {
                Logger.Log(exc);
                ShowError($"Error choosing ADIF directory.\r\n\r\n{exc.Message}");
            }
        }

        /**
         * <summary>
         *   Creates ADIF test QSOs files in response to the "Create QSOs Files" button.
         * </summary>
         */
        private void BtnCreateQsosFiles_Click(object sender, EventArgs e)
        {
            try
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

                exporting = true;
                EnableControls(false);

                Cursor = Cursors.WaitCursor;

                new FileCreator(
                    TxtAdifPath.Text,
                    Application.StartupPath,
                    Application.ProductName,
                    Application.ProductVersion,
                    new FileCreator.ProgressReporter(ShowProgress),
                    new FileCreator.UserPrompter(PromptUser)).CreateAdifTestFiles();
            }
            catch (Exception exc)
            {
                Logger.Log(exc);

                string innerExceptionMessage = exc.InnerException != null ? $"\r\n{exc.InnerException.Message}" : string.Empty;

                ShowError($"Error creating test QSOs files.\r\n\r\n{exc.Message}{innerExceptionMessage}");
            }
            finally
            {
                try
                {
                    exporting = false;
                    Cursor = Cursors.Default;
                    EnableControls(true);
                }
                catch { }
            }
        }

        /**
         * <summary>
         *   Displays a progress message in the status bar and logs the message.&#160;
         *   Checks to see if the Close button has been clicked and if so, ends the program.
         * </summary>
         */
        private void ShowProgress(string message)
        {
            AdifReleaseLib.Logger.Log(message);

            TsslProgress.Text = Common.ToSingleLine(message);
            SsProgress.Refresh();
            Application.DoEvents();
            if (cancel)
            {
                Logger.Log("Program exit while creating files due to the Close button or window Close box/menu item being clicked.");
                Logger.Close();
                Environment.Exit(0);
            }
        }

        /**
         * <summary>
         *   Enables or disables some of the user interface controls.
         * </summary>
         */
        private void EnableControls(bool enable)
        {
            LblAdif.Enabled =
            TxtAdifPath.Enabled =
            BtnChooseFile.Enabled = enable;

            if (enable)
            {
                BtnCreateQsosFiles.Enabled = Directory.Exists(TxtAdifPath.Text);
            }
            else
            {
                BtnCreateQsosFiles.Enabled = false;
            }
        }

        /**
         * <summary>
         *   Shows a Message Box containing details of an error.
         * </summary>
         */
        private static void ShowError(string message)
        {
            _ = MessageBox.Show(
                message,
                "Create QSOs Files Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation);
        }

        /**
         * <summary>
         *   Closes the program in response to the Close button.&#160;
         *   Sets the 'cancel' flag so that if an export is in progress, the program can be ended at the next call to the <see cref="ShowProgress"/> method.
         * </summary>
         */
        private void BtnClose_Click(object sender, EventArgs e)
        {
            try
            {
                Close();
                cancel = true;
            }
            catch (Exception exc)
            {
                Logger.Log(exc);
                ShowError($"Error closing application.\r\n\r\n{exc.Message}");
            }
        }

        /**
         * <summary>
         *   Closes the log file if no export is in progress.
         * </summary>
         */
        private void CreateFiles_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (!exporting) Logger.Close();
        }

        /**
         * <summary>
         *   Sets the flag to close the program if it is currently exporting.&#160;
         *   This is necessary when the windows close box is clicked.
         * </summary>
         */
        private void CreateFiles_FormClosing(object sender, FormClosingEventArgs e)
        {
            cancel = true;
        }

        /**
         * <summary>
         *   Enables or disables the <see cref="BtnCreateQsosFiles"/> button depending on whether the path in <see cref="TxtAdifPath"/> exists.
         * </summary>
         */
        private void TxtAllFile_TextChanged(object sender, EventArgs e)
        {
            try
            {
                bool directoryExists = Directory.Exists(TxtAdifPath.Text);

                BtnCreateQsosFiles.Enabled = directoryExists;

                if (directoryExists)
                {
                    settings.TxtAdifPath = TxtAdifPath.Text;
                    settings.Save();
                }
            }
            catch { }
        }
    }
}
