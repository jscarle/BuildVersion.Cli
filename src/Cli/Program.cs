using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BuildVersion.Cli;

internal static partial class Program
{
    private const string BuildVersionEnvironmentVariable = "BUILD_VERSION";

    public static async Task Main(string[] args)
    {
        var rootCommand = new RootCommand("A command line tool to emit automatic build versions in a CI/CD pipeline.");
        var environment = CreateEnvironmentOption(rootCommand);
        var output = CreateOutputOption(rootCommand);
        var baseOverride = CreateBaseOverrideOption(rootCommand);
        var majorOverride = CreateMajorOverrideOption(rootCommand);
        var minorOverride = CreateMinorOverrideOption(rootCommand);
        var patchOverride = CreatePatchOverrideOption(rootCommand);
        var buildOverride = CreateBuildOverrideOption(rootCommand);
        rootCommand.SetAction(parseResult =>
        {
            GetBuildVersion(
                parseResult.GetRequiredValue(environment),
                parseResult.GetValue(baseOverride),
                parseResult.GetValue(majorOverride),
                parseResult.GetValue(minorOverride),
                parseResult.GetValue(patchOverride),
                parseResult.GetValue(buildOverride),
                parseResult.GetRequiredValue(output));
        });
        var parseResult = rootCommand.Parse(args);
        var exitCode = await parseResult.InvokeAsync(new InvocationConfiguration()).ConfigureAwait(false);
        System.Environment.Exit(exitCode);
    }

    private static void GetBuildVersion(Environment environment, string? baseOverride, int? majorOverride, int? minorOverride, string? patchOverride, int? buildOverride, Output output)
    {
        var now = GetDate();

        var major = now.Year % 100;
        var minor = ISOWeek.GetWeekOfYear(now);
        var patch = 0;
        var build = GetBuildVersion(now);

        if (!string.IsNullOrWhiteSpace(baseOverride))
        {
            var match = VersionRegex().Match(baseOverride);
            if (match.Success)
            {
                major = int.Parse(match.Groups[1].Value, NumberFormatInfo.InvariantInfo);
                if (!string.IsNullOrWhiteSpace(match.Groups[2].Value))
                {
                    minor = int.Parse(match.Groups[2].Value, NumberFormatInfo.InvariantInfo);
                }

                if (!string.IsNullOrWhiteSpace(match.Groups[3].Value))
                {
                    patch = int.Parse(match.Groups[3].Value, NumberFormatInfo.InvariantInfo);
                }
            }
        }

        if (majorOverride.HasValue)
            major = majorOverride.Value;
        if (minorOverride.HasValue)
            minor = minorOverride.Value;
        if (patchOverride == "auto")
            patch = ISOWeek.GetWeekOfYear(now);
        else
        {
            if (patchOverride is not null && int.TryParse(patchOverride, out var parsedPatchValue))
                patch = parsedPatchValue;
        }
        if (buildOverride.HasValue)
            build = buildOverride.Value;

        var version = GetVersion(environment, major, minor, patch, build);

        switch (output)
        {
            case Output.Plain:
                Console.WriteLine($"Build version is {version}.");
                break;
            case Output.GitHub:
                WriteGitHubVariable(BuildVersionEnvironmentVariable, version);
                break;
            case Output.DevOps:
                WriteVsoVariable(BuildVersionEnvironmentVariable, version);
                break;
            default:
                Console.Error.WriteLine($"Unknown output: {output}");
                System.Environment.Exit(1);
                return;
        }
    }

    private static string GetVersion(Environment environment, int major, int minor, int patch, int build)
    {
        return environment switch
        {
            Environment.Development => $"{major}.{minor}.{patch}-dev.{build}",
            Environment.Staging => $"{major}.{minor}.{patch}-rc.{build}",
            Environment.Test => $"{major}.{minor}.{patch}-test.{build}",
            Environment.Production => $"{major}.{minor}.{patch}.{build}",
            _ => throw new NotImplementedException($"Environment '{environment}' has not been implemented.")
        };
    }

    private static int GetBuildVersion(DateTime now)
    {
        var startOfWeek = GetStartOfWeek(now);
        var timeSinceStart = now.Subtract(startOfWeek);
        var minutes = (int)timeSinceStart.TotalMinutes;
        return minutes;
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        var year = ISOWeek.GetYear(date);
        var week = ISOWeek.GetWeekOfYear(date);
        var startOfWeek = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
        return startOfWeek;
    }

    private static DateTime GetDate()
    {
        const string canadianEstTimeZoneId = "America/Toronto";

        var canadianEstTimeZone = TimeZoneInfo.FindSystemTimeZoneById(canadianEstTimeZoneId);
        var currentTimeUtc = DateTimeOffset.UtcNow;
        var currentTimeInEst = TimeZoneInfo.ConvertTime(currentTimeUtc, canadianEstTimeZone);

        return currentTimeInEst.DateTime;
    }

    private static Option<Environment> CreateEnvironmentOption(Command command)
    {
        var option = new Option<Environment>("--environment")
        {
            Description = "Environment to generate the build version for.",
            CustomParser = arg => Enum.Parse<Environment>(arg.Tokens.Single().Value, true),
            Required = true
        };
        command.Add(option);
        return option;
    }

    private static Option<string?> CreateBaseOverrideOption(Command command)
    {
        var option = new Option<string?>("--base")
        {
            Description = "Override the automatically generated version using the base version",
            CustomParser = arg => arg.Tokens.Single().Value
        };
        option.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string?>();
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (!VersionRegex().IsMatch(value))
                result.AddError("Not a valid version string.");
        });
        command.Add(option);
        return option;
    }

    private static Option<int?> CreateMajorOverrideOption(Command command)
    {
        var option = new Option<int?>("--major")
        {
            Description = "Override the automatically generated major version"
        };
        option.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<int?>();
            if (value is < 0)
                result.AddError("Major version cannot be negative.");
        });
        command.Add(option);
        return option;
    }

    private static Option<int?> CreateMinorOverrideOption(Command command)
    {
        var option = new Option<int?>("--minor")
        {
            Description = "Override the automatically generated minor version"
        };
        option.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<int?>();
            if (value is < 0)
                result.AddError("Minor version cannot be negative.");
        });
        command.Add(option);
        return option;
    }

    private static Option<string?> CreatePatchOverrideOption(Command command)
    {
        var option = new Option<string?>("--patch")
        {
            Description = "Override the patch version or use 'auto' to set it to the current week of the year"
        };
        option.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string?>();
            if (string.IsNullOrWhiteSpace(value) || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return;

            if (!int.TryParse(value, out var intValue))
                result.AddError("Patch must be a number or 'auto'.");
            else if (intValue < 0)
                result.AddError("Patch version cannot be negative.");
        });
        command.Add(option);
        return option;
    }

    private static Option<int?> CreateBuildOverrideOption(Command command)
    {
        var option = new Option<int?>("--build")
        {
            Description = "Override the automatically generated build version"
        };
        option.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<int?>();
            if (value is < 0)
                result.AddError("Build version cannot be negative.");
        });
        command.Add(option);
        return option;
    }

    private static Option<Output> CreateOutputOption(Command command)
    {
        var outputOption = new Option<Output>("--output")
        {
            Description = "The output format of the build version.",
            CustomParser = arg => arg.Tokens.Count == 0 ? Output.Plain : Enum.Parse<Output>(arg.Tokens.Single().Value, true),
            DefaultValueFactory = _ => Output.Plain
        };
        command.Add(outputOption);
        return outputOption;
    }
    
    private static void WriteGitHubVariable(string name, string value)
    {
        Console.WriteLine($"{name}={value}");

        var envPath = System.Environment.GetEnvironmentVariable("GITHUB_ENV");
        if (!string.IsNullOrEmpty(envPath))
            File.AppendAllText(envPath, $"{name}={value}{System.Environment.NewLine}");

        var outputPath = System.Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrEmpty(outputPath))
            File.AppendAllText(outputPath, $"{name}={value}{System.Environment.NewLine}");
    }

    private static void WriteVsoVariable(string name, string value)
    {
        Console.WriteLine($"{name}: {value}");
        Console.WriteLine($"##vso[task.setvariable variable={name}]{value}");
        Console.WriteLine($"##vso[task.setvariable variable={name};isoutput=true]{value}");
    }

    private enum Environment
    {
        Development = 0,
        Staging = 1,
        Test = 2,
        Production = 3,
    }

    private enum Output
    {
        Plain = 0,
        GitHub = 1,
        DevOps = 2,
    }

    [GeneratedRegex(@"^(\d+)(?:\.(\d+))?(?:\.(\d+))?$")]
    private static partial Regex VersionRegex();
}
