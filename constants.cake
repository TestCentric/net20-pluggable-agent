
//////////////////////////////////////////////////////////////////////
// Constants and readonly values used as constants.
//////////////////////////////////////////////////////////////////////

//////////////////////////////////////////////////////////////////////
// PROJECT-SPECIFIC
//////////////////////////////////////////////////////////////////////

// When copying the scripts to support a different extension, the main
// changes needed should be in this section. (But check everything!)

const string NUGET_ID = "NUnit.Extension.Net20PluggableAgent";
const string CHOCO_ID = "nunit-extension-net20-pluggable-agent";

const string SOLUTION_FILE = "net20-pluggable-agent.sln";
const string OUTPUT_ASSEMBLY = "net20-pluggable-agent.dll";
const string UNIT_TEST_ASSEMBLY = "net20-agent-launcher.tests.exe";
const string MOCK_ASSEMBLY = "mock-assembly.dll";

var GITHUB_SITE = "https://github.com/TestCentric/net20-pluggable-agent";
var WIKI_PAGE = "https://github.com/nunit/docs/wiki/Console-Command-Line";
 
// NOTE: Since GitVersion is only used when running under
// Windows, the default version should be updated to the 
// next version after each release.
const string DEFAULT_VERSION = "1.0.0";
const string DEFAULT_CONFIGURATION = "Release";

//////////////////////////////////////////////////////////////////////
// DIRECTORIES
//////////////////////////////////////////////////////////////////////

// PROJECT_DIR and BIN_DIR can't be readonly since they are initialized
// independently. This is required because they depend instance members.
static string PROJECT_DIR; PROJECT_DIR = Context.Environment.WorkingDirectory.FullPath + "/";
static string BIN_DIR; BIN_DIR = $"{PROJECT_DIR}bin/{configuration}/";
const string PACKAGE_DIR = "output/";
const string PACKAGE_TEST_DIR = PACKAGE_DIR + "test/";

// Package sources for nuget restore
static readonly string[] PACKAGE_SOURCES = {
	"https://www.nuget.org/api/v2",
	"https://www.myget.org/F/nunit/api/v2"
};

// Packaging
readonly string NUGET_PACKAGE_NAME = $"{NUGET_ID}.{packageVersion}.nupkg";
readonly string NUGET_PACKAGE = PACKAGE_DIR + NUGET_PACKAGE_NAME;
const string NUGET_TEST_DIR = PACKAGE_TEST_DIR + NUGET_ID;
readonly string CHOCO_PACKAGE_NAME = $"{CHOCO_ID}.{packageVersion}.nupkg";
readonly string CHOCO_PACKAGE = PACKAGE_DIR + CHOCO_PACKAGE_NAME;
const string CHOCO_TEST_DIR = PACKAGE_TEST_DIR + CHOCO_ID;

// Metadata used in both the nuget and chocolatey packages
var TITLE = "TestCentric - Net20 Pluggable Agent";
var AUTHORS = new[] { "Charlie Poole" };
var OWNERS = new[] { "Charlie Poole" };
var DESCRIPTION = "This extension allows the TestCentric GUI to run tests under the .NET 2.0 framework.";
var SUMMARY = "TestCentric Engine extension for running tests under .NET 2.0";
var COPYRIGHT = "Copyright (c) 2021 Charlie Poole";
var RELEASE_NOTES = new[] { "See https://raw.githubusercontent.com/TestCentric/net20-pluggable-agent/master/CHANGES.txt" };
var TAGS = new[] { "nunit", "testcentric", "agent", "net20" };

// Package content checking
static readonly string[] LAUNCHER_FILES = {
	"net20-agent-launcher.dll", "nunit.engine.api.dll", "testcentric.agent.api.dll"
};

static readonly string[] AGENT_FILES = {
	"net20-pluggable-agent.exe", "net20-pluggable-agent.exe.config", "net20-pluggable-agent.pdb",
	"net20-pluggable-agent-x86.exe", "net20-pluggable-agent-x86.exe.config", "net20-pluggable-agent-x86.pdb",
	"nunit.engine.api.dll",
	"testcentric.agent.api.dll", "testcentric.agent.api.pdb",
	"testcentric.engine.core.dll", "testcentric.engine.core.pdb",
	"testcentric.engine.metadata.dll", "testcentric.engine.metadata.pdb"
};

// Package Testing
const string GUI_RUNNER_ID = "TestCentric.GuiRunner";
const string GUI_RUNNER_VERSION = "2.0.0-dev00013";
static readonly string[] GUI_RUNNER_SOURCE = { "https://www.myget.org/F/testcentric/api/v3/index.json" };

// URLs for uploading packages
const string MYGET_PUSH_URL = "https://www.myget.org/F/testcentric/api/v2";
const string NUGET_PUSH_URL = "https://api.nuget.org/v3/index.json";
const string CHOCO_PUSH_URL = "https://push.chocolatey.org/";

// Environment Variable names holding API keys
const string MYGET_API_KEY = "MYGET_API_KEY";
const string NUGET_API_KEY = "NUGET_API_KEY";
const string CHOCO_API_KEY = "CHOCO_API_KEY";

// Environment Variable names holding GitHub identity of user
const string GITHUB_OWNER = "TestCentric";
const string GITHUB_REPO = "testcentric-gui";
// Access token is used by GitReleaseManager
const string GITHUB_ACCESS_TOKEN = "GITHUB_ACCESS_TOKEN";

// Pre-release labels that we publish
static readonly string[] LABELS_WE_PUBLISH_ON_MYGET = { "dev", "pre" };
static readonly string[] LABELS_WE_PUBLISH_ON_NUGET = { "alpha", "beta", "rc" };
static readonly string[] LABELS_WE_PUBLISH_ON_CHOCOLATEY = { "alpha", "beta", "rc" };
