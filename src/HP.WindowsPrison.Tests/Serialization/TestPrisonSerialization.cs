namespace HP.WindowsPrison.Tests.Serialization
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Diagnostics;
    using System.Linq;

    [TestClass]
    public class TestPrisonSerialization
    {
        [TestMethod]
        public void SavePrison()
        {
            // Arrange

            // Act
            Prison prison = new Prison();

            // Assert
            Assert.IsTrue(Prison.Load().Any(p => p.Id == prison.Id));
        }

        [TestMethod]
        public void LoadPrison()
        {
            // Arrange

            
            Prison prison = new Prison();
            prison.Tag = "uhtst";

            PrisonRules prisonRules = new PrisonRules();
            prisonRules.PrisonHomePath = @"c:\prison_tests\p1";
            prisonRules.CellType = RuleTypes.WindowStation;

            prison.Lockdown(prisonRules);

            // Act
            var prisonLoaded = Prison.LoadPrisonAndAttach(prison.Id);

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

            prison.Destroy();
        }
    }
}