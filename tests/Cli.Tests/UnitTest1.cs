using System.Reflection;
using System.Text.RegularExpressions;
using BuildVersion.Cli;
using Shouldly;
using Xunit;

namespace BuildVersion.Cli.Tests;

public class ProgramTests
{
    [Fact]
    public void Calculates_start_of_week_using_iso_week_rules()
    {
        var midWeekDate = new DateTime(2025, 2, 5, 12, 0, 0, DateTimeKind.Unspecified);

        var startOfWeek = InvokeGetStartOfWeek(midWeekDate);

        startOfWeek.ShouldBe(new DateTime(2025, 2, 3));
    }

    [Fact]
    public void Calculates_minutes_since_start_of_week_for_build_version()
    {
        var sampleDate = new DateTime(2025, 2, 3, 2, 15, 0, DateTimeKind.Unspecified);

        var buildVersion = InvokeGetBuildVersion(sampleDate);

        buildVersion.ShouldBe(135);
    }

    [Fact]
    public void Version_regex_matches_optional_minor_and_patch_segments()
    {
        var regex = InvokeVersionRegex();

        var threePartMatch = regex.Match("5.12.3");
        threePartMatch.Success.ShouldBeTrue();
        threePartMatch.Groups[1].Value.ShouldBe("5");
        threePartMatch.Groups[2].Value.ShouldBe("12");
        threePartMatch.Groups[3].Value.ShouldBe("3");

        var twoPartMatch = regex.Match("7.1");
        twoPartMatch.Success.ShouldBeTrue();
        twoPartMatch.Groups[1].Value.ShouldBe("7");
        twoPartMatch.Groups[2].Value.ShouldBe("1");
        twoPartMatch.Groups[3].Value.ShouldBe(string.Empty);

        regex.IsMatch("alpha").ShouldBeFalse();
    }

    private static DateTime InvokeGetStartOfWeek(DateTime date)
    {
        var methodInfo = typeof(Program).GetMethod(
            "GetStartOfWeek",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(DateTime) },
            modifiers: null) ?? throw new MissingMethodException("Program", "GetStartOfWeek");

        return (DateTime)methodInfo.Invoke(null, new object[] { date })!;
    }

    private static int InvokeGetBuildVersion(DateTime date)
    {
        var methodInfo = typeof(Program).GetMethod(
            "GetBuildVersion",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(DateTime) },
            modifiers: null) ?? throw new MissingMethodException("Program", "GetBuildVersion(DateTime)");

        return (int)methodInfo.Invoke(null, new object[] { date })!;
    }

    private static Regex InvokeVersionRegex()
    {
        var methodInfo = typeof(Program).GetMethod(
            "VersionRegex",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: Array.Empty<Type>(),
            modifiers: null) ?? throw new MissingMethodException("Program", "VersionRegex");

        return (Regex)(methodInfo.Invoke(null, null) ?? throw new InvalidOperationException("VersionRegex returned null."));
    }
}
