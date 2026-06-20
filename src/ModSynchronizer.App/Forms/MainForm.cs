using ModSynchronizer.Core.Models;
using ModSynchronizer.Core.Services;
using ModSynchronizer.App.Services;

namespace ModSynchronizer.App.Forms;

public sealed class MainForm : Form
{
    private readonly Label _titleLabel;
    private readonly ComboBox _profileComboBox;
    private readonly Button _refreshButton;
    private readonly Button _runButton;
    private readonly Button _clearCacheButton;
    private readonly ProgressBar _progressBar;
    private readonly Label _statusLabel;
    private readonly TabControl _tabs;
    private readonly ProfileCatalogService _profileCatalogService;
    private readonly SetupRunner _setupRunner;
    private readonly SelfUpdateService _selfUpdateService;
    private readonly AppRuntimeServices _runtimeServices;

    public MainForm()
        : this(AppRuntimeServicesFactory.Create())
    {
    }

    internal MainForm(AppRuntimeServices runtimeServices)
    {
        _runtimeServices = runtimeServices;
        Text = "ModSynchronizer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 320);
        _selfUpdateService = runtimeServices.SelfUpdateService;
        _profileCatalogService = runtimeServices.ProfileCatalogService;
        _setupRunner = runtimeServices.SetupRunner;

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill
        };

        var setupTabPage = new TabPage("セットアップ");
        var maintenanceTabPage = new TabPage("メンテナンス");

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 3,
            RowCount = 4
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _titleLabel = new Label
        {
            AutoSize = true,
            Text = _profileCatalogService.HasFixedProfile
                ? "この構成でセットアップを実行します。"
                : "構成ファイルを選択してセットアップを実行します。"
        };

        _profileComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        _refreshButton = new Button
        {
            AutoSize = true,
            Text = "再読込"
        };
        _refreshButton.Click += async (_, _) => await LoadProfilesAsync();

        _runButton = new Button
        {
            AutoSize = true,
            Text = "同期して起動"
        };
        _runButton.Click += async (_, _) => await RunSetupAsync();

        _clearCacheButton = new Button
        {
            AutoSize = true,
            Text = "旧キャッシュ削除"
        };
        _clearCacheButton.Click += async (_, _) => await ClearCachesAsync();

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            Text = "待機中です。"
        };

        rootLayout.Controls.Add(_titleLabel, 0, 0);
        rootLayout.SetColumnSpan(_titleLabel, 3);
        rootLayout.Controls.Add(_profileComboBox, 0, 1);
        rootLayout.Controls.Add(_refreshButton, 1, 1);
        rootLayout.Controls.Add(_runButton, 2, 1);
        rootLayout.Controls.Add(_progressBar, 0, 2);
        rootLayout.SetColumnSpan(_progressBar, 3);
        rootLayout.Controls.Add(_statusLabel, 0, 3);
        rootLayout.SetColumnSpan(_statusLabel, 3);

        setupTabPage.Controls.Add(rootLayout);

        var maintenanceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 3
        };
        maintenanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        maintenanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        maintenanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        maintenanceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var maintenanceLabel = new Label
        {
            AutoSize = true,
            Text = "古いバージョンの ModSynchronizer のキャッシュを削除します。"
        };

        maintenanceLayout.Controls.Add(maintenanceLabel, 0, 0);
        maintenanceLayout.Controls.Add(_clearCacheButton, 0, 1);
        maintenanceTabPage.Controls.Add(maintenanceLayout);

        _tabs.TabPages.Add(setupTabPage);
        _tabs.TabPages.Add(maintenanceTabPage);

        Controls.Add(_tabs);

        Shown += async (_, _) => await LoadProfilesAsync();
        FormClosed += (_, _) => _runtimeServices.Dispose();
    }

    private async Task LoadProfilesAsync()
    {
        _refreshButton.Enabled = false;
        _runButton.Enabled = false;
        _statusLabel.Text = "構成ファイルを確認しています。";

        _profileComboBox.BeginUpdate();
        _profileComboBox.Items.Clear();

        try
        {
            var entries = await _profileCatalogService.LoadAvailableProfilesAsync(CancellationToken.None);
            foreach (var entry in entries)
            {
                _profileComboBox.Items.Add(new ProfileItem(entry.ProfileName, entry.DisplayName, entry.Path, entry.IsRemote, entry.WarningMessage));
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "構成ファイルの取得に失敗しました。";
            MessageBox.Show(this, ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _profileComboBox.EndUpdate();
            _refreshButton.Enabled = true;
        }

        if (_profileComboBox.Items.Count > 0)
        {
            _profileComboBox.SelectedIndex = 0;
            _statusLabel.Text = _profileCatalogService.HasFixedProfile
                ? "セットアップを実行できます。"
                : "構成ファイルを選択できます。";
            _runButton.Enabled = true;
            _profileComboBox.Enabled = !_profileCatalogService.HasFixedProfile;

            if (_profileComboBox.SelectedItem is ProfileItem selectedItem && !string.IsNullOrWhiteSpace(selectedItem.WarningMessage))
            {
                _statusLabel.Text = "キャッシュを使って構成ファイルを読み込みました。";
                MessageBox.Show(this, selectedItem.WarningMessage, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else
        {
            _runButton.Enabled = false;
            _profileComboBox.Enabled = false;
            _statusLabel.Text = "利用可能な構成ファイルがありません。";
        }
    }

    private async Task RunSetupAsync()
    {
        if (_profileComboBox.SelectedItem is not ProfileItem item)
        {
            MessageBox.Show(this, "構成ファイルを選択してください。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _runButton.Enabled = false;
        _refreshButton.Enabled = false;
        _clearCacheButton.Enabled = false;
        _tabs.Enabled = false;
        UseWaitCursor = true;
        _progressBar.Value = 0;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _statusLabel.Text = "セットアップを開始しています。";

        try
        {
            var resolvedItem = await _profileCatalogService.RefreshAsync(
                new ProfileCatalogEntry(item.ProfileName, item.DisplayName, item.Path, item.IsRemote, item.WarningMessage),
                CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(resolvedItem.WarningMessage))
            {
                MessageBox.Show(this, resolvedItem.WarningMessage, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            var profile = _setupRunner.LoadProfile(resolvedItem.Path);
            _statusLabel.Text = "自己更新を確認しています。";
            var relaunchArguments = $"--mode setup-and-launch --profile {QuoteCommandLineValue(item.ProfileName)}";
            var selfUpdateResult = await _selfUpdateService.CheckAndApplyAsync(
                profile,
                CancellationToken.None,
                relaunchAfterUpdate: true,
                relaunchArgumentsOverride: relaunchArguments);
            if (!string.IsNullOrWhiteSpace(selfUpdateResult.WarningMessage))
            {
                _statusLabel.Text = "自己更新を確認できなかったため、現在の版で続行します。";
            }

            if (selfUpdateResult.UpdateScheduled)
            {
                _statusLabel.Text = "アプリを更新しています。";
                MessageBox.Show(
                    this,
                    $"新しい版 {selfUpdateResult.LatestVersion} を適用します。更新後にセットアップを続行します。",
                    "更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                BeginInvoke(new Action(Close));
                return;
            }

            var progress = new Progress<SetupProgress>(UpdateProgress);
            _progressBar.Style = ProgressBarStyle.Continuous;
            var result = await _setupRunner.RunAsync(resolvedItem.Path, progress, CancellationToken.None);
            if (result.OfficialLauncherLaunchSucceeded)
            {
                _statusLabel.Text = "Minecraft ランチャーを起動しました。";
                Close();
                return;
            }

            _statusLabel.Text = "セットアップが完了しました。";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "セットアップに失敗しました。";
            MessageBox.Show(this, ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _tabs.Enabled = true;
            _runButton.Enabled = true;
            _refreshButton.Enabled = true;
            _clearCacheButton.Enabled = true;
            if (_progressBar.Style == ProgressBarStyle.Marquee)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
            }
        }
    }

    private async Task ClearCachesAsync()
    {
        _clearCacheButton.Enabled = false;

        try
        {
            var result = _profileCatalogService.ClearLegacyProfileCache();
            var message = string.Join(
                Environment.NewLine,
                [
                    $"削除済み: {result.DeletedDirectories.Count}",
                    $"未存在: {result.MissingDirectories.Count}",
                    "",
                    $"旧キャッシュ1: {ProfileCatalogService.GetVeryLegacyCacheDirectoryPath()}",
                    $"旧キャッシュ: {ProfileCatalogService.GetLegacyCacheDirectoryPath()}"
                ]);

            MessageBox.Show(this, message, "旧キャッシュ削除", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadProfilesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _clearCacheButton.Enabled = true;
        }
    }

    private void UpdateProgress(SetupProgress progress)
    {
        _statusLabel.Text = progress.Message;

        if (progress.Total <= 0)
        {
            _progressBar.Value = 0;
            return;
        }

        var value = (int)Math.Round(progress.Current * 100d / progress.Total);
        _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, value));
    }

    private static string QuoteCommandLineValue(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private sealed record ProfileItem(
        string ProfileName,
        string DisplayName,
        string Path,
        bool IsRemote,
        string? WarningMessage)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
