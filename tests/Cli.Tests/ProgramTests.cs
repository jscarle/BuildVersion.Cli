using System.Reflection;
using System.Text.RegularExpressions;
using Shouldly;

namespace BuildVersion.Cli.Tests;

public class ProgramTests
{
    [Fact]
    public void CalculatesStartOfWeekUsingIsoWeekRules()
    {
        var midWeekDate = new DateTime(2025, 2, 5, 12, 0, 0, DateTimeKind.Unspecified);

        var startOfWeek = InvokeGetStartOfWeek(midWeekDate);

        startOfWeek.ShouldBe(new DateTime(2025, 2, 3));
    }

    [Theory]
    [InlineData(2024, 12, 31, 0, 0, 0, 2024, 12, 30)] // ISO week 1 of 2025 starts on 2024-12-30
    [InlineData(2023, 1, 1, 10, 30, 0, 2022, 12, 26)]
    [InlineData(2025, 5, 13, 8, 0, 0, 2025, 5, 12)]
    [InlineData(2024, 2, 29, 0, 0, 0, 2024, 2, 26)]
    [InlineData(2000, 2, 29, 12, 30, 0, 2000, 2, 28)]
    [InlineData(1970, 1, 1, 0, 0, 0, 1969, 12, 29)]
    [InlineData(2030, 12, 31, 23, 59, 59, 2030, 12, 30)]
    [InlineData(1999, 12, 31, 6, 0, 0, 1999, 12, 27)]
    [InlineData(2020, 2, 29, 14, 45, 0, 2020, 2, 24)]
    [InlineData(2025, 1, 1, 0, 0, 0, 2024, 12, 30)]
    public void CalculatesStartOfWeekForBoundaryDates(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var date = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

        var startOfWeek = InvokeGetStartOfWeek(date);

        startOfWeek.ShouldBe(new DateTime(expectedYear, expectedMonth, expectedDay));
        startOfWeek.DayOfWeek.ShouldBe(DayOfWeek.Monday);
    }

    [Fact]
    public void CalculatesMinutesSinceStartOfWeekForBuildVersion()
    {
        var sampleDate = new DateTime(2025, 2, 3, 2, 15, 0, DateTimeKind.Unspecified);

        var buildVersion = InvokeGetBuildVersion(sampleDate);

        buildVersion.ShouldBe(135);
    }

    [Theory]
    [InlineData(2025, 2, 3, 0, 0, 0, 0)]
    [InlineData(2025, 2, 3, 12, 0, 0, 720)]
    [InlineData(2025, 2, 7, 23, 59, 0, 7199)]
    [InlineData(2024, 2, 29, 0, 0, 0, 4320)]
    [InlineData(2000, 2, 29, 12, 30, 0, 2190)]
    [InlineData(1970, 1, 1, 0, 0, 0, 4320)]
    [InlineData(2030, 12, 31, 23, 59, 59, 2879)]
    [InlineData(1999, 12, 31, 6, 0, 0, 6120)]
    [InlineData(2020, 2, 29, 14, 45, 0, 8085)]
    [InlineData(2025, 1, 1, 0, 0, 0, 2880)]
    public void CalculatesMinutesSinceStartOfWeekForMultipleDates(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        int expectedMinutes)
    {
        var date = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

        var buildVersion = InvokeGetBuildVersion(date);

        buildVersion.ShouldBe(expectedMinutes);
    }

    [Fact]
    public void VersionRegexMatchesOptionalMinorAndPatchSegments()
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

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("10.5")]
    [InlineData("2024")] // Supports major-only overrides
    public void VersionRegexMatchesCommonPatterns(string version)
    {
        var regex = InvokeVersionRegex();

        var match = regex.Match(version);

        match.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData("1.")]
    [InlineData("1.2.")]
    [InlineData("01.02a")]
    [InlineData("v1.0")]
    [InlineData("")]
    public void VersionRegexRejectsInvalidPatterns(string version)
    {
        var regex = InvokeVersionRegex();

        regex.IsMatch(version).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Development", 1, 2, 3, 4, "1.2.3-dev.4")]
    [InlineData("Staging", 1, 2, 3, 4, "1.2.3-rc.4")]
    [InlineData("Test", 1, 2, 3, 4, "1.2.3-test.4")]
    [InlineData("Production", 1, 2, 3, 4, "1.2.3.4")]
    public void GetVersionUsesExpectedSuffixForEachEnvironment(
        string environment,
        int major,
        int minor,
        int patch,
        int build,
        string expectedVersion)
    {
        var version = InvokeGetVersion(environment, major, minor, patch, build);

        version.ShouldBe(expectedVersion);
    }

    private static DateTime InvokeGetStartOfWeek(DateTime date)
    {
        var methodInfo = typeof(Program).GetMethod(
            "GetStartOfWeek",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(DateTime)],
            modifiers: null) ?? throw new MissingMethodException("Program", "GetStartOfWeek");

        return (DateTime)methodInfo.Invoke(null, [date])!;
    }

    private static int InvokeGetBuildVersion(DateTime date)
    {
        var methodInfo = typeof(Program).GetMethod(
            "GetBuildVersion",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(DateTime)],
            modifiers: null) ?? throw new MissingMethodException("Program", "GetBuildVersion(DateTime)");

        return (int)methodInfo.Invoke(null, [date])!;
    }

    private static Regex InvokeVersionRegex()
    {
        var methodInfo = typeof(Program).GetMethod(
            "VersionRegex",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [],
            modifiers: null) ?? throw new MissingMethodException("Program", "VersionRegex");

        return (Regex)(methodInfo.Invoke(null, null) ?? throw new InvalidOperationException("VersionRegex returned null."));
    }

    private static string InvokeGetVersion(string environment, int major, int minor, int patch, int build)
    {
        var programType = typeof(Program);
        var environmentType = programType.GetNestedType("Environment", BindingFlags.NonPublic) ?? throw new MissingMethodException("Program", "Environment");
        var parsedEnvironment = Enum.Parse(environmentType, environment, ignoreCase: true);

        var methodInfo = programType.GetMethod(
            "GetVersion",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [environmentType, typeof(int), typeof(int), typeof(int), typeof(int)],
            modifiers: null) ?? throw new MissingMethodException("Program", "GetVersion");

        return (string)(methodInfo.Invoke(null, [parsedEnvironment, major, minor, patch, build]) ?? throw new InvalidOperationException("GetVersion returned null."));
    }
}
