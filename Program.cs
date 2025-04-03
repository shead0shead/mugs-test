using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
    IEnumerable<string> Aliases { get; }
    string Author { get; }
    string Version { get; }
    string? UsageExample { get; }
    Task ExecuteAsync(string[] args);
}

public static class AppSettings
{
    private const string SettingsFile = "settings.json";
    private static Settings _currentSettings;

    public class Settings
    {
        public string Language { get; set; } = "en";
    }

    public static void Initialize()
    {
        if (File.Exists(SettingsFile))
        {
            try
            {
                var json = File.ReadAllText(SettingsFile);
                _currentSettings = JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
            }
            catch
            {
                _currentSettings = new Settings();
            }
        }
        else
        {
            _currentSettings = new Settings();
            SaveSettings();
        }
    }

    public static string Language
    {
        get => _currentSettings.Language;
        set
        {
            _currentSettings.Language = value;
            SaveSettings();
        }
    }

    private static void SaveSettings()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_currentSettings, Formatting.Indented);
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError("settings_error", ex.Message);
        }
    }
}

public static class Localization
{
    private class LanguageData
    {
        public string LanguageName { get; set; } = "English";
        public Dictionary<string, string> Translations { get; set; } = new Dictionary<string, string>();
    }

    private static Dictionary<string, string> _currentLanguage = new Dictionary<string, string>();
    private static Dictionary<string, LanguageData> _allLanguages = new Dictionary<string, LanguageData>();
    private static string _currentLanguageCode = "en";
    private const string LanguagesFolder = "Languages";

    public static void Initialize()
    {
        Directory.CreateDirectory(LanguagesFolder);
        LoadAllLanguages();

        var savedLanguage = AppSettings.Language;
        var systemLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToLower();

        if (!string.IsNullOrEmpty(savedLanguage))
        {
            SetLanguage(savedLanguage);
        }
        else if (_allLanguages.ContainsKey(systemLanguage))
        {
            SetLanguage(systemLanguage);
            AppSettings.Language = systemLanguage;
        }
        else
        {
            SetLanguage("en");
            AppSettings.Language = "en";
        }
    }

    public static void LoadAllLanguages()
    {
        _allLanguages.Clear();

        foreach (var file in Directory.GetFiles(LanguagesFolder, "*.json"))
        {
            try
            {
                var languageCode = Path.GetFileNameWithoutExtension(file);
                var json = File.ReadAllText(file);
                var languageData = JsonConvert.DeserializeObject<LanguageData>(json);

                if (languageData?.Translations != null)
                {
                    _allLanguages[languageCode] = languageData;
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError("Error loading language file {0}: {1}", Path.GetFileName(file), ex.Message);
            }
        }

        // Если нет ни одного языка, создаем английский по умолчанию
        if (!_allLanguages.ContainsKey("en"))
        {
            _allLanguages["en"] = new LanguageData
            {
                LanguageName = "English",
                Translations = CreateDefaultEnglishTranslations()
            };
        }
    }

    private static Dictionary<string, string> CreateDefaultEnglishTranslations()
    {
        return new Dictionary<string, string>
        {
            // General
            ["app_title"] = "Mugs",
            ["welcome_message"] = "Console application with dynamic command loading\nType 'help' for command list or 'exit' to quit",
            ["checking_updates"] = "Checking for updates...",
            ["update_available"] = "Update available {0} (current version {1})\nDownload: {2}\nDescription: {3}\nTo install type: update install",
            ["no_update_available"] = "You have the latest version {0}",
            ["update_error"] = "Error checking for updates:\n{0}",
            ["update_success"] = "Application successfully updated!",
            ["command_not_found"] = "Command '{0}' not found. Type 'help' for command list",
            ["command_error"] = "Command execution error: {0}",
            ["exit_confirmation"] = "Are you sure you want to exit? (y/n)",
            ["invalid_input"] = "Invalid input",

            // Help command
            ["builtin_commands"] = "Built-in commands:",
            ["verified_commands"] = "Verified commands (✅ safe):",
            ["external_commands"] = "Third-party commands (use with caution):",
            ["command_help"] = "For detailed help type: help <command>",
            ["help_command"] = "help",
            ["help_description"] = "Shows command help",
            ["help_usage"] = "help update, help new",

            // Language command
            ["language_description"] = "Sets or shows the current language",
            ["current_language"] = "Current language: {0}",
            ["available_languages"] = "Available languages: {0}",
            ["language_changed"] = "Language changed to {0}",
            ["invalid_language"] = "Invalid language code: {0}",
            ["language_usage"] = "language en\nlanguage ru",

            // List command
            ["list_description"] = "Lists all available commands and their status",
            ["available_commands"] = "Available commands:",
            ["disabled_extensions"] = "Disabled extensions:",
            ["example"] = "Usage example",
            ["enable_usage"] = "To enable use: enable <command_name>",
            ["verified"] = "Verified",

            // Reload command
            ["reload_description"] = "Reloads all commands from files",
            ["reloading_commands"] = "Reloading commands...",
            ["commands_reloaded"] = "Commands successfully reloaded",

            // Clear command
            ["clear_description"] = "Clears the console",

            // Restart command
            ["restart_description"] = "Completely restarts the application",
            ["restarting"] = "Restarting application...",

            // Time command
            ["time_description"] = "Shows current time",
            ["current_time"] = "Current time: {0}",

            // Update command
            ["update_description"] = "Checks for and installs application updates",
            ["confirm_update"] = "Are you sure you want to install the update? (y/n)",
            ["update_cancelled"] = "Update cancelled",
            ["starting_update"] = "Starting update process...",
            ["downloading_update"] = "Downloading update...",
            ["extracting_update"] = "Extracting update...",
            ["creating_backup"] = "Creating backup...",
            ["installing_update"] = "Installing update...",
            ["finishing_update"] = "Finishing installation...",
            ["update_failed"] = "Error installing update: {0}",

            // New command
            ["new_description"] = "Creates a new extension script template in Extensions folder",
            ["missing_command_name"] = "Specify command name (e.g.: new mycommand)",
            ["file_exists"] = "File {0} already exists!",
            ["template_created"] = "Command template created: {0}",
            ["reload_usage"] = "To use execute: reload",

            // Enable/Disable commands
            ["enable_description"] = "Enables a disabled extension",
            ["disable_description"] = "Disables an extension",
            ["missing_extension_name"] = "Specify command name or extension file to enable (e.g.: enable mycommand or enable myextension.csx.disable)",
            ["extension_not_found"] = "File '{0}' not found",
            ["multiple_extensions"] = "Found multiple disabled extensions for command '{0}':",
            ["specify_filename"] = "Specify filename to enable",
            ["no_disabled_extensions"] = "No disabled extensions found for command/file '{0}'",
            ["extension_enabled"] = "Extension '{0}' enabled",
            ["extension_disabled"] = "Extension '{0}' disabled",
            ["command_not_found_disable"] = "Command/file '{0}' not found",

            // Import command
            ["import_description"] = "Downloads and installs an extension from the specified URL",
            ["missing_url"] = "Specify extension URL to download (e.g.: import https://example.com/extension.csx)",
            ["downloading_extension"] = "Downloading extension from URL: {0}",
            ["extension_downloaded"] = "Extension successfully downloaded: {0}",
            ["download_error"] = "Error downloading extension: {0}",

            // Debug command
            ["debug_description"] = "Runs a command in debug mode",
            ["missing_debug_command"] = "Specify command to debug (e.g.: debug mycommand --args \"test\")",
            ["debug_start"] = "Running {0} with arguments: {1}",
            ["debug_vars"] = "Variables: args = {0}",
            ["debug_completed"] = "Command completed in {0} ms",
            ["debug_error"] = "Execution error: {0}: {1}",

            // Command details
            ["command"] = "Command",
            ["description"] = "Description",
            ["aliases"] = "Aliases",
            ["author"] = "Author",
            ["version"] = "Version",
            ["usage_examples"] = "Usage examples",
            ["verification"] = "Verification",
            ["verified_safe"] = "This command is verified and safe",

            // Script command
            ["script_description"] = "Executes commands from a text file",
            ["missing_script_file"] = "Specify script file to execute (e.g.: script commands.txt)",
            ["script_file_not_found"] = "Script file '{0}' not found",
            ["executing_command"] = "Executing: {0}",
            ["command_output"] = "Command output: {0}",
            ["script_completed"] = "Script execution completed",
            ["script_error"] = "Script execution error: {0}",

            // Settings
            ["verified_load_error"] = "Error loading verified hashes: {0}",
            ["settings_error"] = "Error saving settings: {0}"
        };
    }

    public static void SetLanguage(string languageCode)
    {
        if (_allLanguages.TryGetValue(languageCode, out var languageData))
        {
            _currentLanguage = languageData.Translations;
            _currentLanguageCode = languageCode;
        }
        else
        {
            ConsoleHelper.WriteError("Language '{0}' not found. Using default 'en'", languageCode);
            _currentLanguage = _allLanguages["en"].Translations;
            _currentLanguageCode = "en";
        }
    }

    public static string GetString(string key, params object[] args)
    {
        if (_currentLanguage.TryGetValue(key, out var value))
        {
            return args.Length > 0 ? string.Format(value, args) : value;
        }

        if (_allLanguages.TryGetValue("en", out var english) && english.Translations.TryGetValue(key, out var englishValue))
        {
            return args.Length > 0 ? string.Format(englishValue, args) : englishValue;
        }

        return key;
    }

    public static string GetLanguageName(string languageCode)
    {
        return _allLanguages.TryGetValue(languageCode, out var languageData)
            ? languageData.LanguageName
            : languageCode.ToUpper();
    }

    public static IEnumerable<string> GetAvailableLanguages()
    {
        return _allLanguages.Keys.OrderBy(k => k);
    }

    public static string CurrentLanguage => _currentLanguageCode;
}

public static class ConsoleHelper
{
    private static int inputRow = -1;
    private static List<string> commandHistory = new List<string>();
    private static int historyIndex = -1;

    // Цвета для разных типов сообщений
    private const char BorderChar = '▌';
    private static readonly ConsoleColor DefaultBorderColor = ConsoleColor.DarkGray;
    private static readonly ConsoleColor SuccessBorderColor = ConsoleColor.DarkGreen;
    private static readonly ConsoleColor WarningBorderColor = ConsoleColor.DarkYellow;
    private static readonly ConsoleColor ErrorBorderColor = ConsoleColor.DarkRed;
    private static readonly ConsoleColor InfoBorderColor = ConsoleColor.DarkCyan;
    private static readonly ConsoleColor DebugBorderColor = ConsoleColor.DarkMagenta;
    private static readonly ConsoleColor CommandBorderColor = ConsoleColor.DarkBlue;

    public static void Initialize()
    {
        inputRow = Console.WindowHeight - 1;
        ClearInputLine();
    }

    public static string ReadLineWithColorHighlighting(CommandManager manager)
    {
        if (inputRow == -1) Initialize();

        var input = string.Empty;
        var position = 0;
        var suggestions = new List<string>();
        var suggestionIndex = -1;

        while (true)
        {
            RedrawInputLine(manager, input, position);

            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                if (!string.IsNullOrEmpty(input))
                {
                    commandHistory.Add(input);
                    historyIndex = commandHistory.Count;
                }
                ClearInputLine();
                return input;
            }
            else if (key.Key == ConsoleKey.Tab && suggestions.Any())
            {
                suggestionIndex = (suggestionIndex + 1) % suggestions.Count;
                input = suggestions[suggestionIndex];
                position = input.Length;
            }
            else if (key.Key == ConsoleKey.Backspace && position > 0)
            {
                input = input.Remove(position - 1, 1);
                position--;
                suggestions.Clear();
                suggestionIndex = -1;
            }
            else if (key.Key == ConsoleKey.LeftArrow && position > 0)
            {
                position--;
            }
            else if (key.Key == ConsoleKey.RightArrow && position < input.Length)
            {
                position++;
            }
            else if (key.Key == ConsoleKey.UpArrow && commandHistory.Any())
            {
                if (historyIndex > 0) historyIndex--;
                if (historyIndex >= 0 && historyIndex < commandHistory.Count)
                {
                    input = commandHistory[historyIndex];
                    position = input.Length;
                }
            }
            else if (key.Key == ConsoleKey.DownArrow && commandHistory.Any())
            {
                if (historyIndex < commandHistory.Count - 1)
                {
                    historyIndex++;
                    input = commandHistory[historyIndex];
                    position = input.Length;
                }
                else
                {
                    historyIndex = commandHistory.Count;
                    input = string.Empty;
                    position = 0;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                input = input.Insert(position, key.KeyChar.ToString());
                position++;

                var currentWord = input.Split(' ').FirstOrDefault() ?? string.Empty;
                suggestions = manager.GetCommandNamesStartingWith(currentWord).ToList();
                suggestionIndex = -1;
            }
        }
    }

    private static void RedrawInputLine(CommandManager manager, string input, int cursorPosition)
    {
        Console.SetCursorPosition(0, inputRow);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, inputRow);
        Console.Write("> ");

        var isValid = manager.IsValidCommand(input);
        Console.ForegroundColor = isValid ? ConsoleColor.Gray : ConsoleColor.Red;
        Console.Write(input);
        Console.ResetColor();

        Console.SetCursorPosition(Math.Min(cursorPosition + 2, Console.WindowWidth - 1), inputRow);
    }

    public static void ClearInputLine()
    {
        Console.SetCursorPosition(0, inputRow);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, inputRow);
    }

    public static void WriteResponse(string messageKey, params object[] args)
    {
        var message = Localization.GetString(messageKey, args);
        WriteColored(message, DefaultBorderColor);
    }

    public static void WriteSuccess(string messageKey, params object[] args)
    {
        var message = Localization.GetString(messageKey, args);
        WriteColored(message, SuccessBorderColor);
    }

    public static void WriteWarning(string messageKey, params object[] args)
    {
        var message = Localization.GetString(messageKey, args);
        WriteColored(message, WarningBorderColor);
    }

    public static void WriteError(string messageKey, params object[] args)
    {
        var message = Localization.GetString(messageKey, args);
        WriteColored(message, ErrorBorderColor);
    }

    public static void WriteInfo(string messageKey, params object[] args)
    {
        var message = Localization.GetString(messageKey, args);
        WriteColored(message, InfoBorderColor);
    }

    public static void WriteCommand(string messageKey, params object[] args)
    {
        var message = Localization.GetString(messageKey, args);
        WriteColored(message, CommandBorderColor);
    }

    public static void WriteDebug(string message)
    {
        var lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            Console.ForegroundColor = DebugBorderColor;
            Console.Write($"{BorderChar} ");
            Console.ResetColor();
            Console.WriteLine($"[DEBUG] {line}");
        }
        Console.WriteLine();
    }

    private static void WriteColored(string message, ConsoleColor borderColor)
    {
        var lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            Console.ForegroundColor = borderColor;
            Console.Write($"{BorderChar} ");
            Console.ResetColor();
            Console.WriteLine(line);
        }
        Console.WriteLine();
    }
}

public class VerifiedExtensionsChecker
{
    private const string VerifiedHashesUrl = "https://raw.githubusercontent.com/shead0shead/mugs-test/main/verified_hashes.json";
    private static readonly HttpClient _httpClient = new HttpClient();
    private static Dictionary<string, string> _verifiedHashes = new Dictionary<string, string>();
    private static bool _hashesLoaded = false;

    static VerifiedExtensionsChecker()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConsoleAppVerifiedChecker");
    }

    public static async Task EnsureHashesLoadedAsync()
    {
        if (_hashesLoaded) return;

        try
        {
            var response = await _httpClient.GetStringAsync(VerifiedHashesUrl);
            _verifiedHashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
            _hashesLoaded = true;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError("verified_load_error", ex.Message);
            _verifiedHashes = new Dictionary<string, string>();
            _hashesLoaded = true;
        }
    }

    public static bool IsExtensionVerified(string fileName)
    {
        if (!_hashesLoaded || !_verifiedHashes.Any())
            return false;

        var normalizedFileName = fileName.ToLowerInvariant();
        return _verifiedHashes.Any(kv => kv.Key.ToLowerInvariant() == normalizedFileName);
    }

    public static string? GetVerifiedHash(string fileName)
    {
        var normalizedFileName = fileName.ToLowerInvariant();
        return _verifiedHashes.FirstOrDefault(kv => kv.Key.ToLowerInvariant() == normalizedFileName).Value;
    }
}

public class UpdateChecker
{
    private const string GitHubRepoOwner = "shead0shead";
    private const string GitHubRepoName = "mugs-test";
    private const string GitHubReleasesUrl = $"https://api.github.com/repos/{GitHubRepoOwner}/{GitHubRepoName}/releases/latest";
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly Version CurrentVersion = new Version("1.0.0");

    static UpdateChecker()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ConsoleAppUpdater");
    }

    public static async Task CheckForUpdatesAsync(bool notifyIfNoUpdate = false)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(GitHubReleasesUrl);
            dynamic release = JsonConvert.DeserializeObject(response);
            var latestVersion = new Version(release.tag_name.ToString().TrimStart('v'));

            if (latestVersion > CurrentVersion)
            {
                ConsoleHelper.WriteResponse("update_available",
                    latestVersion,
                    CurrentVersion,
                    release.html_url,
                    release.body);
            }
            else if (notifyIfNoUpdate)
            {
                ConsoleHelper.WriteResponse("no_update_available", CurrentVersion);
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError("update_error", ex.Message);
        }
    }

    public static async Task InstallUpdateAsync()
    {
        try
        {
            ConsoleHelper.WriteResponse("starting_update");

            var response = await _httpClient.GetStringAsync(GitHubReleasesUrl);
            dynamic release = JsonConvert.DeserializeObject(response);
            string downloadUrl = release.assets[0].browser_download_url;
            string version = release.tag_name;

            string tempDir = Path.Combine(Path.GetTempPath(), "ConsoleAppUpdate");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            ConsoleHelper.WriteResponse("downloading_update");
            string zipPath = Path.Combine(tempDir, "update.zip");
            using (var client = new WebClient())
            {
                await client.DownloadFileTaskAsync(new Uri(downloadUrl), zipPath);
            }

            ConsoleHelper.WriteResponse("extracting_update");
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
            string currentDir = Path.GetDirectoryName(currentExePath);
            string backupDir = Path.Combine(currentDir, "Backup_" + DateTime.Now.ToString("yyyyMMddHHmmss"));

            ConsoleHelper.WriteResponse("creating_backup");
            Directory.CreateDirectory(backupDir);
            foreach (var file in Directory.GetFiles(currentDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (!file.EndsWith(".dll") && !file.EndsWith(".exe")) continue;
                File.Copy(file, Path.Combine(backupDir, Path.GetFileName(file)));
            }

            ConsoleHelper.WriteResponse("installing_update");
            foreach (var file in Directory.GetFiles(tempDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                string destPath = Path.Combine(currentDir, Path.GetFileName(file));
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(file, destPath);
            }

            ConsoleHelper.WriteResponse("finishing_update");
            Process.Start(new ProcessStartInfo
            {
                FileName = currentExePath,
                Arguments = "--updated",
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError("update_failed", ex.Message);
        }
    }
}

public class ExtensionManager
{
    private readonly string _extensionsPath;

    public ExtensionManager(string extensionsPath)
    {
        _extensionsPath = extensionsPath;
        Directory.CreateDirectory(extensionsPath);
    }

    public async Task EnableExtensionAsync(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            ConsoleHelper.WriteError("missing_extension_name");
            return;
        }

        string disabledFile;
        string enabledFile;

        if (name.EndsWith(".csx.disable", StringComparison.OrdinalIgnoreCase))
        {
            disabledFile = Path.Combine(_extensionsPath, name);
            if (!File.Exists(disabledFile))
            {
                ConsoleHelper.WriteError("extension_not_found", name);
                return;
            }

            enabledFile = Path.Combine(_extensionsPath, Path.GetFileNameWithoutExtension(name));
        }
        else
        {
            var commandName = name.ToLowerInvariant();
            var disabledFiles = Directory.GetFiles(_extensionsPath, "*.csx.disable")
                .Where(f => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f))
                .Equals(commandName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!disabledFiles.Any())
            {
                var possibleFile = Path.Combine(_extensionsPath, commandName + ".csx.disable");
                if (File.Exists(possibleFile))
                {
                    disabledFiles.Add(possibleFile);
                }
                else
                {
                    ConsoleHelper.WriteError("no_disabled_extensions", commandName);
                    return;
                }
            }

            if (disabledFiles.Count > 1)
            {
                ConsoleHelper.WriteResponse("multiple_extensions", commandName);
                foreach (var file in disabledFiles)
                {
                    ConsoleHelper.WriteResponse($"- {Path.GetFileName(file)}");
                }
                ConsoleHelper.WriteError("specify_filename");
                return;
            }

            disabledFile = disabledFiles.First();
            enabledFile = Path.Combine(_extensionsPath, Path.GetFileNameWithoutExtension(disabledFile));
        }

        File.Move(disabledFile, enabledFile);
        ConsoleHelper.WriteResponse("extension_enabled", Path.GetFileName(enabledFile));
    }

    public async Task DisableExtensionAsync(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            ConsoleHelper.WriteError("missing_extension_name");
            return;
        }

        string sourceFile;
        string disabledFile;

        if (name.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
        {
            sourceFile = Path.Combine(_extensionsPath, name);
            if (!File.Exists(sourceFile))
            {
                ConsoleHelper.WriteError("extension_not_found", name);
                return;
            }

            disabledFile = sourceFile + ".disable";
        }
        else
        {
            var commandName = name.ToLowerInvariant();
            var sourceFiles = Directory.GetFiles(_extensionsPath, "*.csx")
                .Where(f => Path.GetFileNameWithoutExtension(f)
                    .Equals(commandName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!sourceFiles.Any())
            {
                var possibleFile = Path.Combine(_extensionsPath, commandName + ".csx");
                if (File.Exists(possibleFile))
                {
                    sourceFile = possibleFile;
                    disabledFile = possibleFile + ".disable";
                    File.Move(sourceFile, disabledFile);
                    ConsoleHelper.WriteResponse("extension_disabled", Path.GetFileName(sourceFile));
                    return;
                }

                ConsoleHelper.WriteError("command_not_found_disable", commandName);
                return;
            }

            if (sourceFiles.Count > 1)
            {
                ConsoleHelper.WriteResponse("multiple_extensions", commandName);
                foreach (var file in sourceFiles)
                {
                    ConsoleHelper.WriteResponse($"- {Path.GetFileName(file)}");
                }
                ConsoleHelper.WriteError("specify_filename");
                return;
            }

            sourceFile = sourceFiles.First();
            disabledFile = sourceFile + ".disable";
        }

        File.Move(sourceFile, disabledFile);
        ConsoleHelper.WriteResponse("extension_disabled", Path.GetFileName(sourceFile));
    }

    public async Task ImportFromUrlAsync(string url)
    {
        try
        {
            ConsoleHelper.WriteResponse("downloading_extension", url);

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var fileName = Path.GetFileName(url) ?? $"extension_{DateTime.Now:yyyyMMddHHmmss}.csx";
            var filePath = Path.Combine(_extensionsPath, fileName);

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream);

            ConsoleHelper.WriteResponse("extension_downloaded", fileName);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError("download_error", ex.Message);
        }
    }
}

public class CommandManager
{
    private readonly Dictionary<string, ICommand> _commands = new();
    private readonly string _extensionsPath;

    public CommandManager(string extensionsPath)
    {
        _extensionsPath = extensionsPath;
        Directory.CreateDirectory(extensionsPath);
    }

    public async Task LoadCommandsAsync()
    {
        _commands.Clear();
        RegisterBuiltInCommands();
        await LoadExternalCommandsAsync();
    }

    private void RegisterBuiltInCommands()
    {
        RegisterCommand(new HelpCommand(this));
        RegisterCommand(new ListCommandsCommand(this));
        RegisterCommand(new ReloadCommandsCommand(this));
        RegisterCommand(new ClearCommand(this));
        RegisterCommand(new RestartCommand());
        RegisterCommand(new TimeCommand());
        RegisterCommand(new UpdateCommand());
        RegisterCommand(new NewCommand(this));
        RegisterCommand(new DebugCommand(this));
        RegisterCommand(new EnableCommand(this));
        RegisterCommand(new DisableCommand(this));
        RegisterCommand(new ImportCommand(this));
        RegisterCommand(new LanguageCommand());
        RegisterCommand(new ScriptCommand());
    }

    private async Task LoadExternalCommandsAsync()
    {
        var csFiles = Directory.GetFiles(_extensionsPath, "*.cs");
        var csxFiles = Directory.GetFiles(_extensionsPath, "*.csx")
            .Where(f => !f.EndsWith(".disable"));
        var allFiles = csFiles.Concat(csxFiles).Distinct();

        foreach (var file in allFiles)
        {
            try
            {
                var commands = await CompileAndLoadCommandsAsync(file);
                foreach (var command in commands)
                {
                    RegisterCommand(command);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError("Error loading command from file {0}: {1}", Path.GetFileName(file), ex.Message);
            }
        }
    }

    private async Task<IEnumerable<ICommand>> CompileAndLoadCommandsAsync(string filePath)
    {
        var code = await File.ReadAllTextAsync(filePath);
        var isScript = Path.GetExtension(filePath).Equals(".csx", StringComparison.OrdinalIgnoreCase);

        return isScript ? await LoadFromScriptAsync(code) : await LoadFromClassFileAsync(code);
    }

    private async Task<IEnumerable<ICommand>> LoadFromScriptAsync(string code)
    {
        try
        {
            var assemblies = new[]
            {
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(System.ComponentModel.Component).Assembly,
                typeof(System.Diagnostics.Process).Assembly,
                typeof(System.Dynamic.DynamicObject).Assembly,
                typeof(System.IO.File).Assembly,
                typeof(System.Net.WebClient).Assembly,
                typeof(System.Text.RegularExpressions.Regex).Assembly,
                typeof(System.Xml.XmlDocument).Assembly,
                Assembly.GetExecutingAssembly()
            };

            var imports = new[]
            {
                "System", "System.IO", "System.Linq", "System.Collections",
                "System.Collections.Generic", "System.Diagnostics", "System.Threading",
                "System.Threading.Tasks", "System.Text", "System.Text.RegularExpressions",
                "System.Net", "System.Net.Http", "System.Dynamic", "System.Xml", "System.Xml.Linq"
            };

            var scriptOptions = ScriptOptions.Default
                .WithReferences(assemblies)
                .WithImports(imports);

            var globals = new CommandGlobals();
            var script = CSharpScript.Create<ICommand>(code, scriptOptions, typeof(CommandGlobals));

            var compilation = script.GetCompilation();
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (diagnostics.Any())
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, diagnostics.Select(d => d.GetMessage())));
            }

            var result = await script.RunAsync(globals);
            if (result.Exception != null) throw result.Exception;

            return result.ReturnValue != null ? new[] { result.ReturnValue } : Enumerable.Empty<ICommand>();
        }
        catch (CompilationErrorException ex)
        {
            ConsoleHelper.WriteError("Script compilation error: {0}", string.Join(Environment.NewLine, ex.Diagnostics));
            return Enumerable.Empty<ICommand>();
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError("Script execution error: {0}", ex.Message);
            return Enumerable.Empty<ICommand>();
        }
    }

    private async Task<IEnumerable<ICommand>> LoadFromClassFileAsync(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ICommand).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location)
        };

        var compilation = CSharpCompilation.Create(
            "DynamicCommands",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, emitResult.Diagnostics.Select(d => d.GetMessage())));
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        return assembly.GetTypes()
            .Where(t => typeof(ICommand).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(type => (ICommand)Activator.CreateInstance(type));
    }

    public void RegisterCommand(ICommand command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));

        _commands[command.Name.ToLowerInvariant()] = command;

        foreach (var alias in command.Aliases ?? Enumerable.Empty<string>())
        {
            _commands[alias.ToLowerInvariant()] = command;
        }
    }

    public ICommand GetCommand(string name)
    {
        return _commands.TryGetValue(name.ToLowerInvariant(), out var command) ? command : null;
    }

    public bool IsValidCommand(string input)
    {
        var commandName = input.Split(' ').FirstOrDefault();
        return !string.IsNullOrEmpty(commandName) && _commands.ContainsKey(commandName.ToLowerInvariant());
    }

    public IEnumerable<ICommand> GetAllCommands() => _commands.Values
        .GroupBy(c => c.Name)
        .Select(g => g.First())
        .OrderBy(c => c.Name);

    public IEnumerable<string> GetCommandNamesStartingWith(string prefix)
    {
        return _commands.Keys
            .Where(cmd => cmd.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(cmd => cmd);
    }

    private class HelpCommand : ICommand
    {
        private readonly CommandManager _manager;
        private readonly HashSet<string> _builtInCommands = new()
    {
        "help", "list", "reload", "clear", "restart",
        "time", "update", "new", "debug", "enable",
        "disable", "import", "language", "script"
    };

        public HelpCommand(CommandManager manager) => _manager = manager;
        public string Name => "help";
        public string Description => Localization.GetString("help_description");
        public IEnumerable<string> Aliases => new[] { "?" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => Localization.GetString("help_usage");

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length > 0)
            {
                var commandName = args[0].ToLowerInvariant();
                var command = _manager.GetCommand(commandName);

                if (command != null)
                {
                    await ShowCommandDetails(command);
                    return;
                }

                ConsoleHelper.WriteError("command_not_found", commandName);
            }

            await ShowAllCommands();
        }

        private async Task ShowCommandDetails(ICommand command)
        {
            var response = new StringBuilder();

            // Заголовок команды
            response.AppendLine($"{Localization.GetString("command")}: {command.Name}\n");

            // Основная информация
            response.AppendLine($"{Localization.GetString("description")}: {command.Description}");

            if (command.Aliases.Any())
            {
                response.AppendLine($"{Localization.GetString("aliases")}: {string.Join(", ", command.Aliases)}");
            }

            response.AppendLine($"{Localization.GetString("author")}: {command.Author}");
            response.AppendLine($"{Localization.GetString("version")}: {command.Version}");

            // Примеры использования
            if (!string.IsNullOrEmpty(command.UsageExample))
            {
                response.AppendLine();
                response.AppendLine(Localization.GetString("usage_examples") + ":");
                var examples = command.UsageExample.Split('\n');
                foreach (var example in examples)
                {
                    response.AppendLine($"  {example.Trim()}");
                }
            }

            // Проверка верификации
            var fileName = $"{command.Name.ToLower()}.csx";
            if (VerifiedExtensionsChecker.IsExtensionVerified(fileName))
            {
                response.AppendLine();
                response.AppendLine($"{Localization.GetString("verification")}: ✅ {Localization.GetString("verified_safe")}");
            }

            ConsoleHelper.WriteResponse(response.ToString().TrimEnd());
        }

        private async Task ShowAllCommands()
        {
            await VerifiedExtensionsChecker.EnsureHashesLoadedAsync();

            var response = new StringBuilder();
            var allCommands = _manager.GetAllCommands()
                .GroupBy(c => c.Name)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .ToList();

            // Встроенные команды
            response.AppendLine(Localization.GetString("builtin_commands"));
            foreach (var cmd in allCommands.Where(c => _builtInCommands.Contains(c.Name)))
            {
                response.AppendLine(FormatCommandLine(cmd));
            }

            // Проверенные команды
            var verifiedCommands = new List<ICommand>();
            foreach (var cmd in allCommands.Where(c => !_builtInCommands.Contains(c.Name)))
            {
                var cmdFileName = $"{cmd.Name.ToLower()}.csx";
                if (VerifiedExtensionsChecker.IsExtensionVerified(cmdFileName))
                {
                    verifiedCommands.Add(cmd);
                }
            }

            if (verifiedCommands.Any())
            {
                response.AppendLine();
                response.AppendLine(Localization.GetString("verified_commands"));
                foreach (var cmd in verifiedCommands)
                {
                    response.AppendLine(FormatCommandLine(cmd) + " ✅");
                }
            }

            // Сторонние команды
            var externalCommands = allCommands
                .Where(c => !_builtInCommands.Contains(c.Name) &&
                       !verifiedCommands.Contains(c))
                .ToList();

            if (externalCommands.Any())
            {
                response.AppendLine();
                response.AppendLine(Localization.GetString("external_commands"));
                foreach (var cmd in externalCommands)
                {
                    response.AppendLine(FormatCommandLine(cmd));
                }
            }

            response.AppendLine();
            response.Append(Localization.GetString("command_help"));
            ConsoleHelper.WriteResponse(response.ToString());
        }

        private string FormatCommandLine(ICommand cmd)
        {
            var aliases = cmd.Aliases.Any()
                ? $" ({string.Join(", ", cmd.Aliases)})"
                : "";

            return $"  {cmd.Name,-12}{aliases,-15} - {cmd.Description}";
        }
    }

    private class ListCommandsCommand : ICommand
    {
        private readonly CommandManager _manager;

        public ListCommandsCommand(CommandManager manager) => _manager = manager;
        public string Name => "list";
        public string Description => Localization.GetString("list_description");
        public IEnumerable<string> Aliases => new[] { "ls", "dir" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => null;

        public async Task ExecuteAsync(string[] args)
        {
            await VerifiedExtensionsChecker.EnsureHashesLoadedAsync();

            var response = new StringBuilder();
            response.AppendLine(Localization.GetString("available_commands"));

            foreach (var cmd in _manager.GetAllCommands()
                .GroupBy(c => c.Name)
                .Select(g => g.First())
                .OrderBy(c => c.Name))
            {
                var fileName = $"{cmd.Name.ToLower()}.csx";
                var isVerified = VerifiedExtensionsChecker.IsExtensionVerified(fileName);
                var verifiedMark = isVerified ? " ✅" : "";

                response.AppendLine($"- {cmd.Name}{(cmd.Aliases.Any() ? $" ({Localization.GetString("aliases")}: {string.Join(", ", cmd.Aliases)})" : "")}{verifiedMark}");
                response.AppendLine($"  {Localization.GetString("version")}: {cmd.Version}, {Localization.GetString("author")}: {cmd.Author}");
                if (isVerified)
                {
                    response.AppendLine($"  {Localization.GetString("verified")}");
                }
                if (!string.IsNullOrEmpty(cmd.UsageExample))
                {
                    response.AppendLine($"  {Localization.GetString("example")}: {cmd.UsageExample}");
                }
                response.AppendLine();
            }

            var disabledFiles = Directory.GetFiles(_manager._extensionsPath, "*.csx.disable");
            if (disabledFiles.Any())
            {
                response.AppendLine(Localization.GetString("disabled_extensions"));
                foreach (var file in disabledFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var isVerified = VerifiedExtensionsChecker.IsExtensionVerified($"{fileName}.csx");
                    var verifiedMark = isVerified ? " ✅" : "";
                    response.AppendLine($"- {fileName}{verifiedMark}");
                }
                response.AppendLine("\n" + Localization.GetString("enable_usage"));
            }

            ConsoleHelper.WriteResponse(response.ToString().TrimEnd());
        }
    }

    private class ReloadCommandsCommand : ICommand
    {
        private readonly CommandManager _manager;

        public ReloadCommandsCommand(CommandManager manager) => _manager = manager;
        public string Name => "reload";
        public string Description => Localization.GetString("reload_description");
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => null;

        public async Task ExecuteAsync(string[] args)
        {
            ConsoleHelper.WriteResponse("reloading_commands");
            await _manager.LoadCommandsAsync();
            ConsoleHelper.WriteResponse("commands_reloaded");
        }
    }

    private class ClearCommand : ICommand
    {
        private readonly CommandManager _manager;

        public ClearCommand(CommandManager manager) => _manager = manager;
        public string Name => "clear";
        public string Description => Localization.GetString("clear_description");
        public IEnumerable<string> Aliases => new[] { "cls", "clean" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => null;

        public Task ExecuteAsync(string[] args)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Clear();
                Console.Write("\x1b[3J");
                Console.SetCursorPosition(0, 0);
            }
            else
            {
                Console.Write("\x1b[2J\x1b[H");
            }

            ConsoleHelper.Initialize();
            return Task.CompletedTask;
        }
    }

    private class RestartCommand : ICommand
    {
        public string Name => "restart";
        public string Description => Localization.GetString("restart_description");
        public IEnumerable<string> Aliases => new[] { "reboot" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => null;

        public Task ExecuteAsync(string[] args)
        {
            ConsoleHelper.WriteResponse("restarting");

            var currentProcess = Process.GetCurrentProcess();
            var startInfo = new ProcessStartInfo
            {
                FileName = currentProcess.MainModule.FileName,
                Arguments = Environment.CommandLine,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            Environment.Exit(0);

            return Task.CompletedTask;
        }
    }

    private class TimeCommand : ICommand
    {
        public string Name => "time";
        public string Description => Localization.GetString("time_description");
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => null;

        public Task ExecuteAsync(string[] args)
        {
            ConsoleHelper.WriteResponse("current_time", DateTime.Now.ToString("T"));
            return Task.CompletedTask;
        }
    }

    private class UpdateCommand : ICommand
    {
        public string Name => "update";
        public string Description => Localization.GetString("update_description");
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "update install";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("install", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.WriteResponse("confirm_update");
                var response = Console.ReadLine();
                if (response.Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    await UpdateChecker.InstallUpdateAsync();
                }
                else
                {
                    ConsoleHelper.WriteResponse("update_cancelled");
                }
            }
            else
            {
                await UpdateChecker.CheckForUpdatesAsync(true);
            }
        }
    }

    private class NewCommand : ICommand
    {
        private readonly CommandManager _manager;

        public NewCommand(CommandManager manager) => _manager = manager;
        public string Name => "new";
        public string Description => Localization.GetString("new_description");
        public IEnumerable<string> Aliases => new[] { "template" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "new mycommand";

        public Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("missing_command_name");
                return Task.CompletedTask;
            }

            var commandName = args[0].ToLowerInvariant();
            var fileName = $"{commandName}.csx";
            var filePath = Path.Combine(_manager._extensionsPath, fileName);

            if (File.Exists(filePath))
            {
                ConsoleHelper.WriteError("file_exists", fileName);
                return Task.CompletedTask;
            }

            var template = $@"// Example extension script for command '{commandName}'
// Remove comments and implement your command

public class {char.ToUpper(commandName[0]) + commandName.Substring(1)}Command : ICommand
{{
    public string Name => ""{commandName}"";
    public string Description => ""Description of {commandName} command"";
    public IEnumerable<string> Aliases => new[] {{ ""{commandName[0]}"", ""{commandName.Substring(0, Math.Min(3, commandName.Length))}"" }};
    public string Author => ""Your Name"";
    public string Version => ""1.0"";
    public string? UsageExample => ""{commandName} arg1 arg2\n{commandName} --option"";

    public async Task ExecuteAsync(string[] args)
    {{
        // Your code here
        Print(""Command '{commandName}' executed!"");
        
        // Example argument handling
        if (args.Length > 0)
        {{
            Print($""Received arguments: {{string.Join("", "", args)}}"");
        }}
    }}
}}

// Return command instance
new {char.ToUpper(commandName[0]) + commandName.Substring(1)}Command()";

            File.WriteAllText(filePath, template);
            ConsoleHelper.WriteResponse("template_created", fileName);
            ConsoleHelper.WriteResponse("reload_usage");

            return Task.CompletedTask;
        }
    }

    private class EnableCommand : ICommand
    {
        private readonly CommandManager _manager;
        private readonly ExtensionManager _extensionManager;

        public EnableCommand(CommandManager manager)
        {
            _manager = manager;
            _extensionManager = new ExtensionManager(manager._extensionsPath);
        }

        public string Name => "enable";
        public string Description => Localization.GetString("enable_description");
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "enable mycommand";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("missing_extension_name");
                return;
            }

            await _extensionManager.EnableExtensionAsync(args[0]);
            await _manager.LoadCommandsAsync();
        }
    }

    private class DisableCommand : ICommand
    {
        private readonly CommandManager _manager;
        private readonly ExtensionManager _extensionManager;

        public DisableCommand(CommandManager manager)
        {
            _manager = manager;
            _extensionManager = new ExtensionManager(manager._extensionsPath);
        }

        public string Name => "disable";
        public string Description => Localization.GetString("disable_description");
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "disable mycommand";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("missing_extension_name");
                return;
            }

            await _extensionManager.DisableExtensionAsync(args[0]);
            await _manager.LoadCommandsAsync();
        }
    }

    private class DebugCommand : ICommand
    {
        private readonly CommandManager _manager;

        public DebugCommand(CommandManager manager) => _manager = manager;
        public string Name => "debug";
        public string Description => Localization.GetString("debug_description");
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "debug mycommand --args \"test\"";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("missing_debug_command");
                return;
            }

            var commandName = args[0];
            var commandArgs = ParseDebugArgs(args.Skip(1).ToArray());

            var command = _manager.GetCommand(commandName);
            if (command == null)
            {
                ConsoleHelper.WriteError("command_not_found", commandName);
                return;
            }

            ConsoleHelper.WriteDebug(Localization.GetString("debug_start", commandName, string.Join(" ", commandArgs)));
            ConsoleHelper.WriteDebug(Localization.GetString("debug_vars", JsonConvert.SerializeObject(commandArgs)));

            try
            {
                var stopwatch = Stopwatch.StartNew();
                await command.ExecuteAsync(commandArgs);
                stopwatch.Stop();

                ConsoleHelper.WriteDebug(Localization.GetString("debug_completed", stopwatch.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteDebug(Localization.GetString("debug_error", ex.GetType().Name, ex.Message));
                throw;
            }
        }

        private string[] ParseDebugArgs(string[] args)
        {
            var result = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--args" && i + 1 < args.Length)
                {
                    var argValue = args[i + 1];
                    if (argValue.StartsWith("\"") && argValue.EndsWith("\""))
                    {
                        argValue = argValue.Substring(1, argValue.Length - 2);
                    }
                    result.Add(argValue);
                    i++;
                }
                else
                {
                    result.Add(args[i]);
                }
            }
            return result.ToArray();
        }
    }

    private class ImportCommand : ICommand
    {
        private readonly CommandManager _manager;
        private readonly ExtensionManager _extensionManager;

        public ImportCommand(CommandManager manager)
        {
            _manager = manager;
            _extensionManager = new ExtensionManager(manager._extensionsPath);
        }

        public string Name => "import";
        public string Description => Localization.GetString("import_description");
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "import https://example.com/extension.csx";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("missing_url");
                return;
            }

            var url = args[0];
            await _extensionManager.ImportFromUrlAsync(url);
            await _manager.LoadCommandsAsync();
        }
    }

    private class LanguageCommand : ICommand
    {
        public string Name => "language";
        public string Description => Localization.GetString("language_description");
        public IEnumerable<string> Aliases => new[] { "lang" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "language en, language ru";

        public Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                // Показываем текущий язык и доступные языки
                var response = new StringBuilder();
                response.AppendLine(Localization.GetString("current_language",
                    $"{Localization.GetLanguageName(Localization.CurrentLanguage)} ({Localization.CurrentLanguage})"));

                var availableLangs = Localization.GetAvailableLanguages()
                    .Select(lang => $"{Localization.GetLanguageName(lang)} ({lang})");

                response.AppendLine(Localization.GetString("available_languages", string.Join(", ", availableLangs)));

                ConsoleHelper.WriteResponse(response.ToString().TrimEnd());
            }
            else
            {
                // Меняем язык
                var langCode = args[0].ToLower();
                if (Localization.GetAvailableLanguages().Contains(langCode))
                {
                    Localization.SetLanguage(langCode);
                    AppSettings.Language = langCode;
                    ConsoleHelper.WriteResponse("language_changed",
                        $"{Localization.GetLanguageName(langCode)} ({langCode})");
                }
                else
                {
                    ConsoleHelper.WriteError("invalid_language", langCode);
                }
            }

            return Task.CompletedTask;
        }
    }

    private class ScriptCommand : ICommand
    {
        public string Name => "script";
        public string Description => Localization.GetString("script_description");
        public IEnumerable<string> Aliases => new[] { "batch", "run" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "script commands.txt";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("missing_script_file");
                return;
            }

            var fileName = args[0];
            if (!File.Exists(fileName))
            {
                ConsoleHelper.WriteError("script_file_not_found", fileName);
                return;
            }

            try
            {
                var commands = await File.ReadAllLinesAsync(fileName);
                foreach (var command in commands)
                {
                    if (string.IsNullOrWhiteSpace(command)) continue;
                    if (command.TrimStart().StartsWith("#")) continue;

                    ConsoleHelper.WriteResponse("executing_command", command);
                    var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var cmdName = parts[0];
                    var cmdArgs = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();

                    ConsoleHelper.WriteResponse("command_output", $"Executing: {command}");
                    await Task.Delay(100);
                }
                ConsoleHelper.WriteResponse("script_completed");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError("script_error", ex.Message);
            }
        }
    }
}

public class CommandGlobals
{
    public void Print(string message) => ConsoleHelper.WriteResponse(message);
    public void PrintError(string message) => ConsoleHelper.WriteError(message);
    public string ReadLine() => Console.ReadLine();
    public string Version => "1.0";

    public void DebugLog(string message) => ConsoleHelper.WriteDebug(message);
    public void DebugVar(string name, object value) => ConsoleHelper.WriteDebug($"{name} = {JsonConvert.SerializeObject(value)}");

    public CommandManager Manager { get; set; }
}

public class Program
{
    private const string ExtensionsFolder = "Extensions";

    public static async Task Main(string[] args)
    {
        AppSettings.Initialize();
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        ConsoleHelper.Initialize();
        Localization.Initialize();
        Console.Title = Localization.GetString("app_title");

        if (args.All(a => a != "--updated"))
        {
            ConsoleHelper.WriteResponse("checking_updates");
            await UpdateChecker.CheckForUpdatesAsync();
        }
        else
        {
            ConsoleHelper.WriteResponse("update_success");
        }

        var manager = new CommandManager(ExtensionsFolder);
        await manager.LoadCommandsAsync();

        ConsoleHelper.WriteResponse("welcome_message");

        while (true)
        {
            var input = ConsoleHelper.ReadLineWithColorHighlighting(manager);

            if (string.IsNullOrEmpty(input)) continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.WriteResponse("exit_confirmation");
                var confirm = Console.ReadLine();
                if (confirm.Equals("y", StringComparison.OrdinalIgnoreCase))
                    break;
                continue;
            }

            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var commandName = parts[0];
            var commandArgs = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();

            var command = manager.GetCommand(commandName);
            if (command == null)
            {
                ConsoleHelper.WriteError("command_not_found", commandName);
                continue;
            }

            try
            {
                await command.ExecuteAsync(commandArgs);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError("command_error", ex.Message);
            }
        }
    }
}