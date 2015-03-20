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
        [TestMethod]
        public void TestSimpleEcho()
        {
            // Arrange
            Prison prison = new Prison();
            prison.Tag = "uhtst";

            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.None;
            prisonRules.PrisonHomePath = @"c:\prison_tests\p9";

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
            Prison prison = new Prison();
            prison.Tag = "uhtst";

            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.None;
            prisonRules.PrisonHomePath = String.Format(@"c:\prison_tests\{0}", prison.Id);

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

            prison.Destroy();
        }

        [TestMethod]
        public void TestExitCode()
        {
            // Arrange
            Prison prison = new Prison();
            prison.Tag = "uhtst";

            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.None;
            prisonRules.Rules |= RuleTypes.WindowStation;

            prisonRules.PrisonHomePath = String.Format(@"c:\prison_tests\{0}", prison.Id);

            prison.Lockdown(prisonRules);

            // Act
            Process process = prison.Execute(
                @"c:\windows\system32\cmd.exe",
                @"/c exit 667");

            process.WaitForExit();

            prison.Destroy();

            // Assert
            Assert.AreEqual(667, process.ExitCode);
        }

        [TestMethod]
        public void TestPowerShell()
        {
            // Arrange
            Prison prison = new Prison();
            prison.Tag = "uhtst";

            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.None;
            prisonRules.Rules |= RuleTypes.WindowStation;
            prisonRules.Rules |= RuleTypes.IISGroup;

            prisonRules.PrisonHomePath = String.Format(@"c:\prison_tests\{0}", prison.Id);

            prison.Lockdown(prisonRules);

            // Act

            Process process = prison.Execute(
    @"c:\windows\system32\cmd.exe",
    @" /c powershell.exe -Command Get-NetIPAddress");

            process.WaitForExit();

            prison.Destroy();

            // Assert
            Assert.AreEqual(0, process.ExitCode);
        }
    }
}
