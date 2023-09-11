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
using TestCentric.Engine.Communication.Transports.Remoting;

namespace TestCentric.Agents
{
    public class Net40Agent : TestCentricAgent<Net40Agent>
    {
        public static void Main(string[] args) => TestCentricAgent<Net40Agent>.Execute(args);
    }
}
