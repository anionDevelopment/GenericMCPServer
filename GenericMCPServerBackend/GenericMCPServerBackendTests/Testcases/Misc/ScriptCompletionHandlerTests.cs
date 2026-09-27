using GenericMCPServerBackend.Core.Misc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;
using System.Collections.Generic;
using System.Linq;

namespace GenericMCPServerBackend.Tests.Testcases.Misc
{
    /// <summary>
    /// Covers what the answer to a request for the possible values of an argument carries.
    /// </summary>
    [TestClass]
    public class ScriptCompletionHandlerTests
    {
        /// <remarks>
        /// A client which receives every value it asked for must be able to see that these are all of them, and it can
        /// not see that by counting.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AllValuesAreHandedOverWhenThereAreNotTooManyOfThem()
        {
            // arrange
            List<string> values = new List<string>() { "cryptography", "compilers", "networking" };

            // act
            Completion completion = ScriptCompletionHandler.CreateCompletion(values);

            // assert
            CollectionAssert.AreEqual(values, completion.Values.ToList());
            Assert.AreEqual(3, completion.Total);
            Assert.AreEqual<bool?>(false, completion.HasMore);
        }

        /// <remarks>
        /// The protocol limits how many values one answer carries, so an answer which exceeds that limit is one a
        /// client is allowed to reject. How many there are in total is handed over with it, so that whoever asked
        /// learns that the list is not complete instead of believing it is.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void MoreValuesThanOneAnswerCarriesAreCutOffAndReportedAsIncomplete()
        {
            // arrange
            List<string> values = Enumerable.Range(0, 150).Select(index => $"value-{index}").ToList();

            // act
            Completion completion = ScriptCompletionHandler.CreateCompletion(values);

            // assert
            Assert.AreEqual(100, completion.Values.Count);
            Assert.AreEqual("value-0", completion.Values[0]);
            Assert.AreEqual("value-99", completion.Values[99]);
            Assert.AreEqual(150, completion.Total);
            Assert.AreEqual<bool?>(true, completion.HasMore);
        }
    }
}
