using System;
using System.Windows.Forms;

namespace ModInstaller.FormsUI
{
    public partial class DownloadHelper : Form, ModInstaller.DownloadProgressListener
    {
        public DownloadHelper()
        {
            InitializeComponent();
        }

        public DownloadHelper(Uri uri, string path, string modname)
        {
            _modname = modname;
            InitializeComponent();
        }

		void ModInstaller.DownloadProgressListener.Progress(long bytesReceived, long totalBytesToReceive, long elapsedMilliseconds)
		{
			labelModname.Text = $"Downloading {_modname}";

			// Calculate download speed and output it to labelSpeed.
			labelSpeed.Text = $"{(bytesReceived / 1024d / elapsedMilliseconds / 1000).ToString("0.00")} kb/s";

			// Update the progressbar percentage only when the value is not the same.
			progressBar.Value = (int)(bytesReceived / totalBytesToReceive);

			// Update the label with how much data have been downloaded so far and the total size of the file we are currently downloading
			labelDownloaded.Text = $"{(bytesReceived / 1024d / 1024d).ToString("0.00")} MB / {(totalBytesToReceive / 1024d / 1024d).ToString("0.00")} MB";
		}

		void ModInstaller.DownloadProgressListener.Completed(long sizeInBytes, long timeInMilliseconds)
		{
			MessageBox.Show("Download completed!");
			Close();
		}

		void ModInstaller.DownloadProgressListener.Aborted(Exception e)
		{
			MessageBox.Show("Download has been aborted due {0}.", e.Message);
			Close();
		}

		void ModInstaller.DownloadProgressListener.Cancelled()
		{
			MessageBox.Show("Download has been canceled.");
			Close();
		}

        private readonly string _modname;
    }
}
