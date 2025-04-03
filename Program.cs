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
using System.Threading.Tasks;
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
        Console.ResetColor();

        Console.SetCursorPosition(Math.Min(cursorPosition + 2, Console.WindowWidth - 1), inputRow);
    }

    public static void ClearInputLine()
    {
        Console.SetCursorPosition(0, inputRow);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, inputRow);
    }

    public static void WriteResponse(string message)
    {
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

    public static void WriteError(string message)
    {
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
            ConsoleHelper.WriteError($"Ошибка загрузки проверенных хешей: {ex.Message}");
            _verifiedHashes = new Dictionary<string, string>();
            _hashesLoaded = true; // Чтобы не пытаться загружать снова
        }
    }

    public static bool IsExtensionVerified(string fileName)
    {
        if (!_hashesLoaded || !_verifiedHashes.Any())
            return false;

        // Нормализуем имя файла для сравнения
        var normalizedFileName = fileName.ToLowerInvariant();

        return _verifiedHashes.Any(kv =>
            kv.Key.ToLowerInvariant() == normalizedFileName);
    }

    public static string? GetVerifiedHash(string fileName)
    {
        var normalizedFileName = fileName.ToLowerInvariant();
        return _verifiedHashes.FirstOrDefault(kv =>
            kv.Key.ToLowerInvariant() == normalizedFileName).Value;
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
                ConsoleHelper.WriteResponse(
                    $"Доступно обновление {latestVersion} (текущая версия {CurrentVersion})\n" +
                    $"Скачать: {release.html_url}\n" +
                    $"Описание: {release.body}\n" +
                    $"Для установки введите: update install"
                );
            }
            else if (notifyIfNoUpdate)
            {
                ConsoleHelper.WriteResponse($"У вас установлена актуальная версия {CurrentVersion}");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError("Ошибка при проверке обновлений:\n" + $"{ex.Message}");
        }
    }

    public static async Task InstallUpdateAsync()
    {
        try
        {
            ConsoleHelper.WriteResponse("Начинаем процесс обновления...");

            var response = await _httpClient.GetStringAsync(GitHubReleasesUrl);
            dynamic release = JsonConvert.DeserializeObject(response);
            string downloadUrl = release.assets[0].browser_download_url;
            string version = release.tag_name;

            string tempDir = Path.Combine(Path.GetTempPath(), "ConsoleAppUpdate");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            ConsoleHelper.WriteResponse("Скачивание обновления...");
            string zipPath = Path.Combine(tempDir, "update.zip");
            using (var client = new WebClient())
            {
                await client.DownloadFileTaskAsync(new Uri(downloadUrl), zipPath);
            }

            ConsoleHelper.WriteResponse("Распаковка обновления...");
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
            string currentDir = Path.GetDirectoryName(currentExePath);
            string backupDir = Path.Combine(currentDir, "Backup_" + DateTime.Now.ToString("yyyyMMddHHmmss"));

            ConsoleHelper.WriteResponse("Создание резервной копии...");
            Directory.CreateDirectory(backupDir);
            foreach (var file in Directory.GetFiles(currentDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (!file.EndsWith(".dll") && !file.EndsWith(".exe")) continue;
                File.Copy(file, Path.Combine(backupDir, Path.GetFileName(file)));
            }

            ConsoleHelper.WriteResponse("Установка обновления...");
            foreach (var file in Directory.GetFiles(tempDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                string destPath = Path.Combine(currentDir, Path.GetFileName(file));
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(file, destPath);
            }

            ConsoleHelper.WriteResponse("Завершение установки...");
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
            ConsoleHelper.WriteError($"Ошибка при установке обновления: {ex.Message}");
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
            ConsoleHelper.WriteError("Укажите имя команды или файла дополнения для включения");
            return;
        }

        string disabledFile;
        string enabledFile;

        if (name.EndsWith(".csx.disable", StringComparison.OrdinalIgnoreCase))
        {
            disabledFile = Path.Combine(_extensionsPath, name);
            if (!File.Exists(disabledFile))
            {
                ConsoleHelper.WriteError($"Файл '{name}' не найден");
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
                    ConsoleHelper.WriteError($"Не найдено отключенных дополнений для команды/файла '{commandName}'");
                    return;
                }
            }

            if (disabledFiles.Count > 1)
            {
                ConsoleHelper.WriteResponse($"Найдено несколько отключенных дополнений для команды '{commandName}':");
                foreach (var file in disabledFiles)
                {
                    ConsoleHelper.WriteResponse($"- {Path.GetFileName(file)}");
                }
                ConsoleHelper.WriteError("Уточните имя файла для включения");
                return;
            }

            disabledFile = disabledFiles.First();
            enabledFile = Path.Combine(_extensionsPath, Path.GetFileNameWithoutExtension(disabledFile));
        }

        File.Move(disabledFile, enabledFile);
        ConsoleHelper.WriteResponse($"Дополнение '{Path.GetFileName(enabledFile)}' включено");
    }

    public async Task DisableExtensionAsync(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            ConsoleHelper.WriteError("Укажите имя команды или файла дополнения для отключения");
            return;
        }

        string sourceFile;
        string disabledFile;

        if (name.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
        {
            sourceFile = Path.Combine(_extensionsPath, name);
            if (!File.Exists(sourceFile))
            {
                ConsoleHelper.WriteError($"Файл '{name}' не найден");
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
                    ConsoleHelper.WriteResponse($"Дополнение '{Path.GetFileName(sourceFile)}' отключено");
                    return;
                }

                ConsoleHelper.WriteError($"Команда/файл '{commandName}' не найдена");
                return;
            }

            if (sourceFiles.Count > 1)
            {
                ConsoleHelper.WriteResponse($"Найдено несколько дополнений для команды '{commandName}':");
                foreach (var file in sourceFiles)
                {
                    ConsoleHelper.WriteResponse($"- {Path.GetFileName(file)}");
                }
                ConsoleHelper.WriteError("Уточните имя файла для отключения");
                return;
            }

            sourceFile = sourceFiles.First();
            disabledFile = sourceFile + ".disable";
        }

        File.Move(sourceFile, disabledFile);
        ConsoleHelper.WriteResponse($"Дополнение '{Path.GetFileName(sourceFile)}' отключено");
    }

    public async Task ImportFromUrlAsync(string url)
    {
        try
        {
            ConsoleHelper.WriteResponse($"Загрузка дополнения из URL: {url}");

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var fileName = Path.GetFileName(url) ?? $"extension_{DateTime.Now:yyyyMMddHHmmss}.csx";
            var filePath = Path.Combine(_extensionsPath, fileName);

            // Скачиваем файл
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream);

            ConsoleHelper.WriteResponse($"Дополнение успешно загружено: {fileName}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Ошибка загрузки дополнения: {ex.Message}");
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
                ConsoleHelper.WriteError($"Ошибка загрузки команды из файла {Path.GetFileName(file)}: {ex.Message}");
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
            ConsoleHelper.WriteError($"Ошибка компиляции скрипта: {string.Join(Environment.NewLine, ex.Diagnostics)}");
            return Enumerable.Empty<ICommand>();
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Ошибка выполнения скрипта: {ex.Message}");
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
        private readonly HashSet<string> _builtInCommands = new() { "help", "list", "reload", "clear", "restart", "time", "update", "new", "debug", "enable", "disable", "import" };

        public HelpCommand(CommandManager manager) => _manager = manager;
        public string Name => "help";
        public string Description => "Показывает справку по командам";
        public IEnumerable<string> Aliases => new[] { "?" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "help update, help new";

        public async Task ExecuteAsync(string[] args)
        {
            await VerifiedExtensionsChecker.EnsureHashesLoadedAsync();

            var response = new StringBuilder();
            var allCommands = _manager.GetAllCommands()
                .GroupBy(c => c.Name)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .ToList();

            // Встроенные команды
            response.AppendLine("Встроенные команды:");
            foreach (var cmd in allCommands.Where(c => _builtInCommands.Contains(c.Name)))
            {
                response.AppendLine($"  {FormatCommand(cmd)}");
            }

            // Проверенные команды
            var verifiedCommands = new List<ICommand>();
            foreach (var cmd in allCommands.Where(c => !_builtInCommands.Contains(c.Name)))
            {
                var fileName = $"{cmd.Name.ToLower()}.csx";
                if (VerifiedExtensionsChecker.IsExtensionVerified(fileName))
                {
                    verifiedCommands.Add(cmd);
                }
            }

            if (verifiedCommands.Any())
            {
                response.AppendLine("\nПроверенные команды (✅ безопасны):");
                foreach (var cmd in verifiedCommands)
                {
                    response.AppendLine($"  {FormatCommand(cmd)} ✅");
                }
            }

            // Сторонние команды
            var externalCommands = allCommands
                .Where(c => !_builtInCommands.Contains(c.Name) &&
                       !verifiedCommands.Contains(c))
                .ToList();

            if (externalCommands.Any())
            {
                response.AppendLine("\nСторонние команды (используйте с осторожностью):");
                foreach (var cmd in externalCommands)
                {
                    response.AppendLine($"  {FormatCommand(cmd)}");
                }
            }

            response.Append("\nДля подробной справки по команде введите: help <команда>");
            ConsoleHelper.WriteResponse(response.ToString());
        }

        private string FormatCommand(ICommand cmd)
        {
            var aliases = cmd.Aliases.Any()
                ? $" ({string.Join(", ", cmd.Aliases)})"
                : "";
            return $"{cmd.Name,-12}{aliases,-15} - {cmd.Description}";
        }
    }

    private class ListCommandsCommand : ICommand
    {
        private readonly CommandManager _manager;

        public ListCommandsCommand(CommandManager manager) => _manager = manager;
        public string Name => "list";
        public string Description => "Список всех доступных команд и их статус";
        public IEnumerable<string> Aliases => new[] { "ls", "dir" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => null;

        public async Task ExecuteAsync(string[] args)
        {
            await VerifiedExtensionsChecker.EnsureHashesLoadedAsync();

            var response = new StringBuilder();
            response.AppendLine("Доступные команды (основные и алиасы):");

            foreach (var cmd in _manager.GetAllCommands()
                .GroupBy(c => c.Name)
                .Select(g => g.First())
                .OrderBy(c => c.Name))
            {
                var fileName = $"{cmd.Name.ToLower()}.csx";
                var isVerified = VerifiedExtensionsChecker.IsExtensionVerified(fileName);
                var verifiedMark = isVerified ? " ✅" : "";

                response.AppendLine($"- {cmd.Name}{(cmd.Aliases.Any() ? $" (алиасы: {string.Join(", ", cmd.Aliases)})" : "")}{verifiedMark}");
                response.AppendLine($"  Версия: {cmd.Version}, Автор: {cmd.Author}");
                if (isVerified)
                {
                    response.AppendLine("  Проверено разработчиками");
                }
                if (!string.IsNullOrEmpty(cmd.UsageExample))
                {
                    response.AppendLine($"  Пример: {cmd.UsageExample}");
                }
                response.AppendLine();
            }

            var disabledFiles = Directory.GetFiles(_manager._extensionsPath, "*.csx.disable");
            if (disabledFiles.Any())
            {
                response.AppendLine("Отключенные дополнения:");
                foreach (var file in disabledFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var isVerified = VerifiedExtensionsChecker.IsExtensionVerified($"{fileName}.csx");
                    var verifiedMark = isVerified ? " ✅" : "";
                    response.AppendLine($"- {fileName}{verifiedMark}");
                }
                response.AppendLine("\nДля включения используйте: enable <имя_команды>");
            }

            ConsoleHelper.WriteResponse(response.ToString().TrimEnd());
        }
    }

    private class ReloadCommandsCommand : ICommand
    {
        private readonly CommandManager _manager;

        public ReloadCommandsCommand(CommandManager manager) => _manager = manager;
        public string Name => "reload";
        public string Description => "Перезагружает все команды из файлов";
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => null;

        public async Task ExecuteAsync(string[] args)
        {
            ConsoleHelper.WriteResponse("Перезагрузка команд...");
            await _manager.LoadCommandsAsync();
            ConsoleHelper.WriteResponse("Команды успешно перезагружены");
        }
    }

    private class ClearCommand : ICommand
    {
        private readonly CommandManager _manager;

        public ClearCommand(CommandManager manager) => _manager = manager;
        public string Name => "clear";
        public string Description => "Очищает консоль";
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
        public string Description => "Полностью перезапускает приложение";
        public IEnumerable<string> Aliases => new[] { "reboot" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => null;

        public Task ExecuteAsync(string[] args)
        {
            ConsoleHelper.WriteResponse("Перезапуск приложения...");

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
        public string Description => "Показывает текущее время";
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => null;

        public Task ExecuteAsync(string[] args)
        {
            ConsoleHelper.WriteResponse($"Текущее время: {DateTime.Now:T}");
            return Task.CompletedTask;
        }
    }

    private class UpdateCommand : ICommand
    {
        public string Name => "update";
        public string Description => "Проверяет и устанавливает обновления приложения";
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "update install";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length > 0 && args[0].Equals("install", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleHelper.WriteResponse("Вы уверены, что хотите установить обновление? (y/n)");
                var response = Console.ReadLine();
                if (response.Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    await UpdateChecker.InstallUpdateAsync();
                }
                else
                {
                    ConsoleHelper.WriteResponse("Обновление отменено");
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
        public string Description => "Создает шаблон скрипта дополнения в папке Extensions";
        public IEnumerable<string> Aliases => new[] { "template" };
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "new mycommand";

        public Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("Укажите имя команды (например: new mycommand)");
                return Task.CompletedTask;
            }

            var commandName = args[0].ToLowerInvariant();
            var fileName = $"{commandName}.csx";
            var filePath = Path.Combine(_manager._extensionsPath, fileName);

            if (File.Exists(filePath))
            {
                ConsoleHelper.WriteError($"Файл {fileName} уже существует!");
                return Task.CompletedTask;
            }

            var template = $@"// Пример скрипта дополнения для команды '{commandName}'
// Удалите комментарии и реализуйте свою команду

public class {char.ToUpper(commandName[0]) + commandName.Substring(1)}Command : ICommand
{{
    public string Name => ""{commandName}"";
    public string Description => ""Описание команды {commandName}"";
    public IEnumerable<string> Aliases => new[] {{ ""{commandName[0]}"", ""{commandName.Substring(0, Math.Min(3, commandName.Length))}"" }};
    public string Author => ""Ваше имя"";
    public string Version => ""1.0"";
    public string? UsageExample => ""{commandName} arg1 arg2\n{commandName} --option"";

    public async Task ExecuteAsync(string[] args)
    {{
        // Ваш код здесь
        Print(""Команда '{commandName}' выполнена!"");
        
        // Пример работы с аргументами
        if (args.Length > 0)
        {{
            Print($""Получены аргументы: {{string.Join("", "", args)}}"");
        }}
    }}
}}

// Возвращаем экземпляр команды
new {char.ToUpper(commandName[0]) + commandName.Substring(1)}Command()";

            File.WriteAllText(filePath, template);
            ConsoleHelper.WriteResponse($"Шаблон команды создан: {fileName}");
            ConsoleHelper.WriteResponse($"Для использования выполните: reload");

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
        public string Description => "Включает отключенное дополнение";
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "enable mycommand";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("Укажите имя команды или файла дополнения для включения (например: enable mycommand или enable myextension.csx.disable)");
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
        public string Description => "Отключает дополнение";
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "disable mycommand";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("Укажите имя команды или файла дополнения для отключения (например: disable mycommand или disable myextension.csx)");
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
        public string Description => "Запускает команду в режиме отладки";
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "debug mycommand --args \"test\"";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("Укажите команду для отладки (например: debug mycommand --args \"test\")");
                return;
            }

            var commandName = args[0];
            var commandArgs = ParseDebugArgs(args.Skip(1).ToArray());

            var command = _manager.GetCommand(commandName);
            if (command == null)
            {
                ConsoleHelper.WriteError($"Команда '{commandName}' не найдена");
                return;
            }

            ConsoleHelper.WriteDebug($"Запуск {commandName} с аргументами: {string.Join(" ", commandArgs)}");
            ConsoleHelper.WriteDebug($"Переменные: args = {JsonConvert.SerializeObject(commandArgs)}");

            try
            {
                var stopwatch = Stopwatch.StartNew();
                await command.ExecuteAsync(commandArgs);
                stopwatch.Stop();

                ConsoleHelper.WriteDebug($"Команда выполнена за {stopwatch.ElapsedMilliseconds} мс");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteDebug($"Ошибка выполнения: {ex.GetType().Name}: {ex.Message}");
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
        public string Description => "Загружает и устанавливает дополнение из указанного URL";
        public IEnumerable<string> Aliases => Enumerable.Empty<string>();
        public string Author => "System";
        public string Version => "1.0";
        public string? UsageExample => "import https://example.com/extension.csx";

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                ConsoleHelper.WriteError("Укажите URL дополнения для загрузки (например: import https://example.com/extension.csx)");
                return;
            }

            var url = args[0];
            await _extensionManager.ImportFromUrlAsync(url);
            await _manager.LoadCommandsAsync();
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
    public void DebugVar(string name, object value) => ConsoleHelper.WriteDebug($"Переменная: {name} = {JsonConvert.SerializeObject(value)}");

    public CommandManager Manager { get; set; }
}

public class Program
{
    private const string ExtensionsFolder = "Extensions";

    public static async Task Main(string[] args)
    {
        Console.Title = "Mugs";
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        ConsoleHelper.Initialize();

        if (args.All(a => a != "--updated"))
        {
            ConsoleHelper.WriteResponse("Проверка обновлений...");
            await UpdateChecker.CheckForUpdatesAsync();
        }
        else
        {
            ConsoleHelper.WriteResponse("Приложение успешно обновлено!");
        }

        var manager = new CommandManager(ExtensionsFolder);
        await manager.LoadCommandsAsync();

        ConsoleHelper.WriteResponse("Консольное приложение с динамической загрузкой команд" + "\n" + "Введите 'help' для списка команд или 'exit' для выхода");

        while (true)
        {
            var input = ConsoleHelper.ReadLineWithColorHighlighting(manager);

            if (string.IsNullOrEmpty(input)) continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var commandName = parts[0];
            var commandArgs = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();

            var command = manager.GetCommand(commandName);
            if (command == null)
            {
                ConsoleHelper.WriteError($"Команда '{commandName}' не найдена. Введите 'help' для списка команд");
                continue;
            }

            try
            {
                await command.ExecuteAsync(commandArgs);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Ошибка выполнения команды: {ex.Message}");
            }
        }
    }
}