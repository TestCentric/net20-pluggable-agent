//////////////////////////////////////////////////////////////////////
// ARGUMENTS  
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
string configuration = Argument("configuration", DEFAULT_CONFIGURATION);

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

		if (branch == MAIN_BRANCH && !isPullRequest)
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

bool IsProductionRelease = !PackageVersion.Contains("-");
bool IsDevelopmentRelease = PackageVersion.Contains("-dev-");

// Can't load the lower level scripts until  both
// configuration and PackageVersion are set.
#load "cake/constants.cake"
#load "cake/package-checks.cake"
#load "cake/test-results.cake"
#load "cake/package-tests.cake"

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
		InstallPackage(NUGET_PACKAGE, NUGET_TEST_DIR);
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
		InstallPackage(CHOCO_PACKAGE, CHOCO_TEST_DIR);
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
		NuGetPush(NUGET_PACKAGE, new NuGetPushSettings()
		{
			ApiKey = MYGET_API_KEY,
			Source = MYGET_PUSH_URL
		});

		ChocolateyPush(CHOCO_PACKAGE, new ChocolateyPushSettings()
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
