#tool nuget:?package=GitVersion.CommandLine&version=5.0.0
#tool nuget:?package=GitReleaseManager&version=0.11.0

#load nuget:?package=TestCentric.Cake.Recipe&version=1.0.0-dev00022

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
		nugetPackageSource: "nuget/Net20PluggableAgent.nuspec",
		chocoId: "nunit-extension-net20-pluggable-agent",
		chocolateyPackageSource: "choco/net20-pluggable-agent.nuspec",
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
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

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
//	.IsDependentOn("RunTests");

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
