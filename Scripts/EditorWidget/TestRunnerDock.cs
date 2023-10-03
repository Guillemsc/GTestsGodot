#if TOOLS

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using GTestsGodot.Enums;
using GTestsGodot.Filters;
using GTestsGodot.Listeners;
using GTestsGodot.Runners;
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
        [Export] public Texture2D DotTexture;
        
        readonly Dictionary<ITest, TreeItem> _testTreeItems = new();

        GTestsGodotTestRunner? _gTestsGodotTestRunner;
        
        public override void _Ready()
        {
            InitializeTestRunner();
            ConnectEvents();
        }
        
        public override void _Process(double delta)
        {
            base._Process(delta);

            bool enabled = _gTestsGodotTestRunner != null && !_gTestsGodotTestRunner!.IsTestsRunning;    
            SetButtonsEnabled(enabled);
        }

        void ConnectEvents()
        {
            RefreshButton.Connect("pressed", Callable.From(RefreshButton_Click));
            RunButton.Connect("pressed", Callable.From(RunButton_Click));
            ResultTree.Connect("item_selected", Callable.From(TestResultTree_ItemSelected));
            ResultTree.Connect("item_activated", Callable.From(TestResultTree_ItemActivated));
        }

        void InitializeTestRunner()
        {
            if (_gTestsGodotTestRunner != null)
            {
                return;
            }

            _gTestsGodotTestRunner = new GTestsGodotTestRunner();

            RefreshAvaliableTests();
        }
        
        void RefreshButton_Click()
        {
            RefreshAvaliableTests();
        }

        void RunButton_Click()
        {
            RefreshAvaliableTests();
            StartTestRun(MatchEverythingTestFilter.Instance);
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

            bool hasLoadedTests = _gTestsGodotTestRunner!.TryGetTestTree(out ITest? testsTree);

            if (!hasLoadedTests)
            {
                return;
            }

            CreateTreeItemForTest(testsTree!);

            foreach (ITest test in _testTreeItems.Keys)
            {
                UpdateTestTreeItem(test);
            }
        }
        
        void StartTestRun(ITestFilter filter)
        {
            InitializeTestRunner();
            
            GTestsGodotTestListener testListener = new(
                WhenTestStarted,
                WhenTestFinished
            );
            
            _gTestsGodotTestRunner!.StartTestRun(filter, UpdateTestTreeItem, testListener);
        }
        
        void WhenTestStarted(ITest test)
        {
            void Run()
            {
                CreateTreeItemForTest(test);
                UpdateTestTreeItem(test);
            }
            
            Callable.From(Run).CallDeferred();
        }

        void WhenTestFinished(ITestResult testResult)
        {
            void Run()
            {
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
            bool hasTreeItem = _testTreeItems.TryGetValue(test, out TreeItem? treeItem);

            if (!hasTreeItem)
            {
                return;
            }
            
            TestState state = GetTestState(test);
            string label = GetTestLabel(test);
            Color iconColor = TestStateToIconColor(state);
            
            treeItem!.SetText(0, label);
            treeItem!.SetIconModulate(0, iconColor);

            if (test.Parent == null)
            {
                return;
            }
            
            UpdateTestTreeItem(test.Parent);
        }

        string GetTestLabel(ITest test)
        {
            if (!test.IsSuite)
            {
                return $"{test.Name}";
            }

            bool hasTestResults = _gTestsGodotTestRunner!.TryGetTestResult(test, out ITestResult? testResult);
            
            if (!hasTestResults || testResult == null)
            {
                return $"{test.Name} ({test.TestCaseCount} found)";
            }
            
            return $"{test.Name} ({testResult!.PassCount} / {test.TestCaseCount} passing)";
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

            bool hasTestResults = _gTestsGodotTestRunner!.TryGetTestResult(test, out ITestResult? testResult);

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
        
        Color TestStateToIconColor(TestState state)
        {
            return state switch
            {
                TestState.NotRun => new Color(0.8f, 0.8f, 0.8f),
                TestState.InProgress => new Color(0.8f, 0.8f, 0.8f),
                TestState.Passed => new Color(0.3f, 1f, 0.3f),
                TestState.Failed => new Color(1f, 0.3f, 0.3f),
                TestState.Warning => new Color(0.3f, 0.3f, 0.3f),
                TestState.Inconclusive => new Color(0.4f, 0.4f, 0.4f),
                _ => new Color(0.2f, 0.2f, 0.2f)
            };
        }


        void DisplayTestOutput(ITest test)
        {
            bool hasTestResults = _gTestsGodotTestRunner!.TryGetTestResult(test, out ITestResult? testResult);
            
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
            
            treeItem.SetIcon(0, DotTexture);
            treeItem.SetIconMaxWidth(0, 10);
            
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