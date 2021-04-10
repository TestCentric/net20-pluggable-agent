public abstract class PackageTester
{
    const string TEST_RESULT = "TestResult.xml";
    const string NUGET_GUI_RUNNER = PACKAGE_TEST_DIR + GUI_RUNNER_NUGET_ID + "." + GUI_RUNNER_VERSION + "/tools/testcentric.exe";
    const string CHOCO_GUI_RUNNER = PACKAGE_TEST_DIR + GUI_RUNNER_CHOCO_ID + "." + GUI_RUNNER_VERSION + "/tools/testcentric.exe";

    static readonly ExpectedResult EXPECTED_RESULT = new ExpectedResult("Failed")
    {
        Total = 36,
        Passed = 23,
        Failed = 5,
        Warnings = 0,
        Inconclusive = 1,
        Skipped = 7,
        Assemblies = new AssemblyResult[]
        {
            new AssemblyResult() { Name = MOCK_ASSEMBLY, Runtime = "net20" }
        }
    };

    protected ICakeContext _context;

    public PackageTester(ICakeContext context, string version)
    {
        _context = context;

        PackageVersion = version;
    }

    protected abstract string PackageId { get; }
    protected string PackageVersion { get; }
    protected string PackageName => $"{PackageId}.{PackageVersion}";
    protected string PackageTestDirectory => PACKAGE_TEST_DIR + PackageId;

    protected abstract string GuiRunner { get; }

    public abstract void CheckPackageContent();

    public void RunAllTests()
    {
        CreateTestDirectory();

        try
        {
            CheckPackageContent();

            int errors = 0;
            foreach (var runtime in new[] { "net20", "net35" })
            {
                _context.Information("Running mock-assembly tests under " + runtime);

                var actual = RunTest(runtime);

                var report = new TestReport(EXPECTED_RESULT, actual);
                errors += report.Errors.Count;
                report.DisplayErrors();
            }

            if (errors > 0)
                throw new System.Exception("A package test failed!");
        }
        finally
        {
            // We must delete the test directory so that we don't have both
            // the nuget and chocolatey packages installed at the same time.
            //RemoveTestDirectory();
        }
    }

    private void CreateTestDirectory()
    {
        _context.Information("Creating package test directory...");
        
        _context.CreateDirectory(PackageTestDirectory);
        _context.CleanDirectory(PackageTestDirectory);
        _context.Unzip(PACKAGE_DIR + PackageName + ".nupkg", PackageTestDirectory);

        _context.Information($"   {PackageTestDirectory}");
    }

    private void RemoveTestDirectory()
    {
        _context.Information("Removing package test directory");

        _context.DeleteDirectory(
            PackageTestDirectory,
            new DeleteDirectorySettings()
            {
                Recursive = true
            });
    }

    private ActualResult RunTest(string runtime)
    {
        // Delete result file ahead of time so we don't mistakenly
        // read a left-over file from another test run. Leave the
        // file after the run in case we need it to debug a failure.
        if (_context.FileExists(TEST_RESULT))
            _context.DeleteFile(TEST_RESULT);

        RunGuiUnattended($"{BIN_DIR}/{runtime}/{MOCK_ASSEMBLY}");

        return new ActualResult(TEST_RESULT);
    }

    public void RunGuiUnattended(string testAssembly)
    {
        Console.WriteLine($"Using GUI Runner {GuiRunner}");
        _context.StartProcess(GuiRunner, new ProcessSettings()
        {
            Arguments = $"{testAssembly} --run --unattended --trace:Debug"
        });
    }
}

public class NuGetPackageTester : PackageTester
{
    public NuGetPackageTester(ICakeContext context, string version)
        :base(context, version) { }

    protected override string PackageId => NUGET_ID;

    protected override string GuiRunner => $"{PACKAGE_TEST_DIR}{GUI_RUNNER_NUGET_ID}.{GUI_RUNNER_VERSION}/tools/testcentric.exe";

    public override void CheckPackageContent()
    {
        _context.Information("Checking the package");

        Check.That(PackageTestDirectory,
        HasFiles("LICENSE.txt", "CHANGES.txt"),
            HasDirectory("tools").WithFiles(LAUNCHER_FILES),
            HasDirectory("tools/agent").WithFiles(AGENT_FILES));

        _context.Information("   SUCCESS: All checks were successful");
    }
}

public class ChocolateyPackageTester : PackageTester
{
    public ChocolateyPackageTester(ICakeContext context, string version)
        : base(context, version) { }

    protected override string PackageId => CHOCO_ID;

    protected override string GuiRunner => $"{PACKAGE_TEST_DIR}{GUI_RUNNER_CHOCO_ID}.{GUI_RUNNER_VERSION}/tools/testcentric.exe";

    public override void CheckPackageContent()
    {
        _context.Information("Checking package " + PackageName);

        Check.That(PackageTestDirectory,
            HasDirectory("tools").WithFiles("LICENSE.txt", "CHANGES.txt", "VERIFICATION.txt").WithFiles(LAUNCHER_FILES),
            HasDirectory("tools/agent").WithFiles(AGENT_FILES));

        _context.Information("   SUCCESS: All checks were successful");
    }
}
