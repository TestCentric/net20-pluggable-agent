// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric contributors.
// Licensed under the MIT License. See LICENSE file in root directory.
// ***********************************************************************

using System.Reflection;
using NUnitLite;

namespace TestCentric.Engine
{
    class Program
    {
        static int Main(string[] args)
        {
            return new AutoRun().Execute(args);
        }
    }
}
