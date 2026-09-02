using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AowEmailWrapper.Helpers;
using AowEmailWrapper.Localization;

namespace AowEmailWrapper.Controls
{
    /// <summary>
    /// Small modal dialog that downloads an update's installer with a progress bar and a Cancel
    /// button. Returns the downloaded file's path, or null when cancelled or failed.
    /// </summary>
    public class UpdateForm : Form
    {
        private const string DownloadingKey = "msgWrapperUpdateDownloading";
        private const string DownloadFailedKey = "msgWrapperUpdateDownloadFailed";
        private const string CancelKey = "buttonCancel";
        private const string TitleKey = "msgWrapperUpdateAvailable";
        private const int Pad = 16;

        private readonly UpdateInfo _update;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly Label _label;
        private readonly ProgressBar _progress;
        private readonly Button _cancelButton;
        private string _result;
        private bool _finished;

        public static string Download(IWin32Window owner, UpdateInfo update)
        {
            using (UpdateForm form = new UpdateForm(update))
            {
                form.ShowDialog(owner);
                return form._result;
            }
        }

        private UpdateForm(UpdateInfo update)
        {
            _update = update;

            Text = Translator.Translate(TitleKey);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 120);

            _label = new Label();
            _label.AutoEllipsis = true;
            _label.Location = new Point(Pad, Pad);
            _label.Size = new Size(ClientSize.Width - Pad * 2, 20);
            _label.Text = Translator.Translate(DownloadingKey, update.AssetName ?? update.Describe());
            Controls.Add(_label);

            _progress = new ProgressBar();
            _progress.Location = new Point(Pad, _label.Bottom + 8);
            _progress.Size = new Size(ClientSize.Width - Pad * 2, 22);
            _progress.Style = update.Size > 0 ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            _progress.Maximum = 1000;
            Controls.Add(_progress);

            _cancelButton = new Button();
            _cancelButton.Text = Translator.Translate(CancelKey);
            _cancelButton.Size = new Size(90, 26);
            _cancelButton.Location = new Point(ClientSize.Width - Pad - _cancelButton.Width, _progress.Bottom + 12);
            _cancelButton.Click += (sender, e) => _cancel.Cancel();
            Controls.Add(_cancelButton);
            CancelButton = _cancelButton;

            Shown += UpdateForm_Shown;
            FormClosing += UpdateForm_FormClosing;
        }

        private async void UpdateForm_Shown(object sender, EventArgs e)
        {
            Progress<long> progress = new Progress<long>(ReportProgress);
            try
            {
                _result = await UpdateHelper.DownloadAsync(_update, progress, _cancel.Token);
            }
            catch (OperationCanceledException)
            {
                _result = null;
            }
            catch (Exception ex)
            {
                _result = null;
                Trace.TraceError("Update download failed: {0}", ex);
                MessageBox.Show(this, Translator.Translate(DownloadFailedKey, ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _finished = true;
            Close();
        }

        private void UpdateForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_finished)
            {
                //Let the download task observe the cancellation and close the form itself
                e.Cancel = true;
                _cancel.Cancel();
            }
        }

        private void ReportProgress(long received)
        {
            if (_update.Size > 0)
            {
                int value = (int)Math.Min(_progress.Maximum, received * _progress.Maximum / _update.Size);
                _progress.Value = value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancel.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
