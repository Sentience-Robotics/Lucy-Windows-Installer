using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Lucy_windows_installer
{
    public partial class MainWindow : Window
    {
        private const string REPO_URL = "https://github.com/Sentience-Robotics/lucy_ws.git";
        private const string INSTALL_FOLDER_NAME = "Lucy";
        private const string WINDOWS_LAUNCHER_URL = "https://github.com/Sentience-Robotics/Lucy-windows-launcher/releases/latest/download/Lucy.exe";

        private static readonly List<string> COMMANDS = new()
        {
            "python3 Lucy.py",
            "curl -L -o Lucy.exe https://github.com/Sentience-Robotics/Lucy-windows-launcher/releases/latest/download/Lucy.exe"
        };

        private int _pageIndex = 0;
        private readonly List<UIElement> _pages = new();
        private bool _isInstalling;
        private bool _installationCompleted;
        private string _pixiExecutable = "pixi";
        private string? _downloadedLauncherPath;

        public MainWindow()
        {
            InitializeComponent();

            InstallPathTextBox.Text = @"C:\Program Files\Lucy";

            // Register page order
            _pages.Add(PageWelcome);
            _pages.Add(PageLocation);
            _pages.Add(PageAdvanced);
            _pages.Add(PageProgress);

            PageAdvanced.Visibility = Visibility.Visible;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            UpdateSourceSelectionVisibility();
            UpdatePageVisibility();

            _ = PreLoadBranchesAsync();
            _ = PreLoadTagsAsync();
        }

        private async Task PreLoadBranchesAsync()
        {
            try
            {
                var branches = await GetGitHubBranchesAsync(REPO_URL);
                BranchesComboBox.ItemsSource = branches;

                var masterBranch = branches.FirstOrDefault(b => b.Name == "master");
                if (masterBranch != null)
                    BranchesComboBox.SelectedItem = masterBranch;
                else if (branches.Count > 0)
                    BranchesComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                AppendLog($"Warning: Could not pre-load branches: {ex.Message}");
            }
        }

        private async Task PreLoadTagsAsync()
        {
            try
            {
                var tags = await GetGitHubTagsAsync(REPO_URL);
                TagsComboBox.ItemsSource = tags;

                if (tags.Count > 0)
                    TagsComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                AppendLog($"Warning: Could not pre-load tags: {ex.Message}");
            }
        }

        #region Navigation
        private void UpdatePageVisibility()
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].Visibility = (i == _pageIndex) ? Visibility.Visible : Visibility.Collapsed;
            }

            BackButton.IsEnabled = _pageIndex > 0 && !_isInstalling;
            BackButton.Visibility = _pageIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Content = (_pageIndex == _pages.Count - 1) ? "Finish" : "Next";
            NextButton.IsEnabled = _pageIndex != _pages.Count - 1 || _installationCompleted;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pageIndex == _pages.Count - 1 && AdvancedConfigCheckbox.IsChecked != true)
            {
                _pageIndex = 1;
                UpdatePageVisibility();
                return;
            }

            if (_pageIndex == 2)
            {
                if (AdvancedConfigCheckbox.IsChecked == true)
                {
                    // Page 3 -> Page 2
                    _pageIndex = 1;
                }
                else
                {
                    // Page 3 -> Page 1
                    _pageIndex = 0;
                }

                UpdatePageVisibility();
                return;
            }

            if (_pageIndex > 0)
            {
                _pageIndex--;
                UpdatePageVisibility();
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isInstalling)
                return;

            if (_pageIndex == 1)
            {
                if (AdvancedConfigCheckbox.IsChecked == true)
                {
                    _pageIndex = 2;
                    UpdatePageVisibility();
                    return;
                }

                _pageIndex = _pages.Count - 1;
                UpdatePageVisibility();
                await RunCloneAndCommandsAsync();
                return;
            }

            if (_pageIndex < _pages.Count - 2)
            {
                _pageIndex++;
                UpdatePageVisibility();
            }
            else if (_pageIndex == _pages.Count - 2)
            {
                _pageIndex++;
                UpdatePageVisibility();
                await RunCloneAndCommandsAsync();
            }
            else
            {
                this.Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion

        #region Folder browse
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select install folder (it will contain the cloned repository)",
                UseDescriptionForTitle = true
            };
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                InstallPathTextBox.Text = dlg.SelectedPath;
            }
        }
        #endregion

        #region Advanced UI
        private async void LoadBranchesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadBranchesButton.IsEnabled = false;
            BranchesComboBox.ItemsSource = null;
            AppendLog($"Loading branches for {REPO_URL}...");

            try
            {
                var branches = await GetGitHubBranchesAsync(REPO_URL);
                BranchesComboBox.ItemsSource = branches;
                if (branches.Count > 0)
                    BranchesComboBox.SelectedIndex = 0;
                AppendLog($"Loaded {branches.Count} branches.");
            }
            catch (Exception ex)
            {
                AppendLog($"Error loading branches: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to load branches: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadBranchesButton.IsEnabled = true;
            }
        }

        private async void LoadTagsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTagsButton.IsEnabled = false;
            TagsComboBox.ItemsSource = null;
            AppendLog($"Loading release tags for {REPO_URL}...");

            try
            {
                var tags = await GetGitHubTagsAsync(REPO_URL);
                TagsComboBox.ItemsSource = tags;
                if (tags.Count > 0)
                    TagsComboBox.SelectedIndex = 0;
                AppendLog($"Loaded {tags.Count} release tags.");
            }
            catch (Exception ex)
            {
                AppendLog($"Error loading tags: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to load release tags: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadTagsButton.IsEnabled = true;
            }
        }

        private void SourceTypeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (BranchSelectionPanel is null || TagSelectionPanel is null)
                return;

            UpdateSourceSelectionVisibility();
        }

        private void UpdateSourceSelectionVisibility()
        {
            bool isTagSelected = ReleaseTagRadioButton.IsChecked == true;
            BranchSelectionPanel.Visibility = isTagSelected ? Visibility.Collapsed : Visibility.Visible;
            TagSelectionPanel.Visibility = isTagSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        private record BranchInfo(string Name);
        #endregion

        #region GitHub API
        private static (string owner, string repo) ParseOwnerRepo(string repoUrl)
        {
            // Expect formats like:
            // https://github.com/owner/repo
            // git@github.com:owner/repo.git
            // https://github.com/owner/repo.git
            if (string.IsNullOrWhiteSpace(repoUrl))
                throw new FormatException("Repository URL is empty.");

            string trimmed = repoUrl.Trim();

            if (trimmed.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
            {
                // git@github.com:owner/repo.git
                var parts = trimmed.Split(':', 2);
                if (parts.Length < 2) throw new FormatException("Invalid SSH URL");
                var path = parts[1].TrimEnd('/');
                if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    path = path.Substring(0, path.Length - 4);
                var segs = path.Split('/');
                if (segs.Length < 2) throw new FormatException("Invalid SSH URL path");
                return (segs[0], segs[1]);
            }
            else
            {
                if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                    throw new FormatException("Invalid URL");
                var segs = uri.AbsolutePath.Trim('/').Split('/');
                if (segs.Length < 2) throw new FormatException("URL must contain owner and repo");
                var owner = segs[0];
                var repo = segs[1];
                if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    repo = repo.Substring(0, repo.Length - 4);
                return (owner, repo);
            }
        }

        private static async Task<List<BranchInfo>> GetGitHubBranchesAsync(string repoUrl)
        {
            var (owner, repo) = ParseOwnerRepo(repoUrl);
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/branches";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Lucy-windows-installer");
            var resp = await client.GetAsync(apiUrl);
            resp.EnsureSuccessStatusCode();
            using var s = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);
            var list = new List<BranchInfo>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("name", out var nameProp))
                {
                    var n = nameProp.GetString();
                    if (!string.IsNullOrEmpty(n)) list.Add(new BranchInfo(n));
                }
            }
            return list;
        }

        private static async Task<List<BranchInfo>> GetGitHubTagsAsync(string repoUrl)
        {
            var (owner, repo) = ParseOwnerRepo(repoUrl);
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Lucy-windows-installer");

            var results = new Dictionary<string, BranchInfo>(StringComparer.OrdinalIgnoreCase);

            var releasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";
            var releasesResp = await client.GetAsync(releasesUrl);
            if (releasesResp.IsSuccessStatusCode)
            {
                using var releasesStream = await releasesResp.Content.ReadAsStreamAsync();
                using var releasesDoc = await JsonDocument.ParseAsync(releasesStream);
                foreach (var el in releasesDoc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("tag_name", out var tagProp))
                    {
                        var name = tagProp.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            results[name] = new BranchInfo(name);
                    }
                }
            }

            var tagsUrl = $"https://api.github.com/repos/{owner}/{repo}/tags";
            var tagsResp = await client.GetAsync(tagsUrl);
            if (tagsResp.IsSuccessStatusCode)
            {
                using var tagsStream = await tagsResp.Content.ReadAsStreamAsync();
                using var tagsDoc = await JsonDocument.ParseAsync(tagsStream);
                foreach (var el in tagsDoc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("name", out var tagProp))
                    {
                        var name = tagProp.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            results[name] = new BranchInfo(name);
                    }
                }
            }

            return results.Values.ToList();
        }
        #endregion

        #region Clone and run commands
        private async Task RunCloneAndCommandsAsync()
        {
            _isInstalling = true;
            _installationCompleted = false;
            UpdatePageVisibility();

            string selectedFolder = InstallPathTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(selectedFolder))
            {
                System.Windows.MessageBox.Show("Please select an install location.", "No location", MessageBoxButton.OK, MessageBoxImage.Warning);
                _pageIndex = 1; // go back to location
                _isInstalling = false;
                UpdatePageVisibility();
                return;
            }

            string targetFolder = Path.GetFileName(
                selectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Equals(INSTALL_FOLDER_NAME, StringComparison.OrdinalIgnoreCase)
                ? selectedFolder
                : Path.Combine(selectedFolder, INSTALL_FOLDER_NAME);

            InstallPathTextBox.Text = targetFolder;

            if (!await EnsureRequiredToolsAsync())
            {
                _isInstalling = false;
                UpdatePageVisibility();
                return;
            }

            if (COMMANDS.Count == 0)
            {
                System.Windows.MessageBox.Show("No commands configured.", "No commands", MessageBoxButton.OK, MessageBoxImage.Warning);
                _pageIndex = 2;
                _isInstalling = false;
                UpdatePageVisibility();
                return;
            }

            var useTag = ReleaseTagRadioButton.IsChecked == true;
            var selectedRef = useTag
                ? (TagsComboBox.SelectedItem as BranchInfo)?.Name
                : (BranchesComboBox.SelectedItem as BranchInfo)?.Name;

            if (string.IsNullOrWhiteSpace(selectedRef))
            {
                selectedRef = useTag ? "latest" : "master";
            }

            try
            {
                Directory.CreateDirectory(targetFolder);
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to create target folder: {ex.Message}");
                System.Windows.MessageBox.Show($"Cannot create target folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _isInstalling = false;
                UpdatePageVisibility();
                return;
            }

            ProgressBar.Value = 0;
            LogTextBox.Clear();

            var (owner, repo) = ParseOwnerRepo(REPO_URL);
            string cloneTarget = targetFolder;

            if (Directory.Exists(cloneTarget) && Directory.EnumerateFileSystemEntries(cloneTarget).Any())
            {
                var res = System.Windows.MessageBox.Show($"Target folder {cloneTarget} already exists and is not empty. Overwrite/replace?", "Target exists", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res != MessageBoxResult.Yes)
                {
                    AppendLog("User cancelled due to existing folder.");
                    _isInstalling = false;
                    UpdatePageVisibility();
                    return;
                }

                try
                {
                    DeleteDirectoryWithRetry(cloneTarget);
                    Directory.CreateDirectory(cloneTarget);
                }
                catch (Exception ex)
                {
                    AppendLog($"Failed to remove existing folder: {ex.Message}");
                    System.Windows.MessageBox.Show($"Cannot remove existing folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _isInstalling = false;
                    UpdatePageVisibility();
                    return;
                }
            }

            _pixiExecutable = await EnsurePixiInstalledAsync();
            if (string.IsNullOrEmpty(_pixiExecutable))
            {
                _isInstalling = false;
                UpdatePageVisibility();
                return;
            }

            if (useTag)
            {
                var archiveUrl = $"https://github.com/{owner}/{repo}/archive/refs/tags/{Uri.EscapeDataString(selectedRef)}.zip";
                var archivePath = Path.Combine(targetFolder, $"{repo}-{selectedRef}.zip");
                var extractionPath = Path.Combine(targetFolder, ".lucy-release-extract");
                AppendLog($"Downloading release tag {selectedRef}...");
                ProgressBar.Value = 10;

                try
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Lucy-windows-installer");
                    var bytes = await client.GetByteArrayAsync(archiveUrl);
                    File.WriteAllBytes(archivePath, bytes);
                    Directory.CreateDirectory(extractionPath);
                    ZipFile.ExtractToDirectory(archivePath, extractionPath);

                    var extractedDir = Directory.EnumerateDirectories(extractionPath).FirstOrDefault();
                    if (extractedDir is null)
                        throw new DirectoryNotFoundException("The release archive did not contain a project directory.");

                    foreach (var sourceFile in Directory.EnumerateFiles(extractedDir))
                        File.Move(sourceFile, Path.Combine(targetFolder, Path.GetFileName(sourceFile)));

                    foreach (var sourceDirectory in Directory.EnumerateDirectories(extractedDir))
                        Directory.Move(sourceDirectory, Path.Combine(targetFolder, Path.GetFileName(sourceDirectory)));

                    Directory.Delete(extractionPath, true);
                    File.Delete(archivePath);
                    cloneTarget = targetFolder;
                    ProgressBar.Value = 30;
                }
                catch (Exception ex)
                {
                    AppendLog($"Tag archive failed: {ex.Message}");
                    System.Windows.MessageBox.Show($"Failed to download or extract selected release tag: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _isInstalling = false;
                    UpdatePageVisibility();
                    return;
                }
            }
            else
            {
                AppendLog($"Starting clone: {REPO_URL}");
                ProgressBar.Value = 10;
                var cloneArgs = selectedRef is null ? $"clone \"{REPO_URL}\"" : $"clone --branch \"{selectedRef}\" \"{REPO_URL}\"";
                bool cloneSuccess = await RunProcessAsync("git", $"{cloneArgs} \"{cloneTarget}\"", targetFolder, output => AppendLog(output));

                if (!cloneSuccess)
                {
                    AppendLog("Clone failed. Aborting.");
                    System.Windows.MessageBox.Show("Git clone failed. See logs for details.", "Clone failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    _isInstalling = false;
                    UpdatePageVisibility();
                    return;
                }

                AppendLog("Clone completed.");
                ProgressBar.Value = 30;
            }

            if (!string.IsNullOrWhiteSpace(_downloadedLauncherPath) && File.Exists(_downloadedLauncherPath))
            {
                var launcherPath = Path.Combine(cloneTarget, "Lucy.exe");
                File.Copy(_downloadedLauncherPath, launcherPath, true);
                File.Delete(_downloadedLauncherPath);
                _downloadedLauncherPath = null;
                AppendLog($"Downloaded Windows launcher to {launcherPath}.");
            }

            // Run post-clone commands from inside the new repository
            int total = COMMANDS.Count;
            for (int i = 0; i < total; i++)
            {
                var cmd = COMMANDS[i].Trim();
                if (string.IsNullOrEmpty(cmd)) continue;
                string command = cmd.StartsWith("pixi ", StringComparison.OrdinalIgnoreCase)
                    ? $"\"{_pixiExecutable}\"{cmd[4..]}"
                    : cmd;
                string repoCommand = $"cd /d \"{cloneTarget}\" && {command}";
                ProgressBar.Value = 30 + (i * 70.0 / Math.Max(1, total));
                AppendLog($"> Executing: {cmd}");
                bool ok = await RunProcessAsync("cmd.exe", $"/c \"{repoCommand}\"", targetFolder, output => AppendLog(output));
                ProgressBar.Value = 30 + ((i + 1) * 70.0 / Math.Max(1, total));
                if (!ok)
                {
                    AppendLog($"Command failed: {cmd}");
                    var res = System.Windows.MessageBox.Show($"Command failed: {cmd}\nContinue with remaining commands?", "Command failed", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes)
                    {
                        AppendLog("Aborted by user after failed command.");
                        _isInstalling = false;
                        UpdatePageVisibility();
                        return;
                    }
                }
            }

            ProgressBar.Value = 100;
            AppendLog("All steps completed.");
            _isInstalling = false;
            _installationCompleted = true;
            UpdatePageVisibility();
            System.Windows.MessageBox.Show("Installation complete.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task<bool> EnsureRequiredToolsAsync()
        {
            var missing = new List<string>();

            AppendLog("Checking required tools...");

            if (!await IsPythonAvailableAsync())
                missing.Add("Python");
            if (!await IsBuildToolsInstalledAsync())
                missing.Add("Microsoft Visual Studio 2022 Build Tools");
            if (!await IsCommandAvailableAsync("pixi"))
                missing.Add("pixi");

            if (missing.Count == 0)
            {
                AppendLog("All required tools are available.");
                return true;
            }

            var missingText = string.Join(", ", missing);
            var result = System.Windows.MessageBox.Show(
                $"The following required tools are missing: {missingText}. Would you like the installer to install them automatically?",
                "Missing prerequisites",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                AppendLog("User declined installing missing prerequisites.");
                return false;
            }

            if (!await IsPythonAvailableAsync())
            {
                AppendLog("Installing Python...");
                bool pythonInstalled = await RunProcessAsync(
                    "winget",
                    "install Python.Python.3 -e --accept-source-agreements --accept-package-agreements",
                    Environment.CurrentDirectory,
                    AppendLog);
                if (!pythonInstalled)
                {
                    System.Windows.MessageBox.Show("Could not install Python. See the log for details.", "Python installation failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (OperatingSystem.IsWindows())
                {
                    _downloadedLauncherPath = Path.Combine(Path.GetTempPath(), $"Lucy-{Guid.NewGuid():N}.exe");
                    AppendLog("Downloading the latest Lucy Windows launcher...");
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Lucy-windows-installer");
                    try
                    {
                        using var response = await client.GetAsync(WINDOWS_LAUNCHER_URL);
                        response.EnsureSuccessStatusCode();
                        await using var source = await response.Content.ReadAsStreamAsync();
                        await using var destination = File.Create(_downloadedLauncherPath);
                        await source.CopyToAsync(destination);
                        AppendLog("Lucy Windows launcher download completed.");
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Lucy Windows launcher download failed: {ex.Message}");
                        return false;
                    }
                }
            }

            if (!await IsBuildToolsInstalledAsync())
            {
                AppendLog("Installing Visual Studio Build Tools...");
                bool buildToolsInstalled = await RunProcessAsync(
                    "winget",
                    "install Microsoft.VisualStudio.2022.BuildTools --override \"--quiet --wait --norestart --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64\" --accept-source-agreements --accept-package-agreements",
                    Environment.CurrentDirectory,
                    AppendLog);
                if (!buildToolsInstalled)
                {
                    System.Windows.MessageBox.Show("Could not install Visual Studio Build Tools. See the log for details.", "Build Tools installation failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            if (!await IsCommandAvailableAsync("pixi"))
            {
                AppendLog("Installing pixi...");
                bool pixiInstalled = await RunProcessAsync(
                    "powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"irm https://pixi.sh/install.ps1 | iex\"",
                    Environment.CurrentDirectory,
                    AppendLog);
                if (!pixiInstalled)
                {
                    System.Windows.MessageBox.Show("Could not install pixi. See the log for details.", "pixi installation failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            if (!await IsPythonAvailableAsync() || !await IsBuildToolsInstalledAsync() || !await IsCommandAvailableAsync("pixi"))
            {
                System.Windows.MessageBox.Show("One or more required tools are still unavailable after installation. Please check the log and try again.", "Prerequisites not ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private async Task<string> EnsurePixiInstalledAsync()
        {
            if (await IsCommandAvailableAsync("pixi"))
            {
                AppendLog("pixi is already installed.");
                return "pixi";
            }

            var result = System.Windows.MessageBox.Show(
                "pixi is not installed. Would you like the installer to install it automatically?",
                "pixi is required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                AppendLog("pixi installation was declined.");
                return string.Empty;
            }

            AppendLog("Installing pixi...");
            bool installed = await RunProcessAsync(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"irm https://pixi.sh/install.ps1 | iex\"",
                Environment.CurrentDirectory,
                AppendLog);
            if (!installed)
            {
                System.Windows.MessageBox.Show("Could not install pixi. See the log for details.", "pixi installation failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
            }

            if (await IsCommandAvailableAsync("pixi"))
                return "pixi";

            string installedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pixi", "bin", "pixi.exe");
            if (File.Exists(installedPath))
                return installedPath;

            System.Windows.MessageBox.Show("pixi was installed but could not be found. Restart the installer and try again.", "pixi not found", MessageBoxButton.OK, MessageBoxImage.Error);
            return string.Empty;
        }

        private static async Task<bool> IsPythonAvailableAsync()
        {
            return await IsCommandAvailableAsync("python") || await IsCommandAvailableAsync("py");
        }

        private static async Task<bool> IsBuildToolsInstalledAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = "list --id Microsoft.VisualStudio.2022.BuildTools --exact",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            SanitizeEnvironment(psi);
            using var process = Process.Start(psi);
            if (process is null)
                return false;
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 &&
                   (output.Contains("Microsoft.VisualStudio.2022.BuildTools", StringComparison.OrdinalIgnoreCase)
                    || error.Contains("Microsoft.VisualStudio.2022.BuildTools", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<bool> IsCommandAvailableAsync(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            SanitizeEnvironment(psi);
            using var process = Process.Start(psi);
            if (process is null)
                return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        private static void DeleteDirectoryWithRetry(string path)
        {
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        File.SetAttributes(file, FileAttributes.Normal);
                    foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                        File.SetAttributes(directory, FileAttributes.Directory);
                    Directory.Delete(path, true);
                    return;
                }
                catch when (attempt < maxAttempts)
                {
                    Thread.Sleep(500);
                }
            }
        }

        private async Task<bool> RunProcessAsync(string fileName, string arguments, string workingDirectory, Action<string> onOutput)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                SanitizeEnvironment(psi);
                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                bool started = proc.Start();
                if (!started)
                {
                    InvokeOnUi(() => onOutput("Failed to start process."));
                    return false;
                }

                Task outputTask = ReadProcessOutputAsync(proc.StandardOutput, onOutput);
                Task errorTask = ReadProcessOutputAsync(proc.StandardError, onOutput);
                await Task.WhenAll(proc.WaitForExitAsync(), outputTask, errorTask);

                InvokeOnUi(() => onOutput($"Process exited with code {proc.ExitCode}"));
                return proc.ExitCode == 0;
            }

            catch (Exception ex)
            {
                InvokeOnUi(() => onOutput($"Process error: {ex.Message}"));
                return false;
            }
        }

        private static void SanitizeEnvironment(ProcessStartInfo psi)
        {
            // Pull the freshest possible values, not the stale inherited ones
            string machinePath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "";
            string userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";

            string combined = machinePath + ";" + userPath;

            // Strip any stray newlines/carriage returns and empty/garbage entries
            var cleanEntries = combined
                .Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            psi.EnvironmentVariables["Path"] = string.Join(";", cleanEntries);
        }

        private async Task ReadProcessOutputAsync(StreamReader reader, Action<string> onOutput)
        {
            while (await reader.ReadLineAsync() is { } line)
                InvokeOnUi(() => onOutput(line));
        }
        #endregion

        private void AppendLog(string text)
        {
            InvokeOnUi(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now:HH:mm:ss} {text}\r\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void InvokeOnUi(Action a)
        {
            if (Dispatcher.CheckAccess()) a();
            else Dispatcher.Invoke(a);
        }
    }
}
