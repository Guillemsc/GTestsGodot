#if TOOLS

using System;
using NUnit.Framework.Interfaces;

namespace GTestsGodot.Listeners
{
    public sealed class GTestsGodotTestListener : ITestListener
    {
        readonly Action<ITest> _testStartedCallback;
        readonly Action<ITestResult> _testFinishedCallback;

        public GTestsGodotTestListener(
            Action<ITest> testStartedCallback,
            Action<ITestResult> testFinishedCallback
            )
        {
            _testStartedCallback = testStartedCallback;
            _testFinishedCallback = testFinishedCallback;
        }
        
        public void SendMessage(TestMessage message) { }
        public void TestOutput(TestOutput output) { }
        
        public void TestStarted(ITest test)
            => _testStartedCallback?.Invoke(test);
        
        public void TestFinished(ITestResult result)
            => _testFinishedCallback?.Invoke(result);
    }
}

#endif