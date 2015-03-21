using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HP.WindowsPrison.Tests.JobObjects
{
    [TestClass]
    public class TestJobObjects
    {
        Prison prison = null;

        [ClassInitialize]
        public static void PrisonInit(TestContext context)
        {
            Prison.Init();
        }

        [TestInitialize]
        public void PrisonTestSetup()
        {
            prison = new Prison();
            prison.Tag = "uhtst";
        }

        [TestCleanup]
        public void PrisonTestCleanup()
        {
            if (prison != null)
            {
                prison.Destroy();
                prison.Dispose();
                prison = null;
            }
        }


        [TestMethod]
        public void TestSimpleEcho()
        {
            // Arrange
            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.None;
            prisonRules.PrisonHomeRootPath = @"c:\prison_tests\p9";

            prison.Lockdown(prisonRules);

            // Act
            Process process = prison.Execute(
                @"c:\windows\system32\cmd.exe",
                @"/c echo test");

            // Assert
            Assert.AreNotEqual(0, process.Id);
        }

        [TestMethod]
        public void TestMultipleEcho()
        {
            // Arrange
            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.None;
            prisonRules.PrisonHomeRootPath = String.Format(@"c:\prison_tests\{0}", prison.Id);

            prison.Lockdown(prisonRules);

            // Act
            Process process1 = prison.Execute(
                @"c:\windows\system32\cmd.exe",
                @"/c echo test");

            Process process2 = prison.Execute(
                @"c:\windows\system32\cmd.exe",
                @"/c echo test");

            // Assert
            Assert.AreNotEqual(0, process1.Id);
            Assert.AreNotEqual(0, process2.Id);
        }

        [TestMethod]
        public void TestExitCode()
        {
            // Arrange
            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.None;
            prisonRules.Rules |= RuleTypes.WindowStation;
            prisonRules.PrisonHomeRootPath = String.Format(@"c:\prison_tests\{0}", prison.Id);

            prison.Lockdown(prisonRules);

            // Act
            Process process = prison.Execute(
                @"c:\windows\system32\cmd.exe",
                @"/c exit 667");

            process.WaitForExit();

            // Assert
            Assert.AreEqual(667, process.ExitCode);
        }

        [TestMethod]
        public void TestPowerShell()
        {
            // Arrange
            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.None;
            prisonRules.Rules |= RuleTypes.WindowStation;
            prisonRules.Rules |= RuleTypes.IISGroup;
            prisonRules.PrisonHomeRootPath = String.Format(@"c:\prison_tests\{0}", prison.Id);

            prison.Lockdown(prisonRules);

            // Act

            Process process = prison.Execute(
    @"c:\windows\system32\cmd.exe",
    @" /c powershell.exe -Command Get-NetIPAddress");

            process.WaitForExit();

            // Assert
            Assert.AreEqual(0, process.ExitCode);
        }
    }
}
