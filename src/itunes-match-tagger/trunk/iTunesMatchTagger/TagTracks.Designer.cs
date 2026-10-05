namespace iTunesMatchTagger
{
    partial class TagTracks
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TagTracks));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cbUpdateDiscNumber = new System.Windows.Forms.CheckBox();
            this.cbUpdateDiscCount = new System.Windows.Forms.CheckBox();
            this.cbUpdatePrimaryGenreName = new System.Windows.Forms.CheckBox();
            this.cbUpdateTrackNumber = new System.Windows.Forms.CheckBox();
            this.cbUpdateTrackCount = new System.Windows.Forms.CheckBox();
            this.cbUpdateArtistName = new System.Windows.Forms.CheckBox();
            this.cbUpdateCollectionName = new System.Windows.Forms.CheckBox();
            this.cbUpdateTrackName = new System.Windows.Forms.CheckBox();
            this.btnUpdateTracks = new System.Windows.Forms.Button();
            this.lbCountries = new System.Windows.Forms.ListBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnSelectAll = new System.Windows.Forms.Button();
            this.tbLog = new System.Windows.Forms.TextBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.cbShowDebug = new System.Windows.Forms.CheckBox();
            this.btnInfo = new System.Windows.Forms.Button();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cbUpdateDiscNumber);
            this.groupBox1.Controls.Add(this.cbUpdateDiscCount);
            this.groupBox1.Controls.Add(this.cbUpdatePrimaryGenreName);
            this.groupBox1.Controls.Add(this.cbUpdateTrackNumber);
            this.groupBox1.Controls.Add(this.cbUpdateTrackCount);
            this.groupBox1.Controls.Add(this.cbUpdateArtistName);
            this.groupBox1.Controls.Add(this.cbUpdateCollectionName);
            this.groupBox1.Controls.Add(this.cbUpdateTrackName);
            this.groupBox1.Location = new System.Drawing.Point(146, 10);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(104, 212);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Update Options";
            // 
            // cbUpdateDiscNumber
            // 
            this.cbUpdateDiscNumber.AutoSize = true;
            this.cbUpdateDiscNumber.Location = new System.Drawing.Point(8, 187);
            this.cbUpdateDiscNumber.Name = "cbUpdateDiscNumber";
            this.cbUpdateDiscNumber.Size = new System.Drawing.Size(87, 17);
            this.cbUpdateDiscNumber.TabIndex = 7;
            this.cbUpdateDiscNumber.Text = "Disc Number";
            this.cbUpdateDiscNumber.UseVisualStyleBackColor = true;
            // 
            // cbUpdateDiscCount
            // 
            this.cbUpdateDiscCount.AutoSize = true;
            this.cbUpdateDiscCount.Location = new System.Drawing.Point(8, 163);
            this.cbUpdateDiscCount.Name = "cbUpdateDiscCount";
            this.cbUpdateDiscCount.Size = new System.Drawing.Size(78, 17);
            this.cbUpdateDiscCount.TabIndex = 6;
            this.cbUpdateDiscCount.Text = "Disc Count";
            this.cbUpdateDiscCount.UseVisualStyleBackColor = true;
            // 
            // cbUpdatePrimaryGenreName
            // 
            this.cbUpdatePrimaryGenreName.AutoSize = true;
            this.cbUpdatePrimaryGenreName.Checked = true;
            this.cbUpdatePrimaryGenreName.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbUpdatePrimaryGenreName.Location = new System.Drawing.Point(8, 91);
            this.cbUpdatePrimaryGenreName.Name = "cbUpdatePrimaryGenreName";
            this.cbUpdatePrimaryGenreName.Size = new System.Drawing.Size(55, 17);
            this.cbUpdatePrimaryGenreName.TabIndex = 5;
            this.cbUpdatePrimaryGenreName.Text = "Genre";
            this.cbUpdatePrimaryGenreName.UseVisualStyleBackColor = true;
            // 
            // cbUpdateTrackNumber
            // 
            this.cbUpdateTrackNumber.AutoSize = true;
            this.cbUpdateTrackNumber.Location = new System.Drawing.Point(8, 139);
            this.cbUpdateTrackNumber.Name = "cbUpdateTrackNumber";
            this.cbUpdateTrackNumber.Size = new System.Drawing.Size(94, 17);
            this.cbUpdateTrackNumber.TabIndex = 4;
            this.cbUpdateTrackNumber.Text = "Track Number";
            this.cbUpdateTrackNumber.UseVisualStyleBackColor = true;
            // 
            // cbUpdateTrackCount
            // 
            this.cbUpdateTrackCount.AutoSize = true;
            this.cbUpdateTrackCount.Location = new System.Drawing.Point(8, 115);
            this.cbUpdateTrackCount.Name = "cbUpdateTrackCount";
            this.cbUpdateTrackCount.Size = new System.Drawing.Size(85, 17);
            this.cbUpdateTrackCount.TabIndex = 3;
            this.cbUpdateTrackCount.Text = "Track Count";
            this.cbUpdateTrackCount.UseVisualStyleBackColor = true;
            // 
            // cbUpdateArtistName
            // 
            this.cbUpdateArtistName.AutoSize = true;
            this.cbUpdateArtistName.Checked = true;
            this.cbUpdateArtistName.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbUpdateArtistName.Location = new System.Drawing.Point(8, 67);
            this.cbUpdateArtistName.Name = "cbUpdateArtistName";
            this.cbUpdateArtistName.Size = new System.Drawing.Size(49, 17);
            this.cbUpdateArtistName.TabIndex = 2;
            this.cbUpdateArtistName.Text = "Artist";
            this.cbUpdateArtistName.UseVisualStyleBackColor = true;
            // 
            // cbUpdateCollectionName
            // 
            this.cbUpdateCollectionName.AutoSize = true;
            this.cbUpdateCollectionName.Checked = true;
            this.cbUpdateCollectionName.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbUpdateCollectionName.Location = new System.Drawing.Point(8, 43);
            this.cbUpdateCollectionName.Name = "cbUpdateCollectionName";
            this.cbUpdateCollectionName.Size = new System.Drawing.Size(55, 17);
            this.cbUpdateCollectionName.TabIndex = 1;
            this.cbUpdateCollectionName.Text = "Album";
            this.cbUpdateCollectionName.UseVisualStyleBackColor = true;
            // 
            // cbUpdateTrackName
            // 
            this.cbUpdateTrackName.AutoSize = true;
            this.cbUpdateTrackName.Checked = true;
            this.cbUpdateTrackName.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbUpdateTrackName.Location = new System.Drawing.Point(8, 19);
            this.cbUpdateTrackName.Name = "cbUpdateTrackName";
            this.cbUpdateTrackName.Size = new System.Drawing.Size(85, 17);
            this.cbUpdateTrackName.TabIndex = 0;
            this.cbUpdateTrackName.Text = "Track Name";
            this.cbUpdateTrackName.UseVisualStyleBackColor = true;
            // 
            // btnUpdateTracks
            // 
            this.btnUpdateTracks.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnUpdateTracks.Location = new System.Drawing.Point(147, 226);
            this.btnUpdateTracks.Name = "btnUpdateTracks";
            this.btnUpdateTracks.Size = new System.Drawing.Size(104, 23);
            this.btnUpdateTracks.TabIndex = 0;
            this.btnUpdateTracks.Text = "Update tracks";
            this.btnUpdateTracks.UseVisualStyleBackColor = true;
            this.btnUpdateTracks.Click += new System.EventHandler(this.btnUpdateTracks_Click);
            // 
            // lbCountries
            // 
            this.lbCountries.FormattingEnabled = true;
            this.lbCountries.Location = new System.Drawing.Point(3, 16);
            this.lbCountries.Name = "lbCountries";
            this.lbCountries.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lbCountries.Size = new System.Drawing.Size(119, 186);
            this.lbCountries.TabIndex = 2;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.lbCountries);
            this.groupBox2.Controls.Add(this.btnSelectAll);
            this.groupBox2.Location = new System.Drawing.Point(9, 10);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(131, 239);
            this.groupBox2.TabIndex = 3;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "iTunes Store Countries";
            // 
            // btnSelectAll
            // 
            this.btnSelectAll.Location = new System.Drawing.Point(3, 207);
            this.btnSelectAll.Name = "btnSelectAll";
            this.btnSelectAll.Size = new System.Drawing.Size(119, 23);
            this.btnSelectAll.TabIndex = 5;
            this.btnSelectAll.Text = "Select all";
            this.btnSelectAll.UseVisualStyleBackColor = true;
            this.btnSelectAll.Click += new System.EventHandler(this.btnSelectAll_Click);
            // 
            // tbLog
            // 
            this.tbLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbLog.Location = new System.Drawing.Point(3, 16);
            this.tbLog.Multiline = true;
            this.tbLog.Name = "tbLog";
            this.tbLog.ReadOnly = true;
            this.tbLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbLog.Size = new System.Drawing.Size(418, 193);
            this.tbLog.TabIndex = 4;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.tbLog);
            this.groupBox3.Location = new System.Drawing.Point(256, 10);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(424, 212);
            this.groupBox3.TabIndex = 6;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Log";
            // 
            // cbShowDebug
            // 
            this.cbShowDebug.AutoSize = true;
            this.cbShowDebug.Location = new System.Drawing.Point(541, 230);
            this.cbShowDebug.Name = "cbShowDebug";
            this.cbShowDebug.Size = new System.Drawing.Size(58, 17);
            this.cbShowDebug.TabIndex = 7;
            this.cbShowDebug.Text = "Debug";
            this.cbShowDebug.UseVisualStyleBackColor = true;
            // 
            // btnInfo
            // 
            this.btnInfo.Location = new System.Drawing.Point(605, 226);
            this.btnInfo.Name = "btnInfo";
            this.btnInfo.Size = new System.Drawing.Size(75, 23);
            this.btnInfo.TabIndex = 8;
            this.btnInfo.Text = "Info";
            this.btnInfo.UseVisualStyleBackColor = true;
            this.btnInfo.Click += new System.EventHandler(this.btnInfo_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(256, 226);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(279, 23);
            this.progressBar1.Step = 1;
            this.progressBar1.TabIndex = 9;
            // 
            // TagTracks
            // 
            this.AcceptButton = this.btnUpdateTracks;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(689, 254);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.btnInfo);
            this.Controls.Add(this.cbShowDebug);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnUpdateTracks);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TagTracks";
            this.Text = "iTunes Match Tagger";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox cbUpdatePrimaryGenreName;
        private System.Windows.Forms.CheckBox cbUpdateTrackNumber;
        private System.Windows.Forms.CheckBox cbUpdateTrackCount;
        private System.Windows.Forms.CheckBox cbUpdateArtistName;
        private System.Windows.Forms.CheckBox cbUpdateCollectionName;
        private System.Windows.Forms.CheckBox cbUpdateTrackName;
        private System.Windows.Forms.Button btnUpdateTracks;
        private System.Windows.Forms.CheckBox cbUpdateDiscNumber;
        private System.Windows.Forms.CheckBox cbUpdateDiscCount;
        private System.Windows.Forms.ListBox lbCountries;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox tbLog;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.CheckBox cbShowDebug;
        private System.Windows.Forms.Button btnInfo;
        private System.Windows.Forms.ProgressBar progressBar1;
    }
}

