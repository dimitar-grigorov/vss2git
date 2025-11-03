/* Copyright 2009 HPDI, LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Hpdi.VssLogicalLib;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Main form for the application.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public partial class MainForm : Form
    {
        private readonly Dictionary<int, EncodingInfo> codePages = new Dictionary<int, EncodingInfo>();
        private readonly WorkQueue workQueue = new WorkQueue(1);
        private Logger logger = Logger.Null;
        private RevisionAnalyzer revisionAnalyzer;
        private ChangesetBuilder changesetBuilder;

        public MainForm()
        {
            InitializeComponent();
        }

        private void OpenLog(string filename)
        {
            logger = string.IsNullOrEmpty(filename) ? Logger.Null : new Logger(filename);
        }

        private void goButton_Click(object sender, EventArgs e)
        {
            // If migration is running, toggle pause/resume
            if (!workQueue.IsIdle)
            {
                if (workQueue.IsSuspended)
                {
                    workQueue.Resume();
                }
                else
                {
                    workQueue.Suspend();
                }
                return;
            }

            // Start new migration
            try
            {
                OpenLog(logTextBox.Text);

                logger.WriteLine("VSS2Git version {0}", Assembly.GetExecutingAssembly().GetName().Version);

                WriteSettings();

                Encoding encoding = Encoding.Default;
                EncodingInfo encodingInfo;
                if (codePages.TryGetValue(encodingComboBox.SelectedIndex, out encodingInfo))
                {
                    encoding = encodingInfo.GetEncoding();
                }

                logger.WriteLine("VSS encoding: {0} (CP: {1}, IANA: {2})",
                    encoding.EncodingName, encoding.CodePage, encoding.WebName);
                logger.WriteLine("Comment transcoding: {0}",
                    transcodeCheckBox.Checked ? "enabled" : "disabled");
                logger.WriteLine("Ignore errors: {0}",
                    ignoreErrorsCheckBox.Checked ? "enabled" : "disabled");

                var df = new VssDatabaseFactory(vssDirTextBox.Text);
                df.Encoding = encoding;
                var db = df.Open();

                var path = vssProjectTextBox.Text.Trim();

                // Default to root project if path is empty
                if (string.IsNullOrEmpty(path))
                {
                    path = "$";
                    logger.WriteLine("VSS project path was empty, defaulting to root: $");
                }

                logger.WriteLine("VSS project: {0}", path);
                VssItem item;
                try
                {
                    item = db.GetItem(path);
                }
                catch (VssPathException ex)
                {
                    MessageBox.Show(ex.Message, "Invalid project path",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var project = item as VssProject;
                if (project == null)
                {
                    MessageBox.Show(path + " is not a project", "Invalid project path",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var userInteraction = new MessageBoxUserInteraction(this);

                revisionAnalyzer = new RevisionAnalyzer(workQueue, logger, db, userInteraction);
                if (!string.IsNullOrEmpty(excludeTextBox.Text))
                {
                    revisionAnalyzer.ExcludeFiles = excludeTextBox.Text;
                }
                revisionAnalyzer.AddItem(project);

                changesetBuilder = new ChangesetBuilder(workQueue, logger, revisionAnalyzer, userInteraction);
                changesetBuilder.AnyCommentThreshold = TimeSpan.FromSeconds((double)anyCommentUpDown.Value);
                changesetBuilder.SameCommentThreshold = TimeSpan.FromSeconds((double)sameCommentUpDown.Value);
                changesetBuilder.BuildChangesets();

                if (!string.IsNullOrEmpty(outDirTextBox.Text))
                {
                    var outDir = outDirTextBox.Text.Trim();
                    if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
                    {
                        var dlgRes = MessageBox.Show(
                            "The output directory is not empty. Do you want to continue?",
                            "Output directory not empty",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);
                        if (dlgRes != DialogResult.Yes)
                            return;
                    }

                    var gitExporter = new GitExporter(workQueue, logger,
                        revisionAnalyzer, changesetBuilder, userInteraction);
                    if (!string.IsNullOrEmpty(domainTextBox.Text))
                    {
                        gitExporter.EmailDomain = domainTextBox.Text;
                    }
                    if (!string.IsNullOrEmpty(commentTextBox.Text))
                    {
                        gitExporter.DefaultComment = commentTextBox.Text;
                    }
                    if (!transcodeCheckBox.Checked)
                    {
                        gitExporter.CommitEncoding = encoding;
                    }
                    gitExporter.IgnoreErrors = ignoreErrorsCheckBox.Checked;
                    gitExporter.ExportProjectToGitRoot = exportProjectToGitRootCheckBox.Checked;
                    gitExporter.ExportToGit(outDir);
                }

                workQueue.Idle += delegate
                {
                    logger.Dispose();
                    logger = Logger.Null;
                };

                statusTimer.Enabled = true;
            }
            catch (Exception ex)
            {
                logger.Dispose();
                logger = Logger.Null;
                ShowException(ex);
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            workQueue.Abort();
        }

        private void statusTimer_Tick(object sender, EventArgs e)
        {
            statusLabel.Text = workQueue.LastStatus ?? "Idle";
            timeLabel.Text = string.Format("Elapsed: {0:HH:mm:ss}",
                new DateTime(workQueue.ActiveTime.Ticks));

            if (revisionAnalyzer != null)
            {
                fileLabel.Text = "Files: " + revisionAnalyzer.FileCount;
                revisionLabel.Text = "Revisions: " + revisionAnalyzer.RevisionCount;
            }

            if (changesetBuilder != null)
            {
                changeLabel.Text = "Changesets: " + changesetBuilder.Changesets.Count;
            }

            // Update button states based on work queue status
            if (workQueue.IsIdle)
            {
                revisionAnalyzer = null;
                changesetBuilder = null;

                statusTimer.Enabled = false;
                goButton.Text = "Go";
                goButton.Enabled = true;
                cancelButton.Enabled = false;
            }
            else if (workQueue.IsSuspended)
            {
                goButton.Text = "Resume";
                goButton.Enabled = true;
                cancelButton.Enabled = true;
            }
            else
            {
                // Check if we're in Git export phase (pausable) or analysis/changeset phase (not pausable)
                // TODO: Add status as type
                var status = workQueue.LastStatus ?? "";
                bool isGitExportPhase = status.Contains("Replaying") || status.Contains("Committing") ||
                                        status.Contains("tag") || status.Contains("Initializing Git");

                if (isGitExportPhase)
                {
                    goButton.Text = "Pause";
                    goButton.Enabled = true;
                }
                else
                {
                    goButton.Text = "Running...";
                    goButton.Enabled = false;
                }
                cancelButton.Enabled = true;
            }

            var exceptions = workQueue.FetchExceptions();
            if (exceptions != null)
            {
                foreach (var exception in exceptions)
                {
                    ShowException(exception);
                }
            }
        }

        private void ShowException(Exception exception)
        {
            var message = ExceptionFormatter.Format(exception);
            logger.WriteLine("ERROR: {0}", message);
            logger.WriteLine(exception);

            MessageBox.Show(message, "Unhandled Exception",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.Text += " " + Assembly.GetExecutingAssembly().GetName().Version;

            Encoding systemEncoding = GetSystemDefaultEncoding();
            var defaultCodePage = systemEncoding.CodePage;
            var description = string.Format("System default - {0}", systemEncoding.EncodingName);
            var defaultIndex = encodingComboBox.Items.Add(description);
            encodingComboBox.SelectedIndex = defaultIndex;

            var encodings = Encoding.GetEncodings();
            foreach (var encoding in encodings)
            {
                var codePage = encoding.CodePage;
                description = string.Format("CP{0} - {1}", codePage, encoding.DisplayName);
                var index = encodingComboBox.Items.Add(description);
                codePages[index] = encoding;
                if (codePage == defaultCodePage)
                {
                    codePages[defaultIndex] = encoding;
                }
            }

            ReadSettings();

            // Initialize button states
            cancelButton.Enabled = false;
        }

        private static Encoding GetSystemDefaultEncoding()
        {
            int ansiCodePage = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            return Encoding.GetEncoding(ansiCodePage);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Ask user if they want to quit while migration is running
            if (!workQueue.IsIdle)
            {
                var result = MessageBox.Show(
                    "VSS to Git migration is currently running. Do you really want to quit?",
                    "Migration in Progress",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            WriteSettings();

            workQueue.Abort();
            workQueue.WaitIdle();
        }

        private void ReadSettings()
        {
            var settings = Properties.Settings.Default;
            vssDirTextBox.Text = settings.VssDirectory;
            vssProjectTextBox.Text = settings.VssProject;
            excludeTextBox.Text = settings.VssExcludePaths;
            outDirTextBox.Text = settings.GitDirectory;
            domainTextBox.Text = settings.DefaultEmailDomain;
            commentTextBox.Text = settings.DefaultComment;
            logTextBox.Text = settings.LogFile;
            transcodeCheckBox.Checked = settings.TranscodeComments;
            forceAnnotatedCheckBox.Checked = settings.ForceAnnotatedTags;
            exportProjectToGitRootCheckBox.Checked = settings.ExportProjectToGitRoot;
            anyCommentUpDown.Value = settings.AnyCommentSeconds;
            sameCommentUpDown.Value = settings.SameCommentSeconds;
        }

        private void WriteSettings()
        {
            var settings = Properties.Settings.Default;
            settings.VssDirectory = vssDirTextBox.Text;
            settings.VssProject = vssProjectTextBox.Text;
            settings.VssExcludePaths = excludeTextBox.Text;
            settings.GitDirectory = outDirTextBox.Text;
            settings.DefaultEmailDomain = domainTextBox.Text;
            settings.LogFile = logTextBox.Text;
            settings.TranscodeComments = transcodeCheckBox.Checked;
            settings.ForceAnnotatedTags = forceAnnotatedCheckBox.Checked;
            settings.ExportProjectToGitRoot = exportProjectToGitRootCheckBox.Checked;
            settings.AnyCommentSeconds = (int)anyCommentUpDown.Value;
            settings.SameCommentSeconds = (int)sameCommentUpDown.Value;
            settings.Save();
        }

        private void BrowseForFolder(TextBox textBox, string description)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = description;
                folderDialog.ShowNewFolderButton = true;
                string existingPath = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(existingPath) && Directory.Exists(existingPath))
                {
                    folderDialog.SelectedPath = existingPath;
                }

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = folderDialog.SelectedPath;
                }
            }
        }

        private void vssDirBrowseButton_Click(object sender, EventArgs e)
        {
            BrowseForFolder(vssDirTextBox, "Select VSS Database Directory");
        }

        private void outDirBrowseButton_Click(object sender, EventArgs e)
        {
            BrowseForFolder(outDirTextBox, "Select Git Output Directory");
        }
    }
}
