using System.Text.Json;

namespace Domain.Utilities;

public static class PrivateArgsHelper
{
    public static int GetInt(Dictionary<string, object> args, string key, int defaultValue = 0)
    {
        if (!args.TryGetValue(key, out var value))
            return defaultValue;

        return value switch
        {
            int intValue => intValue,
            JsonElement { ValueKind: JsonValueKind.Number } je => je.GetInt32(),
            JsonElement { ValueKind: JsonValueKind.String } je
                when int.TryParse(je.GetString(), out var parsed) => parsed,
            not null when int.TryParse(value.ToString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    public static long GetLong(Dictionary<string, object> args, string key, long defaultValue = 0)
    {
        if (!args.TryGetValue(key, out var value))
            return defaultValue;

        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            JsonElement { ValueKind: JsonValueKind.Number } je => je.GetInt64(),
            JsonElement { ValueKind: JsonValueKind.String } je
                when long.TryParse(je.GetString(), out var parsed) => parsed,
            not null when long.TryParse(value.ToString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    public static void SetTimestamp(Dictionary<string, object> args, string key)
    {
        args[$"ts_{key}"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public static long GetTimestamp(Dictionary<string, object> args, string key)
    {
        return GetLong(args, $"ts_{key}");
    }

    public static string GetString(Dictionary<string, object> args, string key, string defaultValue = "")
    {
        if (!args.TryGetValue(key, out var value))
            return defaultValue;

        return value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString() ?? defaultValue,
            not null => value.ToString() ?? defaultValue,
            _ => defaultValue
        };
    }
}
