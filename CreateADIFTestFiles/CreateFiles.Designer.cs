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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateFiles));
            SsProgress = new System.Windows.Forms.StatusStrip();
            TsslProgress = new System.Windows.Forms.ToolStripStatusLabel();
            BtnClose = new System.Windows.Forms.Button();
            BtnCreateQsosFiles = new System.Windows.Forms.Button();
            BtnChooseFile = new System.Windows.Forms.Button();
            LblAdif = new System.Windows.Forms.Label();
            toolTip = new System.Windows.Forms.ToolTip(components);
            TxtAdifPath = new System.Windows.Forms.TextBox();
            FbdAdif = new System.Windows.Forms.FolderBrowserDialog();
            SsProgress.SuspendLayout();
            SuspendLayout();
            // 
            // SsProgress
            // 
            SsProgress.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { TsslProgress });
            SsProgress.Location = new System.Drawing.Point(0, 178);
            SsProgress.Name = "SsProgress";
            SsProgress.Padding = new System.Windows.Forms.Padding(1, 0, 16, 0);
            SsProgress.Size = new System.Drawing.Size(653, 22);
            SsProgress.TabIndex = 11;
            SsProgress.Text = "statusStrip1";
            // 
            // TsslProgress
            // 
            TsslProgress.Name = "TsslProgress";
            TsslProgress.Size = new System.Drawing.Size(0, 17);
            // 
            // BtnClose
            // 
            BtnClose.Anchor = System.Windows.Forms.AnchorStyles.Top;
            BtnClose.AutoSize = true;
            BtnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            BtnClose.Location = new System.Drawing.Point(337, 128);
            BtnClose.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            BtnClose.Name = "BtnClose";
            BtnClose.Size = new System.Drawing.Size(121, 27);
            BtnClose.TabIndex = 10;
            BtnClose.Text = "Close";
            toolTip.SetToolTip(BtnClose, "Close this application.");
            BtnClose.UseVisualStyleBackColor = true;
            BtnClose.Click += BtnClose_Click;
            // 
            // BtnCreateQsosFiles
            // 
            BtnCreateQsosFiles.Anchor = System.Windows.Forms.AnchorStyles.Top;
            BtnCreateQsosFiles.Location = new System.Drawing.Point(201, 128);
            BtnCreateQsosFiles.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            BtnCreateQsosFiles.Name = "BtnCreateQsosFiles";
            BtnCreateQsosFiles.Size = new System.Drawing.Size(121, 27);
            BtnCreateQsosFiles.TabIndex = 9;
            BtnCreateQsosFiles.Text = "Create QSOs Files";
            toolTip.SetToolTip(BtnCreateQsosFiles, "Create the ADIF test QSOs files");
            BtnCreateQsosFiles.UseVisualStyleBackColor = true;
            BtnCreateQsosFiles.Click += BtnCreateQsosFiles_Click;
            // 
            // BtnChooseFile
            // 
            BtnChooseFile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            BtnChooseFile.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            BtnChooseFile.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
            BtnChooseFile.Location = new System.Drawing.Point(600, 42);
            BtnChooseFile.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            BtnChooseFile.Name = "BtnChooseFile";
            BtnChooseFile.Size = new System.Drawing.Size(35, 27);
            BtnChooseFile.TabIndex = 8;
            BtnChooseFile.Text = "...";
            toolTip.SetToolTip(BtnChooseFile, "Browse for Directory");
            BtnChooseFile.UseVisualStyleBackColor = true;
            BtnChooseFile.Click += BtnChooseDirectory_Click;
            // 
            // LblAdif
            // 
            LblAdif.AutoSize = true;
            LblAdif.Location = new System.Drawing.Point(16, 46);
            LblAdif.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            LblAdif.Name = "LblAdif";
            LblAdif.Size = new System.Drawing.Size(127, 15);
            LblAdif.TabIndex = 6;
            LblAdif.Text = "ADIF Version Directory:";
            toolTip.SetToolTip(LblAdif, "The directory path for the ADIF version.\r\nThis is normally the ADIF version as a 3-digit number, e.g. 314 for ADIF version 3.1.4");
            // 
            // TxtAdifPath
            // 
            TxtAdifPath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            TxtAdifPath.Location = new System.Drawing.Point(160, 43);
            TxtAdifPath.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            TxtAdifPath.Name = "TxtAdifPath";
            TxtAdifPath.Size = new System.Drawing.Size(443, 23);
            TxtAdifPath.TabIndex = 7;
            toolTip.SetToolTip(TxtAdifPath, "The directory path for the ADIF version.\r\nThis is normally the ADIF version as a 3-digit number, e.g. 314 for ADIF version 3.1.4");
            TxtAdifPath.TextChanged += TxtAllFile_TextChanged;
            // 
            // FbdAdif
            // 
            FbdAdif.Description = "Choose ADIF version number directory:";
            FbdAdif.ShowNewFolderButton = false;
            // 
            // CreateFiles
            // 
            AcceptButton = BtnCreateQsosFiles;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = BtnClose;
            ClientSize = new System.Drawing.Size(653, 200);
            Controls.Add(SsProgress);
            Controls.Add(BtnClose);
            Controls.Add(BtnCreateQsosFiles);
            Controls.Add(BtnChooseFile);
            Controls.Add(LblAdif);
            Controls.Add(TxtAdifPath);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "CreateFiles";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Create ADIF Test Files";
            Load += CreateFiles_Load;
            SsProgress.ResumeLayout(false);
            SsProgress.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
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

