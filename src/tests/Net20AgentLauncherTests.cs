// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric Engine contributors.
// Licensed under the MIT License. See LICENSE.txt in root directory.
// ***********************************************************************

using System;
using System.Linq;
using NUnit.Framework;
using System.IO;
using System.Diagnostics;
using NUnit.Engine;

namespace TestCentric.Engine.Services
{
    public class Net20AgentLauncherTests
    {
        private static readonly Guid AGENTID = Guid.NewGuid();
        private const string AGENT_URL = "tcp://127.0.0.1:1234/TestAgency";
        private static readonly string REQUIRED_ARGS = $"{AGENT_URL} --pid={Process.GetCurrentProcess().Id}";
        private const string AGENT_NAME = "net20-pluggable-agent.exe";
        private const string AGENT_NAME_X86 = "net20-pluggable-agent-x86.exe";
        private static string AGENT_DIR = Path.Combine(TestContext.CurrentContext.TestDirectory, "agent");

        // Constants used for settings
        private const string TARGET_RUNTIME_FRAMEWORK = "TargetRuntimeFramework";
        private const string RUN_AS_X86 = "RunAsX86";
        private const string DEBUG_AGENT = "DebugAgent";
        private const string TRACE_LEVEL = "InternalTraceLevel";
        private const string WORK_DIRECTORY = "WorkDirectory";
        private const string LOAD_USER_PROFILE = "LoadUserProfile";


        private static readonly string[] RUNTIMES = new string[]
        {
            "net-2.0", "net-3.0", "net-3.5", "net-4.0", "net-4.5",
            "netcore-1.1", "netcore-2.1", "netcore-3.1", "netcore-5.0"
        };

        private static readonly string[] SUPPORTED = new string[] { "net-2.0", "net-3.0", "net-3.5" };

        private Net20AgentLauncher _launcher;
        private TestPackage _package;

        [SetUp]
        public void SetUp()
        {
            _launcher = new Net20AgentLauncher();
            _package = new TestPackage("junk.dll");
        }

        [TestCaseSource(nameof(RUNTIMES))]
        public void CanCreateProcess(string runtime)
        {
            _package.Settings[TARGET_RUNTIME_FRAMEWORK] = runtime;
            _package.Settings[RUN_AS_X86] = false;

            bool supported = SUPPORTED.Contains(runtime);
            Assert.That(_launcher.CanCreateProcess(_package), Is.EqualTo(supported));
        }

        [TestCaseSource(nameof(RUNTIMES))]
        public void CanCreateX86Process(string runtime)
        {
            _package.Settings[TARGET_RUNTIME_FRAMEWORK] = runtime;
            _package.Settings[RUN_AS_X86] = true;

            bool supported = SUPPORTED.Contains(runtime);
            Assert.That(_launcher.CanCreateProcess(_package), Is.EqualTo(supported));
        }

        [TestCaseSource(nameof(RUNTIMES))]
        public void CreateProcess(string runtime)
        {
            _package.Settings[TARGET_RUNTIME_FRAMEWORK] = runtime;
            _package.Settings[RUN_AS_X86] = false;

            if (SUPPORTED.Contains(runtime))
            {
                var process = _launcher.CreateProcess(AGENTID, AGENT_URL, _package);
                CheckStandardProcessSettings(process);
                CheckAgentPath(process, false);
            }
            else
            {
                Assert.That(_launcher.CreateProcess(AGENTID, AGENT_URL, _package), Is.Null);
            }
        }

        private void CheckAgentPath(Process process, bool x86)
        {
            string agentPath = Path.Combine(AGENT_DIR, x86 ? AGENT_NAME_X86 : AGENT_NAME);
            Assert.That(process.StartInfo.FileName, Is.SamePath(agentPath));
        }

        [TestCaseSource(nameof(RUNTIMES))]
        public void CreateX86Process(string runtime)
        {
            _package.Settings[TARGET_RUNTIME_FRAMEWORK] = runtime;
            _package.Settings[RUN_AS_X86] = true;

            if (SUPPORTED.Contains(runtime))
            {
                var process = _launcher.CreateProcess(AGENTID, AGENT_URL, _package);
                CheckStandardProcessSettings(process);
                CheckAgentPath(process, true);
            }
            else
            {
                Assert.That(_launcher.CreateProcess(AGENTID, AGENT_URL, _package), Is.Null);
            }
        }

        private void CheckStandardProcessSettings(Process process)
        {
            Assert.NotNull(process);
            Assert.True(process.EnableRaisingEvents, "EnableRaisingEvents");

            var startInfo = process.StartInfo;
            Assert.False(startInfo.UseShellExecute, "UseShellExecute");
            Assert.True(startInfo.CreateNoWindow, "CreateNoWindow");
            Assert.False(startInfo.LoadUserProfile, "LoadUserProfile");
            Assert.That(startInfo.WorkingDirectory, Is.EqualTo(Environment.CurrentDirectory));

            var arguments = startInfo.Arguments;
            Assert.That(arguments, Does.Not.Contain("--trace="));
            Assert.That(arguments, Does.Not.Contain("--debug-agent"));
            Assert.That(arguments, Does.Not.Contain("--work="));
        }

        [Test]
        public void DebugAgentSetting()
        {
            var runtime = SUPPORTED[0];
            _package.Settings[TARGET_RUNTIME_FRAMEWORK] = runtime;
            _package.Settings[DEBUG_AGENT] = true;
            var agentProcess = _launcher.CreateProcess(AGENTID, AGENT_URL, _package);
            Assert.That(agentProcess.StartInfo.Arguments, Does.Contain("--debug-agent"));
        }

        [Test]
        public void TraceLevelSetting()
        {
            var runtime = SUPPORTED[0];
            _package.Settings[TARGET_RUNTIME_FRAMEWORK] = runtime;
            _package.Settings[TRACE_LEVEL] = "Debug";
            var agentProcess = _launcher.CreateProcess(AGENTID, AGENT_URL, _package);
            Assert.That(agentProcess.StartInfo.Arguments, Does.Contain("--trace=Debug"));
        }

        [Test]
        public void WorkDirectorySetting()
        {
            var runtime = SUPPORTED[0];
            _package.Settings[TARGET_RUNTIME_FRAMEWORK] = runtime;
            _package.Settings[WORK_DIRECTORY] = "WORKDIRECTORY";
            var agentProcess = _launcher.CreateProcess(AGENTID, AGENT_URL, _package);
            Assert.That(agentProcess.StartInfo.Arguments, Does.Contain("--work=WORKDIRECTORY"));
        }

        [Test]
        public void LoadUserProfileSetting()
        {
            var runtime = SUPPORTED[0];
            _package.Settings[TARGET_RUNTIME_FRAMEWORK] = runtime;
            _package.Settings[LOAD_USER_PROFILE] = true;
            var agentProcess = _launcher.CreateProcess(AGENTID, AGENT_URL, _package);
            Assert.True(agentProcess.StartInfo.LoadUserProfile);
        }
    }
}
