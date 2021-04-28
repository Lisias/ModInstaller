using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using MIManager = ModInstaller.Manager;

// ReSharper disable LocalizableElement

namespace ModInstaller.FormsUI
{
    public partial class ModManager : Form, MIManager.InstallationPathListener, MIManager.InstallResultListener
    {
		private class ModField : MIManager.ModEntry
		{
			private Label name;
			public new Label Name
			{
				get { return name; }
				set
				{
					this.name = new Label();
					this.name.Text = base.Name = value.Name;
				}
			}

			public Button EnableButton { get; set; }
			public Button InstallButton { get; set; }
			public Button ReadmeButton { get; set; }
		}

        public bool IsOffline {get; internal set; }

        private List<Panel> _panelList = new List<Panel>();

        public ModManager()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
			try
			{
				Manager.Instance.CheckLocalInstallation(this);
    			this.CheckUpdate();
				Manager.Instance.PiracyCheck();
				if (Manager.Instance.CheckApiInstalled(this))
					MessageBox.Show("Modding API successfully installed!");
				PopulateList();
				FillPanel();
				ResizeUI();
				Text = "Mod Manager " + MIManager.Version + " by " + MIManager.Author;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "Warning", MessageBoxButtons.OK);
				Application.Exit();
				Close();
			}
        }
        
        #region Loading and building the mod manager

        private void CheckUpdate()
        {
            try
            {
            #if !DEBUG
                Manager.CheckUpdate();
            #endif
            }
            catch (Exception)
            {
                ConnectionFailedForm form = new ConnectionFailedForm(this);
                form.Closed += Form4_Closed;
                Hide();
                form.ShowDialog();
                Application.Exit();
            }
        }


        private void Form4_Closed(object sender, EventArgs e)
        {
            if (this.IsOffline) return;
            Manager.Instance.FillModsList();
        }

        private void PopulateList()
        {
            Manager.Instance.SearchInstalledFiles(this);

            foreach (MIManager.Mod mod in Manager.Instance.ModsSortedList)
            {
                if (Manager.Instance.AllMods.Contains(mod.Name)) 
                    continue;

                ModField entry = (ModField) Manager.Instance.ModEntries.Find(e => e.Name == mod.Name);
                panel.Controls.Add(entry.Name);
                panel.Controls.Add(entry.EnableButton);
                panel.Controls.Add(entry.InstallButton);
                panel.Controls.Add(entry.ReadmeButton);
            }

            const int space = 50;
            const int hgt = 10;

            foreach (MIManager.Mod mod in Manager.Instance.ModsSortedList)
            {
                ModField entry = (ModField) Manager.Instance.ModEntries.Find(e => e.Name == mod.Name);

				Panel panelEntry = new Panel();
                panelEntry.Size = new Size(450, 33);
                panelEntry.Controls.Add(entry.Name);
                panelEntry.Controls.Add(entry.EnableButton);
                panelEntry.Controls.Add(entry.InstallButton);
                panelEntry.Controls.Add(entry.ReadmeButton);
                
                entry.Name.Left = 20;
                entry.Name.Top = hgt+5;
                entry.Name.AutoSize = true;

                entry.EnableButton.Left = 6 + 150 + space;
                entry.EnableButton.Top = hgt;
                entry.EnableButton.Text = entry.IsEnabled ? "Disable" : "Enable";
                entry.EnableButton.Enabled = entry.IsInstalled;
                entry.EnableButton.Click += OnEnableButtonClick;

                entry.InstallButton.Left = 6 + 225 + space;
                entry.InstallButton.Top = hgt;
                entry.InstallButton.Text = entry.IsInstalled ? "Uninstall" : "Install";
                entry.InstallButton.Click += OnInstallButtonClick;

                entry.ReadmeButton.Left = 6 + 300 + space;
                entry.ReadmeButton.Top = hgt;
                entry.ReadmeButton.Text = "Readme";
                entry.ReadmeButton.Enabled = entry.IsInstalled;
                entry.ReadmeButton.Click += OnReadmeButtonClick;
                
                _panelList.Add(panelEntry);
            }

            button1.Text = Manager.Instance.VanillaEnabled
                ? "Enable Modding API"
                : "Revert Back To Unmodded";
        }

        private void FillPanel()
        {
            panel = new Panel();
            panel.AutoScroll = true;
            panel.Size = new Size(480, 1);
            Controls.Add(panel);

			int filtered = 0;

            string filter = "";
            if (search.Text != "Search...") filter = search.Text;

            if (string.Equals(filter, "Installed", StringComparison.InvariantCultureIgnoreCase))
            {
                for (int i = 0; i < _panelList.Count; i++)
                {
					Panel modPanel = _panelList[i];

                    if (Manager.Instance.ModEntries[i].IsInstalled)
                    {
                        panel.Controls.Add(modPanel);
                        modPanel.Location = new Point(0, modPanel.Height * filtered);
                        filtered++;
                    }
                }
            }
            
            else if (string.Equals(filter, "uninstalled", StringComparison.InvariantCultureIgnoreCase))
            {
                for (int i = 0; i < _panelList.Count; i++)
                {
					Panel modPanel = _panelList[i];

                    if (!Manager.Instance.ModEntries[i].IsInstalled)
                    {
                        panel.Controls.Add(modPanel);
                        modPanel.Location = new Point(0, modPanel.Height * filtered);
                        filtered++;
                    }
                }
            }
            
            else if (string.Equals(filter, "enabled", StringComparison.InvariantCultureIgnoreCase))
            {
                for (int i = 0; i < _panelList.Count; i++)
                {
					Panel modPanel = _panelList[i];

                    if (Manager.Instance.ModEntries[i].IsEnabled)
                    {
                        panel.Controls.Add(modPanel);
                        modPanel.Location = new Point(0, modPanel.Height * filtered);
                        filtered++;
                    }
                }
            }
            
            else if (string.Equals(filter, "disabled", StringComparison.InvariantCultureIgnoreCase))
            {
                for (int i = 0; i < _panelList.Count; i++)
                {
					Panel modPanel = _panelList[i];

                    if (Manager.Instance.ModEntries[i].IsInstalled && !Manager.Instance.ModEntries[i].IsEnabled)
                    {
                        panel.Controls.Add(modPanel);
                        modPanel.Location = new Point(0, modPanel.Height * filtered);
                        filtered++;
                    }
                }
            }
            
            else
            {
                for (int i = 0; i < _panelList.Count; i++)
                {
                    var modPanel = _panelList[i];

                    if (modPanel.Controls[0].Text.IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        panel.Controls.Add(modPanel);
                        modPanel.Location = new Point(0, modPanel.Height * filtered);
                        filtered++;
                    }
                }
            }
        }

        private void OnReadmeButtonClick(object sender, EventArgs e)
        {
			Button button = (Button) sender;
            ModField entry = (ModField) Manager.Instance.ModEntries.First(f => ((ModField) f).ReadmeButton == button);

            try
            {
                Manager.Instance.OpenReadMe(entry);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void OnInstallButtonClick(object sender, EventArgs e)
        {
            var button = (Button) sender;
            ModField entry = (ModField) Manager.Instance.ModEntries.First(f => ((ModField) f).InstallButton == button);
            MIManager.Mod mod = Manager.Instance.ModsList.First(m => m.Name == entry.Name.Text);
            string modname = mod.Name;

            if (entry.IsInstalled)
            {
                DialogResult result = MessageBox.Show
                (
                    $"Do you want to remove {modname} from your computer?",
                    "Confirm removal",
                    MessageBoxButtons.YesNo
                );

                if (result != DialogResult.Yes)
                    return;
                
                Manager.Instance.Uninstall(modname, entry, this);
            }
            else
            {
                if (Manager.Instance.InstalledMods.Contains(modname)) return;

                DialogResult result = MessageBox.Show
                (
                    $"Do you want to install {modname}?",
                    "Confirm installation",
                    MessageBoxButtons.YesNo
                );

                if (result != DialogResult.Yes) return;
                
                Manager.Instance.InstallDependencies(mod, this);

                if (mod.Optional.Any())
                {
                    foreach (string optional in mod.Optional)
                    {
                        if (Manager.Instance.InstalledMods.Contains(optional)) continue;

                        ModField optMod = (ModField) Manager.Instance.ModEntries.First(e => e.Name == optional);
                        
                        DialogResult depInstall = MessageBox.Show
                        (
                            $"The mod author suggests installing {optional} together with this mod.\nDo you want to install {optional}?",
                            "Confirm installation",
                            MessageBoxButtons.YesNo
                        );
                        
                        if (depInstall != DialogResult.Yes) 
                            continue;
                        
                        Manager.Instance.Install(optional, true, false, true, optMod, this);
                        
                        MessageBox.Show($"{optional} successfully installed!");
                    }
                }

                Manager.Instance.Install(modname, true, false, true, entry, this);
            }
        }

        private void OnEnableButtonClick(object sender, EventArgs e)
        {
            var button = (Button) sender;
            ModField entry = (ModField) Manager.Instance.ModEntries.First(f => ((ModField) f).EnableButton == button);
            MIManager.Mod mod = Manager.Instance.ModsList.First(m => m.Name == entry.Name.Text);
            string modname = mod.Name;

            if (entry.IsEnabled)
                Manager.Instance.Disable(entry);
            else
                Manager.Instance.Enable(entry);

            entry.IsEnabled = !entry.IsEnabled;
            entry.EnableButton.Text = entry.IsEnabled ? "Disable" : "Enable";
        }

        private bool CheckModUpdated(string filename, MIManager.Mod mod, bool isEnabled)
        {
            if (Manager.Instance.CheckModUpdated(filename, mod, isEnabled))
                return false;

            DialogResult update = MessageBox.Show
            (
                $"{mod.Name} is outdated. Would you like to update it?",
                "Outdated mod",
                MessageBoxButtons.YesNo
            );

            if (update != DialogResult.Yes) 
                return true;

            ModField updateMod = (ModField)Manager.Instance.ModEntries.First(upd => upd.Name == mod.Name);
            Manager.Instance.UpdateMod(mod, isEnabled, updateMod, this);

            return true;
        }

        private void ResizeUI()
        {
            const int height = 480;
            panel.Width = panel.Controls.Count > 0 ? panel.Controls[0].Width + 20 : 480;
            panel.Height = height;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            search.Size = new Size(panel.Width - 3, 50);
            button1.Size = new Size(panel.Width, 23);
            button2.Size = new Size(panel.Width, 23);
            button3.Size = new Size(panel.Width, 23);
            _button4.Size = new Size(panel.Width, 23);
            _browser.Size = new Size(panel.Width, 23);
            search.Top = height + 9;
            search.Left = 5;
            button1.Top = search.Bottom + 9;
            button1.Left = 3;
            button2.Top = button1.Bottom;
            button2.Left = 3;
            button3.Top = button2.Bottom;
            button3.Left = 3;
            _button4.Top = button3.Bottom;
            _button4.Left = 3;
            panel.PerformLayout();
            PerformAutoScale();
        }

		#endregion

		bool MIManager.InstallationPathListener.IsInstallationPath(string path)
		{
			DialogResult dialogResult = MessageBox.Show
			(
				"Is this your Hollow Knight installation path?\n" + path,
				"Path confirmation",
				MessageBoxButtons.YesNo
			);

			return (dialogResult != DialogResult.Yes);
		}

		void MIManager.InstallationPathListener.SetInstallationPathManually()
		{
			ManualPathLocation form = new ManualPathLocation();
			form.FormClosed += ManualPathClosed;
			form.ShowDialog();
		}

		void MIManager.InstallResultListener.InstalledWithSuccess(string modname)
		{
			MessageBox.Show($"{modname} successfully installed!");
		}

		void MIManager.InstallResultListener.UpdatedWithSuccess(string modname)
		{
			MessageBox.Show($"{modname} successfully updated!");
		}

		void MIManager.InstallResultListener.UninstalledWithSuccess(string modname)
		{
			MessageBox.Show($"{modname} successfully uninstalled!");
		}

		void MIManager.InstallResultListener.Update(MIManager.ModEntry e)
		{
			ModField entry = (ModField)e;
			entry.InstallButton.Text = entry.IsInstalled ? "Uninstall" : "Install";
			entry.EnableButton.Enabled = entry.IsInstalled;
			entry.EnableButton.Text = entry.IsInstalled ? "Disable" : "Enable";
			entry.ReadmeButton.Enabled = entry.IsInstalled;
		}

		MIManager.ModEntry MIManager.InstallResultListener.Register(MIManager.ModEntry e)
		{
			ModField entry = new ModField
			{
				Name = new Label(),
				EnableButton = new Button(),
				InstallButton = new Button(),
				ReadmeButton = new Button(),
				IsInstalled = e.IsInstalled,
				IsEnabled = e.IsEnabled
			};
			entry.Name.Text = e.Name;
			return entry;
		}

		void MIManager.InstallResultListener.Download(Uri uri, string path, string name)
		{
			DownloadHelper download = new DownloadHelper(uri, path, name);
			download.ShowDialog();
		}
	}
}
