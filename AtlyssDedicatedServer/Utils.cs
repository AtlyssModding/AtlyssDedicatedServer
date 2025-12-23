using System.Text.RegularExpressions;

namespace AtlyssDedicatedServer;

public static class Utils
{
    public static bool TryParseInterval(string text, out int seconds)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            seconds = 0;
            return false;
        }
        
        const string regex = @"^\s*(?:([0-9]+)d)?\s*(?:([0-9]+)h)?\s*(?:([0-9]+)m)?\s*(?:([0-9]+)s)?\s*$";
        var restartInMatch = Regex.Match(text, regex);
        var hasDays = restartInMatch.Groups[1].Success;
        var hasHours = restartInMatch.Groups[2].Success;
        var hasMinutes = restartInMatch.Groups[3].Success;
        var hasSeconds = restartInMatch.Groups[4].Success;
        
        seconds = 0;

        if (!restartInMatch.Success || !(hasDays || hasHours || hasMinutes || hasSeconds))
            return false;
        
        seconds += hasDays ? int.Parse(restartInMatch.Groups[1].Value) * 86400 : 0;
        seconds += hasHours ? int.Parse(restartInMatch.Groups[2].Value) * 3600 : 0;
        seconds += hasMinutes ? int.Parse(restartInMatch.Groups[3].Value) * 60 : 0;
        seconds += hasSeconds ? int.Parse(restartInMatch.Groups[4].Value) : 0;
        return true;
    }

    public static string FormatInterval(int seconds, bool pretty = false)
    {
        if (seconds == 0)
            return pretty ? "0 second(s)" : "0s";
        
        var days = seconds / 86400;
        seconds = seconds % 86400;

        var hours = seconds / 3600;
        seconds = seconds % 3600;

        var minutes = seconds / 60;
        seconds = seconds % 60;

        var assembledString = "";

        if (days != 0)
            assembledString += pretty ? $"{days} day(s)" : $"{days}d";

        if (pretty && days != 0 && hours != 0)
            assembledString += ", ";
        
        if (hours != 0)
            assembledString += pretty ? $"{hours} hour(s)" : $"{hours}h";

        if (pretty && hours != 0 && minutes != 0)
            assembledString += ", ";

        if (minutes != 0)
            assembledString += pretty ? $"{minutes} minute(s)" : $"{minutes}m";

        if (pretty && minutes != 0 && seconds != 0)
            assembledString += ", ";

        if (seconds != 0)
            assembledString += pretty ? $"{seconds} second(s)" : $"{seconds}s";

        return assembledString;
    }
}