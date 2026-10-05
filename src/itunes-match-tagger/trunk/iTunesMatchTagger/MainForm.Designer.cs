namespace iTunesMatchTagger
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.lbCountries = new System.Windows.Forms.ListBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnSelectAll = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.gridTracks = new System.Windows.Forms.DataGridView();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.tbLog = new System.Windows.Forms.TextBox();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnLookupTracks = new System.Windows.Forms.Button();
            this.btnGetTracks = new System.Windows.Forms.Button();
            this.btnUpdateTracks = new System.Windows.Forms.Button();
            this.cbShowDebug = new System.Windows.Forms.CheckBox();
            this.btnInfo = new System.Windows.Forms.Button();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.gridUpdateOptions = new System.Windows.Forms.DataGridView();
            this.Attribute = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Update = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.Overwrite = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.OverwriteValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.OverwriteValueList = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.groupBox2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridTracks)).BeginInit();
            this.groupBox3.SuspendLayout();
            this.panel1.SuspendLayout();
            this.groupBox5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridUpdateOptions)).BeginInit();
            this.SuspendLayout();
            // 
            // lbCountries
            // 
            this.lbCountries.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lbCountries.FormattingEnabled = true;
            this.lbCountries.Location = new System.Drawing.Point(3, 16);
            this.lbCountries.Name = "lbCountries";
            this.lbCountries.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.lbCountries.Size = new System.Drawing.Size(128, 341);
            this.lbCountries.TabIndex = 2;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.lbCountries);
            this.groupBox2.Controls.Add(this.btnSelectAll);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox2.Location = new System.Drawing.Point(834, 148);
            this.groupBox2.Name = "groupBox2";
            this.tableLayoutPanel1.SetRowSpan(this.groupBox2, 2);
            this.groupBox2.Size = new System.Drawing.Size(134, 383);
            this.groupBox2.TabIndex = 3;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "iTunes Store Countries";
            // 
            // btnSelectAll
            // 
            this.btnSelectAll.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnSelectAll.Location = new System.Drawing.Point(3, 357);
            this.btnSelectAll.Name = "btnSelectAll";
            this.btnSelectAll.Size = new System.Drawing.Size(128, 23);
            this.btnSelectAll.TabIndex = 5;
            this.btnSelectAll.Text = "Select all countries";
            this.btnSelectAll.UseVisualStyleBackColor = true;
            this.btnSelectAll.Click += new System.EventHandler(this.btnSelectAll_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 393F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            this.tableLayoutPanel1.Controls.Add(this.groupBox4, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox3, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.groupBox2, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.panel1, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox5, 1, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 145F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 268F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(971, 534);
            this.tableLayoutPanel1.TabIndex = 10;
            // 
            // groupBox4
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.groupBox4, 2);
            this.groupBox4.Controls.Add(this.gridTracks);
            this.groupBox4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox4.Location = new System.Drawing.Point(3, 3);
            this.groupBox4.Name = "groupBox4";
            this.tableLayoutPanel1.SetRowSpan(this.groupBox4, 2);
            this.groupBox4.Size = new System.Drawing.Size(825, 260);
            this.groupBox4.TabIndex = 1;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Tracks";
            // 
            // gridTracks
            // 
            this.gridTracks.AllowUserToAddRows = false;
            this.gridTracks.AllowUserToDeleteRows = false;
            this.gridTracks.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridTracks.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridTracks.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridTracks.Location = new System.Drawing.Point(3, 16);
            this.gridTracks.Name = "gridTracks";
            this.gridTracks.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.gridTracks.Size = new System.Drawing.Size(819, 241);
            this.gridTracks.TabIndex = 7;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.tbLog);
            this.groupBox3.Controls.Add(this.progressBar1);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox3.Location = new System.Drawing.Point(3, 269);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(432, 262);
            this.groupBox3.TabIndex = 6;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Log";
            // 
            // tbLog
            // 
            this.tbLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbLog.Location = new System.Drawing.Point(3, 16);
            this.tbLog.Multiline = true;
            this.tbLog.Name = "tbLog";
            this.tbLog.ReadOnly = true;
            this.tbLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbLog.Size = new System.Drawing.Size(426, 223);
            this.tbLog.TabIndex = 4;
            // 
            // progressBar1
            // 
            this.progressBar1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.progressBar1.Location = new System.Drawing.Point(3, 239);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(426, 20);
            this.progressBar1.Step = 1;
            this.progressBar1.TabIndex = 9;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnLookupTracks);
            this.panel1.Controls.Add(this.btnGetTracks);
            this.panel1.Controls.Add(this.btnUpdateTracks);
            this.panel1.Controls.Add(this.cbShowDebug);
            this.panel1.Controls.Add(this.btnInfo);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(834, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(134, 139);
            this.panel1.TabIndex = 4;
            // 
            // btnLookupTracks
            // 
            this.btnLookupTracks.Location = new System.Drawing.Point(3, 32);
            this.btnLookupTracks.Name = "btnLookupTracks";
            this.btnLookupTracks.Size = new System.Drawing.Size(128, 23);
            this.btnLookupTracks.TabIndex = 10;
            this.btnLookupTracks.Text = "2. Lookup tracks";
            this.btnLookupTracks.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnLookupTracks.UseVisualStyleBackColor = true;
            this.btnLookupTracks.Click += new System.EventHandler(this.btnLookupTracks_Click);
            // 
            // btnGetTracks
            // 
            this.btnGetTracks.Location = new System.Drawing.Point(3, 3);
            this.btnGetTracks.Name = "btnGetTracks";
            this.btnGetTracks.Size = new System.Drawing.Size(128, 23);
            this.btnGetTracks.TabIndex = 10;
            this.btnGetTracks.Text = "1. Get selected tracks";
            this.btnGetTracks.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnGetTracks.UseVisualStyleBackColor = true;
            this.btnGetTracks.Click += new System.EventHandler(this.btnGetTracks_Click);
            // 
            // btnUpdateTracks
            // 
            this.btnUpdateTracks.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnUpdateTracks.Location = new System.Drawing.Point(3, 61);
            this.btnUpdateTracks.Name = "btnUpdateTracks";
            this.btnUpdateTracks.Size = new System.Drawing.Size(128, 23);
            this.btnUpdateTracks.TabIndex = 0;
            this.btnUpdateTracks.Text = "3. Update tracks";
            this.btnUpdateTracks.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnUpdateTracks.UseVisualStyleBackColor = true;
            this.btnUpdateTracks.Click += new System.EventHandler(this.btnUpdateTracks_Click);
            // 
            // cbShowDebug
            // 
            this.cbShowDebug.AutoSize = true;
            this.cbShowDebug.Location = new System.Drawing.Point(3, 90);
            this.cbShowDebug.Name = "cbShowDebug";
            this.cbShowDebug.Size = new System.Drawing.Size(58, 17);
            this.cbShowDebug.TabIndex = 7;
            this.cbShowDebug.Text = "Debug";
            this.cbShowDebug.UseVisualStyleBackColor = true;
            // 
            // btnInfo
            // 
            this.btnInfo.Location = new System.Drawing.Point(3, 113);
            this.btnInfo.Name = "btnInfo";
            this.btnInfo.Size = new System.Drawing.Size(128, 23);
            this.btnInfo.TabIndex = 8;
            this.btnInfo.Text = "Info";
            this.btnInfo.UseVisualStyleBackColor = true;
            this.btnInfo.Click += new System.EventHandler(this.btnInfo_Click);
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.gridUpdateOptions);
            this.groupBox5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox5.Location = new System.Drawing.Point(441, 269);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(387, 262);
            this.groupBox5.TabIndex = 8;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Update Options";
            // 
            // gridUpdateOptions
            // 
            this.gridUpdateOptions.AllowUserToAddRows = false;
            this.gridUpdateOptions.AllowUserToDeleteRows = false;
            this.gridUpdateOptions.AllowUserToResizeColumns = false;
            this.gridUpdateOptions.AllowUserToResizeRows = false;
            this.gridUpdateOptions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridUpdateOptions.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Attribute,
            this.Update,
            this.Overwrite,
            this.OverwriteValue,
            this.OverwriteValueList});
            this.gridUpdateOptions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridUpdateOptions.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridUpdateOptions.Location = new System.Drawing.Point(3, 16);
            this.gridUpdateOptions.MultiSelect = false;
            this.gridUpdateOptions.Name = "gridUpdateOptions";
            this.gridUpdateOptions.RowHeadersVisible = false;
            this.gridUpdateOptions.ShowRowErrors = false;
            this.gridUpdateOptions.Size = new System.Drawing.Size(381, 243);
            this.gridUpdateOptions.TabIndex = 7;
            this.gridUpdateOptions.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridUpdateOptions_CellValueChanged);
            this.gridUpdateOptions.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.gridUpdateOptions_DataError);
            this.gridUpdateOptions.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.gridUpdateOptions_EditingControlShowing);
            // 
            // Attribute
            // 
            this.Attribute.DataPropertyName = "HeaderText";
            this.Attribute.HeaderText = "Attribute";
            this.Attribute.Name = "Attribute";
            this.Attribute.ReadOnly = true;
            this.Attribute.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // Update
            // 
            this.Update.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.Update.DataPropertyName = "Update";
            this.Update.HeaderText = "Update";
            this.Update.Name = "Update";
            this.Update.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.Update.Width = 48;
            // 
            // Overwrite
            // 
            this.Overwrite.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.Overwrite.DataPropertyName = "Overwrite";
            this.Overwrite.HeaderText = "Overwrite";
            this.Overwrite.Name = "Overwrite";
            this.Overwrite.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.Overwrite.Width = 58;
            // 
            // OverwriteValue
            // 
            this.OverwriteValue.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.OverwriteValue.DataPropertyName = "OverwriteValue";
            this.OverwriteValue.HeaderText = "Overwrite value";
            this.OverwriteValue.Name = "OverwriteValue";
            this.OverwriteValue.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.OverwriteValue.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // OverwriteValueList
            // 
            this.OverwriteValueList.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.OverwriteValueList.DropDownWidth = 200;
            this.OverwriteValueList.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.OverwriteValueList.HeaderText = "";
            this.OverwriteValueList.MaxDropDownItems = 10;
            this.OverwriteValueList.MinimumWidth = 20;
            this.OverwriteValueList.Name = "OverwriteValueList";
            this.OverwriteValueList.Width = 20;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(971, 534);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "iTunes Match Tagger";
            this.groupBox2.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridTracks)).EndInit();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridUpdateOptions)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox lbCountries;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.DataGridView gridTracks;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.TextBox tbLog;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.DataGridView gridUpdateOptions;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnGetTracks;
        private System.Windows.Forms.Button btnUpdateTracks;
        private System.Windows.Forms.CheckBox cbShowDebug;
        private System.Windows.Forms.Button btnInfo;
        private System.Windows.Forms.Button btnLookupTracks;
        private System.Windows.Forms.DataGridViewTextBoxColumn Attribute;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Update;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Overwrite;
        private System.Windows.Forms.DataGridViewTextBoxColumn OverwriteValue;
        private System.Windows.Forms.DataGridViewComboBoxColumn OverwriteValueList;
    }
}

