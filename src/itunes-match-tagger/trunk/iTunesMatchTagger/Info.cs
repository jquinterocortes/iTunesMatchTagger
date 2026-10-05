using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace iTunesMatchTagger
{
    public partial class Info : Form
    {
        public Info()
        {
            InitializeComponent();

            LinkProjectUrl.Text = Settings.Default.ProjectUrl;
            LinkAuthorMail.Text = "mailto:" + Settings.Default.AuthorEmail;

            LinkProjectUrl.Links.Add(0, LinkProjectUrl.Text.Length, LinkProjectUrl.Text);
            LinkAuthorMail.Links.Add(0, LinkAuthorMail.Text.Length, LinkAuthorMail.Text);
        }

        private void LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(e.Link.LinkData.ToString());
            }
            catch (Exception) {}
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
