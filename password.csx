public class PasswordCommand : ICommand
{
    public string Name => "password";
    public string Description => "Генератор сложных паролей";
    public IEnumerable<string> Aliases => new[] { "pass" };
    public string Author => "Shead";
    public string Version => "1.0";
    public string? UsageExample => "password 12 --special";

    public Task ExecuteAsync(string[] args)
    {
        int length = 12;
        bool useSpecial = false;

        if (args.Length > 0) int.TryParse(args[0], out length);
        if (args.Contains("--special")) useSpecial = true;

        var password = GeneratePassword(length, useSpecial);
        ConsoleHelper.WriteResponse($"Сгенерированный пароль: {password}");
        return Task.CompletedTask;
    }

    private string GeneratePassword(int length, bool useSpecial)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        const string specials = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        var validChars = useSpecial ? chars + specials : chars;
        var rnd = new Random();

        return new string(Enumerable.Repeat(validChars, length)
            .Select(s => s[rnd.Next(s.Length)]).ToArray());
    }
}

return new PasswordCommand();