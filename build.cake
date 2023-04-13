#tool nuget:?package=GitVersion.CommandLine&version=5.6.3
#tool nuget:?package=GitReleaseManager&version=0.12.1

#load nuget:?package=TestCentric.Cake.Recipe&version=1.0.0-dev00039

var target = Argument("target", Argument("t", "Default"));
 
BuildSettings.Initialize
(
	context: Context,
	title: "Net20PluggableAgent",
	solutionFile: "net20-pluggable-agent.sln",
	unitTests: "net20-agent-launcher.tests.exe",
	guiVersion: "2.0.0-dev00226",
	githubOwner: "TestCentric",
	githubRepository: "net20-pluggable-agent"
);

Information($"Net20PluggableAgent {BuildSettings.Configuration} version {BuildSettings.PackageVersion}");

if (BuildSystem.IsRunningOnAppVeyor)
	AppVeyor.UpdateBuildVersion(BuildSettings.PackageVersion + "-" + AppVeyor.Environment.Build.Number);

var packageTests = new PackageTest[] {
	new PackageTest(
		1, "Net20PackageTest", "Run mock-assembly.dll targeting .NET 2.0",
		"tests/net20/mock-assembly.dll", CommonResult),
	new PackageTest(
		1, "Net35PackageTest", "Run mock-assembly.dll targeting .NET 3.5",
		"tests/net35/mock-assembly.dll", CommonResult)
};

var nugetPackage = new NuGetPackage(
	id: "NUnit.Extension.Net20PluggableAgent",
	source: "nuget/Net20PluggableAgent.nuspec",
	basePath: BuildSettings.OutputDirectory,
	checks: new PackageCheck[] {
		HasFiles("LICENSE.txt", "CHANGES.txt"),
		HasDirectory("tools").WithFiles("net20-agent-launcher.dll", "nunit.engine.api.dll"),
		HasDirectory("tools/agent").WithFiles(
			"net20-pluggable-agent.exe", "net20-pluggable-agent.exe.config",
			"net20-pluggable-agent-x86.exe", "net20-pluggable-agent-x86.exe.config",
			"nunit.engine.api.dll", "testcentric.engine.core.dll",
			"testcentric.engine.metadata.dll", "testcentric.extensibility.dll")},
	testRunner: new GuiRunner("TestCentric.GuiRunner", "2.0.0-dev00226"),
	tests: packageTests );

var chocolateyPackage = new ChocolateyPackage(
	id: "nunit-extension-net20-pluggable-agent",
	source: "choco/net20-pluggable-agent.nuspec",
	basePath: BuildSettings.OutputDirectory,
	checks: new PackageCheck[] {
		HasDirectory("tools").WithFiles("net20-agent-launcher.dll", "nunit.engine.api.dll")
			.WithFiles("LICENSE.txt", "CHANGES.txt", "VERIFICATION.txt"),
		HasDirectory("tools/agent").WithFiles(
			"net20-pluggable-agent.exe", "net20-pluggable-agent.exe.config",
			"net20-pluggable-agent-x86.exe", "net20-pluggable-agent-x86.exe.config",
			"nunit.engine.api.dll", "testcentric.engine.core.dll")},
	testRunner: new GuiRunner("testcentric-gui", "2.0.0-dev00226"),
	tests: packageTests);

BuildSettings.Packages.AddRange(new PackageDefinition[] { nugetPackage, chocolateyPackage });

ExpectedResult CommonResult => new ExpectedResult("Failed")
{
	Total = 36,
	Passed = 23,
	Failed = 5,
	Warnings = 1,
	Inconclusive = 1,
	Skipped = 7,
	Assemblies = new ExpectedAssemblyResult[]
	{
		new ExpectedAssemblyResult("mock-assembly.dll", "Net20AgentLauncher")
	}
};

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("BuildTestAndPackage")
	.IsDependentOn("Build")
	.IsDependentOn("Test")
	.IsDependentOn("Package");

Task("Appveyor")
	.IsDependentOn("Build")
	.IsDependentOn("Test")
	.IsDependentOn("Package")
	.IsDependentOn("Publish")
	.IsDependentOn("CreateDraftRelease")
	.IsDependentOn("CreateProductionRelease");

//Task("Travis")
//	.IsDependentOn("Build")
//	.IsDependentOn("RunTests");

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
