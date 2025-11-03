namespace Hpdi.Vss2Git
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            vssGroupBox = new System.Windows.Forms.GroupBox();
            vssDirBrowseButton = new System.Windows.Forms.Button();
            encodingLabel = new System.Windows.Forms.Label();
            encodingComboBox = new System.Windows.Forms.ComboBox();
            excludeTextBox = new System.Windows.Forms.TextBox();
            excludeLabel = new System.Windows.Forms.Label();
            vssProjectTextBox = new System.Windows.Forms.TextBox();
            vssDirTextBox = new System.Windows.Forms.TextBox();
            vssProjectLabel = new System.Windows.Forms.Label();
            vssDirLabel = new System.Windows.Forms.Label();
            goButton = new System.Windows.Forms.Button();
            statusTimer = new System.Windows.Forms.Timer(components);
            statusStrip = new System.Windows.Forms.StatusStrip();
            statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            fileLabel = new System.Windows.Forms.ToolStripStatusLabel();
            revisionLabel = new System.Windows.Forms.ToolStripStatusLabel();
            changeLabel = new System.Windows.Forms.ToolStripStatusLabel();
            timeLabel = new System.Windows.Forms.ToolStripStatusLabel();
            outputGroupBox = new System.Windows.Forms.GroupBox();
            exportProjectToGitRootCheckBox = new System.Windows.Forms.CheckBox();
            outDirBrowseButton = new System.Windows.Forms.Button();
            ignoreErrorsCheckBox = new System.Windows.Forms.CheckBox();
            commentTextBox = new System.Windows.Forms.TextBox();
            commentLabel = new System.Windows.Forms.Label();
            forceAnnotatedCheckBox = new System.Windows.Forms.CheckBox();
            transcodeCheckBox = new System.Windows.Forms.CheckBox();
            domainTextBox = new System.Windows.Forms.TextBox();
            domainLabel = new System.Windows.Forms.Label();
            outDirTextBox = new System.Windows.Forms.TextBox();
            outDirLabel = new System.Windows.Forms.Label();
            logTextBox = new System.Windows.Forms.TextBox();
            logLabel = new System.Windows.Forms.Label();
            cancelButton = new System.Windows.Forms.Button();
            changesetGroupBox = new System.Windows.Forms.GroupBox();
            label4 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            sameCommentUpDown = new System.Windows.Forms.NumericUpDown();
            label2 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            anyCommentUpDown = new System.Windows.Forms.NumericUpDown();
            vssGroupBox.SuspendLayout();
            statusStrip.SuspendLayout();
            outputGroupBox.SuspendLayout();
            changesetGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)sameCommentUpDown).BeginInit();
            ((System.ComponentModel.ISupportInitialize)anyCommentUpDown).BeginInit();
            SuspendLayout();
            // 
            // vssGroupBox
            // 
            vssGroupBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            vssGroupBox.Controls.Add(vssDirBrowseButton);
            vssGroupBox.Controls.Add(encodingLabel);
            vssGroupBox.Controls.Add(encodingComboBox);
            vssGroupBox.Controls.Add(excludeTextBox);
            vssGroupBox.Controls.Add(excludeLabel);
            vssGroupBox.Controls.Add(vssProjectTextBox);
            vssGroupBox.Controls.Add(vssDirTextBox);
            vssGroupBox.Controls.Add(vssProjectLabel);
            vssGroupBox.Controls.Add(vssDirLabel);
            vssGroupBox.Location = new System.Drawing.Point(14, 14);
            vssGroupBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            vssGroupBox.Name = "vssGroupBox";
            vssGroupBox.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            vssGroupBox.Size = new System.Drawing.Size(701, 145);
            vssGroupBox.TabIndex = 0;
            vssGroupBox.TabStop = false;
            vssGroupBox.Text = "VSS Settings";
            // 
            // vssDirBrowseButton
            // 
            vssDirBrowseButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            vssDirBrowseButton.Location = new System.Drawing.Point(662, 22);
            vssDirBrowseButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            vssDirBrowseButton.Name = "vssDirBrowseButton";
            vssDirBrowseButton.Size = new System.Drawing.Size(33, 25);
            vssDirBrowseButton.TabIndex = 8;
            vssDirBrowseButton.Text = "...";
            vssDirBrowseButton.UseVisualStyleBackColor = true;
            vssDirBrowseButton.Click += vssDirBrowseButton_Click;
            // 
            // encodingLabel
            // 
            encodingLabel.AutoSize = true;
            encodingLabel.Location = new System.Drawing.Point(7, 115);
            encodingLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            encodingLabel.Name = "encodingLabel";
            encodingLabel.Size = new System.Drawing.Size(57, 15);
            encodingLabel.TabIndex = 6;
            encodingLabel.Text = "Encoding";
            // 
            // encodingComboBox
            // 
            encodingComboBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            encodingComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            encodingComboBox.FormattingEnabled = true;
            encodingComboBox.Location = new System.Drawing.Point(110, 112);
            encodingComboBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            encodingComboBox.Name = "encodingComboBox";
            encodingComboBox.Size = new System.Drawing.Size(584, 23);
            encodingComboBox.TabIndex = 7;
            // 
            // excludeTextBox
            // 
            excludeTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            excludeTextBox.Location = new System.Drawing.Point(110, 82);
            excludeTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            excludeTextBox.Name = "excludeTextBox";
            excludeTextBox.Size = new System.Drawing.Size(584, 23);
            excludeTextBox.TabIndex = 5;
            // 
            // excludeLabel
            // 
            excludeLabel.AutoSize = true;
            excludeLabel.Location = new System.Drawing.Point(7, 85);
            excludeLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            excludeLabel.Name = "excludeLabel";
            excludeLabel.Size = new System.Drawing.Size(71, 15);
            excludeLabel.TabIndex = 4;
            excludeLabel.Text = "Exclude files";
            // 
            // vssProjectTextBox
            // 
            vssProjectTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            vssProjectTextBox.Location = new System.Drawing.Point(110, 52);
            vssProjectTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            vssProjectTextBox.Name = "vssProjectTextBox";
            vssProjectTextBox.Size = new System.Drawing.Size(584, 23);
            vssProjectTextBox.TabIndex = 3;
            // 
            // vssDirTextBox
            // 
            vssDirTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            vssDirTextBox.Location = new System.Drawing.Point(110, 22);
            vssDirTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            vssDirTextBox.Name = "vssDirTextBox";
            vssDirTextBox.Size = new System.Drawing.Size(548, 23);
            vssDirTextBox.TabIndex = 1;
            // 
            // vssProjectLabel
            // 
            vssProjectLabel.AutoSize = true;
            vssProjectLabel.Location = new System.Drawing.Point(7, 55);
            vssProjectLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            vssProjectLabel.Name = "vssProjectLabel";
            vssProjectLabel.Size = new System.Drawing.Size(44, 15);
            vssProjectLabel.TabIndex = 2;
            vssProjectLabel.Text = "Project";
            // 
            // vssDirLabel
            // 
            vssDirLabel.AutoSize = true;
            vssDirLabel.Location = new System.Drawing.Point(7, 25);
            vssDirLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            vssDirLabel.Name = "vssDirLabel";
            vssDirLabel.Size = new System.Drawing.Size(55, 15);
            vssDirLabel.TabIndex = 0;
            vssDirLabel.Text = "Directory";
            // 
            // goButton
            // 
            goButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            goButton.Location = new System.Drawing.Point(533, 432);
            goButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            goButton.Name = "goButton";
            goButton.Size = new System.Drawing.Size(88, 27);
            goButton.TabIndex = 3;
            goButton.Text = "Go!";
            goButton.UseVisualStyleBackColor = true;
            goButton.Click += goButton_Click;
            // 
            // statusTimer
            // 
            statusTimer.Tick += statusTimer_Tick;
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { statusLabel, fileLabel, revisionLabel, changeLabel, timeLabel });
            statusStrip.Location = new System.Drawing.Point(0, 466);
            statusStrip.Name = "statusStrip";
            statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 16, 0);
            statusStrip.Size = new System.Drawing.Size(729, 22);
            statusStrip.TabIndex = 5;
            statusStrip.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new System.Drawing.Size(427, 17);
            statusLabel.Spring = true;
            statusLabel.Text = "Idle";
            statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // fileLabel
            // 
            fileLabel.Name = "fileLabel";
            fileLabel.Size = new System.Drawing.Size(42, 17);
            fileLabel.Text = "Files: 0";
            // 
            // revisionLabel
            // 
            revisionLabel.Name = "revisionLabel";
            revisionLabel.Size = new System.Drawing.Size(68, 17);
            revisionLabel.Text = "Revisions: 0";
            // 
            // changeLabel
            // 
            changeLabel.Name = "changeLabel";
            changeLabel.Size = new System.Drawing.Size(80, 17);
            changeLabel.Text = "Changesets: 0";
            // 
            // timeLabel
            // 
            timeLabel.Name = "timeLabel";
            timeLabel.Size = new System.Drawing.Size(95, 17);
            timeLabel.Text = "Elapsed: 00:00:00";
            // 
            // outputGroupBox
            // 
            outputGroupBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            outputGroupBox.Controls.Add(exportProjectToGitRootCheckBox);
            outputGroupBox.Controls.Add(outDirBrowseButton);
            outputGroupBox.Controls.Add(ignoreErrorsCheckBox);
            outputGroupBox.Controls.Add(commentTextBox);
            outputGroupBox.Controls.Add(commentLabel);
            outputGroupBox.Controls.Add(forceAnnotatedCheckBox);
            outputGroupBox.Controls.Add(transcodeCheckBox);
            outputGroupBox.Controls.Add(domainTextBox);
            outputGroupBox.Controls.Add(domainLabel);
            outputGroupBox.Controls.Add(outDirTextBox);
            outputGroupBox.Controls.Add(outDirLabel);
            outputGroupBox.Controls.Add(logTextBox);
            outputGroupBox.Controls.Add(logLabel);
            outputGroupBox.Location = new System.Drawing.Point(14, 166);
            outputGroupBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            outputGroupBox.Name = "outputGroupBox";
            outputGroupBox.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            outputGroupBox.Size = new System.Drawing.Size(701, 167);
            outputGroupBox.TabIndex = 1;
            outputGroupBox.TabStop = false;
            outputGroupBox.Text = "Output Settings";
            // 
            // exportProjectToGitRootCheckBox
            // 
            exportProjectToGitRootCheckBox.AutoSize = true;
            exportProjectToGitRootCheckBox.Location = new System.Drawing.Point(536, 114);
            exportProjectToGitRootCheckBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            exportProjectToGitRootCheckBox.Name = "exportProjectToGitRootCheckBox";
            exportProjectToGitRootCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            exportProjectToGitRootCheckBox.Size = new System.Drawing.Size(156, 19);
            exportProjectToGitRootCheckBox.TabIndex = 10;
            exportProjectToGitRootCheckBox.Text = "Export project to Git root";
            exportProjectToGitRootCheckBox.UseVisualStyleBackColor = true;
            // 
            // outDirBrowseButton
            // 
            outDirBrowseButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            outDirBrowseButton.Location = new System.Drawing.Point(662, 22);
            outDirBrowseButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            outDirBrowseButton.Name = "outDirBrowseButton";
            outDirBrowseButton.Size = new System.Drawing.Size(33, 25);
            outDirBrowseButton.TabIndex = 9;
            outDirBrowseButton.Text = "...";
            outDirBrowseButton.UseVisualStyleBackColor = true;
            outDirBrowseButton.Click += outDirBrowseButton_Click;
            // 
            // ignoreErrorsCheckBox
            // 
            ignoreErrorsCheckBox.AutoSize = true;
            ignoreErrorsCheckBox.Location = new System.Drawing.Point(421, 114);
            ignoreErrorsCheckBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            ignoreErrorsCheckBox.Name = "ignoreErrorsCheckBox";
            ignoreErrorsCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            ignoreErrorsCheckBox.Size = new System.Drawing.Size(111, 19);
            ignoreErrorsCheckBox.TabIndex = 8;
            ignoreErrorsCheckBox.Text = "Ignore Git errors";
            ignoreErrorsCheckBox.UseVisualStyleBackColor = true;
            // 
            // commentTextBox
            // 
            commentTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            commentTextBox.Location = new System.Drawing.Point(110, 82);
            commentTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            commentTextBox.Name = "commentTextBox";
            commentTextBox.Size = new System.Drawing.Size(584, 23);
            commentTextBox.TabIndex = 6;
            // 
            // commentLabel
            // 
            commentLabel.AutoSize = true;
            commentLabel.Location = new System.Drawing.Point(5, 85);
            commentLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            commentLabel.Name = "commentLabel";
            commentLabel.Size = new System.Drawing.Size(100, 15);
            commentLabel.TabIndex = 8;
            commentLabel.Text = "Default comment";
            // 
            // forceAnnotatedCheckBox
            // 
            forceAnnotatedCheckBox.AutoSize = true;
            forceAnnotatedCheckBox.Checked = true;
            forceAnnotatedCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            forceAnnotatedCheckBox.Location = new System.Drawing.Point(205, 114);
            forceAnnotatedCheckBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            forceAnnotatedCheckBox.Name = "forceAnnotatedCheckBox";
            forceAnnotatedCheckBox.Size = new System.Drawing.Size(208, 19);
            forceAnnotatedCheckBox.TabIndex = 7;
            forceAnnotatedCheckBox.Text = "Force use of annotated tag objects";
            forceAnnotatedCheckBox.UseVisualStyleBackColor = true;
            // 
            // transcodeCheckBox
            // 
            transcodeCheckBox.AutoSize = true;
            transcodeCheckBox.Checked = true;
            transcodeCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            transcodeCheckBox.Location = new System.Drawing.Point(8, 114);
            transcodeCheckBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            transcodeCheckBox.Name = "transcodeCheckBox";
            transcodeCheckBox.Size = new System.Drawing.Size(189, 19);
            transcodeCheckBox.TabIndex = 6;
            transcodeCheckBox.Text = "Transcode comments to UTF-8";
            transcodeCheckBox.UseVisualStyleBackColor = true;
            // 
            // domainTextBox
            // 
            domainTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            domainTextBox.Location = new System.Drawing.Point(110, 52);
            domainTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            domainTextBox.Name = "domainTextBox";
            domainTextBox.Size = new System.Drawing.Size(250, 23);
            domainTextBox.TabIndex = 3;
            // 
            // domainLabel
            // 
            domainLabel.AutoSize = true;
            domainLabel.Location = new System.Drawing.Point(7, 55);
            domainLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            domainLabel.Name = "domainLabel";
            domainLabel.Size = new System.Drawing.Size(80, 15);
            domainLabel.TabIndex = 2;
            domainLabel.Text = "Email domain";
            // 
            // outDirTextBox
            // 
            outDirTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            outDirTextBox.Location = new System.Drawing.Point(110, 22);
            outDirTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            outDirTextBox.Name = "outDirTextBox";
            outDirTextBox.Size = new System.Drawing.Size(548, 23);
            outDirTextBox.TabIndex = 1;
            // 
            // outDirLabel
            // 
            outDirLabel.AutoSize = true;
            outDirLabel.Location = new System.Drawing.Point(7, 25);
            outDirLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            outDirLabel.Name = "outDirLabel";
            outDirLabel.Size = new System.Drawing.Size(55, 15);
            outDirLabel.TabIndex = 0;
            outDirLabel.Text = "Directory";
            // 
            // logTextBox
            // 
            logTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            logTextBox.Location = new System.Drawing.Point(444, 52);
            logTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            logTextBox.Name = "logTextBox";
            logTextBox.Size = new System.Drawing.Size(250, 23);
            logTextBox.TabIndex = 5;
            // 
            // logLabel
            // 
            logLabel.AutoSize = true;
            logLabel.Location = new System.Drawing.Point(368, 55);
            logLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            logLabel.Name = "logLabel";
            logLabel.Size = new System.Drawing.Size(46, 15);
            logLabel.TabIndex = 4;
            logLabel.Text = "Log file";
            // 
            // cancelButton
            // 
            cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            cancelButton.Location = new System.Drawing.Point(628, 432);
            cancelButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new System.Drawing.Size(88, 27);
            cancelButton.TabIndex = 4;
            cancelButton.Text = "Cancel";
            cancelButton.UseVisualStyleBackColor = true;
            cancelButton.Click += cancelButton_Click;
            // 
            // changesetGroupBox
            // 
            changesetGroupBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            changesetGroupBox.Controls.Add(label4);
            changesetGroupBox.Controls.Add(label3);
            changesetGroupBox.Controls.Add(sameCommentUpDown);
            changesetGroupBox.Controls.Add(label2);
            changesetGroupBox.Controls.Add(label1);
            changesetGroupBox.Controls.Add(anyCommentUpDown);
            changesetGroupBox.Location = new System.Drawing.Point(14, 339);
            changesetGroupBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            changesetGroupBox.Name = "changesetGroupBox";
            changesetGroupBox.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            changesetGroupBox.Size = new System.Drawing.Size(701, 87);
            changesetGroupBox.TabIndex = 2;
            changesetGroupBox.TabStop = false;
            changesetGroupBox.Text = "Changeset Building";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(226, 54);
            label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(213, 15);
            label4.TabIndex = 5;
            label4.Text = "seconds, if the comments are the same";
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(7, 54);
            label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(142, 15);
            label3.TabIndex = 3;
            label3.Text = "or within";
            label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // sameCommentUpDown
            // 
            sameCommentUpDown.Location = new System.Drawing.Point(156, 52);
            sameCommentUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            sameCommentUpDown.Maximum = new decimal(new int[] { 86400, 0, 0, 0 });
            sameCommentUpDown.Name = "sameCommentUpDown";
            sameCommentUpDown.Size = new System.Drawing.Size(63, 23);
            sameCommentUpDown.TabIndex = 4;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(226, 24);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(201, 15);
            label2.TabIndex = 2;
            label2.Text = "seconds, regardless of the comment,";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(7, 24);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(141, 15);
            label1.TabIndex = 0;
            label1.Text = "Combine revisions within";
            // 
            // anyCommentUpDown
            // 
            anyCommentUpDown.Location = new System.Drawing.Point(156, 22);
            anyCommentUpDown.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            anyCommentUpDown.Maximum = new decimal(new int[] { 86400, 0, 0, 0 });
            anyCommentUpDown.Name = "anyCommentUpDown";
            anyCommentUpDown.Size = new System.Drawing.Size(63, 23);
            anyCommentUpDown.TabIndex = 1;
            // 
            // MainForm
            // 
            AcceptButton = goButton;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = cancelButton;
            ClientSize = new System.Drawing.Size(729, 488);
            Controls.Add(changesetGroupBox);
            Controls.Add(cancelButton);
            Controls.Add(outputGroupBox);
            Controls.Add(goButton);
            Controls.Add(vssGroupBox);
            Controls.Add(statusStrip);
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 204);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MinimumSize = new System.Drawing.Size(532, 477);
            Name = "MainForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "VSS2Git";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            vssGroupBox.ResumeLayout(false);
            vssGroupBox.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            outputGroupBox.ResumeLayout(false);
            outputGroupBox.PerformLayout();
            changesetGroupBox.ResumeLayout(false);
            changesetGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)sameCommentUpDown).EndInit();
            ((System.ComponentModel.ISupportInitialize)anyCommentUpDown).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox vssGroupBox;
        private System.Windows.Forms.TextBox vssProjectTextBox;
        private System.Windows.Forms.TextBox vssDirTextBox;
        private System.Windows.Forms.Label vssProjectLabel;
        private System.Windows.Forms.Label vssDirLabel;
        private System.Windows.Forms.Button goButton;
        private System.Windows.Forms.Timer statusTimer;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel fileLabel;
        private System.Windows.Forms.ToolStripStatusLabel timeLabel;
        private System.Windows.Forms.ToolStripStatusLabel revisionLabel;
        private System.Windows.Forms.ToolStripStatusLabel changeLabel;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.GroupBox outputGroupBox;
        private System.Windows.Forms.TextBox logTextBox;
        private System.Windows.Forms.Label logLabel;
        private System.Windows.Forms.TextBox outDirTextBox;
        private System.Windows.Forms.Label outDirLabel;
        private System.Windows.Forms.TextBox domainTextBox;
        private System.Windows.Forms.Label domainLabel;
        private System.Windows.Forms.TextBox excludeTextBox;
        private System.Windows.Forms.Label excludeLabel;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.GroupBox changesetGroupBox;
        private System.Windows.Forms.NumericUpDown anyCommentUpDown;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown sameCommentUpDown;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label encodingLabel;
        private System.Windows.Forms.ComboBox encodingComboBox;
        private System.Windows.Forms.CheckBox transcodeCheckBox;
        private System.Windows.Forms.CheckBox forceAnnotatedCheckBox;
        private System.Windows.Forms.CheckBox ignoreErrorsCheckBox;
        private System.Windows.Forms.TextBox commentTextBox;
        private System.Windows.Forms.Label commentLabel;
        private System.Windows.Forms.Button vssDirBrowseButton;
        private System.Windows.Forms.Button outDirBrowseButton;
        private System.Windows.Forms.CheckBox exportProjectToGitRootCheckBox;
    }
}

