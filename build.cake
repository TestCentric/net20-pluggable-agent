	//////////////////////////////////////////////////////////////////////
	// ARGUMENTS  
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
string configuration = Argument("configuration", DEFAULT_CONFIGURATION);

// Special (optional) arguments for the script. You pass these
// through the Cake bootscrap script via the -ScriptArgs argument
// for example: 
//   ./build.ps1 -t RePackageNuget -ScriptArgs --nugetVersion="3.9.9"
//   ./build.ps1 -t RePackageNuget -ScriptArgs '--binaries="rel3.9.9" --nugetVersion="3.9.9"'
//var nugetVersion = Argument("nugetVersion", (string)null);
//var chocoVersion = Argument("chocoVersion", (string)null);
//var binaries = Argument("binaries", (string)null);

//////////////////////////////////////////////////////////////////////
// SET PACKAGE VERSION
//////////////////////////////////////////////////////////////////////

var dbgSuffix = configuration == "Debug" ? "-dbg" : "";
var PackageVersion = DEFAULT_VERSION + dbgSuffix;

if (BuildSystem.IsRunningOnAppVeyor)
{
	var tag = AppVeyor.Environment.Repository.Tag;

	if (tag.IsTag)
	{
		PackageVersion = tag.Name;
	}
	else
	{
		var buildNumber = AppVeyor.Environment.Build.Number.ToString("00000");
		var branch = AppVeyor.Environment.Repository.Branch;
		var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;

		if (branch == "master" && !isPullRequest)
		{
			PackageVersion = DEFAULT_VERSION + "-dev-" + buildNumber + dbgSuffix;
		}
		else
		{
			var suffix = "-ci-" + buildNumber + dbgSuffix;

			if (isPullRequest)
				suffix += "-pr-" + AppVeyor.Environment.PullRequest.Number;
			else
				suffix += "-" + branch;

			// Nuget limits "special version part" to 20 chars. Add one for the hyphen.
			if (suffix.Length > 21)
				suffix = suffix.Substring(0, 21);

            suffix = suffix.Replace(".", "");

			PackageVersion = DEFAULT_VERSION + suffix;
		}
	}

	AppVeyor.UpdateBuildVersion(PackageVersion);
}

// Can't load the lower level scripts until  both
// configuration and PackageVersion are set.
#load "constants.cake"
#load "package-checks.cake"
#load "test-results.cake"
#load "package-tests.cake"

//////////////////////////////////////////////////////////////////////
// CLEAN
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(BIN_DIR);
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
    NuGetRestore(SOLUTION_FILE, new NuGetRestoreSettings()
	{
		Source = PACKAGE_SOURCES
	});
});

//////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("NuGetRestore")
    .Does(() =>
    {
		//if (binaries != null)
		//    throw new Exception("The --binaries option may only be specified when re-packaging an existing build.");

		if(IsRunningOnWindows())
		{
			MSBuild(SOLUTION_FILE, new MSBuildSettings()
				.SetConfiguration(configuration)
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
				.WithProperty("Configuration", configuration)
				.SetVerbosity(Verbosity.Minimal)
			);
		}
    });

//////////////////////////////////////////////////////////////////////
// TEST
//////////////////////////////////////////////////////////////////////

Task("Test")
	.IsDependentOn("Build")
	.Does(() =>
	{
		StartProcess(BIN_DIR + UNIT_TEST_ASSEMBLY);
	});

//////////////////////////////////////////////////////////////////////
// PACKAGING
//////////////////////////////////////////////////////////////////////

Task("BuildNuGetPackage")
	.Does(() =>
	{
		CreateDirectory(PACKAGE_DIR);

		NuGetPack("nuget/Net20PluggableAgent.nuspec", new NuGetPackSettings()
		{
			Version = PackageVersion,
			OutputDirectory = PACKAGE_DIR,
			NoPackageAnalysis = true
		});
	});

Task("TestNuGetPackage")
	.IsDependentOn("InstallGuiRunner")
	.Does(() =>
	{
		new NuGetPackageTester(Context, PackageVersion).RunAllTests();
	});

Task("BuildChocolateyPackage")
    .Does(() =>
    {
        CreateDirectory(PACKAGE_DIR);

		ChocolateyPack("choco/net20-pluggable-agent.nuspec", new ChocolateyPackSettings()
		{
			Version = PackageVersion,
			OutputDirectory = PACKAGE_DIR
		});
	});

Task("TestChocolateyPackage")
	.IsDependentOn("InstallGuiRunner")
	.Does(() =>
	{
		new ChocolateyPackageTester(Context, PackageVersion).RunAllTests();
	});

Task("InstallGuiRunner")
	.Does(() =>
	{
		NuGetInstall(GUI_RUNNER_ID,
			new NuGetInstallSettings()
			{
				Version = GUI_RUNNER_VERSION,
				Source = GUI_RUNNER_SOURCE,
				OutputDirectory = PACKAGE_TEST_DIR
			});
	});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Package")
	.IsDependentOn("PackageNuGet")
    .IsDependentOn("PackageChocolatey");

Task("PackageNuGet")
	.IsDependentOn("BuildNuGetPackage")
	.IsDependentOn("TestNuGetPackage");

Task("PackageChocolatey")
	.IsDependentOn("BuildChocolateyPackage")
	.IsDependentOn("TestChocolateyPackage");

Task("Appveyor")
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
