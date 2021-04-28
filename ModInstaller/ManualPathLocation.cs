using System;
using System.Windows.Forms;

// ReSharper disable LocalizableElement
// ReSharper disable BuiltInTypeReferenceStyle

namespace ModInstaller
{
    public partial class ManualPathLocation : Form
    {
        public ManualPathLocation()
        {
            InitializeComponent();
        }

        public ManualPathLocation(string os)
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.Reset();
            folderBrowserDialog1.ShowDialog();
            
            if (!string.IsNullOrEmpty(folderBrowserDialog1.SelectedPath))
            {
                if (Manager.PathCheck(folderBrowserDialog1.SelectedPath))
                {
                    Manager.Instance.SetInstallationPath(folderBrowserDialog1.SelectedPath);
                    MessageBox.Show($"Hollow Knight installation path:\n{Properties.Settings.Default.installFolder}");
                    Close();
                }
                else
                {
                    MessageBox.Show("Invalid path selected.\nPlease select the correct installation path for Hollow Knight.");
                    button1_Click(null, EventArgs.Empty);
                }
            }
            else
                MessageBox.Show("Please select your installation folder to proceed.");
        }
   }
}