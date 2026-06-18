using System.Diagnostics;

namespace ModSynchronizer.App.Forms;

public sealed class ProxySyncProgressForm : Form
{
    private readonly Process _process;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Label _messageLabel;
    private readonly Label _profileLabel;
    private readonly ProgressBar _progressBar;
    private readonly string _defaultProfileText;

    public ProxySyncProgressForm(Process process, string profileName)
    {
        _process = process;

        Text = "ModSynchronizer";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 120);

        _messageLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 18),
            Size = new Size(340, 24),
            Text = "起動準備中です。"
        };

        _profileLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 44),
            Size = new Size(340, 20),
            Text = $"構成を同期しています: {profileName}"
        };
        _defaultProfileText = _profileLabel.Text;

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 76),
            Size = new Size(340, 20),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 25
        };

        Controls.Add(_messageLabel);
        Controls.Add(_profileLabel);
        Controls.Add(_progressBar);

        _timer = new System.Windows.Forms.Timer
        {
            Interval = 200
        };
        _timer.Tick += HandleTimerTick;
        Shown += HandleShown;
        FormClosed += HandleFormClosed;
    }

    private void HandleShown(object? sender, EventArgs e)
    {
        if (_process.HasExited)
        {
            Close();
            return;
        }

        _timer.Start();
    }

    private void HandleTimerTick(object? sender, EventArgs e)
    {
        if (_process.HasExited)
        {
            Close();
        }
    }

    private void HandleFormClosed(object? sender, FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
    }

    public void UpdateProgress(string message, int current, int total)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateProgress(message, current, total));
            return;
        }

        _messageLabel.Text = "起動準備中です。";
        _profileLabel.Text = string.IsNullOrWhiteSpace(message) ? _defaultProfileText : message;

        if (total > 0)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = total;
            _progressBar.Value = Math.Max(0, Math.Min(current, total));
        }
        else
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
            _progressBar.MarqueeAnimationSpeed = 25;
        }
    }
}
