#if TOOLS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot;
using GTestsGodot.Enums;
using GTestsGodot.Filters;
using GTestsGodot.Listeners;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;

namespace GTestsGodot.EditorWidget
{
    [Tool]
    public partial class TestRunnerDock : Control
    {
        [Export] public Button RefreshButton;
        [Export] public Button RunButton;
        [Export] public Tree ResultTree;
        [Export] public RichTextLabel TestOutputLabel;
        
        readonly Dictionary<ITest, TreeItem> _testTreeItems = new();
        readonly Dictionary<ITest, ITestResult?> _testResults = new();
        
        FrameworkController? _nUnitFramework;

        public override void _Ready()
        {
            InitializeNUnitIfNeeded();
            ConnectEvents();
        }
        
        public override void _Process(double delta)
        {
            base._Process(delta);

            bool enabled = _nUnitFramework != null && !_nUnitFramework!.Runner.IsTestRunning;    
            SetButtonsEnabled(enabled);
        }

        void ConnectEvents()
        {
            RefreshButton.Connect("pressed", Callable.From(RefreshButton_Click));
            RunButton.Connect("pressed", Callable.From(RunButton_Click));
            ResultTree.Connect("item_selected", Callable.From(TestResultTree_ItemSelected));
            ResultTree.Connect("item_activated", Callable.From(TestResultTree_ItemActivated));
        }

        void InitializeNUnitIfNeeded()
        {
            if (_nUnitFramework != null)
            {
                return;
            }
            
            _nUnitFramework = new FrameworkController(
                Assembly.GetExecutingAssembly(),
                "gnur",
                new Dictionary<string, object>()
            );
            
            _nUnitFramework.LoadTests();

            RefreshAvaliableTests();
        }
        
        void RefreshButton_Click()
        {
            RefreshAvaliableTests();
        }

        void RunButton_Click()
        {
            RefreshAvaliableTests();
            StartTestRun(new MatchEverythingTestFilter());
        }

        void TestResultTree_ItemSelected()
        {
            TreeItem selectedItem = ResultTree.GetSelected();

            bool testFound = TryGetTestFromTreeItem(selectedItem, out ITest? test);

            if (!testFound)
            {
                return;
            }
            
            DisplayTestOutput(test!);
        }

        void TestResultTree_ItemActivated()
        {
            TreeItem selectedItem = ResultTree.GetSelected();
            
            bool testFound = TryGetTestFromTreeItem(selectedItem, out ITest? test);

            if (!testFound)
            {
                return;
            }
            
            StartTestRun(new MatchDescendantsOfFilter(test!));
        }

        void RefreshAvaliableTests()
        {
            _testTreeItems.Clear();
            ResultTree.Clear();

            CreateTreeItemForTest(_nUnitFramework!.Runner.LoadedTest);

            foreach (ITest test in _testTreeItems.Keys)
            {
                UpdateTestTreeItem(test);
            }
        }
        
        void StartTestRun(ITestFilter filter)
        {
            InitializeNUnitIfNeeded();
            
            // Mark all of the tests matched by the filter as "not run".
            ITest[] testsToClear = _testResults
                .Keys
                .Where(filter.Pass)
                .ToArray();

            foreach (ITest test in testsToClear)
            {
                _testResults.Remove(test);
                
                UpdateTestTreeItem(test);
            }

            // Whenever a test starts or finishes, update its results and its tree item.
            GTestsGodotTestListener testListener = new(
                WhenTestStarted,
                WhenTestFinished
            );
            
            _nUnitFramework!.Runner.RunAsync(testListener, filter);
        }
        
        void WhenTestStarted(ITest test)
        {
            void Run()
            {
                CreateTreeItemForTest(test);
                _testResults[test] = null;
                UpdateTestTreeItem(test);
            }
            
            Callable.From(Run).CallDeferred();
        }

        void WhenTestFinished(ITestResult testResult)
        {
            void Run()
            {
                _testResults[testResult.Test] = testResult;
                UpdateTestTreeItem(testResult.Test);
            }
            
            Callable.From(Run).CallDeferred();
        }

        void SetButtonsEnabled(bool enabled)
        {
            bool disabled = !enabled;

            RefreshButton.Disabled = disabled;
            RunButton.Disabled = disabled;
        }
        
        void UpdateTestTreeItem(ITest test)
        {
            TreeItem treeItem = _testTreeItems[test];
            treeItem.SetText(0, GetTestLabel(test));

            if (test.Parent == null)
            {
                return;
            }
            
            // Recursively update all ancestor items
            UpdateTestTreeItem(test.Parent);
        }

        string GetTestLabel(ITest test)
        {
            TestState state = GetTestState(test);
            string icon = TestStateToIcon(state);

            if (!test.IsSuite)
            {
                return $"{icon} {test.Name}";
            }

            bool hasTestResults = _testResults.TryGetValue(test, out ITestResult? testResult);
            
            if (!hasTestResults || _testResults[test] == null)
            {
                return $"{icon} {test.Name} ({test.TestCaseCount} found)";
            }
            
            return $"{icon} {test.Name} ({testResult!.PassCount} / {test.TestCaseCount} passing)";
        }

        /// <summary>
        /// Gets a value corresponding to the "icon" that should be displayed
        /// next to a test's name
        /// 
        /// If there are children, it examines the results of all of them and
        /// returns the "worst" of them.
        /// </summary>
        /// <param name="test"></param>
        /// <returns></returns>
        TestState GetTestState(ITest test)
        {
            // Recursive case: find the worst of the children.
            if (test.HasChildren)
            {
                TestState worstState = TestState.Passed;

                foreach (ITest child in test.Tests)
                {
                    var childState = GetTestState(child);
                    
                    if (childState < worstState)
                    {
                        worstState = childState;
                    }
                }

                return worstState;
            }

            bool hasTestResults = _testResults.TryGetValue(test, out ITestResult? testResult);

            // Tests that haven't been run do not have an entry in _testResults.
            if (!hasTestResults)
            {
                return TestState.NotRun;
            }

            // Tests that are in progress have a null entry in _testResults
            if (testResult == null)
            {
                return TestState.InProgress;
            }

            // All others are self-explanatory.
            return testResult.ResultState.Status switch
            {
                TestStatus.Failed => TestState.Failed,
                TestStatus.Passed => TestState.Passed,
                TestStatus.Skipped => TestState.Skipped,
                TestStatus.Warning => TestState.Warning,
                TestStatus.Inconclusive => TestState.Inconclusive,
                _ => TestState.Inconclusive
            };
        }

        string TestStateToIcon(TestState state)
        {
            return state switch
            {
                TestState.NotRun => string.Empty,
                TestState.InProgress => "(...)",
                TestState.Passed => "\u2714\ufe0f",
                TestState.Failed => "\u274c",
                TestState.Warning => "\u26a0\ufe0f",
                TestState.Inconclusive => "?",
                _ => "?"
            };
        }


        void DisplayTestOutput(ITest test)
        {
            bool hasTestResults = _testResults.TryGetValue(test, out ITestResult? testResult);
            
            if (!hasTestResults)
            {
                TestOutputLabel.Text = "Test not run.";
                return;
            }

            if (testResult == null)
            {
                TestOutputLabel.Text = "Test in progress...";
                return;
            }

            if (testResult.ResultState.Status == TestStatus.Passed)
            {
                TestOutputLabel.Text = "Test passed.";
                return;
            }

            StringBuilder builder = new();

            builder.AppendLine(testResult.Name);
            
            void PrintIfNotEmpty(string? message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    builder.AppendLine(message);
                }
            }
            
            PrintIfNotEmpty(testResult.Message);
            PrintIfNotEmpty(testResult.Output);

            if (testResult.ResultState.Status != TestStatus.Passed)
            {
                PrintIfNotEmpty(testResult.StackTrace);
            }

            TestOutputLabel.Text = builder.ToString();
        }

        void CreateTreeItemForTest(ITest test)
        {
            if (_testTreeItems.ContainsKey(test))
            {
                return;
            }

            // Create a tree item for this test
            TreeItem? parentTreeItem = test.Parent == null
                ? null
                : _testTreeItems[test.Parent];

            TreeItem treeItem = ResultTree.CreateItem(parentTreeItem);
            _testTreeItems[test] = treeItem;

            // Create tree items for all child tests
            foreach (ITest child in test.Tests)
            {
                CreateTreeItemForTest(child);
            }
        }

        bool TryGetTestFromTreeItem(TreeItem treeItem, out ITest? test)
        {
            test = _testTreeItems
                .Where(kvp => kvp.Value == treeItem)
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            return test != null;
        }
    }
}

#endif