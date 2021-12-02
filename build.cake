#tool nuget:?package=GitVersion.CommandLine&version=5.0.0

//////////////////////////////////////////////////////////////////////
// PROJECT-SPECIFIC CONSTANTS
//////////////////////////////////////////////////////////////////////

const string NUGET_ID = "NUnit.Extension.Net20PluggableAgent";
const string CHOCO_ID = "nunit-extension-net20-pluggable-agent";

const string SOLUTION_FILE = "net20-pluggable-agent.sln";
const string OUTPUT_ASSEMBLY = "net20-pluggable-agent.dll";
const string UNIT_TEST_ASSEMBLY = "net20-agent-launcher.tests.exe";
const string MOCK_ASSEMBLY = "mock-assembly.dll";

const string DEFAULT_VERSION = "2.0.0";

#load nuget:?package=TestCentric.Cake.Recipe&version=1.0.0-dev00009

//////////////////////////////////////////////////////////////////////
// ARGUMENTS  
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

// Additional Arguments Defined by TestCentric.Cake.Recipe
//
// --configuration=CONFIGURATION (parameters.cake)
//     Sets the configuration (default is specified in DEFAULT_CONFIGURATION)
//
// --packageVersion=VERSION (versioning.cake)
//     Bypasses GitVersion and causes the specified version to be used instead.
  
//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

Setup<BuildParameters>((context) =>
{
	var parameters = new BuildParameters(context);
	parameters.GuiVersion = "2.0.0-dev00081";

	Information($"Net20PluggableAgent {parameters.Configuration} version {parameters.PackageVersion}");

	if (BuildSystem.IsRunningOnAppVeyor)
		AppVeyor.UpdateBuildVersion(parameters.PackageVersion);

	return parameters;
});

//////////////////////////////////////////////////////////////////////
// CLEAN
//////////////////////////////////////////////////////////////////////

Task("Clean")
	.Does<BuildParameters>((parameters) =>
	{
		Information("Cleaning " + parameters.OutputDirectory);
		CleanDirectory(parameters.OutputDirectory);

		Information("Cleaning " + parameters.PackageTestDirectory);
		CleanDirectory(parameters.PackageTestDirectory);
	});

//////////////////////////////////////////////////////////////////////
// DELETE ALL OBJ DIRECTORIES
//////////////////////////////////////////////////////////////////////

Task("DeleteObjectDirectories")
	.Does(() =>
	{
		Information("Deleting object directories");

		foreach (var dir in GetDirectories("src/**/obj/"))
			DeleteDirectory(dir, new DeleteDirectorySettings() { Recursive = true });
	});

Task("CleanAll")
	.Description("Perform standard 'Clean' followed by deleting object directories")
	.IsDependentOn("Clean")
	.IsDependentOn("DeleteObjectDirectories");

//////////////////////////////////////////////////////////////////////
// INITIALIZE FOR BUILD
//////////////////////////////////////////////////////////////////////

static readonly string[] PACKAGE_SOURCES =
{
   "https://www.nuget.org/api/v2",
   "https://www.myget.org/F/nunit/api/v2",
   "https://www.myget.org/F/testcentric/api/v2"
};

Task("NuGetRestore")
    .Does(() =>
	{
		NuGetRestore(SOLUTION_FILE, new NuGetRestoreSettings()
		{
			Source = PACKAGE_SOURCES
		});
	});

//////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////

Task("Build")
	.IsDependentOn("Clean")
	.IsDependentOn("NuGetRestore")
	.IsDependentOn("CheckHeaders")
	.Does<BuildParameters>((parameters) =>
	{
		if (IsRunningOnWindows())
		{
			MSBuild(SOLUTION_FILE, new MSBuildSettings()
				.SetConfiguration(parameters.Configuration)
				.SetMSBuildPlatform(MSBuildPlatform.Automatic)
				.SetVerbosity(Verbosity.Minimal)
				.SetNodeReuse(false)
				.SetPlatformTarget(PlatformTarget.MSIL)
			);
		}
		else
		{
			XBuild(SOLUTION_FILE, new XBuildSettings()
				.WithTarget("Build")
				.WithProperty("Configuration", parameters.Configuration)
				.SetVerbosity(Verbosity.Minimal)
			);
		}
    });

//////////////////////////////////////////////////////////////////////
// TEST
//////////////////////////////////////////////////////////////////////

Task("Test")
	.IsDependentOn("Build")
	.Does<BuildParameters>((parameters) =>
	{
		StartProcess(parameters.OutputDirectory + UNIT_TEST_ASSEMBLY);
	});

//////////////////////////////////////////////////////////////////////
// BUILD PACKAGES
//////////////////////////////////////////////////////////////////////

Task("BuildNuGetPackage")
	.Does<BuildParameters>((parameters) =>
	{
		CreateDirectory(parameters.PackageDirectory);

		NuGetPack("nuget/Net20PluggableAgent.nuspec", new NuGetPackSettings()
		{
			Version = parameters.PackageVersion,
			OutputDirectory = parameters.PackageDirectory,
			NoPackageAnalysis = true
		});
	});

Task("BuildChocolateyPackage")
	.Does<BuildParameters>((parameters) =>
	{
		CreateDirectory(parameters.PackageDirectory);

		ChocolateyPack("choco/net20-pluggable-agent.nuspec", new ChocolateyPackSettings()
		{
			Version = parameters.PackageVersion,
			OutputDirectory = parameters.PackageDirectory
		});
	});

//////////////////////////////////////////////////////////////////////
// INSTALL PACKAGES
//////////////////////////////////////////////////////////////////////

Task("InstallNuGetPackage")
	.Does<BuildParameters>((parameters) =>
	{
		if (System.IO.Directory.Exists(parameters.NuGetTestDirectory))
			DeleteDirectory(parameters.NuGetTestDirectory,
				new DeleteDirectorySettings()
				{
					Recursive = true
				});

		CreateDirectory(parameters.NuGetTestDirectory);

		Unzip(parameters.NuGetPackage, parameters.NuGetTestDirectory);

		Information($"  Installed {System.IO.Path.GetFileName(parameters.NuGetPackage)}");
		Information($"    at {parameters.NuGetTestDirectory}");
	});

Task("InstallChocolateyPackage")
	.Does<BuildParameters>((parameters) =>
	{
		if (System.IO.Directory.Exists(parameters.ChocolateyTestDirectory))
			DeleteDirectory(parameters.ChocolateyTestDirectory,
				new DeleteDirectorySettings()
				{
					Recursive = true
				});

		CreateDirectory(parameters.ChocolateyTestDirectory);

		Unzip(parameters.ChocolateyPackage, parameters.ChocolateyTestDirectory);

		Information($"  Installed {System.IO.Path.GetFileName(parameters.ChocolateyPackage)}");
		Information($"    at {parameters.ChocolateyTestDirectory}");
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
	.Does<BuildParameters>((parameters) =>
	{
		Check.That(parameters.NuGetTestDirectory,
		HasFiles("LICENSE.txt", "CHANGES.txt"),
			HasDirectory("tools").WithFiles(LAUNCHER_FILES),
			HasDirectory("tools/agent").WithFiles(AGENT_FILES));

		Information("  SUCCESS: All checks were successful");
	});

Task("VerifyChocolateyPackage")
	.IsDependentOn("InstallChocolateyPackage")
	.Does<BuildParameters>((parameters) =>
	{
		Check.That(parameters.ChocolateyTestDirectory,
			HasDirectory("tools").WithFiles("LICENSE.txt", "CHANGES.txt", "VERIFICATION.txt").WithFiles(LAUNCHER_FILES),
			HasDirectory("tools/agent").WithFiles(AGENT_FILES));

		Information("  SUCCESS: All checks were successful");
	});

//////////////////////////////////////////////////////////////////////
// TEST PACKAGES
//////////////////////////////////////////////////////////////////////

Task("TestNuGetPackage")
	.IsDependentOn("InstallNuGetPackage")
	.Does<BuildParameters>((parameters) =>
	{
		new NuGetPackageTester(parameters).RunAllTests();
	});

Task("TestChocolateyPackage")
	.IsDependentOn("InstallChocolateyPackage")
	.Does<BuildParameters>((parameters) =>
	{
		new ChocolateyPackageTester(parameters).RunAllTests();
	});

//////////////////////////////////////////////////////////////////////
// PUBLISH PACKAGES
//////////////////////////////////////////////////////////////////////

static bool hadPublishingErrors = false;

Task("PublishPackages")
	.Description("Publish nuget and chocolatey packages according to the current settings")
	.IsDependentOn("PublishToMyGet")
	.IsDependentOn("PublishToNuGet")
	.IsDependentOn("PublishToChocolatey")
	.Does(() =>
	{
		if (hadPublishingErrors)
			throw new Exception("One of the publishing steps failed.");
	});

// This task may either be run by the PublishPackages task,
// which depends on it, or directly when recovering from errors.
Task("PublishToMyGet")
	.Description("Publish packages to MyGet")
	.Does<BuildParameters>((parameters) =>
	{
		if (!parameters.IsProductionRelease && !parameters.IsDevelopmentRelease)
			Information("Nothing to publish to MyGet from this run.");
		else
		try
		{
			NuGetPush(parameters.NuGetPackage, new NuGetPushSettings()
			{
				ApiKey = EnvironmentVariable(MYGET_API_KEY),
				Source = MYGET_PUSH_URL
			});

			ChocolateyPush(parameters.ChocolateyPackage, new ChocolateyPushSettings()
			{
				ApiKey = EnvironmentVariable(MYGET_API_KEY),
				Source = MYGET_PUSH_URL
			});
		}
		catch (Exception)
		{
			hadPublishingErrors = true;
		}
	});

// This task may either be run by the PublishPackages task,
// which depends on it, or directly when recovering from errors.
Task("PublishToNuGet")
	.Description("Publish packages to NuGet")
	.Does<BuildParameters>((parameters) =>
	{
		if (!parameters.IsProductionRelease)
			Information("Nothing to publish to NuGet from this run.");
		else
		try
		{
			NuGetPush(parameters.NuGetPackage, new NuGetPushSettings()
			{
				ApiKey = EnvironmentVariable(NUGET_API_KEY),
				Source = NUGET_PUSH_URL
			});
		}
		catch (Exception)
		{
			hadPublishingErrors = true;
		}
	});

// This task may either be run by the PublishPackages task,
// which depends on it, or directly when recovering from errors.
Task("PublishToChocolatey")
	.Description("Publish packages to Chocolatey")
	.Does<BuildParameters>((parameters) =>
	{
		if (!parameters.IsProductionRelease)
			Information("Nothing to publish to Chocolatey from this run.");
		else
		try
		{
			ChocolateyPush(parameters.ChocolateyPackage, new ChocolateyPushSettings()
			{
				ApiKey = EnvironmentVariable(CHOCO_API_KEY),
				Source = CHOCO_PUSH_URL
			});
		}
		catch (Exception)
		{
			hadPublishingErrors = true;
		}
	});

//private void PushNuGetPackage(FilePath package, string apiKey, string url)
//{
//	CheckPackageExists(package);
//	NuGetPush(package, new NuGetPushSettings() { ApiKey = apiKey, Source = url });
//}

//private void PushChocolateyPackage(FilePath package, string apiKey, string url)
//{
//	CheckPackageExists(package);
//	ChocolateyPush(package, new ChocolateyPushSettings() { ApiKey = apiKey, Source = url });
//}

//private void CheckPackageExists(FilePath package)
//{
//	if (!FileExists(package))
//		throw new InvalidOperationException(
//			$"Package not found: {package.GetFilename()}.\nCode may have changed since package was last built.");
//}

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
	.IsDependentOn("Test")
	.IsDependentOn("Package")
	.IsDependentOn("PublishPackages");

Task("Full")
	.IsDependentOn("Build")
	.IsDependentOn("Test")
	.IsDependentOn("Package");

//Task("Travis")
//	.IsDependentOn("Build")
//	.IsDependentOn("Test");

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
