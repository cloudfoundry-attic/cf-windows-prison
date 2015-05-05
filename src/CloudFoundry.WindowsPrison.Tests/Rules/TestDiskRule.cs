using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudFoundry.WindowsPrison.Tests.Rules
{
    [TestClass]
    public class TestDiskRule
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
            prison.Tag = "prtst";
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
        public void AllowLessThenLimitDisk()
        {
            // Arrange
            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.Disk;
            prisonRules.DiskQuotaBytes = 100 * 1024 * 1024;
            prisonRules.PrisonHomeRootPath = @"C:\Workspace\dea_security\PrisonHome";

            prison.Lockdown(prisonRules);

            // Act
            string exe = Utilities.CreateExeForPrison(
@"
for (int size = 1; size < 50; size++)
{{
    byte[] content = new byte[1024 * 1024];

    File.AppendAllText(Guid.NewGuid().ToString(""N""), ASCIIEncoding.ASCII.GetString(content));
}}", prison);
            
            Process process = prison.Execute(exe);

            process.WaitForExit();

            // Assert
            Assert.AreEqual(0, process.ExitCode);
        }

        [TestMethod]
        public void DenyExcesiveDiskUsage()
        {
            // Arrange
            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.Rules = RuleTypes.Disk;
            prisonRules.DiskQuotaBytes = 50 * 1024 * 1024;
            prisonRules.PrisonHomeRootPath = @"C:\Workspace\dea_security\PrisonHome";

            prison.Lockdown(prisonRules);

            // Act
            string exe = Utilities.CreateExeForPrison(
@"
for (int size = 1; size < 100; size++)
{{
    byte[] content = new byte[1024 * 1024];

    File.AppendAllText(Guid.NewGuid().ToString(""N""), ASCIIEncoding.ASCII.GetString(content));
}}", prison);

            Process process = prison.Execute(exe);

            process.WaitForExit();

            // Assert
            Assert.AreNotEqual(0, process.ExitCode);
        }
    }
}
