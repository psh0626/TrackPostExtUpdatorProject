using Microsoft.Win32;

namespace TrackPostExtUpdator;

internal static class BrowserChecker
{
    internal static string GetDefaultBrowser()
    {
        const string userChoicePath = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice";
        const string progIdValue = "ProgId";

        // Try to get the ProgId for the user-specific default browser.
        using var userChoiceKey = Registry.CurrentUser.OpenSubKey(userChoicePath);
        if (userChoiceKey == null)
            return GetSystemDefault();

        var progId = userChoiceKey.GetValue(progIdValue);
        if (progId == null)
            return GetSystemDefault();

        // Use the ProgId to find the command to open the default browser.
        string commandPath = $@"{progId}\shell\open\command";
        using var commandKey = Registry.ClassesRoot.OpenSubKey(commandPath);
        if (commandKey == null)
            return GetSystemDefault();

        var command = commandKey.GetValue(string.Empty);
        return command?.ToString() ?? GetSystemDefault();

    }

    static string GetSystemDefault()
    {
        const string httpKeyPath = @"http\shell\open\command";
        using var httpKey = Registry.ClassesRoot.OpenSubKey(httpKeyPath);
        if (httpKey == null)
            return string.Empty;

        var command = httpKey.GetValue(string.Empty);
        return command?.ToString() ?? string.Empty;
    }

    internal static string ExtractExecutablePath(string command)
    {
        if (command.StartsWith('"'))
        {
            int endIndex = command.IndexOf('"', 1);
            if (endIndex > 0)
            {
                return command[1..endIndex];
            }
        }
        else
        {
            int endIndex = command.IndexOf(' ');
            if (endIndex > 0)
            {
                return command[..endIndex];
            }
        }

        return command;
    }

}
