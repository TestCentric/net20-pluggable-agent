#tool nuget:?package=GitVersion.CommandLine&version=5.0.0

//////////////////////////////////////////////////////////////////////
// ARGUMENTS  
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
string configuration = Argument("configuration", DEFAULT_CONFIGURATION);

// Additional Argument
//
// --packageVersion=VERSION bypasses GitVersion and causes the specified
//                          version to be used instead. (versioning.cake)
  
//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

string PackageVersion, NuGetPackageName, NuGetPackage, ChocoPackageName, ChocoPackage;
bool IsDevelopmentRelease, IsProductionRelease;

Setup((context) =>
{
	PackageVersion = new BuildVersion(context).PackageVersion;
	IsProductionRelease = !PackageVersion.Contains("-");
	IsDevelopmentRelease = PackageVersion.Contains("-dev-");

	NuGetPackageName = $"{NUGET_ID}.{PackageVersion}.nupkg";
	NuGetPackage = PACKAGE_DIR + NuGetPackageName;
	ChocoPackageName = $"{CHOCO_ID}.{PackageVersion}.nupkg";
	ChocoPackage = PACKAGE_DIR + ChocoPackageName;

	Information($"Net20PluggableAgent {configuration} version {PackageVersion}");

	if (BuildSystem.IsRunningOnAppVeyor)
		AppVeyor.UpdateBuildVersion(PackageVersion);
});

// Can't load the lower level scripts until  both
// configuration and PackageVersion are set.
#load "cake/constants.cake"
#load "cake/package-checks.cake"
#load "cake/test-results.cake"
#load "cake/package-tests.cake"
#load "cake/versioning.cake"

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
// NUGET PACKAGING
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

Task("InstallNuGetGuiRunner")
	.Does(() =>
	{
		InstallGuiRunner(GUI_RUNNER_NUGET_ID);
	});

Task("InstallNuGetPackage")
	.Does(() =>
	{
		InstallPackage(NuGetPackage, NUGET_TEST_DIR);
	});

Task("VerifyNuGetPackage")
	.IsDependentOn("InstallNuGetPackage")
	.Does(() =>
	{
		Check.That(NUGET_TEST_DIR,
		HasFiles("LICENSE.txt", "CHANGES.txt"),
			HasDirectory("tools").WithFiles(LAUNCHER_FILES),
			HasDirectory("tools/agent").WithFiles(AGENT_FILES));

		Information("  SUCCESS: All checks were successful");
	});

Task("TestNuGetPackage")
	.IsDependentOn("InstallNuGetGuiRunner")
	.IsDependentOn("InstallNuGetPackage")
	.Does(() =>
	{
		new PackageTester(Context, PackageVersion, NUGET_ID, NUGET_GUI_RUNNER).RunAllTests();
	});

//////////////////////////////////////////////////////////////////////
// CHOCOLATEY PACKAGING
//////////////////////////////////////////////////////////////////////

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

Task("InstallChocolateyRunner")
	.Does(() =>
	{
		InstallGuiRunner(GUI_RUNNER_CHOCO_ID);
	});

Task("InstallChocolateyPackage")
	.Does(() =>
	{
		InstallPackage(ChocoPackage, CHOCO_TEST_DIR);
	});

Task("VerifyChocolateyPackage")
	.IsDependentOn("InstallChocolateyPackage")
	.Does(() =>
	{
		Check.That(CHOCO_TEST_DIR,
			HasDirectory("tools").WithFiles("LICENSE.txt", "CHANGES.txt", "VERIFICATION.txt").WithFiles(LAUNCHER_FILES),
			HasDirectory("tools/agent").WithFiles(AGENT_FILES));

		Information("  SUCCESS: All checks were successful");
	});


Task("TestChocolateyPackage")
	.IsDependentOn("InstallChocolateyRunner")
	.IsDependentOn("InstallChocolateyPackage")
	.Does(() =>
	{
		new PackageTester(Context, PackageVersion, CHOCO_ID, CHOCO_GUI_RUNNER).RunAllTests();
	});

//////////////////////////////////////////////////////////////////////
// PACKAGING HELPERS
//////////////////////////////////////////////////////////////////////

void InstallGuiRunner(string packageId)
{
	NuGetInstall(packageId,
		new NuGetInstallSettings()
		{
			Version = GUI_RUNNER_VERSION,
			Source = PACKAGE_SOURCES,
			OutputDirectory = PACKAGE_TEST_DIR
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
	.WithCriteria(IsProductionRelease || IsDevelopmentRelease)
	.IsDependentOn("Package")
	.Does(() =>
	{
		NuGetPush(NuGetPackage, new NuGetPushSettings()
		{
			ApiKey = MYGET_API_KEY,
			Source = MYGET_PUSH_URL
		});

		ChocolateyPush(ChocoPackage, new ChocolateyPushSettings()
		{
			ApiKey = MYGET_API_KEY,
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
