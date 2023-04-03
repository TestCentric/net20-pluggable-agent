// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric contributors.
// Licensed under the MIT License. See LICENSE file in root directory.
// ***********************************************************************

using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using TestCentric.Engine.Internal;
using TestCentric.Engine.Communication.Transports.Remoting;

namespace TestCentric.Engine.Agents
{
    public class TestCentricAgent
    {
        static Process AgencyProcess;
        static RemoteTestAgent Agent;
        private static Logger log;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            var options = new AgentOptions(args);
            var pid = Process.GetCurrentProcess().Id;
            var logName = $"testcentric-agent_{pid}.log";

            InternalTrace.Initialize(Path.Combine(options.WorkDirectory, logName), options.TraceLevel);
            log = InternalTrace.GetLogger(typeof(TestCentricAgent));

            if (options.DebugAgent || options.DebugTests)
                TryLaunchDebugger();

            LocateAgencyProcess(options.AgencyPid);

            log.Info($".NET 2.0 Agent process {pid} starting");
            log.Info($"  AgentId:   {options.AgentId}");
            log.Info($"  AgencyUrl: {options.AgencyUrl}");

            log.Info("Starting RemoteTestAgent");
            Agent = new RemoteTestAgent(options.AgentId);
            Agent.Transport = new TestAgentRemotingTransport(Agent, options.AgencyUrl);

            try
            {
                if (Agent.Start())
                    WaitForStop();
                else
                {
                    log.Error("Failed to start RemoteTestAgent");
                    Environment.Exit(AgentExitCodes.FAILED_TO_START_REMOTE_AGENT);
                }
            }
            catch (Exception ex)
            {
                log.Error("Exception in RemoteTestAgent. {0}", ExceptionHelper.BuildMessageAndStackTrace(ex));
                Environment.Exit(AgentExitCodes.UNEXPECTED_EXCEPTION);
            }
            log.Info("Agent process {0} exiting cleanly", pid);

            Environment.Exit(AgentExitCodes.OK);
        }

        private static void LocateAgencyProcess(string agencyPid)
        {
            var agencyProcessId = int.Parse(agencyPid);
            try
            {
                AgencyProcess = Process.GetProcessById(agencyProcessId);
            }
            catch (Exception e)
            {
                log.Error($"Unable to connect to agency process with PID: {agencyProcessId}");
                log.Error($"Failed with exception: {e.Message} {e.StackTrace}");
                Environment.Exit(AgentExitCodes.UNABLE_TO_LOCATE_AGENCY);
            }
        }

        private static void WaitForStop()
        {
            log.Debug("Waiting for stopSignal");

            while (!Agent.WaitForStop(500))
            {
                if (AgencyProcess.HasExited)
                {
                    log.Error("Parent process has been terminated.");
                    Environment.Exit(AgentExitCodes.PARENT_PROCESS_TERMINATED);
                }
            }

            log.Debug("Stop signal received");
        }

        private static void TryLaunchDebugger()
        {
            if (Debugger.IsAttached)
                return;

            try
            {
                Debugger.Launch();
            }
            catch (SecurityException se)
            {
                if (InternalTrace.Initialized)
                {
                    log.Error($"System.Security.Permissions.UIPermission is not set to start the debugger. {se} {se.StackTrace}");
                }
                Environment.Exit(AgentExitCodes.DEBUGGER_SECURITY_VIOLATION);
            }
            catch (NotImplementedException nie) //Debugger is not implemented on mono
            {
                if (InternalTrace.Initialized)
                {
                    log.Error($"Debugger is not available on all platforms. {nie} {nie.StackTrace}");
                }
                Environment.Exit(AgentExitCodes.DEBUGGER_NOT_IMPLEMENTED);
            }
        }
    }
}
