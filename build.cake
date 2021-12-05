#tool nuget:?package=GitVersion.CommandLine&version=5.0.0
#tool nuget:?package=GitReleaseManager&version=0.11.0

//////////////////////////////////////////////////////////////////////
// PROJECT-SPECIFIC CONSTANTS
//////////////////////////////////////////////////////////////////////

const string SOLUTION_FILE = "net20-pluggable-agent.sln";
const string UNIT_TEST_ASSEMBLY = "net20-agent-launcher.tests.exe";

const string DEFAULT_VERSION = "2.0.0";

#load nuget:?package=TestCentric.Cake.Recipe&version=1.0.0-dev00012

//////////////////////////////////////////////////////////////////////
// ARGUMENTS  
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

// Additional Arguments Defined by TestCentric.Cake.Recipe
//
// --configuration=CONFIGURATION (settings.cake)
//     Sets the configuration (default is specified in DEFAULT_CONFIGURATION)
//
// --packageVersion=VERSION (versioning.cake)
//     Bypasses GitVersion and causes the specified version to be used instead.
  
//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

Setup<BuildSettings>((context) =>
{
	var settings = BuildSettings.Initialize
	(
		context: context,
		title: "Net20PluggableAgent",
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
// CLEAN
//////////////////////////////////////////////////////////////////////

Task("Clean")
	.Does<BuildSettings>((settings) =>
	{
		Information("Cleaning " + settings.OutputDirectory);
		CleanDirectory(settings.OutputDirectory);

		Information("Cleaning " + settings.PackageTestDirectory);
		CleanDirectory(settings.PackageTestDirectory);
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
			Source = PACKAGE_SOURCES,
			Verbosity = NuGetVerbosity.Detailed
		});
	});

//////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////

Task("Build")
	.IsDependentOn("Clean")
	.IsDependentOn("NuGetRestore")
	.IsDependentOn("CheckHeaders")
	.Does<BuildSettings>((settings) =>
	{
		if (IsRunningOnWindows())
		{
			MSBuild(SOLUTION_FILE, new MSBuildSettings()
				.SetConfiguration(settings.Configuration)
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
				.WithProperty("Configuration", settings.Configuration)
				.SetVerbosity(Verbosity.Minimal)
			);
		}
    });

//////////////////////////////////////////////////////////////////////
// TEST
//////////////////////////////////////////////////////////////////////

Task("Test")
	.IsDependentOn("Build")
	.Does<BuildSettings>((settings) =>
	{
		StartProcess(settings.OutputDirectory + UNIT_TEST_ASSEMBLY);
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
// PUBLISH PACKAGES
//////////////////////////////////////////////////////////////////////

static bool hadPublishingErrors = false;

Task("Publish")
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
	.Does<BuildSettings>((settings) =>
	{
		if (!settings.IsProductionRelease && !settings.IsDevelopmentRelease)
			Information("Nothing to publish to MyGet from this run.");
		else
		try
		{
			PushNuGetPackage(settings.NuGetPackage, settings.MyGetApiKey, settings.MyGetPushUrl);
			PushChocolateyPackage(settings.ChocolateyPackage, settings.MyGetApiKey, settings.MyGetPushUrl);
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
	.Does<BuildSettings>((settings) =>
	{
		if (!settings.IsProductionRelease)
			Information("Nothing to publish to NuGet from this run.");
		else
		try
		{
			PushNuGetPackage(settings.NuGetPackage, settings.NuGetApiKey, settings.NuGetPushUrl);
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
	.Does<BuildSettings>((settings) =>
	{
		if (!settings.IsProductionRelease)
			Information("Nothing to publish to Chocolatey from this run.");
		else
		try
		{
			PushChocolateyPackage(settings.ChocolateyPackage, settings.ChocolateyApiKey, settings.ChocolateyPushUrl);
		}
		catch (Exception)
		{
			hadPublishingErrors = true;
		}
	});

private void PushNuGetPackage(FilePath package, string apiKey, string url)
{
    CheckPackageExists(package);
    NuGetPush(package, new NuGetPushSettings() { ApiKey = apiKey, Source = url });
}

private void PushChocolateyPackage(FilePath package, string apiKey, string url)
{
    CheckPackageExists(package);
    ChocolateyPush(package, new ChocolateyPushSettings() { ApiKey = apiKey, Source = url });
}

private void CheckPackageExists(FilePath package)
{
    if (!FileExists(package))
        throw new InvalidOperationException(
            $"Package not found: {package.GetFilename()}.\nCode may have changed since package was last built.");
}

//////////////////////////////////////////////////////////////////////
// CREATE A DRAFT RELEASE
//////////////////////////////////////////////////////////////////////

Task("CreateDraftRelease")
	.Does<BuildSettings>((settings) =>
	{
		if (settings.BuildVersion.IsReleaseBranch)
		{
			// NOTE: Since this is a release branch, the pre-release label
			// is "pre", which we don't want to use for the draft release.
			// The branch name contains the full information to be used
			// for both the name of the draft release and the milestone,
			// i.e. release-2.0.0, release-2.0.0-beta2, etc.
			string milestone = settings.BranchName.Substring(8);
			string releaseName = $"{settings.Title} {milestone}";

			Information($"Creating draft release for {releaseName}");

			try
			{
				GitReleaseManagerCreate(settings.GitHubAccessToken, settings.GitHubOwner, settings.GitHubRepository, new GitReleaseManagerCreateSettings()
				{
					Name = releaseName,
					Milestone = milestone
				});
			}
			catch
			{
				Error($"Unable to create draft release for {releaseName}.");
				Error($"Check that there is a {milestone} milestone with at least one closed issue.");
				Error("");
				throw;
			}
		}
		else
		{
			Information("Skipping Release creation because this is not a release branch");
		}
	});

//////////////////////////////////////////////////////////////////////
// CREATE A PRODUCTION RELEASE
//////////////////////////////////////////////////////////////////////

Task("CreateProductionRelease")
	.Does<BuildSettings>((settings) =>
	{
		if (settings.IsProductionRelease)
		{
			string token = settings.GitHubAccessToken;
			string owner = settings.GitHubOwner;
			string repository = settings.GitHubRepository;
			string tagName = settings.PackageVersion;
			string assets = IsRunningOnWindows()
				? $"\"{settings.NuGetPackage},{settings.ChocolateyPackage}\""
				: $"\"{settings.NuGetPackage}\"";

			Information($"Publishing release {tagName} to GitHub");

			GitReleaseManagerAddAssets(token, owner, repository, tagName, assets);
			GitReleaseManagerClose(token, owner, repository, tagName);
		}
		else
		{
			Information("Skipping CreateProductionRelease because this is not a production release");
		}
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
	.IsDependentOn("Test")
	.IsDependentOn("Package")
	.IsDependentOn("Publish")
	.IsDependentOn("CreateDraftRelease")
	.IsDependentOn("CreateProductionRelease");

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
