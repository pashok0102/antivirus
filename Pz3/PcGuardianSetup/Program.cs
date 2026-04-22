using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace PcGuardianSetup;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length >= 2 && args[0].Equals("--install", StringComparison.OrdinalIgnoreCase))
        {
            InstallCore.Install(args[1], silent: false);
            return;
        }

        if (args.Length >= 2 && args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
        {
            if (!AdminTools.IsRunningAsAdministrator())
            {
                AdminTools.RequestElevated("--uninstall", args[1]);
                return;
            }

            InstallCore.Uninstall(args[1]);
            MessageBox.Show("PC Guardian was removed.", "PC Guardian Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new SetupForm());
    }
}

internal sealed class SetupForm : Form
{
    private const int BcmSetShield = 0x0000160C;
    private readonly TextBox _pathTextBox = new();
    private readonly Button _installButton = new();
    private readonly Button _browseButton = new();
    private readonly Button _closeButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();

    public SetupForm()
    {
        Text = "PC Guardian Setup";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(700, 470);
        BackColor = Color.FromArgb(239, 244, 249);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Shield;
        BuildUi();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!AdminTools.IsRunningAsAdministrator())
        {
            SendMessage(_installButton.Handle, BcmSetShield, IntPtr.Zero, new IntPtr(1));
        }
    }

    private void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Color.FromArgb(17, 26, 44) };
        Controls.Add(header);
        header.Controls.Add(new PictureBox { Image = (Icon ?? SystemIcons.Shield).ToBitmap(), SizeMode = PictureBoxSizeMode.StretchImage, Size = new Size(72, 72), Location = new Point(34, 36) });
        header.Controls.Add(new Label { AutoSize = true, Text = "PC Guardian", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 25F), Location = new Point(128, 34) });
        header.Controls.Add(new Label { AutoSize = true, Text = "Security desktop analyzer setup", ForeColor = Color.FromArgb(205, 216, 232), Font = new Font("Segoe UI", 10.5F), Location = new Point(132, 86) });
        header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 4, BackColor = Color.FromArgb(24, 196, 132) });

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(34, 26, 34, 10), BackColor = BackColor };
        Controls.Add(body);
        body.Controls.Add(new Label
        {
            AutoSize = false,
            Text = "Choose where PC Guardian should be installed. When you click Install, Windows will ask for administrator permission.",
            ForeColor = Color.FromArgb(41, 53, 72),
            Font = new Font("Segoe UI", 10.5F),
            Location = new Point(34, 24),
            Size = new Size(630, 54)
        });

        var card = new Panel { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Location = new Point(34, 92), Size = new Size(630, 118) };
        body.Controls.Add(card);
        card.Controls.Add(new Label { AutoSize = true, Text = "Install location", ForeColor = Color.FromArgb(39, 51, 70), Font = new Font("Segoe UI Semibold", 10F), Location = new Point(18, 16) });
        _pathTextBox.Location = new Point(18, 50);
        _pathTextBox.Size = new Size(456, 27);
        _pathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PC Guardian");
        card.Controls.Add(_pathTextBox);
        _browseButton.Text = "Browse...";
        _browseButton.Location = new Point(492, 48);
        _browseButton.Size = new Size(112, 31);
        _browseButton.FlatStyle = FlatStyle.System;
        _browseButton.Click += (_, _) => BrowseInstallFolder();
        card.Controls.Add(_browseButton);

        _progressBar.Location = new Point(34, 232);
        _progressBar.Size = new Size(630, 18);
        body.Controls.Add(_progressBar);
        _statusLabel.Text = "Ready. Administrator permission will be requested when you click Install.";
        _statusLabel.Location = new Point(34, 260);
        _statusLabel.Size = new Size(630, 28);
        _statusLabel.ForeColor = Color.FromArgb(73, 85, 105);
        body.Controls.Add(_statusLabel);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 82, BackColor = Color.FromArgb(231, 237, 244) };
        Controls.Add(footer);
        _installButton.Text = "Install";
        _installButton.Location = new Point(410, 20);
        _installButton.Size = new Size(120, 38);
        _installButton.FlatStyle = FlatStyle.System;
        _installButton.Click += (_, _) => Install();
        footer.Controls.Add(_installButton);
        _closeButton.Text = "Close";
        _closeButton.Location = new Point(544, 20);
        _closeButton.Size = new Size(120, 38);
        _closeButton.FlatStyle = FlatStyle.System;
        _closeButton.Click += (_, _) => Close();
        footer.Controls.Add(_closeButton);
    }

    private void BrowseInstallFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose PC Guardian install folder",
            SelectedPath = _pathTextBox.Text,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Install()
    {
        try
        {
            var installDir = _pathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(installDir))
            {
                MessageBox.Show(this, "Choose an install folder.", "PC Guardian Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!AdminTools.IsRunningAsAdministrator())
            {
                _statusLabel.Text = "Requesting administrator permission...";
                AdminTools.RequestElevated("--install", installDir);
                Close();
                return;
            }

            _installButton.Enabled = false;
            _browseButton.Enabled = false;
            _pathTextBox.Enabled = false;
            _progressBar.Value = 15;
            _statusLabel.Text = "Installing...";
            InstallCore.Install(installDir, silent: true);
            _progressBar.Value = 100;
            _statusLabel.Text = "Installed successfully.";
            MessageBox.Show(this, "PC Guardian installed successfully.", "PC Guardian Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _installButton.Enabled = true;
            _statusLabel.Text = "Installation failed.";
            MessageBox.Show(this, ex.Message, "Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}

internal static class AdminTools
{
    public static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RequestElevated(string command, string installDir)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            Arguments = $"{command} \"{installDir.Replace("\"", "\\\"")}\"",
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };
        System.Diagnostics.Process.Start(startInfo);
    }
}

internal static class InstallCore
{
    private const string MarkerFile = ".pcguardian-install";
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PC Guardian";

    public static void Install(string installDir, bool silent)
    {
        Directory.CreateDirectory(installDir);
        var installedExe = Path.Combine(installDir, "PcGuardian.exe");
        CloseRunningApplication(installedExe, askUser: !silent);
        ExtractPayload(installedExe);
        var uninstallerPath = CopyUninstaller(installDir);
        File.WriteAllText(Path.Combine(installDir, MarkerFile), "PC Guardian installer marker");
        CreateShortcuts(installedExe, installDir);
        RegisterUninstallEntry(installedExe, installDir, uninstallerPath);
    }

    public static void Uninstall(string installDir)
    {
        var installedExe = Path.Combine(installDir, "PcGuardian.exe");
        CloseRunningApplication(installedExe, askUser: false);
        RemoveShortcuts();
        RemoveUninstallEntry();
        DeleteFileIfExists(installedExe);
        DeleteFileIfExists(Path.Combine(installDir, MarkerFile));
        DeleteFileIfExists(Path.Combine(installDir, "Uninstall.exe"));
        TryRemoveDirectoryOnlyIfEmpty(installDir);
    }

    private static void ExtractPayload(string targetPath)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.PcGuardian.exe")
            ?? throw new InvalidOperationException("Payload.PcGuardian.exe was not found in setup.");
        using var file = File.Create(targetPath);
        stream.CopyTo(file);
    }

    private static string CopyUninstaller(string installDir)
    {
        var uninstallerPath = Path.Combine(installDir, "Uninstall.exe");
        File.Copy(Application.ExecutablePath, uninstallerPath, overwrite: true);
        return uninstallerPath;
    }

    private static void RegisterUninstallEntry(string installedExe, string installDir, string uninstallerPath)
    {
        using var key = Registry.LocalMachine.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", "PC Guardian");
        key.SetValue("DisplayVersion", "1.0.0");
        key.SetValue("Publisher", "PC Guardian");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", installedExe);
        key.SetValue("UninstallString", $"\"{uninstallerPath}\" --uninstall \"{installDir}\"");
        key.SetValue("QuietUninstallString", $"\"{uninstallerPath}\" --uninstall \"{installDir}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        if (File.Exists(installedExe))
        {
            key.SetValue("EstimatedSize", Math.Max(1, (int)(new FileInfo(installedExe).Length / 1024)), RegistryValueKind.DWord);
        }
    }

    private static void RemoveUninstallEntry()
    {
        Registry.LocalMachine.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
    }

    private static void CreateShortcuts(string targetPath, string workingDirectory)
    {
        RemoveShortcuts();
        var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "PC Guardian.lnk");
        var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "PC Guardian");
        var startMenuShortcut = Path.Combine(startMenuDir, "PC Guardian.lnk");
        Directory.CreateDirectory(startMenuDir);
        CreateShortcut(desktopShortcut, targetPath, workingDirectory);
        CreateShortcut(startMenuShortcut, targetPath, workingDirectory);
    }

    private static void RemoveShortcuts()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        foreach (var path in new[]
        {
            Path.Combine(desktop, "PC Guardian.lnk"),
            Path.Combine(desktop, "PcGuardian.lnk"),
            Path.Combine(desktop, "PCGuardian.lnk"),
            Path.Combine(programs, "PC Guardian", "PC Guardian.lnk"),
            Path.Combine(programs, "PC Guardian", "PcGuardian.lnk"),
            Path.Combine(programs, "PC Guardian", "PCGuardian.lnk")
        })
        {
            DeleteFileIfExists(path);
        }

        var startMenuDir = Path.Combine(programs, "PC Guardian");
        TryRemoveDirectoryOnlyIfEmpty(startMenuDir);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is not available.");
        dynamic shell = Activator.CreateInstance(shellType) ?? throw new InvalidOperationException("Could not create WScript.Shell.");
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.Description = "PC Guardian";
        shortcut.IconLocation = targetPath;
        shortcut.Save();
    }

    private static void CloseRunningApplication(string installedExe, bool askUser)
    {
        var runningProcesses = System.Diagnostics.Process.GetProcessesByName("PcGuardian").ToList();
        if (runningProcesses.Count == 0)
        {
            return;
        }

        if (askUser)
        {
            var answer = MessageBox.Show("PC Guardian is currently running. Close it automatically to continue?", "PC Guardian is running", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                throw new InvalidOperationException("Close PC Guardian and run setup again.");
            }
        }

        foreach (var process in runningProcesses)
        {
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(4000))
                {
                    process.Kill();
                    process.WaitForExit(4000);
                }
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private static void TryRemoveDirectoryOnlyIfEmpty(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path, recursive: false);
            }
        }
        catch
        {
            // Never remove non-empty or protected folders.
        }
    }
}
