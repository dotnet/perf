// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Crank.Agent;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Crank.IntegrationTests
{
    public class CommonTests : IClassFixture<AgentFixture>
    {
        private readonly ITestOutputHelper _output;
        private string _crankDirectory;
        private string _crankTestsDirectory;

        public CommonTests(ITestOutputHelper output, AgentFixture fixture)
        {
            _output = output;
            _crankDirectory = Path.GetDirectoryName(typeof(CommonTests).Assembly.Location).Replace("Microsoft.Crank.IntegrationTests", "Microsoft.Crank.Controller");
            _crankTestsDirectory = Path.GetDirectoryName(typeof(CommonTests).Assembly.Location);
            _output.WriteLine($"Running tests in {_crankDirectory}");
        }

        [Fact]
        public async Task ControllerDisplaysErrorForMissingScenario()
        {
            var result = await ProcessUtil.RunAsync(
                "dotnet", 
                "exec crank.dll", 
                workingDirectory: _crankDirectory,
                captureOutput: true,
                throwOnError: false
            );

            Assert.Equal(1, result.ExitCode);
            // Assert.Equal("No jobs were found. Are you missing the --scenario argument?", result.StandardOutput);
        }

        [Fact]
        public async Task BenchmarkHello()
        {
            _output.WriteLine($"Starting controller");

            var result = await ProcessUtil.RunAsync(
                "dotnet", 
                $"exec {Path.Combine(_crankDirectory, "crank.dll")} --config ./assets/hello.benchmarks.yml --scenario hello --profile local", 
                workingDirectory: _crankTestsDirectory,
                captureOutput: true,
                timeout: TimeSpan.FromMinutes(5),
                throwOnError: false,
                outputDataReceived: t => { _output.WriteLine($"[CTL] {t}"); } 
            );

            Assert.Equal(0, result.ExitCode);
                
            Assert.Contains("Requests/sec", result.StandardOutput);
            Assert.Contains(".NET Core SDK Version", result.StandardOutput);
            Assert.Contains(".NET Runtime Version", result.StandardOutput);
            Assert.Contains("ASP.NET Core Version", result.StandardOutput);
        }

        [Fact]
        public async Task ExecutesScripts()
        {
            _output.WriteLine($"Starting controller");

            var result = await ProcessUtil.RunAsync(
                "dotnet", 
                $"exec {Path.Combine(_crankDirectory, "crank.dll")} --config ./assets/hello.benchmarks.yml --scenario hello --profile local --config ./assets/scripts.benchmarks.yml --script add_current_time --output results.json", 
                workingDirectory: _crankTestsDirectory,
                captureOutput: true,
                timeout: TimeSpan.FromMinutes(5),
                throwOnError: false,
                outputDataReceived: t => { _output.WriteLine($"[CTL] {t}"); } 
            );

            Assert.Equal(0, result.ExitCode);
                
            var results = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(_crankTestsDirectory, "results.json")));
            
            Assert.Contains("a default script", result.StandardOutput);
            Assert.NotEmpty(results.RootElement.GetProperty("jobResults").GetProperty("properties").GetProperty("time").GetString());
        }

        [Fact]
        public async Task DotnetCounters()
        {
            _output.WriteLine($"Starting controller");

            var result = await ProcessUtil.RunAsync(
                "dotnet", 
                $"exec {Path.Combine(_crankDirectory, "crank.dll")} --config ./assets/hello.benchmarks.yml --scenario hello --profile local --application.options.counterProviders System.Runtime", 
                workingDirectory: _crankTestsDirectory,
                captureOutput: true,
                timeout: TimeSpan.FromMinutes(5),
                throwOnError: false,
                outputDataReceived: t => { _output.WriteLine($"[CTL] {t}"); } 
            );

            Assert.Equal(0, result.ExitCode);

            Assert.Contains("Lock Contention", result.StandardOutput);
        }
        
        [Fact]
        public async Task Iterations()
        {
            _output.WriteLine($"Starting controller");

            var result = await ProcessUtil.RunAsync(
                "dotnet", 
                $"exec {Path.Combine(_crankDirectory, "crank.dll")} --config ./assets/hello.benchmarks.yml --scenario hello --profile local --exclude 1 --exclude-order load:bombardier/rps/mean --iterations 3", 
                workingDirectory: _crankTestsDirectory,
                captureOutput: true,
                timeout: TimeSpan.FromMinutes(5),
                throwOnError: false,
                outputDataReceived: t => { _output.WriteLine($"[CTL] {t}"); } 
            );

            Assert.Equal(0, result.ExitCode);
                
            Assert.Contains("Iteration 1 of 3", result.StandardOutput);
            Assert.Contains("Iteration 2 of 3", result.StandardOutput);
            Assert.Contains("Iteration 3 of 3", result.StandardOutput);
            Assert.Contains("Excluded values:", result.StandardOutput);
        }
        
        [Fact]
        public async Task MultiClients()
        {
            _output.WriteLine($"Starting controller");

            var result = await ProcessUtil.RunAsync(
                "dotnet", 
                $"exec {Path.Combine(_crankDirectory, "crank.dll")} --config ./assets/multiclient.benchmarks.yml --scenario hello --profile local", 
                workingDirectory: _crankTestsDirectory,
                captureOutput: true,
                timeout: TimeSpan.FromMinutes(5),
                throwOnError: false,
                outputDataReceived: t => { _output.WriteLine($"[CTL] {t}"); } 
            );

            Assert.Equal(0, result.ExitCode);
                
            // Two load jobs are started
            var firstLoad = result.StandardOutput.IndexOf("'load' is now building");
            var secondLoad = result.StandardOutput.IndexOf("'load' is now building", firstLoad + 1);

            Assert.NotEqual(-1, firstLoad);
            Assert.NotEqual(-1, secondLoad);          

            // The results are computed
            Assert.Contains("Requests/sec", result.StandardOutput);
        }
    }
}
