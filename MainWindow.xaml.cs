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

        private static readonly List<string> COMMANDS = new()
        {
            "pixi install",
            "pixi run build"
        };

        private int _pageIndex = 0;
        private readonly List<UIElement> _pages = new();

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

            BackButton.IsEnabled = _pageIndex > 0;
            BackButton.Visibility = _pageIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            NextButton.IsEnabled = true;
            NextButton.Content = (_pageIndex == _pages.Count - 1) ? "Finish" : "Next";
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
            string selectedFolder = InstallPathTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(selectedFolder))
            {
                System.Windows.MessageBox.Show("Please select an install location.", "No location", MessageBoxButton.OK, MessageBoxImage.Warning);
                _pageIndex = 1; // go back to location
                UpdatePageVisibility();
                return;
            }

            string targetFolder = Path.GetFileName(
                selectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Equals(INSTALL_FOLDER_NAME, StringComparison.OrdinalIgnoreCase)
                ? selectedFolder
                : Path.Combine(selectedFolder, INSTALL_FOLDER_NAME);

            InstallPathTextBox.Text = targetFolder;

            if (COMMANDS.Count == 0)
            {
                System.Windows.MessageBox.Show("No commands configured.", "No commands", MessageBoxButton.OK, MessageBoxImage.Warning);
                _pageIndex = 2;
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
                    return;
                }

                try
                {
                    Directory.Delete(cloneTarget, true);
                    Directory.CreateDirectory(cloneTarget);
                }
                catch (Exception ex)
                {
                    AppendLog($"Failed to remove existing folder: {ex.Message}");
                    System.Windows.MessageBox.Show($"Cannot remove existing folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            if (useTag)
            {
                var archiveUrl = $"https://github.com/{owner}/{repo}/archive/refs/tags/{Uri.EscapeDataString(selectedRef)}.zip";
                var archivePath = Path.Combine(targetFolder, $"{repo}-{selectedRef}.zip");
                var extractionPath = Path.Combine(targetFolder, ".lucy-release-extract");
                AppendLog($"Downloading release tag {selectedRef}...");

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
                }
                catch (Exception ex)
                {
                    AppendLog($"Tag archive failed: {ex.Message}");
                    System.Windows.MessageBox.Show($"Failed to download or extract selected release tag: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                AppendLog($"Starting clone: {REPO_URL}");
                var cloneArgs = selectedRef is null ? $"clone \"{REPO_URL}\"" : $"clone --branch \"{selectedRef}\" \"{REPO_URL}\"";
                bool cloneSuccess = await RunProcessAsync("git", $"{cloneArgs} \"{cloneTarget}\"", targetFolder, output => AppendLog(output));

                if (!cloneSuccess)
                {
                    AppendLog("Clone failed. Aborting.");
                    System.Windows.MessageBox.Show("Git clone failed. See logs for details.", "Clone failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AppendLog("Clone completed.");
            }

            // Run post-clone commands from inside the new repository
            int total = COMMANDS.Count;
            for (int i = 0; i < total; i++)
            {
                var cmd = COMMANDS[i].Trim();
                if (string.IsNullOrEmpty(cmd)) continue;
                string repoCommand = $"cd /d \"{cloneTarget}\" && {cmd}";
                ProgressBar.Value = (i * 100.0 / Math.Max(1, total));
                AppendLog($"> Executing: {cmd}");
                bool ok = await RunProcessAsync("cmd.exe", $"/c \"{repoCommand}\"", targetFolder, output => AppendLog(output));
                if (!ok)
                {
                    AppendLog($"Command failed: {cmd}");
                    var res = System.Windows.MessageBox.Show($"Command failed: {cmd}\nContinue with remaining commands?", "Command failed", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes)
                    {
                        AppendLog("Aborted by user after failed command.");
                        return;
                    }
                }
            }

            ProgressBar.Value = 100;
            AppendLog("All steps completed.");
            System.Windows.MessageBox.Show("Installation complete.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
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

                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                proc.OutputDataReceived += (s, e) => { if (e.Data != null) InvokeOnUi(() => onOutput(e.Data)); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) InvokeOnUi(() => onOutput(e.Data)); };

                bool started = proc.Start();
                if (!started)
                {
                    InvokeOnUi(() => onOutput("Failed to start process."));
                    return false;
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                await Task.Run(() => proc.WaitForExit());

                InvokeOnUi(() => onOutput($"Process exited with code {proc.ExitCode}"));
                return proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                InvokeOnUi(() => onOutput($"Process error: {ex.Message}"));
                return false;
            }
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
