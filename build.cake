#tool nuget:?package=GitVersion.CommandLine&version=5.0.0
#tool nuget:?package=GitReleaseManager&version=0.11.0

//////////////////////////////////////////////////////////////////////
// PROJECT-SPECIFIC CONSTANTS
//////////////////////////////////////////////////////////////////////

const string DEFAULT_VERSION = "2.0.0";

#load nuget:?package=TestCentric.Cake.Recipe&version=1.0.0-dev00017

//////////////////////////////////////////////////////////////////////
// ARGUMENTS  
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
 
//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

Setup<BuildSettings>((context) =>
{
	var settings = BuildSettings.Initialize
	(
		context: context,
		title: "Net20PluggableAgent",
		solutionFile: "net20-pluggable-agent.sln",
		unitTest: "net20-agent-launcher.tests.exe",
		nugetId: "NUnit.Extension.Net20PluggableAgent",
		chocoId: "nunit-extension-net20-pluggable-agent",
		guiVersion: "2.0.0-dev00081",
		githubOwner: "TestCentric",
		githubRepository: "net20-pluggable-agent",
		copyright: "Copyright (c) Charlie Poole and TestCentric Engine contributors."
	);

	Information($"Net20PluggableAgent {settings.Configuration} version {settings.PackageVersion}");

	if (BuildSystem.IsRunningOnAppVeyor)
		AppVeyor.UpdateBuildVersion(settings.PackageVersion);

	return settings;
});

//////////////////////////////////////////////////////////////////////
// BUILD PACKAGES
//////////////////////////////////////////////////////////////////////

Task("BuildNuGetPackage")
	.Does<BuildSettings>((settings) =>
	{
		CreateDirectory(settings.PackageDirectory);

		NuGetPack("nuget/Net20PluggableAgent.nuspec", new NuGetPackSettings()
		{
			Version = settings.PackageVersion,
			OutputDirectory = settings.PackageDirectory,
			NoPackageAnalysis = true
		});
	});

Task("BuildChocolateyPackage")
	.Does<BuildSettings>((settings) =>
	{
		CreateDirectory(settings.PackageDirectory);

		ChocolateyPack("choco/net20-pluggable-agent.nuspec", new ChocolateyPackSettings()
		{
			Version = settings.PackageVersion,
			OutputDirectory = settings.PackageDirectory
		});
	});

//////////////////////////////////////////////////////////////////////
// INSTALL PACKAGES
//////////////////////////////////////////////////////////////////////

Task("InstallNuGetPackage")
	.Does<BuildSettings>((settings) =>
	{
		if (System.IO.Directory.Exists(settings.NuGetTestDirectory))
			DeleteDirectory(settings.NuGetTestDirectory,
				new DeleteDirectorySettings()
				{
					Recursive = true
				});

		CreateDirectory(settings.NuGetTestDirectory);

		Unzip(settings.NuGetPackage, settings.NuGetTestDirectory);

		Information($"  Installed {System.IO.Path.GetFileName(settings.NuGetPackage)}");
		Information($"    at {settings.NuGetTestDirectory}");
	});

Task("InstallChocolateyPackage")
	.Does<BuildSettings>((settings) =>
	{
		if (System.IO.Directory.Exists(settings.ChocolateyTestDirectory))
			DeleteDirectory(settings.ChocolateyTestDirectory,
				new DeleteDirectorySettings()
				{
					Recursive = true
				});

		CreateDirectory(settings.ChocolateyTestDirectory);

		Unzip(settings.ChocolateyPackage, settings.ChocolateyTestDirectory);

		Information($"  Installed {System.IO.Path.GetFileName(settings.ChocolateyPackage)}");
		Information($"    at {settings.ChocolateyTestDirectory}");
	});

//////////////////////////////////////////////////////////////////////
// CHECK PACKAGE CONTENT
//////////////////////////////////////////////////////////////////////

static readonly string[] LAUNCHER_FILES = {
	"net20-agent-launcher.dll", "nunit.engine.api.dll"
};

static readonly string[] AGENT_FILES = {
	"net20-pluggable-agent.exe", "net20-pluggable-agent.exe.config",
	"net20-pluggable-agent-x86.exe", "net20-pluggable-agent-x86.exe.config",
	"nunit.engine.api.dll", "testcentric.engine.core.dll"
};

Task("VerifyNuGetPackage")
	.IsDependentOn("InstallNuGetPackage")
	.Does<BuildSettings>((settings) =>
	{
		Check.That(settings.NuGetTestDirectory,
		HasFiles("LICENSE.txt", "CHANGES.txt"),
			HasDirectory("tools").WithFiles(LAUNCHER_FILES),
			HasDirectory("tools/agent").WithFiles(AGENT_FILES));

		Information("  SUCCESS: All checks were successful");
	});

Task("VerifyChocolateyPackage")
	.IsDependentOn("InstallChocolateyPackage")
	.Does<BuildSettings>((settings) =>
	{
		Check.That(settings.ChocolateyTestDirectory,
			HasDirectory("tools").WithFiles("LICENSE.txt", "CHANGES.txt", "VERIFICATION.txt").WithFiles(LAUNCHER_FILES),
			HasDirectory("tools/agent").WithFiles(AGENT_FILES));

		Information("  SUCCESS: All checks were successful");
	});

//////////////////////////////////////////////////////////////////////
// TEST PACKAGES
//////////////////////////////////////////////////////////////////////

Task("TestNuGetPackage")
	.IsDependentOn("InstallNuGetPackage")
	.Does<BuildSettings>((settings) =>
	{
		new NuGetPackageTester(settings).RunAllTests();
	});

Task("TestChocolateyPackage")
	.IsDependentOn("InstallChocolateyPackage")
	.Does<BuildSettings>((settings) =>
	{
		new ChocolateyPackageTester(settings).RunAllTests();
	});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Package")
	.IsDependentOn("Build")
	.IsDependentOn("PackageNuGet")
    .IsDependentOn("PackageChocolatey");

Task("PackageNuGet")
	.IsDependentOn("BuildNuGetPackage")
	.IsDependentOn("VerifyNuGetPackage")
	.IsDependentOn("TestNuGetPackage");

Task("PackageChocolatey")
	.IsDependentOn("BuildChocolateyPackage")
	.IsDependentOn("VerifyChocolateyPackage")
	.IsDependentOn("TestChocolateyPackage");

Task("Appveyor")
	.IsDependentOn("Build")
	.IsDependentOn("UnitTests")
	.IsDependentOn("Package")
	.IsDependentOn("Publish")
	.IsDependentOn("CreateDraftRelease")
	.IsDependentOn("CreateProductionRelease");

Task("Full")
	.IsDependentOn("Build")
	.IsDependentOn("UnitTests")
	.IsDependentOn("Package");

//Task("Travis")
//	.IsDependentOn("Build")
//	.IsDependentOn("RunTests");

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
