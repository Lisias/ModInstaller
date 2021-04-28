using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
// ReSharper disable LocalizableElement

namespace ModInstaller
{
    public partial class ModManager
    {
        private void EnableApiClick(object sender, EventArgs e)
        {
            if (Manager.Instance.VanillaEnabled)
            {
                DialogResult result = MessageBox.Show
                (
                    "Do you want to disable the modding api/revert to vanilla?",
                    "Confirmation dialogue",
                    MessageBoxButtons.YesNo
                );
                if (result != DialogResult.Yes) return;

                if (Manager.Instance.SetVanillaApi())
                    MessageBox.Show("Successfully disabled all installed mods!");
                else
                {
                    MessageBox.Show("Unable to locate vanilla Hollow Knight.\nPlease verify integrity of game files and relaunch this installer.");
                    Application.Exit();
                    Close();
                }
            }
            else
            {
                DialogResult result = MessageBox.Show
                (
                    "Do you want to enable the Modding API?",
                    "Confirmation dialogue",
                    MessageBoxButtons.YesNo
                );
                if (result != DialogResult.Yes) return;

                if (Manager.Instance.SetCustomApi())
                    MessageBox.Show("Successfully enabled all installed mods!");
                else
                {
                    MessageBox.Show("Unable to locate vanilla Hollow Knight. Please verify integrity of game files.");
                }
            }

            button1.Text = Manager.Instance.VanillaEnabled
                ? "Enable Modding API"
                : "Revert Back To Unmodded";
        }

        private void ManualInstallClick(object sender, EventArgs e)
        {
            openFileDialog.Reset();
            openFileDialog.Filter = "Mod files|*.zip; *.dll|All files|*.*";
            openFileDialog.Multiselect = true;
            openFileDialog.Title = "Select the mods you wish to install";
            openFileDialog.ShowDialog();
        }

        private void ChangePathClick(object sender, EventArgs e)
        {
            folderBrowserDialog1.Reset();
            folderBrowserDialog1.ShowDialog();
            
            if (string.IsNullOrEmpty(folderBrowserDialog1.SelectedPath)) return;
            
            if (Manager.PathCheck(folderBrowserDialog1.SelectedPath))
            {
                Manager.Instance.SetInstallationPath(folderBrowserDialog1.SelectedPath);
                MessageBox.Show($"Hollow Knight installation path:\n{Properties.Settings.Default.installFolder}");
            }
            else
            {
                MessageBox.Show
                (
                    "Invalid path selected.\nPlease select the correct installation path for Hollow Knight."
                );
                ChangePathClick(new object(), EventArgs.Empty);
            }
        }
        
        private void DoManualInstall(object sender, EventArgs e)
        {
            if (openFileDialog.FileNames.Length < 1) return;
            foreach (string mod in openFileDialog.FileNames)
            {
                if (Path.GetExtension(mod) == ".zip")
                {
                    Manager.Instance.InstallMods(mod, Properties.Settings.Default.temp, true);
                }
                else
                {
                    File.Copy(mod, $"{Properties.Settings.Default.modFolder}/{Path.GetFileName(mod)}", true);
                }

                MessageBox.Show($"{Path.GetFileName(mod)} successfully installed!");
            }
        }

        private void ManualPathClosed(object sender, FormClosedEventArgs e)
        {
            Show();
            Manager.CheckTemporary();
        }

        private void DonateButtonClick(object sender, EventArgs e)
        {
            Process.Start
            (
                "https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=G5KYSS3ULQFY6&lc=US&item_name=HK%20ModInstaller&item_number=HKMI&currency_code=USD&bn=PP%2dDonationsBF%3abtn_donateCC_LG%2egif%3aNonHosted"
            );
        }

        private void SearchOnGotFocus(object sender, EventArgs e)
        {
            search.Text = "";
            search.ForeColor = Color.Black;
        }

        private void SearchOnLostFocus(object sender, EventArgs e)
        {
            if (search.Text == "")
            {
                search.Text = "Search...";
                search.ForeColor = Color.Gray;
            }
        }

        private void SearchOnKeyUp(object sender, KeyEventArgs e)
        {
            Controls.Remove(panel);
            FillPanel();
            ResizeUI();
        }
    }
}