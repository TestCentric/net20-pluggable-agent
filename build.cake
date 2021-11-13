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

#load "cake/parameters.cake"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS  
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

// Additional Arguments
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
		CleanDirectory(parameters.OutputDirectory);
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

Task("NuGetRestore")
    .Does(() =>
	{
		NuGetRestore(SOLUTION_FILE);
	});

//////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("NuGetRestore")
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
// INSTALL RUNNERS
//////////////////////////////////////////////////////////////////////

Task("InstallNuGetGuiRunner")
	.Does<BuildParameters>((parameters) =>
	{
		InstallGuiRunner(GUI_RUNNER_NUGET_ID, parameters.NuGetTestDirectory);
	});

Task("InstallChocolateyRunner")
	.Does<BuildParameters>((parameters) =>
	{
		InstallGuiRunner(GUI_RUNNER_CHOCO_ID, parameters.ChocolateyTestDirectory);
	});

//////////////////////////////////////////////////////////////////////
// INSTALL PACKAGES
//////////////////////////////////////////////////////////////////////

Task("InstallNuGetPackage")
	.Does<BuildParameters>((parameters) =>
	{
		InstallPackage(parameters.NuGetPackage, parameters.NuGetTestDirectory);
	});

Task("InstallChocolateyPackage")
	.Does<BuildParameters>((parameters) =>
	{
		InstallPackage(parameters.ChocoPackage, parameters.ChocolateyTestDirectory);
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
	.IsDependentOn("InstallNuGetGuiRunner")
	.IsDependentOn("InstallNuGetPackage")
	.Does<BuildParameters>((parameters) =>
	{
		new PackageTester(parameters, NUGET_ID, parameters.PackageTestDirectory + GUI_RUNNER_NUGET_ID).RunAllTests();
	});

Task("TestChocolateyPackage")
	.IsDependentOn("InstallChocolateyRunner")
	.IsDependentOn("InstallChocolateyPackage")
	.Does<BuildParameters>((parameters) =>
	{
		new PackageTester(parameters, CHOCO_ID, parameters.PackageTestDirectory + GUI_RUNNER_CHOCO_ID).RunAllTests();
	});

//////////////////////////////////////////////////////////////////////
// PACKAGING HELPERS
//////////////////////////////////////////////////////////////////////

void InstallGuiRunner(string packageId, string testDir)
{
	NuGetInstall(packageId,
		new NuGetInstallSettings()
		{
			Version = GUI_RUNNER_VERSION,
			OutputDirectory = testDir
		});
}

void InstallPackage(string package, string testDir)
{
	if (System.IO.Directory.Exists(testDir))
		DeleteDirectory(testDir, new DeleteDirectorySettings() { Recursive = true });
	CreateDirectory(testDir);

	Unzip(package, testDir);

	Information($"  Installed {System.IO.Path.GetFileName(package)}");
	Information($"    at {testDir}");
}

//////////////////////////////////////////////////////////////////////
// PUBLISH PACKAGES
//////////////////////////////////////////////////////////////////////

Task("PublishToMyGet")
	.WithCriteria<BuildParameters>((parameters) => parameters.IsProductionRelease || parameters.IsDevelopmentRelease)
	.IsDependentOn("Package")
	.Does<BuildParameters>((parameters) =>
	{
		NuGetPush(parameters.NuGetPackage, new NuGetPushSettings()
		{
			ApiKey = EnvironmentVariable(MYGET_API_KEY),
			Source = MYGET_PUSH_URL
		});

		ChocolateyPush(parameters.ChocoPackage, new ChocolateyPushSettings()
		{
			ApiKey = EnvironmentVariable(MYGET_API_KEY),
			Source = MYGET_PUSH_URL
		});
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

Task("Publish")
	.IsDependentOn("PublishToMyGet");

Task("Appveyor")
	.IsDependentOn("Build")
	.IsDependentOn("Test")
	.IsDependentOn("Package")
	.IsDependentOn("Publish");

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
