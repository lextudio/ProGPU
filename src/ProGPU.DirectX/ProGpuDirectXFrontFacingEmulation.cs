using System.Text.RegularExpressions;

namespace ProGPU.DirectX;

internal static class ProGpuDirectXFrontFacingEmulation
{
    private static readonly Regex s_frontFacingDeclarationRegex = new(
        @"@builtin\(front_facing\)\s+(?<name>[A-Za-z_]\w*)\s*:\s*bool\s*,?",
        RegexOptions.Compiled);

    private static readonly Regex s_emptyStructRegex = new(
        @"struct\s+(?<name>[A-Za-z_]\w*)\s*\{\s*\}\s*",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static bool TryCreateOverrideSource(string source, bool isFrontFacing, out string overrideSource)
    {
        overrideSource = string.Empty;
        var matches = s_frontFacingDeclarationRegex.Matches(source);
        if (matches.Count == 0)
        {
            return false;
        }

        var names = GetDistinctGroupValues(matches, "name");
        var constant = isFrontFacing ? "true" : "false";
        overrideSource = s_frontFacingDeclarationRegex.Replace(source, string.Empty);
        overrideSource = RemoveEmptyStructParameters(overrideSource);

        for (var nameIndex = 0; nameIndex < names.Count; nameIndex++)
        {
            var name = names[nameIndex];
            overrideSource = Regex.Replace(
                overrideSource,
                $@"\b[A-Za-z_]\w*\.{Regex.Escape(name)}\b",
                constant);
            overrideSource = Regex.Replace(
                overrideSource,
                $@"\b{Regex.Escape(name)}\b",
                constant);
        }

        overrideSource = Regex.Replace(overrideSource, @"\(\s*,", "(");
        overrideSource = Regex.Replace(overrideSource, @",\s*,", ",");
        overrideSource = Regex.Replace(overrideSource, @",\s*\)", ")");
        return !overrideSource.Contains("@builtin(front_facing)", StringComparison.Ordinal);
    }

    private static string RemoveEmptyStructParameters(string source)
    {
        var emptyStructNames = GetDistinctGroupValues(s_emptyStructRegex.Matches(source), "name");
        if (emptyStructNames.Count == 0)
        {
            return source;
        }

        var result = s_emptyStructRegex.Replace(source, string.Empty);
        for (var structNameIndex = 0; structNameIndex < emptyStructNames.Count; structNameIndex++)
        {
            var structName = emptyStructNames[structNameIndex];
            result = Regex.Replace(
                result,
                $@"(?<prefix>,\s*)?[A-Za-z_]\w*\s*:\s*{Regex.Escape(structName)}\s*(?<suffix>,\s*)?",
                match => match.Groups["prefix"].Success && match.Groups["suffix"].Success ? ", " : string.Empty);
        }

        return result;
    }

    private static List<string> GetDistinctGroupValues(MatchCollection matches, string groupName)
    {
        var values = new List<string>(matches.Count);
        for (var matchIndex = 0; matchIndex < matches.Count; matchIndex++)
        {
            var value = matches[matchIndex].Groups[groupName].Value;
            if (!ContainsOrdinal(values, value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static bool ContainsOrdinal(IReadOnlyList<string> values, string value)
    {
        for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
        {
            if (string.Equals(values[valueIndex], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
