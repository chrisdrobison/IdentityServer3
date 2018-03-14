#tool "nuget:?package=xunit.runner.console"
#tool "nuget:?package=ilmerge"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var version = Argument("version", "3.0.0.0");
var preRelease = Argument<string>("preRelease", null);

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var srcDirectory = "./source";
var outputDirectory = "./build";
var distDirectory = "./distribution";
var solution = $"{srcDirectory}/IdentityServer3.AccessTokenValidation.sln";
var ilMerge = $"{srcDirectory}/packages/ILMerge.2.13.0307/ILMerge.exe";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(outputDirectory);
    CleanDirectory(distDirectory);
    MSBuild(solution, settings => settings
        .SetConfiguration(configuration)
        .SetVerbosity(Verbosity.Minimal)
        .WithTarget("Clean"));
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore(solution);
});

Task("Update-Version")
    .Does(() =>
{
    CreateAssemblyInfo($"{srcDirectory}/VersionAssemblyInfo.cs", new AssemblyInfoSettings {
        Version = version,
        FileVersion = version
    });
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Update-Version")
    .Does(() =>
{
    MSBuild(solution, settings => settings
        .SetConfiguration(configuration)
        .SetVerbosity(Verbosity.Minimal));
});

Task("RunTests")
    .IsDependentOn("Build")
    .Does(() =>
{
    XUnit2($"{outputDirectory}/AccessTokenValidation.Tests.dll");
});

Task("CreateNugetPackage")
    .IsDependentOn("Build")
    .Does(() =>
{
    var packageVersion = version;
    if (!string.IsNullOrEmpty(preRelease)) {
        packageVersion += $"-{preRelease}";
    }
    NuGetPack($"{srcDirectory}/AccessTokenValidation/AccessTokenValidation.csproj", new NuGetPackSettings() {
        Version = packageVersion,
        OutputDirectory = distDirectory
    });
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
