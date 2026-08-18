using GenericMCPServerBackend.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Server;
using System;
using System.Linq;
using System.Reflection;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    [TestClass]
    public class BusinessLogicServiceTests
    {
        /// <remarks>
        /// The tools of this server are exactly the methods which carry the attribute of the MCP-SDK. A method which
        /// loses that attribute would silently disappear from the server without any other test failing, which is what
        /// this testcase prevents.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void EveryOperationOfTheBusinessLogicIsOfferedAsATool()
        {
            // arrange
            MethodInfo[] methodsOfTheImplementation = typeof(BusinessLogicService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            // act
            string[] toolNames = methodsOfTheImplementation
                .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() != null)
                .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()!.Name!)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            // assert
            CollectionAssert.AreEqual(new string[] { "get_available_maven_versions", "get_available_npm_versions", "get_available_nuget_versions", "get_available_pypi_versions", "get_cve_details" }, toolNames);
            Assert.IsNotNull(typeof(BusinessLogicService).GetCustomAttribute<McpServerToolTypeAttribute>(), "The class has to be marked as a tool-type, otherwise the SDK does not look at its methods.");
        }
        /// <remarks>
        /// The arguments of a tool are provided by the client and are checked by the script which implements the tool.
        /// This testcase covers that way completely (the method, the start of the script and its answer) and needs no
        /// network, because the value never gets far enough to be requested anywhere.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolWhoseArgumentHasAWrongFormatFails()
        {
            // arrange
            IBusinessLogicService businessLogicService = new BusinessLogicService();

            // act & assert
            foreach (string value in new string[] { "CVE-2021-44228; rm -rf /", "../../etc/passwd", "not-a-cve" })
            {
                Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.GetCVEDetails(value), $"The value \"{value}\" was not rejected.");
            }
        }
    }
}
