using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PcGuardian;

public enum RiskLevel
{
    Info,
    Low,
    Medium,
    High
}

public sealed record SecurityFinding(
    RiskLevel Risk,
    string Category,
    string Title,
    string Details,
    string Location);

public sealed record ScanSummary(
    DateTime StartedAt,
    DateTime FinishedAt,
    int FilesChecked,
    int ProcessesChecked,
    int StartupItemsChecked,
    long TempBytes,
    IReadOnlyList<SecurityFinding> Findings)
{
    public int HighCount => Findings.Count(f => f.Risk == RiskLevel.High);
    public int MediumCount => Findings.Count(f => f.Risk == RiskLevel.Medium);
    public int LowCount => Findings.Count(f => f.Risk == RiskLevel.Low);
}

public sealed class SecurityScanner
{
    private static readonly string[] SuspiciousExtensions =
    [
        ".bat", ".cmd", ".ps1", ".vbs", ".jse", ".scr", ".com", ".pif", ".hta", ".exe"
    ];

    private static readonly string[] SuspiciousTokens =
    [
        "crack", "keygen", "trojan", "stealer", "miner", "dropper", "payload",
        "ransom", "backdoor", "rootkit"
    ];

    private static readonly string[] DevelopmentCodeFolders =
    [
        @"\node_modules\", @"\site-packages\", @"\venv\", @"\.venv\",
        @"\npm-cache\", @"\resources\app\node_modules\", @"\documents\github\"
    ];

    private readonly List<SecurityFinding> _findings = [];
    private readonly IProgress<string> _log;
    private readonly IProgress<int> _progress;

    public SecurityScanner(IProgress<string> log, IProgress<int> progress)
    {
        _log = log;
        _progress = progress;
    }

    public Task<ScanSummary> RunQuickScanAsync(CancellationToken token)
    {
        return Task.Run(() => RunScan(includeUserFolders: false, token), token);
    }

    public Task<ScanSummary> RunDeepScanAsync(CancellationToken token)
    {
        return Task.Run(() => RunScan(includeUserFolders: true, token), token);
    }

    public static string BuildReport(ScanSummary summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("PC Guardian - отчет анализа компьютера");
        builder.AppendLine($"Начало: {summary.StartedAt:dd.MM.yyyy HH:mm:ss}");
        builder.AppendLine($"Завершение: {summary.FinishedAt:dd.MM.yyyy HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine($"Проверено файлов: {summary.FilesChecked}");
        builder.AppendLine($"Проверено процессов: {summary.ProcessesChecked}");
        builder.AppendLine($"Элементов автозагрузки: {summary.StartupItemsChecked}");
        builder.AppendLine($"Размер временных файлов: {FormatBytes(summary.TempBytes)}");
        builder.AppendLine($"Риски: высокий={summary.HighCount}, средний={summary.MediumCount}, низкий={summary.LowCount}");
        builder.AppendLine();

        if (summary.Findings.Count == 0)
        {
            builder.AppendLine("Критичных замечаний не найдено.");
            return builder.ToString();
        }

        foreach (var finding in summary.Findings)
        {
            builder.AppendLine($"[{finding.Risk}] {finding.Category}: {finding.Title}");
            builder.AppendLine($"Описание: {finding.Details}");
            builder.AppendLine($"Расположение: {finding.Location}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static string FormatBytes(long bytes)
    {
        string[] sizes = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        double value = bytes;
        var index = 0;

        while (value >= 1024 && index < sizes.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value:0.##} {sizes[index]}";
    }

    private ScanSummary RunScan(bool includeUserFolders, CancellationToken token)
    {
        _findings.Clear();
        var startedAt = DateTime.Now;

        _progress.Report(4);
        _log.Report("Сбор общей информации о системе...");
        CheckSystemDrive();

        _progress.Report(14);
        _log.Report("Проверка параметров Windows Defender...");
        CheckDefenderSettings();

        _progress.Report(28);
        _log.Report("Анализ запущенных процессов...");
        var processesChecked = CheckProcesses(token);

        _progress.Report(42);
        _log.Report("Проверка автозагрузки...");
        var startupItemsChecked = CheckStartupItems(token);

        _progress.Report(58);
        _log.Report("Анализ временных и пользовательских папок...");
        var tempBytes = EstimateTempSize(token);
        var filesChecked = 0;
        var directories = GetScanDirectories(includeUserFolders);

        for (var i = 0; i < directories.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            filesChecked += CheckDirectory(directories[i], token);
            _progress.Report(Math.Min(94, 58 + (int)Math.Round((i + 1) * 36d / directories.Count)));
        }

        if (tempBytes > 2L * 1024 * 1024 * 1024)
        {
            _findings.Add(new SecurityFinding(
                RiskLevel.Low,
                "Очистка",
                "Временные файлы занимают больше 2 ГБ",
                $"Найдено примерно {FormatBytes(tempBytes)} во временных папках. Это не вирус, но может замедлять систему.",
                Path.GetTempPath()));
        }

        _progress.Report(100);
        _log.Report("Сканирование завершено.");

        return new ScanSummary(
            startedAt,
            DateTime.Now,
            filesChecked,
            processesChecked,
            startupItemsChecked,
            tempBytes,
            _findings
                .GroupBy(f => $"{f.Risk}|{f.Category}|{f.Title}|{f.Location}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(f => f.Risk)
                .ThenBy(f => f.Category)
                .ToList());
    }

    private void CheckSystemDrive()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.Equals(root, StringComparison.OrdinalIgnoreCase));
        if (drive is not null && drive.AvailableFreeSpace < 5L * 1024 * 1024 * 1024)
        {
            _findings.Add(new SecurityFinding(
                RiskLevel.Low,
                "Система",
                "Мало свободного места на системном диске",
                $"На диске {drive.Name} свободно {FormatBytes(drive.AvailableFreeSpace)}.",
                drive.Name));
        }
    }

    private void CheckDefenderSettings()
    {
        try
        {
            using var policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender");
            if (policyKey?.GetValue("DisableAntiSpyware") is int disabled && disabled == 1)
            {
                _findings.Add(new SecurityFinding(
                    RiskLevel.High,
                    "Защита",
                    "Windows Defender отключен политикой",
                    "В реестре найдена политика DisableAntiSpyware=1.",
                    @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender"));
            }

            using var serviceKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\WinDefend");
            if (serviceKey?.GetValue("Start") is int start && start == 4)
            {
                _findings.Add(new SecurityFinding(
                    RiskLevel.High,
                    "Защита",
                    "Служба Windows Defender отключена",
                    "Служба WinDefend имеет тип запуска Disabled.",
                    @"HKLM\SYSTEM\CurrentControlSet\Services\WinDefend"));
            }
        }
        catch (Exception ex)
        {
            _findings.Add(new SecurityFinding(RiskLevel.Info, "Защита", "Не удалось прочитать часть параметров Defender", ex.Message, "Реестр Windows"));
        }
    }

    private int CheckProcesses(CancellationToken token)
    {
        var count = 0;
        foreach (var process in Process.GetProcesses().OrderBy(p => p.ProcessName))
        {
            token.ThrowIfCancellationRequested();
            count++;

            var path = SafeProcessPath(process);
            if (IsKnownSafeFile(path))
            {
                continue;
            }

            if (HasSuspiciousToken(process.ProcessName))
            {
                _findings.Add(new SecurityFinding(
                    RiskLevel.Medium,
                    "Процессы",
                    "Подозрительное имя процесса",
                    $"Процесс называется {process.ProcessName}.",
                    path));
            }
            else if (LooksLikeUserWritableExecutable(path) && !IsTrustedSignedFile(path))
            {
                _findings.Add(new SecurityFinding(
                    RiskLevel.Medium,
                    "Процессы",
                    "Процесс запущен из временной или пользовательской папки",
                    "Исполняемые файлы из Temp, Downloads или AppData чаще требуют ручной проверки.",
                    path));
            }
        }

        return count;
    }

    private int CheckStartupItems(CancellationToken token)
    {
        var count = 0;
        count += CheckStartupRegistry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run", token);
        count += CheckStartupRegistry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM Run", token);
        count += CheckStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), token);
        count += CheckStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), token);
        return count;
    }

    private int CheckStartupRegistry(RegistryKey root, string subKey, string label, CancellationToken token)
    {
        var count = 0;
        try
        {
            using var key = root.OpenSubKey(subKey);
            if (key is null)
            {
                return 0;
            }

            foreach (var valueName in key.GetValueNames())
            {
                token.ThrowIfCancellationRequested();
                count++;
                CheckStartupCommand(label, valueName, Convert.ToString(key.GetValue(valueName)) ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            _findings.Add(new SecurityFinding(RiskLevel.Info, "Автозагрузка", $"Не удалось прочитать {label}", ex.Message, subKey));
        }

        return count;
    }

    private int CheckStartupFolder(string folder, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return 0;
        }

        var count = 0;
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            token.ThrowIfCancellationRequested();
            count++;
            CheckStartupCommand("Startup folder", Path.GetFileNameWithoutExtension(file), file);
        }

        return count;
    }

    private void CheckStartupCommand(string source, string name, string command)
    {
        if (IsKnownSafeFile(command))
        {
            return;
        }

        var path = ExtractExecutablePath(command);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".ini" or ".lnk" or ".url")
        {
            return;
        }

        if (HasSuspiciousToken(name) || HasSuspiciousToken(Path.GetFileNameWithoutExtension(path)))
        {
            _findings.Add(new SecurityFinding(
                RiskLevel.High,
                "Автозагрузка",
                $"Подозрительный элемент автозагрузки: {name}",
                $"Источник: {source}. Команда запуска требует проверки.",
                command));
        }
        else if (IsHighRiskLocation(command) && !IsTrustedSignedFile(path))
        {
            _findings.Add(new SecurityFinding(
                RiskLevel.Medium,
                "Автозагрузка",
                $"Элемент автозагрузки из пользовательской папки: {name}",
                $"Источник: {source}.",
                command));
        }
    }

    private int CheckDirectory(string directory, CancellationToken token)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var checkedFiles = 0;
        var skippedFolders = 0;
        var folders = new Stack<string>();
        folders.Push(directory);

        while (folders.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var current = folders.Pop();

            try
            {
                foreach (var child in Directory.EnumerateDirectories(current))
                {
                    folders.Push(child);
                }
            }
            catch
            {
                skippedFolders++;
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    token.ThrowIfCancellationRequested();
                    checkedFiles++;
                    CheckFile(file);
                    if (checkedFiles % 500 == 0)
                    {
                        _log.Report($"Проверено файлов: {checkedFiles} в {directory}");
                    }
                }
            }
            catch
            {
                skippedFolders++;
            }
        }

        if (skippedFolders > 0)
        {
            _log.Report($"Пропущено защищенных папок: {skippedFolders} в {directory}");
        }

        return checkedFiles;
    }

    private void CheckFile(string file)
    {
        if (IsKnownSafeFile(file) || IsDevelopmentCodeFile(file))
        {
            return;
        }

        var extension = Path.GetExtension(file).ToLowerInvariant();
        if (!SuspiciousExtensions.Contains(extension))
        {
            return;
        }

        var name = Path.GetFileNameWithoutExtension(file);
        var inRiskFolder = IsHighRiskLocation(file);
        var suspiciousName = HasSuspiciousToken(name);

        if (suspiciousName && inRiskFolder)
        {
            _findings.Add(new SecurityFinding(
                RiskLevel.High,
                "Файлы",
                "Файл с подозрительным названием",
                "Название похоже на вредоносный инструмент. Проверьте происхождение файла.",
                file));
        }
        else if (inRiskFolder && extension is not ".js" and not ".jse" && !IsTrustedSignedFile(file))
        {
            var info = new FileInfo(file);
            var risk = info.CreationTime > DateTime.Now.AddDays(-7) ? RiskLevel.Medium : RiskLevel.Low;
            _findings.Add(new SecurityFinding(
                risk,
                "Файлы",
                "Исполняемый или скриптовый файл в пользовательской папке",
                $"Расширение {extension} в Temp/Downloads/AppData.",
                file));
        }
    }

    private static List<string> GetScanDirectories(bool includeUserFolders)
    {
        var directories = new List<string>
        {
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
        };

        if (includeUserFolders)
        {
            directories.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            directories.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            directories.Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }

        return directories.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static long EstimateTempSize(CancellationToken token)
    {
        return GetDirectorySize(Path.GetTempPath(), token);
    }

    private static long GetDirectorySize(string directory, CancellationToken token)
    {
        long size = 0;
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var folders = new Stack<string>();
        folders.Push(directory);

        while (folders.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var current = folders.Pop();
            try
            {
                foreach (var child in Directory.EnumerateDirectories(current))
                {
                    folders.Push(child);
                }

                foreach (var file in Directory.EnumerateFiles(current))
                {
                    try
                    {
                        size += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Temporary files may disappear while scanning.
                    }
                }
            }
            catch
            {
                // Skip protected folders.
            }
        }

        return size;
    }

    private static string SafeProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? process.ProcessName;
        }
        catch
        {
            return process.ProcessName;
        }
    }

    private static bool LooksLikeUserWritableExecutable(string value)
    {
        var lower = value.ToLowerInvariant();
        return IsHighRiskLocation(lower) || lower.Contains(@"\appdata\roaming\");
    }

    private static bool IsHighRiskLocation(string value)
    {
        var lower = value.ToLowerInvariant();
        return lower.Contains(@"\appdata\local\temp\") || lower.Contains(@"\temp\") || lower.Contains(@"\downloads\");
    }

    private static bool IsDevelopmentCodeFile(string file)
    {
        var lower = file.ToLowerInvariant();
        if (!DevelopmentCodeFolders.Any(lower.Contains))
        {
            return false;
        }

        return Path.GetExtension(file).Equals(".js", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(file).Equals(".jse", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSuspiciousToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = value.ToLowerInvariant().Split([' ', '.', '_', '-', '(', ')', '[', ']', '{', '}', '+'], StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(token => SuspiciousTokens.Contains(token));
    }

    private static bool IsKnownSafeFile(string value)
    {
        var path = ExtractExecutablePath(value);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        var lower = path.ToLowerInvariant();
        return Path.GetFileName(path).Equals("VsJustInTimeDebuggerRegistrationHelper.exe", StringComparison.OrdinalIgnoreCase)
            && lower.Contains(@"\microsoft.visualstudio.debugger.justintime.")
            && IsSignedByPublisher(path, "Microsoft Corporation");
    }

    private static bool IsTrustedSignedFile(string value)
    {
        var path = ExtractExecutablePath(value);
        return File.Exists(path)
            && (IsSignedByPublisher(path, "Microsoft Corporation")
                || IsSignedByPublisher(path, "Discord Inc.")
                || IsSignedByPublisher(path, "Valve Corp.")
                || IsSignedByPublisher(path, "GitHub, Inc."));
    }

    private static string ExtractExecutablePath(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            return closingQuote > 1 ? trimmed[1..closingQuote] : trimmed.Trim('"');
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeIndex >= 0 ? trimmed[..(exeIndex + 4)] : trimmed;
    }

    private static bool IsSignedByPublisher(string file, string publisher)
    {
        try
        {
#pragma warning disable SYSLIB0057
            using var certificate = X509Certificate2.CreateFromSignedFile(file);
#pragma warning restore SYSLIB0057
            return certificate.Subject.Contains($"O={publisher}", StringComparison.OrdinalIgnoreCase)
                || certificate.Subject.Contains($"CN={publisher}", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
