using System;
using System.IO;

namespace GenericMCPServerBackend.Tests.TestUtilities
{
    /// <summary>
    /// Creates the example-data this product needs to do anything: a tool.
    ///
    /// The product itself contains no tool - which tools exist is declared by an administrator - so a test which wants
    /// to call one has to bring that declaration along. This class writes a folder which contains a script and the
    /// tool-definitions-file which turns it into a tool, exactly as an administrator would.
    /// </summary>
    /// <remarks>
    /// It deliberately lives in the test-project and not in the codeunit itself. The name of a tool in the sourcecode
    /// of the product is a defect (see the specification "tool-definitions"), and a creator of example-tools inside the
    /// product would be exactly that.
    /// </remarks>
    public sealed class ExampleDataCreator : IDisposable
    {
        /// <summary>The name of the tool which this creator declares.</summary>
        public const string ExampleToolName = "example_tool";

        /// <summary>The name of the parameter of that tool.</summary>
        public const string ExampleToolParameterName = "value";

        /// <summary>
        /// The name of the tool which this creator declares as one which everybody may use. It exists next to the
        /// other one, because the interesting question is what a caller without an access-token sees and reaches while
        /// both kinds are declared.
        /// </summary>
        public const string AnonymousExampleToolName = "anonymous_example_tool";

        /// <summary>The mcp-server which only a client with an access-token reaches.</summary>
        public const string UrlNamespace = "Example";

        /// <summary>The mcp-server which everybody may use.</summary>
        public const string AnonymousUrlNamespace = "PublicExample";

        /// <summary>
        /// A script which answers with the value it got, and fails with a readable reason when that value does not have
        /// the expected format. It is the smallest thing which still shows both directions a tool can end in.
        /// </summary>
        private const string ScriptContent = @"import argparse
import re
import sys

VALUE_REGEX = re.compile('^[a-z0-9-]+$')


def value(text: str) -> str:
    if VALUE_REGEX.match(text) is None:
        raise argparse.ArgumentTypeError('the value may only consist of lowercase letters, digits and hyphens')
    return text


parser = argparse.ArgumentParser()
parser.add_argument('--value', type=value, required=True)
arguments = parser.parse_args()
sys.stdout.write('the answer is ' + arguments.value)
";

        /// <summary>The folder which contains the declaration and the script it points to.</summary>
        public string Folder { get; }

        /// <summary>The file which declares the tools; this is what the server has to be pointed at.</summary>
        public string ToolDefinitionsFile { get; }

        /// <summary>
        /// The folder for the virtual environment of the example-tool. It belongs to this creator so that it is
        /// removed together with the example-data instead of staying behind in the temp-folder of the machine.
        /// </summary>
        public string VirtualEnvironmentsFolder { get; }

        /// <summary>
        /// The file which holds the access-tokens of the started server. It belongs to the example-data as well, so
        /// that a testcase gets a server which nobody but it can reach.
        /// </summary>
        public string AccessTokensFile { get; }

        /// <summary>The folder which contains everything this creator wrote.</summary>
        private readonly string _Root;

        public ExampleDataCreator()
        {
            this._Root = Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
            this.Folder = Path.Combine(this._Root, "Tools");
            this.VirtualEnvironmentsFolder = Path.Combine(this._Root, "VirtualEnvironments");
            this.AccessTokensFile = Path.Combine(this._Root, "AccessTokens.csv");
            Directory.CreateDirectory(this.Folder);
            File.WriteAllText(Path.Combine(this.Folder, "ExampleTool.py"), ScriptContent);
            this.ToolDefinitionsFile = Path.Combine(this.Folder, "Tools.json");
            File.WriteAllText(this.ToolDefinitionsFile, @"{
    ""mcp-servers"": [
        {
            ""url-namespace"": """ + UrlNamespace + @""",
            ""tools"": [
                {
                    ""tool-name"": """ + ExampleToolName + @""",
                    ""type"": ""python"",
                    ""calls"": ""ExampleTool.py"",
                    ""description"": ""Answers with the value it got."",
                    ""parameters"": [ """ + ExampleToolParameterName + @""" ]
                }
            ]
        },
        {
            ""url-namespace"": """ + AnonymousUrlNamespace + @""",
            ""anonymous"": true,
            ""tools"": [
                {
                    ""tool-name"": """ + AnonymousExampleToolName + @""",
                    ""type"": ""python"",
                    ""calls"": ""ExampleTool.py"",
                    ""description"": ""Answers with the value it got and may be used by everybody."",
                    ""parameters"": [ """ + ExampleToolParameterName + @""" ]
                }
            ]
        }
    ]
}");
        }

        public void Dispose()
        {
            if (Directory.Exists(this._Root))
            {
                Directory.Delete(this._Root, true);
            }
        }
    }
}
