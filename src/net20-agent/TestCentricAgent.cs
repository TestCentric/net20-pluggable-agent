// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric contributors.
// Licensed under the MIT License. See LICENSE file in root directory.
// ***********************************************************************

using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using TestCentric.Engine.Agents;
using TestCentric.Engine.Internal;
#if NETFRAMEWORK
using TestCentric.Engine.Communication.Transports.Remoting;
#else
using TestCentric.Engine.Communication.Transports.Tcp;
#endif

namespace TestCentric.Agents
{
    public class TestCentricAgent<TAgent>
    {
        static Process AgencyProcess;
        static RemoteTestAgent Agent;
        private static Logger log;
        static int _pid = Process.GetCurrentProcess().Id;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Execute(string[] args)
        {
            var options = new AgentOptions(args);
            var logName = $"testcentric-agent_{_pid}.log";

            InternalTrace.Initialize(Path.Combine(options.WorkDirectory, logName), options.TraceLevel);
            log = InternalTrace.GetLogger(typeof(TAgent));
            log.Info($"{typeof(TAgent).Name} process {_pid} starting");

            if (options.DebugAgent || options.DebugTests)
                TryLaunchDebugger();

            if (!string.IsNullOrEmpty(options.AgencyUrl))
                RegisterAndWaitForCommands(options);
            else if (options.Files.Count != 0)
                new AgentDirectRunner(options).ExecuteTestsDirectly();
            else
                throw new ArgumentException("No file specified for direct execution");
        }

        private static void RegisterAndWaitForCommands(AgentOptions options)
        {
            log.Info($"  AgentId:   {options.AgentId}");
            log.Info($"  AgencyUrl: {options.AgencyUrl}");
            log.Info($"  AgencyPid: {options.AgencyPid}");

            if (!string.IsNullOrEmpty(options.AgencyPid))
                LocateAgencyProcess(options.AgencyPid);

            log.Info("Starting RemoteTestAgent");
            Agent = new RemoteTestAgent(options.AgentId);
#if NETFRAMEWORK
            Agent.Transport = new TestAgentRemotingTransport(Agent, options.AgencyUrl);
#else
            Agent.Transport = new TestAgentTcpTransport(Agent, options.AgencyUrl);
#endif

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
            log.Info("Agent process {0} exiting cleanly", _pid);

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
