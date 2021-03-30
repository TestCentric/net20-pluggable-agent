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
var packageVersion = DEFAULT_VERSION + dbgSuffix;

if (BuildSystem.IsRunningOnAppVeyor)
{
	var tag = AppVeyor.Environment.Repository.Tag;

	if (tag.IsTag)
	{
		packageVersion = tag.Name;
	}
	else
	{
		var buildNumber = AppVeyor.Environment.Build.Number.ToString("00000");
		var branch = AppVeyor.Environment.Repository.Branch;
		var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;

		if (branch == "master" && !isPullRequest)
		{
			packageVersion = DEFAULT_VERSION + "-dev-" + buildNumber + dbgSuffix;
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

			packageVersion = DEFAULT_VERSION + suffix;
		}
	}

	AppVeyor.UpdateBuildVersion(packageVersion);
}

// Can't load the lower level scripts until  both
// configuration and packageVersion are set.
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

// NOTE: Any project to which this file is added is required to have a 'Clean' target
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
// PACKAGE
//////////////////////////////////////////////////////////////////////

// Additional package metadata
var PROJECT_URL = new Uri("http://test-centric.org");
var ICON_URL = new Uri("https://cdn.rawgit.com/nunit/resources/master/images/icon/nunit_256.png");
var LICENSE_URL = new Uri("http://nunit.org/nuget/nunit3-license.txt");
var PROJECT_SOURCE_URL = new Uri(GITHUB_SITE);
var PACKAGE_SOURCE_URL = new Uri(GITHUB_SITE);
var BUG_TRACKER_URL = new Uri(GITHUB_SITE + "/issues");
var DOCS_URL = new Uri(WIKI_PAGE);
var MAILING_LIST_URL = new Uri("https://groups.google.com/forum/#!forum/nunit-discuss");

Task("BuildNuGetPackage")
	.Does(() =>
	{
		CreateDirectory(PACKAGE_DIR);

		NuGetPack(
			new NuGetPackSettings()
			{
				Id = NUGET_ID,
				Version = packageVersion,
				Title = TITLE,
				Authors = AUTHORS,
				Owners = OWNERS,
				Description = DESCRIPTION,
				Summary = SUMMARY,
				ProjectUrl = PROJECT_URL,
				IconUrl = ICON_URL,
				LicenseUrl = LICENSE_URL,
				RequireLicenseAcceptance = false,
				Copyright = COPYRIGHT,
				ReleaseNotes = RELEASE_NOTES,
				Tags = TAGS,
				//Language = "en-US",
				OutputDirectory = PACKAGE_DIR,
				Files = new[] {
					new NuSpecContent { Source = PROJECT_DIR + "LICENSE.txt" },
					new NuSpecContent { Source = PROJECT_DIR + "CHANGES.txt" },
					new NuSpecContent { Source = BIN_DIR + "net20-agent-launcher.dll", Target = "tools" },
					new NuSpecContent { Source = BIN_DIR + "nunit.engine.api.dll", Target = "tools" },
					new NuSpecContent { Source = BIN_DIR + "testcentric.agent.api.dll", Target = "tools" },
					new NuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent.exe", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent.pdb", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent.exe.config", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent-x86.exe", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent-x86.pdb", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent-x86.exe.config", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/nunit.engine.api.dll", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/testcentric.agent.api.dll", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/testcentric.agent.api.pdb", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/testcentric.engine.core.dll", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/testcentric.engine.core.pdb", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/testcentric.engine.metadata.dll", Target = "tools/agent" },
					new NuSpecContent { Source = BIN_DIR + "agent/testcentric.engine.metadata.pdb", Target = "tools/agent" }
				}
			});
	});

Task("TestNuGetPackage")
	.IsDependentOn("InstallGuiRunner")
	.Does(() =>
	{
		new NuGetPackageTester(Context, packageVersion).RunAllTests();
	});

Task("BuildChocolateyPackage")
    .Does(() =>
    {
        CreateDirectory(PACKAGE_DIR);

        ChocolateyPack(
            new ChocolateyPackSettings()
            {
                Id = CHOCO_ID,
                Version = packageVersion,
                Title = TITLE,
                Authors = AUTHORS,
                Owners = OWNERS,
                Description = DESCRIPTION,
                Summary = SUMMARY,
                ProjectUrl = PROJECT_URL,
                IconUrl = ICON_URL,
                LicenseUrl = LICENSE_URL,
                RequireLicenseAcceptance = false,
                Copyright = COPYRIGHT,
                ProjectSourceUrl = PROJECT_SOURCE_URL,
                DocsUrl = DOCS_URL,
                BugTrackerUrl = BUG_TRACKER_URL,
                PackageSourceUrl = PACKAGE_SOURCE_URL,
                MailingListUrl = MAILING_LIST_URL,
                ReleaseNotes = RELEASE_NOTES,
                Tags = TAGS,
                //Language = "en-US",
                OutputDirectory = PACKAGE_DIR,
                Files = new[] {
                    new ChocolateyNuSpecContent { Source = PROJECT_DIR + "LICENSE.txt", Target = "tools" },
                    new ChocolateyNuSpecContent { Source = PROJECT_DIR + "CHANGES.txt", Target = "tools" },
                    new ChocolateyNuSpecContent { Source = PROJECT_DIR + "VERIFICATION.txt", Target = "tools" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "net20-agent-launcher.dll", Target = "tools" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "nunit.engine.api.dll", Target = "tools" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "testcentric.agent.api.dll", Target = "tools" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent.exe", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent.pdb", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent.exe.config", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent-x86.exe", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent-x86.pdb", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/net20-pluggable-agent-x86.exe.config", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/nunit.engine.api.dll", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/testcentric.agent.api.dll", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/testcentric.agent.api.pdb", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/testcentric.engine.core.dll", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/testcentric.engine.core.pdb", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/testcentric.engine.metadata.dll", Target = "tools/agent" },
					new ChocolateyNuSpecContent { Source = BIN_DIR + "agent/testcentric.engine.metadata.pdb", Target = "tools/agent" }
				}
			});
    });

Task("TestChocolateyPackage")
	.IsDependentOn("InstallGuiRunner")
	.Does(() =>
	{
		new ChocolateyPackageTester(Context, packageVersion).RunAllTests();
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
