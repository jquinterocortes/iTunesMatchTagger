namespace iTunesMatchTagger
{
    partial class Info
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
            this.label1 = new System.Windows.Forms.Label();
            this.btnClose = new System.Windows.Forms.Button();
            this.LinkProjectUrl = new System.Windows.Forms.LinkLabel();
            this.label2 = new System.Windows.Forms.Label();
            this.LinkAuthorMail = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(4, 68);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(240, 70);
            this.label1.TabIndex = 0;
            this.label1.Text = "Legal notice:\r\niTunes is a trademark of Apple Inc.\r\niTunes Match Tagger is an ind" +
    "ependent application and has not been authorized, sponsored, or otherwise approv" +
    "ed by Apple Inc.";
            // 
            // btnClose
            // 
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Location = new System.Drawing.Point(82, 141);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 23);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // LinkProjectUrl
            // 
            this.LinkProjectUrl.AutoSize = true;
            this.LinkProjectUrl.Location = new System.Drawing.Point(4, 39);
            this.LinkProjectUrl.Name = "LinkProjectUrl";
            this.LinkProjectUrl.Size = new System.Drawing.Size(53, 13);
            this.LinkProjectUrl.TabIndex = 11;
            this.LinkProjectUrl.TabStop = true;
            this.LinkProjectUrl.Text = "ProjectUrl";
            this.LinkProjectUrl.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkClicked);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(4, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(219, 13);
            this.label2.TabIndex = 12;
            this.label2.Text = "iTunes Match Tagger by Martin Pietschmann\r\n";
            // 
            // LinkAuthorMail
            // 
            this.LinkAuthorMail.AutoSize = true;
            this.LinkAuthorMail.LinkBehavior = System.Windows.Forms.LinkBehavior.NeverUnderline;
            this.LinkAuthorMail.Location = new System.Drawing.Point(4, 24);
            this.LinkAuthorMail.Name = "LinkAuthorMail";
            this.LinkAuthorMail.Size = new System.Drawing.Size(57, 13);
            this.LinkAuthorMail.TabIndex = 13;
            this.LinkAuthorMail.TabStop = true;
            this.LinkAuthorMail.Text = "AuthorMail";
            this.LinkAuthorMail.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkClicked);
            // 
            // Info
            // 
            this.AcceptButton = this.btnClose;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.ClientSize = new System.Drawing.Size(238, 170);
            this.Controls.Add(this.LinkAuthorMail);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.LinkProjectUrl);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Info";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Info";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.LinkLabel LinkProjectUrl;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.LinkLabel LinkAuthorMail;
    }
}