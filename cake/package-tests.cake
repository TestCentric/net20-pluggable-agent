// Package Testing
const string GUI_RUNNER_NUGET_ID = "TestCentric.GuiRunner";
const string GUI_RUNNER_CHOCO_ID = "testcentric-gui";
const string GUI_RUNNER_VERSION = "2.0.0-dev00075";

public class PackageTester
{
    const string TEST_RESULT = "TestResult.xml";

    static readonly ExpectedResult EXPECTED_RESULT = new ExpectedResult("Failed")
    {
        Total = 36,
        Passed = 23,
        Failed = 5,
        Warnings = 1,
        Inconclusive = 1,
        Skipped = 7,
        Assemblies = new AssemblyResult[]
        {
            new AssemblyResult() { Name = MOCK_ASSEMBLY, Runtime = "net20" }
        }
    };

    protected BuildParameters _parameters;
    protected ICakeContext _context;

    public PackageTester(BuildParameters parameters, string packageId, string guiRunner)
    {
        _parameters = parameters;
        _context = parameters.Context;

        PackageId = packageId;
        PackageVersion = parameters.PackageVersion;
        GuiRunner = $"{guiRunner}.{GUI_RUNNER_VERSION}/tools/testcentric.exe";
    }

    protected string PackageId { get; }
    protected string PackageVersion { get; }
    protected string PackageTestDirectory => _parameters.PackageTestDirectory + PackageId;

    protected string GuiRunner { get; }

    public void RunAllTests()
    {
        try
        {
            int errors = 0;
            foreach (var runtime in new[] { "net20", "net35" })
            {
                _context.Information("Running mock-assembly tests under " + runtime);

                var actual = RunTest(runtime); Console.WriteLine("Ran test");

                var report = new TestReport(EXPECTED_RESULT, actual); Console.WriteLine("Got report");
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

        RunGuiUnattended($"{_parameters.OutputDirectory}tests/{runtime}/{MOCK_ASSEMBLY}");

        return new ActualResult(TEST_RESULT);
    }

    public void RunGuiUnattended(string testAssembly)
    {
        Console.WriteLine($"Running Gui at {GuiRunner}");
        Console.WriteLine($"  args: {testAssembly} --run --unattended");
        _context.StartProcess(GuiRunner, new ProcessSettings()
        {
            Arguments = $"{testAssembly} --run --unattended"
        });
    }
}
