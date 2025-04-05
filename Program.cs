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
using System.Security.Cryptography;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Dynamic;

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
        public bool EnableSuggestions { get; set; } = true;
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

    public static bool EnableSuggestions
    {
        get => _currentSettings.EnableSuggestions;
        set
        {
            _currentSettings.EnableSuggestions = value;
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

            // Toggle suggestions
            ["toggle_suggestions"] = "Toggles command suggestions display",
            ["suggestions_enabled"] = "Command suggestions enabled",
            ["suggestions_disabled"] = "Command suggestions disabled",

            // Command Metadata Cashe
            ["cache_save_error"] = "Error saving metadata cache: {0}",
            ["command_requires_recompile"] = "Command '{0}' requires recompilation. Use 'reload' command",
            ["cache_cleared"] = "Metadata cache cleared",

            // Alias command
            ["alias_description"] = "Manage command aliases",
            ["alias_usage"] = "alias add <command> <alias>, alias remove <alias>, alias list",
            ["alias_no_aliases"] = "No custom aliases defined",
            ["alias_header"] = "Custom aliases:",
            ["alias_added"] = "Alias '{0}' added for command '{1}'",
            ["alias_removed"] = "Alias '{0}' removed",
            ["alias_not_found"] = "Alias not found",
            ["alias_invalid_syntax"] = "Invalid alias command syntax",

            // Scan command
            ["scan_description"] = "Scans script for potentially dangerous code",
            ["scan_missing_file"] = "Specify script file to scan (e.g.: scan mycommand.csx)",
            ["scan_file_not_found"] = "File '{0}' not found",
            ["scan_issues_found"] = "Potential security issues found in {0}:",
            ["scan_no_issues"] = "No dangerous code patterns found in {0}",
            ["scan_total_issues"] = "Total issues found: {0}",
            ["scan_error"] = "Scan error: {0}",
            ["full_path_display"] = "Full path: {0}",

            // History command
            ["history_description"] = "Shows command history or searches in history",
            ["history_showing"] = "Showing last {0} commands:",
            ["history_search_results"] = "Search results for \"{0}\":",
            ["history_no_results"] = "No commands found matching \"{0}\"",
            ["history_invalid_count"] = "Invalid count specified. Using default value.",
            ["history_full"] = "Full command history:",

            // Version command
            ["version_description"] = "Shows application version and information",
            ["application"] = "Application",
            ["repo"] = "Repo",
            ["commands"] = "Commands",
            ["extensions"] = "Extensions",
            ["available"] = "available",
            ["loaded"] = "loaded",

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

public class CommandMetadata
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string[] Aliases { get; set; }
    public string Author { get; set; }
    public string Version { get; set; }
    public string FilePath { get; set; }
    public string Hash { get; set; }
    public DateTime LastModified { get; set; }
}

// Добавьте класс для управления кэшем метаданных
public static class MetadataCache
{
    private const string CacheFile = "command_cache.json";
    private static readonly string CachePath = Path.Combine(AppContext.BaseDirectory, CacheFile);
    private static Dictionary<string, CommandMetadata> _cache = new();

    public static void Initialize()
    {
        if (File.Exists(CachePath))
        {
            try
            {
                var json = File.ReadAllText(CachePath);
                _cache = JsonConvert.DeserializeObject<Dictionary<string, CommandMetadata>>(json)
                    ?? new Dictionary<string, CommandMetadata>();
            }
            catch
            {
                _cache = new Dictionary<string, CommandMetadata>();
            }
        }
    }

    public static void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_cache, Formatting.Indented);
            File.WriteAllText(CachePath, json);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError("cache_save_error", ex.Message);
        }
    }

    public static void Clear()
    {
        _cache.Clear();
        Save(); // Сохраняем пустой кэш
    }

    public static bool TryGetFromCache(string filePath, out CommandMetadata metadata)
    {
        var fileHash = CalculateFileHash(filePath);
        var lastModified = File.GetLastWriteTimeUtc(filePath);

        if (_cache.TryGetValue(filePath, out metadata) &&
            metadata.Hash == fileHash &&
            metadata.LastModified == lastModified)
        {
            return true;
        }
        return false;
    }

    public static void UpdateCache(string filePath, CommandMetadata metadata)
    {
        metadata.Hash = CalculateFileHash(filePath);
        metadata.LastModified = File.GetLastWriteTimeUtc(filePath);
        _cache[filePath] = metadata;
    }

    private static string CalculateFileHash(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return Convert.ToBase64String(sha.ComputeHash(stream));
    }
}

public static class ConsoleHelper
{
    private static int inputRow = -1;
    private static List<string> commandHistory = new List<string>();
    private static int historyIndex = -1;
    private const char BorderChar = '▌';
    private static readonly ConsoleColor BorderColor = ConsoleColor.DarkGray;

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

        // Добавляем проверку настройки
        if (AppSettings.EnableSuggestions && !string.IsNullOrEmpty(input))
        {
            var suggestion = manager.GetCommandSuggestion(input.Split(' ')[0]);
            if (suggestion != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(suggestion);
            }
        }

        Console.ResetColor();
        Console.SetCursorPosition(Math.Min(cursorPosition + 2, Console.WindowWidth - 1), inputRow);
    }

    public static void ClearInputLine()
    {
        Console.SetCursorPosition(0, inputRow);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, inputRow);
    }

    public static List<string> GetCommandHistory()
    {
        return new List<string>(commandHistory);
    }

    public static void WriteResponse(string messageKey, params object[] args)
    {
        var message = Localization.GetString(messageKey, args);
        var lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            Console.ForegroundColor = BorderColor;
            Console.Write($"{BorderChar} ");
            Console.ResetColor();
            Console.WriteLine(line);
        }
        Console.WriteLine();
    }

    public static void WriteError(string messageKey, params object[] args)
    {
        var message = Localization.GetString(messageKey, args);
        var lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"{BorderChar} ");
            Console.WriteLine(line);
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    public static void WriteDebug(string message)
    {
        var lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"[DEBUG] ");
            Console.ResetColor();
            Console.WriteLine(line);
        }
    }
}

public static class ScriptCache
{
    private static readonly Dictionary<string, Script> _scriptCache = new();
    private static readonly Dictionary<string, Assembly> _assemblyCache = new();
    private static readonly object _lock = new();

    public static void AddScript(string filePath, Script script)
    {
        lock (_lock)
        {
            var normalizedPath = NormalizePath(filePath);
            _scriptCache[normalizedPath] = script;
        }
    }

    public static bool TryGetScript(string filePath, out Script script)
    {
        lock (_lock)
        {
            var normalizedPath = NormalizePath(filePath);
            return _scriptCache.TryGetValue(normalizedPath, out script);
        }
    }

    public static void AddAssembly(string filePath, Assembly assembly)
    {
        lock (_lock)
        {
            var normalizedPath = NormalizePath(filePath);
            _assemblyCache[normalizedPath] = assembly;
        }
    }

    public static bool TryGetAssembly(string filePath, out Assembly assembly)
    {
        lock (_lock)
        {
            var normalizedPath = NormalizePath(filePath);
            return _assemblyCache.TryGetValue(normalizedPath, out assembly);
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _scriptCache.Clear();
            _assemblyCache.Clear();
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).ToLowerInvariant();
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

public static class SharedData
{
    private static readonly ConcurrentDictionary<string, object> _data = new();
    private static readonly ConcurrentDictionary<string, Assembly> _loadedScripts = new();

    public static void Set(string key, object value) => _data[key] = value;
    public static T Get<T>(string key) => _data.TryGetValue(key, out var value) ? (T)value : default;
    public static bool Contains(string key) => _data.ContainsKey(key);

    public static void RegisterScript(string scriptName, Assembly assembly)
    {
        _loadedScripts[scriptName.ToLowerInvariant()] = assembly;
    }

    public static Assembly GetScriptAssembly(string scriptName)
    {
        _loadedScripts.TryGetValue(scriptName.ToLowerInvariant(), out var assembly);
        return assembly;
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
        _commands.Clear(); // Очищаем все команды
        ScriptCache.Clear();
        MetadataCache.Clear(); // Добавляем очистку кэша метаданных
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
        RegisterCommand(new ToggleSuggestionsCommand());
        RegisterCommand(new AliasCommand());
        RegisterCommand(new ScanCommand(_extensionsPath));
        RegisterCommand(new HistoryCommand());
        RegisterCommand(new VersionCommand(this));
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
                    MetadataCache.UpdateCache(file, new CommandMetadata
                    {
                        Name = command.Name,
                        Description = command.Description,
                        Aliases = command.Aliases.ToArray(),
                        Author = command.Author,
                        Version = command.Version,
                        FilePath = file
                    });
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError("Error loading command: {0}", ex.Message);
            }
        }

        MetadataCache.Save();
    }

    private async Task<IEnumerable<ICommand>> CompileAndLoadCommandsAsync(string filePath)
    {
        var code = await File.ReadAllTextAsync(filePath);
        var isScript = Path.GetExtension(filePath).Equals(".csx", StringComparison.OrdinalIgnoreCase);

        if (isScript)
        {
            // Проверяем кэш для скриптов
            if (ScriptCache.TryGetScript(filePath, out var cachedScript))
            {
                try
                {
                    var result = await cachedScript.RunAsync(new CommandGlobals(_extensionsPath) { Manager = this });
                    if (result.Exception != null) throw result.Exception;
                    var command = result.ReturnValue as ICommand;
                    return command != null ? new[] { command } : Enumerable.Empty<ICommand>();
                }
                catch
                {
                    ScriptCache.Clear();
                    return await LoadFromScriptAsync(code, filePath);
                }
            }

            // Если скрипта нет в кэше, загружаем его
            return await LoadFromScriptAsync(code, filePath);
        }
        else
        {
            // Проверяем кэш для сборок
            if (ScriptCache.TryGetAssembly(filePath, out var cachedAssembly))
            {
                return cachedAssembly.GetTypes()
                    .Where(t => typeof(ICommand).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .Select(type => (ICommand)Activator.CreateInstance(type));
            }

            // Если сборки нет в кэше, компилируем
            var commands = await LoadFromClassFileAsync(code, filePath);
            return commands;
        }
    }

    private async Task<IEnumerable<ICommand>> LoadFromScriptAsync(string code, string filePath)
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

            var globalsType = typeof(CommandGlobals);

            // Проверяем кэш
            if (ScriptCache.TryGetScript(filePath, out var cachedScript))
            {
                try
                {
                    var scriptResult = await cachedScript.RunAsync(new CommandGlobals(_extensionsPath) { Manager = this });
                    if (scriptResult.Exception != null) throw scriptResult.Exception;
                    var cmd = scriptResult.ReturnValue as ICommand;
                    return cmd != null ? new[] { cmd } : Enumerable.Empty<ICommand>();
                }
                catch
                {
                    ScriptCache.Clear();
                    // Продолжаем с обычной загрузкой
                }
            }

            // Добавляем поддержку #load директив
            var processedCode = ProcessLoadDirectives(code, filePath);

            var script = CSharpScript.Create(processedCode, scriptOptions, globalsType);
            var compilation = script.GetCompilation();
            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (diagnostics.Any())
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, diagnostics.Select(d => d.GetMessage())));
            }

            ScriptCache.AddScript(filePath, script);

            var result = await script.RunAsync(new CommandGlobals(_extensionsPath) { Manager = this });
            if (result.Exception != null) throw result.Exception;

            var command = result.ReturnValue as ICommand;
            return command != null ? new[] { command } : Enumerable.Empty<ICommand>();
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

    private string ProcessLoadDirectives(string code, string currentFilePath)
    {
        var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var result = new StringBuilder();
        var loadedScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("#load ", StringComparison.OrdinalIgnoreCase))
            {
                var scriptName = line.Substring(line.IndexOf('"') + 1);
                scriptName = scriptName.Substring(0, scriptName.IndexOf('"'));

                if (!loadedScripts.Contains(scriptName))
                {
                    var scriptPath = Path.Combine(Path.GetDirectoryName(currentFilePath), scriptName);
                    if (File.Exists(scriptPath))
                    {
                        var scriptCode = File.ReadAllText(scriptPath);
                        result.AppendLine(ProcessLoadDirectives(scriptCode, scriptPath));
                        loadedScripts.Add(scriptName);
                    }
                    else
                    {
                        throw new FileNotFoundException($"Script file not found: {scriptName}");
                    }
                }
            }
            else
            {
                result.AppendLine(line);
            }
        }

        return result.ToString();
    }

    private async Task<IEnumerable<ICommand>> LoadFromClassFileAsync(string code, string filePath)
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

        // Кэшируем загруженную сборку
        ScriptCache.AddAssembly(filePath, assembly);

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
        var commandName = name.ToLowerInvariant();

        // Проверка пользовательских алиасов
        if (AliasManager.GetCommandName(commandName) is string resolvedName)
        {
            commandName = resolvedName;
        }

        return _commands.TryGetValue(commandName, out var command) ? command : null;
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

    public string GetCommandSuggestion(string prefix)
    {
        var commands = GetCommandNamesStartingWith(prefix).ToList();
        if (commands.Count == 0) return null;
        var firstMatch = commands.First();
        return firstMatch.Length > prefix.Length
            ? firstMatch.Substring(prefix.Length)
            : null;
    }

    private class HelpCommand : ICommand
    {
        private readonly CommandManager _manager;
        private readonly HashSet<string> _builtInCommands = new()
        {
            "help", "list", "reload", "clear", "restart",
            "time", "update", "new", "debug", "enable",
            "disable", "import", "language", "script",
            "suggestions", "alias", "scan", "history",
            "version"
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
            MetadataCache.Clear(); // Принудительная очистка
            await _manager.LoadCommandsAsync();
            ConsoleHelper.WriteResponse(Localization.GetString("commands_reloaded") + "\n" + Localization.GetString("cache_cleared"));
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

    private class ToggleSuggestionsCommand : ICommand
    {
        public string Name => "suggestions";
        public string Description => Localization.GetString("toggle_suggestions");
        public IEnumerable<string> Aliases => new[] { "ts" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "toggle-suggestions";

        public Task ExecuteAsync(string[] args)
        {
            AppSettings.EnableSuggestions = !AppSettings.EnableSuggestions;
            ConsoleHelper.WriteResponse(AppSettings.EnableSuggestions
                ? Localization.GetString("suggestions_enabled")
                : Localization.GetString("suggestions_disabled"));
            return Task.CompletedTask;
        }
    }

    private class AliasCommand : ICommand
    {
        public string Name => "alias";
        public string Description => Localization.GetString("alias_description");
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => Localization.GetString("alias_usage");

        public Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0 || args[0] == "list")
            {
                var aliases = AliasManager.GetAllAliases();
                if (!aliases.Any())
                {
                    ConsoleHelper.WriteResponse("alias_no_aliases");
                    return Task.CompletedTask;
                }

                var response = new StringBuilder(Localization.GetString("alias_header") + "\n");
                foreach (var alias in aliases)
                {
                    response.AppendLine($"- {alias.Key} => {alias.Value}");
                }
                ConsoleHelper.WriteResponse(response.ToString().TrimEnd());
                return Task.CompletedTask;
            }

            switch (args[0].ToLower())
            {
                case "add" when args.Length >= 3:
                    AliasManager.AddAlias(args[1], args[2]);
                    ConsoleHelper.WriteResponse("alias_added", args[2], args[1]);
                    break;

                case "remove" when args.Length >= 2:
                    if (AliasManager.RemoveAlias(args[1]))
                        ConsoleHelper.WriteResponse("alias_removed", args[1]);
                    else
                        ConsoleHelper.WriteError("alias_not_found");
                    break;

                default:
                    ConsoleHelper.WriteError("alias_invalid_syntax");
                    break;
            }
            return Task.CompletedTask;
        }
    }

    private class ScanCommand : ICommand
    {
        private static readonly HashSet<string> DangerousTypes = new()
        {
            "System.IO.File", "System.IO.Directory", "System.Diagnostics.Process",
            "System.Net.WebClient", "System.Net.Http.HttpClient", "System.Reflection",
            "System.Runtime.InteropServices", "System.Security", "System.Management",
            "Microsoft.Win32", "System.Data.SqlClient", "System.Net.Sockets"
        };

        private static readonly HashSet<string> DangerousMethods = new()
        {
            "Delete", "Kill", "Start", "Execute", "Run", "Format",
            "WriteAllText", "WriteAllBytes", "WriteAllLines",
            "Remove", "Move", "Copy", "Create", "OpenWrite",
            "DownloadFile", "UploadFile", "ExecuteNonQuery",
            "ShellExecute", "CreateProcess", "Invoke",
            "GetProcAddress", "LoadLibrary", "SetWindowsHook"
        };

        private readonly string _extensionsPath;

        public ScanCommand(string extensionsPath)
        {
            _extensionsPath = extensionsPath;
        }

        public string Name => "scan";
        public string Description => Localization.GetString("scan_description");
        public IEnumerable<string> Aliases => new[] { "analyze" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "scan mycommand.csx";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("scan_missing_file");
                return;
            }

            var fileName = args[0];

            // Добавляем .csx если нужно
            if (!fileName.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".csx";
            }

            var fullPath = Path.Combine(_extensionsPath, fileName);

            if (!File.Exists(fullPath))
            {
                ConsoleHelper.WriteError("scan_file_not_found", fileName);
                ConsoleHelper.WriteResponse("full_path_display", Path.GetFullPath(fullPath));
                return;
            }

            try
            {
                var code = await File.ReadAllTextAsync(fullPath);
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                var root = await syntaxTree.GetRootAsync();

                var walker = new DangerousCodeWalker();
                walker.Visit(root);

                if (walker.DangerousCalls.Any())
                {
                    ConsoleHelper.WriteError("scan_issues_found", fileName);
                    foreach (var call in walker.DangerousCalls.Distinct().OrderBy(c => c))
                    {
                        ConsoleHelper.WriteError($"- {call}");
                    }
                    ConsoleHelper.WriteResponse("scan_total_issues", walker.DangerousCalls.Count);
                }
                else
                {
                    ConsoleHelper.WriteResponse("scan_no_issues", fileName);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError("scan_error", ex.Message);
            }
        }

        private class DangerousCodeWalker : CSharpSyntaxWalker
        {
            public List<string> DangerousCalls { get; } = new();

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var methodName = node.ToString();
                if (DangerousMethods.Any(m => methodName.Contains(m)) ||
                    DangerousTypes.Any(t => methodName.StartsWith(t)))
                {
                    DangerousCalls.Add(methodName);
                }

                base.VisitInvocationExpression(node);
            }

            public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
            {
                var typeName = node.Type.ToString();
                if (DangerousTypes.Any(t => typeName.StartsWith(t)))
                {
                    DangerousCalls.Add($"new {typeName}()");
                }

                base.VisitObjectCreationExpression(node);
            }
        }
    }

    private class HistoryCommand : ICommand
    {
        public string Name => "history";
        public string Description => Localization.GetString("history_description");
        public IEnumerable<string> Aliases => new[] { "hist" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "history 10\nhistory --search \"update\"";

        public Task ExecuteAsync(string[] args)
        {
            var history = ConsoleHelper.GetCommandHistory();
            var response = new StringBuilder();

            if (args.Length > 0 && args[0] == "--search")
            {
                if (args.Length < 2)
                {
                    ConsoleHelper.WriteError("missing_search_term");
                    return Task.CompletedTask;
                }

                var searchTerm = string.Join(" ", args.Skip(1)).ToLowerInvariant();
                var results = history.Where(cmd => cmd.ToLowerInvariant().Contains(searchTerm)).ToList();

                if (results.Any())
                {
                    response.AppendLine(Localization.GetString("history_search_results", searchTerm));
                    foreach (var cmd in results)
                    {
                        response.AppendLine($"- {cmd}");
                    }
                }
                else
                {
                    response.AppendLine(Localization.GetString("history_no_results", searchTerm));
                }
            }
            else
            {
                int count = 10;
                if (args.Length > 0 && int.TryParse(args[0], out int requestedCount) && requestedCount > 0)
                {
                    count = requestedCount;
                }
                else if (args.Length > 0)
                {
                    response.AppendLine(Localization.GetString("history_invalid_count"));
                }

                var commandsToShow = history.TakeLast(count).ToList();

                if (count >= history.Count)
                {
                    response.AppendLine(Localization.GetString("history_full"));
                }
                else
                {
                    response.AppendLine(Localization.GetString("history_showing", count));
                }

                foreach (var cmd in commandsToShow)
                {
                    response.AppendLine($"- {cmd}");
                }
            }

            ConsoleHelper.WriteResponse(response.ToString().TrimEnd());
            return Task.CompletedTask;
        }
    }

    private class VersionCommand : ICommand
    {
        private readonly CommandManager _manager;

        public VersionCommand(CommandManager manager)
        {
            _manager = manager;
        }

        public string Name => "version";
        public string Description => Localization.GetString("version_description");
        public IEnumerable<string> Aliases => new[] { "ver" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "version";

        public Task ExecuteAsync(string[] args)
        {
            var asciiArt = new[]
{
                "░░░░░░░░░░░░░     ",
                "▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒ ",
                "▓▓▓▓▓▓▓▓▓▓▓▓▓   ▓▓",
                "▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒ ",
                "░░░░░░░░░░░░░     ",
                " ░░░░░░░░░░░      "
            };

            var extensionsPath = Path.Combine(AppContext.BaseDirectory, "Extensions");
            var extensionsCount = Directory.Exists(extensionsPath)
                ? Directory.GetFiles(extensionsPath, "*.csx").Length
                : 0;

            var info = new[]
            {
                $"{$"{Localization.GetString("application")}:",-15} Mugs Console Add-on Platform",
                $"{$"{Localization.GetString("version")}:",-15} 1.0.0",
                $"{$"{Localization.GetString("author")}:",-15} Shead (https://github.com/shead0shead)",
                $"{$"{Localization.GetString("repo")}:",-15} https://github.com/shead0shead/mugs-test",
                $"{$"{Localization.GetString("commands")}:",-15} {_manager.GetAllCommands().Count()} {Localization.GetString("available")}",
                $"{$"{Localization.GetString("extensions")}:",-15} {extensionsCount} {Localization.GetString("loaded")}"
            };

            var maxArtLength = asciiArt.Max(line => line.Length);
            var output = new StringBuilder();

            for (int i = 0; i < Math.Max(asciiArt.Length, info.Length); i++)
            {
                var artLine = i < asciiArt.Length ? asciiArt[i] : new string(' ', maxArtLength);
                var infoLine = i < info.Length ? info[i] : "";

                output.AppendLine($"{artLine}  {infoLine}");
            }

            ConsoleHelper.WriteResponse(output.ToString().TrimEnd());
            return Task.CompletedTask;
        }
    }
}

public static class AliasManager
{
    private const string AliasFile = "aliases.json";
    private static Dictionary<string, string> _aliases = new();

    public static void Initialize()
    {
        if (File.Exists(AliasFile))
        {
            try
            {
                var json = File.ReadAllText(AliasFile);
                _aliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                          ?? new Dictionary<string, string>();
            }
            catch
            {
                _aliases = new Dictionary<string, string>();
            }
        }
    }

    public static void AddAlias(string commandName, string alias)
    {
        _aliases[alias.ToLowerInvariant()] = commandName.ToLowerInvariant();
        SaveAliases();
    }

    public static bool RemoveAlias(string alias)
    {
        bool removed = _aliases.Remove(alias.ToLowerInvariant());
        if (removed) SaveAliases();
        return removed;
    }

    public static string GetCommandName(string alias)
    {
        return _aliases.TryGetValue(alias.ToLowerInvariant(), out var cmd) ? cmd : null;
    }

    public static Dictionary<string, string> GetAllAliases()
    {
        return new Dictionary<string, string>(_aliases);
    }

    private static void SaveAliases()
    {
        File.WriteAllText(AliasFile, JsonConvert.SerializeObject(_aliases, Formatting.Indented));
    }
}

public class CommandGlobals
{
    private readonly string _extensionsPath;

    public CommandGlobals(string extensionsPath)
    {
        _extensionsPath = extensionsPath;
    }

    public void Print(string message) => ConsoleHelper.WriteResponse(message);
    public void PrintError(string message) => ConsoleHelper.WriteError(message);
    public string ReadLine() => Console.ReadLine();
    public string Version => "1.0";

    public void DebugLog(string message) => ConsoleHelper.WriteDebug(message);
    public void DebugVar(string name, object value) => ConsoleHelper.WriteDebug($"{name} = {JsonConvert.SerializeObject(value)}");

    public CommandManager Manager { get; set; }

    // Новые методы для работы с общими данными и скриптами
    public void SetSharedData(string key, object value) => SharedData.Set(key, value);
    public T GetSharedData<T>(string key) => SharedData.Get<T>(key);
    public bool HasSharedData(string key) => SharedData.Contains(key);

    public dynamic LoadScript(string scriptName)
    {
        var scriptPath = Path.Combine(_extensionsPath, scriptName);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Script file not found: {scriptName}");
        }

        // Проверяем, не загружен ли уже этот скрипт
        var cachedAssembly = SharedData.GetScriptAssembly(scriptName);
        if (cachedAssembly != null)
        {
            return CreateScriptProxy(cachedAssembly);
        }

        // Загружаем скрипт
        var scriptCode = File.ReadAllText(scriptPath);
        var script = CSharpScript.Create(scriptCode,
            ScriptOptions.Default
                .WithReferences(Assembly.GetExecutingAssembly())
                .WithImports("System", "System.Collections.Generic"),
            typeof(CommandGlobals));

        var compilation = script.GetCompilation();
        var diagnostics = compilation.GetDiagnostics();
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, diagnostics.Select(d => d.GetMessage())));
        }

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, emitResult.Diagnostics.Select(d => d.GetMessage())));
        }

        peStream.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(peStream.ToArray());
        SharedData.RegisterScript(scriptName, assembly);

        return CreateScriptProxy(assembly);
    }

    private dynamic CreateScriptProxy(Assembly assembly)
    {
        // Создаем динамический объект, который будет проксировать вызовы к классам из сборки
        dynamic proxy = new ExpandoObject();
        var proxyDict = (IDictionary<string, object>)proxy;

        foreach (var type in assembly.GetTypes().Where(t => t.IsPublic))
        {
            // Для каждого публичного класса добавляем свойство в прокси
            proxyDict[type.Name] = Activator.CreateInstance(type);
        }

        return proxy;
    }
}

public class Program
{
    private const string ExtensionsFolder = "Extensions";

    public static async Task Main(string[] args)
    {
        MetadataCache.Initialize();
        AliasManager.Initialize();
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