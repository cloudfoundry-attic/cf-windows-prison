using HP.WindowsPrison.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HP.WindowsPrison.Tests
{
    [TestClass]
    public class TestPrisonHostRequirements
    {
        [TestMethod]
        public void TestSystemNoReplaceTokenPrivilege()
        {
            // Arrange
            string userName = "system";

            // Act
            bool hasPrivilege = UserRightsAssignmentPolicies.UserHasPrivilege(
                UserRightsAssignmentPolicies.ReplaceTokenPrivilege, 
                userName);

            // Assert
            Assert.IsFalse(hasPrivilege);
        }

        [TestMethod]
        public void TestLocalServiceReplaceTokenPrivilege()
        {
            // Arrange
            string userName = "localservice";

            // Act
            bool hasPrivilege = UserRightsAssignmentPolicies.UserHasPrivilege(
                UserRightsAssignmentPolicies.ReplaceTokenPrivilege,
                userName);

            // Assert
            Assert.IsTrue(hasPrivilege);
        }

        [TestMethod]
        public void TestGrantAndRevokeTokenPrivilege()
        {
            // Arrange
            PrisonUser newUser = new PrisonUser("prtst");
            try
            {
                newUser.Create();
                bool hasPrivilege = UserRightsAssignmentPolicies.UserHasPrivilege(
                    UserRightsAssignmentPolicies.ReplaceTokenPrivilege,
                    newUser.UserName);
                Assert.IsFalse(hasPrivilege);

                // Act
                UserRightsAssignmentPolicies.GrantPrivilegeToUser(
                    UserRightsAssignmentPolicies.ReplaceTokenPrivilege,
                    newUser.UserName);
                hasPrivilege = UserRightsAssignmentPolicies.UserHasPrivilege(
                    UserRightsAssignmentPolicies.ReplaceTokenPrivilege,
                    newUser.UserName);

                // Assert
                Assert.IsTrue(hasPrivilege);

                // Act
                UserRightsAssignmentPolicies.RevokePrivilegeToUser(
                    UserRightsAssignmentPolicies.ReplaceTokenPrivilege,
                    newUser.UserName);
                hasPrivilege = UserRightsAssignmentPolicies.UserHasPrivilege(
                    UserRightsAssignmentPolicies.ReplaceTokenPrivilege,
                    newUser.UserName);

                // Assert
                Assert.IsFalse(hasPrivilege);
            }
            finally
            {
                newUser.Delete();
            }
        }
    }
}
