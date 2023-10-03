using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using GTestsGodot.Listeners;

namespace GTestsGodot.Runners;

public sealed class GTestsGodotTestRunner
{
    public bool IsTestsRunning => _nUnitFramework.Runner.IsTestRunning;

    readonly FrameworkController _nUnitFramework;
    readonly Dictionary<ITest, ITestResult?> _testResults = new();

    public GTestsGodotTestRunner()
    {
        _nUnitFramework = new FrameworkController(
            Assembly.GetExecutingAssembly(),
            "gnur",
            new Dictionary<string, object>()
        );
            
        _nUnitFramework.LoadTests();
    }

    public void StartTestRun(
        ITestFilter filter,
        Action<ITest> onTestSetup,
        GTestsGodotTestListener testsListener
        )
    {
        // Mark all of the tests matched by the filter as "not run".
        ITest[] testsToClear = _testResults
            .Keys
            .Where(filter.Pass)
            .ToArray();

        foreach (ITest test in testsToClear)
        {
            _testResults.Remove(test);
                
            onTestSetup.Invoke(test);
        }
        
        void WhenTestStarted(ITest test)
        {
            _testResults[test] = null;
            
            testsListener.TestStarted(test);
        }

        void WhenTestFinished(ITestResult testResult)
        {
            _testResults[testResult.Test] = testResult;
            
            testsListener.TestFinished(testResult);
        }

        GTestsGodotTestListener finalTestListener = new(
            WhenTestStarted,
            WhenTestFinished
        );
        
        _nUnitFramework.Runner.RunAsync(finalTestListener, filter);
    }

    public bool TryGetTestTree(out ITest? testTree)
    {
        testTree = _nUnitFramework.Runner.LoadedTest;
        return testTree != null;
    }
    
    public bool TryGetTestResult(ITest test, out ITestResult? testResult)
    {
        return _testResults.TryGetValue(test, out testResult);
    }
}