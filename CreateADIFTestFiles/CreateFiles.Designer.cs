namespace CreateADIFTestFiles
{
    partial class CreateFiles
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateFiles));
            this.SsProgress = new System.Windows.Forms.StatusStrip();
            this.TsslProgress = new System.Windows.Forms.ToolStripStatusLabel();
            this.BtnClose = new System.Windows.Forms.Button();
            this.BtnCreateQsosFiles = new System.Windows.Forms.Button();
            this.BtnChooseFile = new System.Windows.Forms.Button();
            this.LblAdif = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.TxtAdifPath = new System.Windows.Forms.TextBox();
            this.FbdAdif = new System.Windows.Forms.FolderBrowserDialog();
            this.SsProgress.SuspendLayout();
            this.SuspendLayout();
            // 
            // SsProgress
            // 
            this.SsProgress.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.TsslProgress});
            this.SsProgress.Location = new System.Drawing.Point(0, 151);
            this.SsProgress.Name = "SsProgress";
            this.SsProgress.Size = new System.Drawing.Size(560, 22);
            this.SsProgress.TabIndex = 11;
            this.SsProgress.Text = "statusStrip1";
            // 
            // TsslProgress
            // 
            this.TsslProgress.Name = "TsslProgress";
            this.TsslProgress.Size = new System.Drawing.Size(0, 17);
            // 
            // BtnClose
            // 
            this.BtnClose.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.BtnClose.AutoSize = true;
            this.BtnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.BtnClose.Location = new System.Drawing.Point(289, 111);
            this.BtnClose.Name = "BtnClose";
            this.BtnClose.Size = new System.Drawing.Size(104, 23);
            this.BtnClose.TabIndex = 10;
            this.BtnClose.Text = "Close";
            this.toolTip.SetToolTip(this.BtnClose, "Close this application.");
            this.BtnClose.UseVisualStyleBackColor = true;
            this.BtnClose.Click += new System.EventHandler(this.BtnClose_Click);
            // 
            // BtnCreateQsosFiles
            // 
            this.BtnCreateQsosFiles.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.BtnCreateQsosFiles.Location = new System.Drawing.Point(172, 111);
            this.BtnCreateQsosFiles.Name = "BtnCreateQsosFiles";
            this.BtnCreateQsosFiles.Size = new System.Drawing.Size(104, 23);
            this.BtnCreateQsosFiles.TabIndex = 9;
            this.BtnCreateQsosFiles.Text = "Create QSOs Files";
            this.toolTip.SetToolTip(this.BtnCreateQsosFiles, "Create the ADIF test QSOs files");
            this.BtnCreateQsosFiles.UseVisualStyleBackColor = true;
            this.BtnCreateQsosFiles.Click += new System.EventHandler(this.BtnCreateQsosFiles_Click);
            // 
            // BtnChooseFile
            // 
            this.BtnChooseFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.BtnChooseFile.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.BtnChooseFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BtnChooseFile.Location = new System.Drawing.Point(514, 36);
            this.BtnChooseFile.Name = "BtnChooseFile";
            this.BtnChooseFile.Size = new System.Drawing.Size(30, 23);
            this.BtnChooseFile.TabIndex = 8;
            this.BtnChooseFile.Text = "...";
            this.toolTip.SetToolTip(this.BtnChooseFile, "Browse for Directory");
            this.BtnChooseFile.UseVisualStyleBackColor = true;
            this.BtnChooseFile.Click += new System.EventHandler(this.BtnChooseDirectory_Click);
            // 
            // LblAdif
            // 
            this.LblAdif.AutoSize = true;
            this.LblAdif.Location = new System.Drawing.Point(14, 40);
            this.LblAdif.Name = "LblAdif";
            this.LblAdif.Size = new System.Drawing.Size(117, 13);
            this.LblAdif.TabIndex = 6;
            this.LblAdif.Text = "ADIF Version Directory:";
            this.toolTip.SetToolTip(this.LblAdif, "The directory path for the ADIF version.\r\nThis is normally the ADIF version as a " +
        "3-digit number, e.g. 314 for ADIF version 3.1.4");
            // 
            // TxtAdifPath
            // 
            this.TxtAdifPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TxtAdifPath.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::CreateADIFTestFiles.Properties.Settings.Default, "TxtAdifPath", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.TxtAdifPath.Location = new System.Drawing.Point(137, 37);
            this.TxtAdifPath.Name = "TxtAdifPath";
            this.TxtAdifPath.Size = new System.Drawing.Size(380, 20);
            this.TxtAdifPath.TabIndex = 7;
            this.TxtAdifPath.Text = global::CreateADIFTestFiles.Properties.Settings.Default.TxtAdifPath;
            this.toolTip.SetToolTip(this.TxtAdifPath, "The directory path for the ADIF version.\r\nThis is normally the ADIF version as a " +
        "3-digit number, e.g. 314 for ADIF version 3.1.4");
            this.TxtAdifPath.TextChanged += new System.EventHandler(this.TxtAllFile_TextChanged);
            // 
            // FbdAdif
            // 
            this.FbdAdif.Description = "Choose ADIF version number directory:";
            this.FbdAdif.ShowNewFolderButton = false;
            // 
            // CreateFiles
            // 
            this.AcceptButton = this.BtnCreateQsosFiles;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(560, 173);
            this.Controls.Add(this.SsProgress);
            this.Controls.Add(this.BtnClose);
            this.Controls.Add(this.BtnCreateQsosFiles);
            this.Controls.Add(this.BtnChooseFile);
            this.Controls.Add(this.LblAdif);
            this.Controls.Add(this.TxtAdifPath);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "CreateFiles";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Create ADIF Test Files";
            this.Load += new System.EventHandler(this.CreateFiles_Load);
            this.SsProgress.ResumeLayout(false);
            this.SsProgress.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip SsProgress;
        private System.Windows.Forms.ToolStripStatusLabel TsslProgress;
        private System.Windows.Forms.Button BtnClose;
        private System.Windows.Forms.Button BtnCreateQsosFiles;
        private System.Windows.Forms.Button BtnChooseFile;
        private System.Windows.Forms.Label LblAdif;
        private System.Windows.Forms.TextBox TxtAdifPath;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.FolderBrowserDialog FbdAdif;
    }
}

