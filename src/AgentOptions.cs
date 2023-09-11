// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric contributors.
// Licensed under the MIT License. See LICENSE file in root directory.
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Engine;

namespace TestCentric.Agents
{
    /// <summary>
    /// All agents, either built-in or pluggable, must be able to 
    /// handle the options defined in this class. In some cases,
    /// it may be permissible to ignore them but they should never
    /// give rise to an error.
    /// </summary>
    public class AgentOptions
    {
        static readonly char[] DELIMS = new[] { '=', ':' };
        static readonly Dictionary<string, bool> VALID_OPTIONS = new Dictionary<string, bool>();
       
        static AgentOptions()
        {
            VALID_OPTIONS["agentId"] = true;
            VALID_OPTIONS["agencyUrl"] = true;
            VALID_OPTIONS["debug-agent"] = false;
            VALID_OPTIONS["debug-tests"] = false;
            VALID_OPTIONS["trace"] = true;
            VALID_OPTIONS["pid"] = true;
            VALID_OPTIONS["work"] = true;
        }

        public AgentOptions(params string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (IsOption(arg))
                {
                    var option = arg.Substring(2);
                    var delim = option.IndexOfAny(DELIMS);
                    var opt = option;
                    string val = null;
                    if (delim > 0)
                    {
                        opt = option.Substring(0, delim);
                        val = option.Substring(delim + 1);
                    }

                    if (!VALID_OPTIONS.ContainsKey(opt))
                        InvalidArgumentError(arg);

                    bool optionTakesValue = VALID_OPTIONS[opt];

                    if (optionTakesValue)
                    {
                        if (val == null && i + 1 < args.Length)
                            val = args[++i];

                        if (val == null)
                            ValueRequiredError(arg);
                    }
                    else if (delim > 0)
                    {
                        ValueNotAllowedError(arg);
                    }

                    switch (opt)
                    {
                        case "agentId":
                            AgentId = new Guid(val);
                            break;
                        case "agencyUrl":
                            AgencyUrl = val;
                            break;
                        case "debug-agent":
                            DebugAgent = true;
                            break;
                        case "debug-tests":
                            DebugTests = true;
                            break;
                        case "trace":
                            TraceLevel = (InternalTraceLevel)Enum.Parse(typeof(InternalTraceLevel), val);
                            break;
                        case "pid":
                            AgencyPid = val;
                            break;
                        case "work":
                            WorkDirectory = val;
                            break;
                        default:
                            InvalidArgumentError(arg);
                            break;
                    }
                }
                else if (File.Exists(arg))
                    Files.Add(arg);
                else
                    FileNotFoundError(arg);
            }

            if (Files.Count > 1)
                TooManyFilesError(Files.Count);
        }

        public Guid AgentId { get; } = Guid.Empty;
        public string AgencyUrl { get; } = string.Empty;
        public string AgencyPid { get; } = string.Empty;
        public bool DebugTests { get; } = false;
        public bool DebugAgent { get; } = false;
        public InternalTraceLevel TraceLevel { get; } = InternalTraceLevel.Off;
        public string WorkDirectory { get; } = string.Empty;

        public List<string> Files { get; } = new List<string>();

        private static bool IsOption(string arg)
        {
            return arg.StartsWith("--");
        }

        private void ValueRequiredError(string arg) =>
            throw new Exception($"Option requires a value: {arg}");

        private void ValueNotAllowedError(string arg) =>
            throw new Exception($"Option does not take a value: {arg}");

        private void InvalidArgumentError(string arg) =>
            throw new Exception($"Invalid argument: {arg}");

        private void FileNotFoundError(string arg) =>
            throw new FileNotFoundException($"FileNotFound: {arg}");

        private void TooManyFilesError(int count) =>
            throw new ArgumentException("Only one file argument is allowed but {count} were supplied");
    }
}
