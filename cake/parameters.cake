#load "./package-checks.cake"
#load "./test-results.cake"
#load "./package-tests.cake"
#load "./versioning.cake"

// URLs for uploading packages
private const string MYGET_PUSH_URL = "https://www.myget.org/F/testcentric/api/v2";
private const string NUGET_PUSH_URL = "https://api.nuget.org/v3/index.json";
private const string CHOCO_PUSH_URL = "https://push.chocolatey.org/";

// Environment Variable names holding API keys
private const string MYGET_API_KEY = "MYGET_API_KEY";
private const string NUGET_API_KEY = "NUGET_API_KEY";
private const string CHOCO_API_KEY = "CHOCO_API_KEY";
private const string GITHUB_ACCESS_TOKEN = "GITHUB_ACCESS_TOKEN";

// Pre-release labels that we publish
static readonly string[] LABELS_WE_PUBLISH_ON_MYGET = { "dev", "pre" };
static readonly string[] LABELS_WE_PUBLISH_ON_NUGET = { "alpha", "beta", "rc" };
static readonly string[] LABELS_WE_PUBLISH_ON_CHOCOLATEY = { "alpha", "beta", "rc" };

// Defaults
const string DEFAULT_CONFIGURATION = "Release";
// NOTE: Since GitVersion is only used when running under
// Windows, the default version should be updated to the 
// next version after each release.
const string DEFAULT_VERSION = "1.0.0";

public class BuildParameters
{
    private ISetupContext _context;

	public BuildParameters(ISetupContext context)
    {
        _context = context;

		Configuration = _context.Argument("configuration", DEFAULT_CONFIGURATION);
		ProjectDirectory = _context.Environment.WorkingDirectory.FullPath + "/";

		BuildVersion = new BuildVersion(context);
    }

    public ISetupContext Context => _context;

	// Arguments
	public string Configuration { get; }

	// Versioning
	public BuildVersion BuildVersion { get; }
	public string PackageVersion => BuildVersion.PackageVersion;
	public string AssemblyVersion => BuildVersion.AssemblyVersion;
	public string AssemblyFileVersion => BuildVersion.AssemblyFileVersion;
	public string AssemblyInformationalVersion => BuildVersion.AssemblyInformationalVersion;
	public bool IsProductionRelease => !PackageVersion.Contains("-");
	public bool IsDevelopmentRelease => PackageVersion.Contains("-dev");

	// Directories
	public string ProjectDirectory { get; }
	public string SourceDirectory => ProjectDirectory + "src/";
	public string OutputDirectory => ProjectDirectory + "bin/" + Configuration + "/";
	public string ZipDirectory => ProjectDirectory + "zip/";
	public string NuGetDirectory => ProjectDirectory + "nuget/";
	public string ChocoDirectory => ProjectDirectory + "choco/";
	public string PackageDirectory => ProjectDirectory + "package/";
	public string ZipImageDirectory => PackageDirectory + "zipimage/";
	public string PackageTestDirectory => PackageDirectory + "test/";
	public string ZipTestDirectory => PackageTestDirectory + "zip/";
	public string NuGetTestDirectory => PackageTestDirectory + "nuget/";
	public string ChocolateyTestDirectory => PackageTestDirectory + "choco/";

	// Packaging
	public string NuGetPackageName => $"{NUGET_ID}.{PackageVersion}.nupkg";
	public string NuGetPackage => PackageDirectory + NuGetPackageName;
	public string ChocoPackageName => $"{CHOCO_ID}.{PackageVersion}.nupkg";
	public string ChocoPackage => PackageDirectory + ChocoPackageName;

}