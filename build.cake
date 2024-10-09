// Load the recipe
#load nuget:?package=TestCentric.Cake.Recipe&version=1.3.2
// Comment out above line and uncomment below for local tests of recipe changes
//#load ../TestCentric.Cake.Recipe/recipe/*.cake

BuildSettings.Initialize
(
	context: Context,
	title: "Net20PluggableAgent",
	solutionFile: "net20-pluggable-agent.sln",
	unitTests: "net20-agent-launcher.tests.exe",
	githubRepository: "net20-pluggable-agent"
);

BuildSettings.Packages.AddRange(new PluggableAgentFactory(".NetFramework, Version=2.0").Packages);

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

Build.Run();
