// Package Testing
const string GUI_RUNNER_NUGET_ID = "TestCentric.GuiRunner";
const string GUI_RUNNER_CHOCO_ID = "testcentric-gui";
const string GUI_RUNNER_VERSION = "2.0.0-dev00075";

public abstract class PackageTester
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
            new AssemblyResult() { Name = MOCK_ASSEMBLY }
        }
    };

    protected BuildParameters _parameters;
    protected ICakeContext _context;

    public PackageTester(BuildParameters parameters)
    {
        _parameters = parameters;
        _context = parameters.Context;

        GuiRunner = _parameters.PackageTestDirectory + $"{RunnerId}.{GUI_RUNNER_VERSION}/tools/testcentric.exe";
    }

    protected abstract string PackageId { get; }
    protected abstract string RunnerId { get; }

    protected string GuiRunner { get; }

    public void RunAllTests()
    {
        int errors = 0;
        foreach (var runtime in new[] { "net20", "net35" })
        {
            _context.Information($"Running {runtime} mock-assembly tests");

            var actual = RunTest(runtime);

            var report = new TestReport(EXPECTED_RESULT, actual);
            errors += report.Errors.Count;
            report.DisplayErrors();
        }

        if (errors > 0)
            throw new System.Exception("A package test failed!");
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
        _context.StartProcess(GuiRunner, new ProcessSettings()
        {
            Arguments = $"{testAssembly} --run --unattended"
        });
    }
}

public class NuGetPackageTester : PackageTester
{
    public NuGetPackageTester(BuildParameters parameters) : base(parameters) { }

    protected override string PackageId => NUGET_ID;
    protected override string RunnerId => GUI_RUNNER_NUGET_ID;
}

public class ChocolateyPackageTester : PackageTester
{
    public ChocolateyPackageTester(BuildParameters parameters) : base(parameters) { }

    protected override string PackageId => CHOCO_ID;
    protected override string RunnerId => GUI_RUNNER_CHOCO_ID;
}
