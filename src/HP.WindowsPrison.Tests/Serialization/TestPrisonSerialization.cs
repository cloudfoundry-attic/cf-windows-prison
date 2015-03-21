namespace HP.WindowsPrison.Tests.Serialization
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Diagnostics;
    using System.Linq;

    [TestClass]
    public class TestPrisonSerialization
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
        public void SavePrison()
        {
            // Arrange
            // prison object is arranged by test init

            // Act
            // action is done by test init

            // Assert
            Assert.IsTrue(PrisonManager.ReadAllPrisonsNoAttach().Any(p => p.Id == prison.Id));
        }

        [TestMethod]
        public void LoadPrison()
        {
            // Arrange
            PrisonConfiguration prisonRules = new PrisonConfiguration();
            prisonRules.PrisonHomeRootPath = @"c:\prison_tests\p1";
            prisonRules.Rules = RuleTypes.WindowStation;

            prison.Lockdown(prisonRules);

            // Act
            var prisonLoaded = PrisonManager.LoadPrisonAndAttach(prison.Id);

            Process process = prison.Execute(
    @"c:\windows\system32\cmd.exe",
    @"/c exit 667");

            process.WaitForExit();


            // Assert
            Process process2 = prisonLoaded.Execute(
@"c:\windows\system32\cmd.exe",
@"/c exit 667");

            process2.WaitForExit();

            // Assert
            Assert.AreEqual(667, process.ExitCode);
        }
    }
}