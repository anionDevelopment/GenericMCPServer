using System;
using System.IO;

namespace GenericMCPServerBackend.Tests.TestUtilities
{
    /// <summary>
    /// Creates the example-data this product needs to do anything: a tool, a resource, a prompt and the completion
    /// of the argument of that prompt.
    ///
    /// The product itself contains none of them - which tools, which resources and which prompts exist is declared by
    /// an administrator - so a test which wants to call, read or ask for one has to bring that declaration along. This
    /// class writes a folder which contains the scripts and the tool-definitions-file which turns them into a tool, a
    /// resource, a prompt and the completion of its argument, exactly as an administrator would.
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

        /// <summary>
        /// The name of the tool which this creator declares with an output-schema. It exists next to the one which
        /// declares none, because the interesting question is what a client sees and receives while both kinds are
        /// declared.
        /// </summary>
        public const string StructuredExampleToolName = "structured_example_tool";

        /// <summary>
        /// The script which implements that tool. It answers with a json-document which fits the declared schema,
        /// except for the one value below, which is what lets a testcase see what happens with an answer which does
        /// not fit.
        /// </summary>
        public const string StructuredExampleToolScriptFileName = "StructuredExampleTool.py";

        /// <summary>The value for which that script answers with something which is no json-document.</summary>
        public const string ValueWhichBreaksTheStructuredAnswer = "no-json";

        /// <summary>
        /// The name of the tool which this creator declares as one which runs until it is released. A call of it is
        /// what a cancellation can arrive for at all: every other tool here is answered before a client could send
        /// anything else.
        /// </summary>
        public const string WaitingExampleToolName = "waiting_example_tool";

        /// <summary>The uri of the resource which this creator declares. A resource is read by exactly this uri.</summary>
        public const string ExampleResourceUri = "example://documents/handbook";

        /// <summary>The name of that resource, which is what a client shows a reader.</summary>
        public const string ExampleResourceName = "example_resource";

        /// <summary>The type of the content of that resource.</summary>
        public const string ExampleResourceMimeType = "text/plain";

        /// <summary>
        /// The script which implements that resource. A testcase which wants a read to fail removes it, which is a
        /// failure the server reports with a readable reason.
        /// </summary>
        public const string ExampleResourceScriptFileName = "ExampleResource.py";

        /// <summary>The name of the prompt which this creator declares. A prompt is asked for by exactly this name.</summary>
        public const string ExamplePromptName = "example_prompt";

        /// <summary>The name of the argument of that prompt, which is filled into its text.</summary>
        public const string ExamplePromptArgumentName = "topic";

        /// <summary>
        /// The script which implements that prompt. A testcase which wants a prompt to fail removes it, which is a
        /// failure the server reports with a readable reason.
        /// </summary>
        public const string ExamplePromptScriptFileName = "ExamplePrompt.py";

        /// <summary>
        /// The script which provides the possible values of the argument of that prompt. A testcase which wants a
        /// completion to fail removes it, which is a failure the server reports with a readable reason.
        /// </summary>
        public const string ExampleCompletionScriptFileName = "ExampleCompletion.py";

        /// <summary>The mcp-server which only a client with an access-token reaches.</summary>
        public const string UrlNamespace = "Example";

        /// <summary>The mcp-server which everybody may use.</summary>
        public const string AnonymousUrlNamespace = "PublicExample";

        /// <summary>The role which may use the mcp-server which is not anonymous.</summary>
        public const string RoleName = "example-role";

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

        /// <summary>
        /// A script which answers with a json-document which fits the declared output-schema, and with plain text for
        /// the one value which is meant to break that promise. Both directions of a declared schema are visible with
        /// it.
        /// </summary>
        private const string StructuredScriptContent = @"import argparse
import json
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--value', required=True)
arguments = parser.parse_args()
if arguments.value == '" + ValueWhichBreaksTheStructuredAnswer + @"':
    sys.stdout.write('this is no json-document')
else:
    sys.stdout.write(json.dumps({'answer': 'the answer is ' + arguments.value, 'length': len(arguments.value)}))
";

        /// <summary>
        /// A script which answers with the uri of the resource it was called for. A resource takes no parameter, so
        /// the uri is everything such a script receives.
        /// </summary>
        private const string ResourceScriptContent = @"import argparse
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--uri', required=True)
arguments = parser.parse_args()
sys.stdout.write('the content of ' + arguments.uri)
";

        /// <summary>
        /// A script which writes the text of a prompt, with the value of the argument it got filled into it. A prompt
        /// is run like a tool, so it receives its arguments in the same way.
        /// </summary>
        private const string PromptScriptContent = @"import argparse
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--topic', required=True)
arguments = parser.parse_args()
sys.stdout.write('Explain ' + arguments.topic + ' to a beginner.')
";

        /// <summary>
        /// A script which writes the values the argument of that prompt can have, one per line and only the ones which
        /// fit the value which was entered so far. A completion is run like a tool, so it receives what it is asked
        /// about in the same way.
        /// </summary>
        private const string CompletionScriptContent = @"import argparse
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--argument', required=True)
parser.add_argument('--value', required=True)
arguments = parser.parse_args()
for value in ['cryptography', 'compilers', 'networking']:
    if value.startswith(arguments.value):
        sys.stdout.write(value + '\n')
";

        /// <summary>The folder which contains the declaration and the script it points to.</summary>
        public string Folder { get; }

        /// <summary>The file which declares the tools; this is what the server has to be pointed at.</summary>
        public string ToolDefinitionsFile { get; }


        /// <summary>The folder which contains everything this creator wrote.</summary>
        private readonly string _Root;

        /// <summary>Whether <see cref="_Root"/> was generated by this instance itself (as opposed to a folder the caller already owns, for example the
        /// configuration-folder of a server which is running in the same test - see <see cref="IntegrationTestFramework"/>). Only an own root may be removed
        /// as a whole by <see cref="Dispose"/>; otherwise only the files this instance actually wrote are removed.</summary>
        private readonly bool _OwnsRoot;

        /// <summary>
        /// Creates the example-data directly inside the given root-folder, or - without one - below a fresh temp-folder of its own.
        /// </summary>
        /// <param name="rootFolder">
        /// The folder the example-data is written into. Pass the configuration-folder of a server that is being started in the same test, so its
        /// default <c>ToolDefinitionsFile</c> ("./Tools.json", relative to the configuration-folder) resolves to the file this creator writes, without
        /// having to configure anything explicitly. Left out, a fresh temp-folder is used and owned (and therefore removed as a whole again) by this instance.
        /// </param>
        public ExampleDataCreator(string? rootFolder = null)
        {
            this._OwnsRoot = rootFolder is null;
            this._Root = rootFolder ?? Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
            this.Folder = this._Root;
            Directory.CreateDirectory(this.Folder);
            File.WriteAllText(Path.Combine(this.Folder, "ExampleTool.py"), ScriptContent);
            File.WriteAllText(Path.Combine(this.Folder, StructuredExampleToolScriptFileName), StructuredScriptContent);
            WaitingScript.WriteInto(this.Folder);
            File.WriteAllText(Path.Combine(this.Folder, ExampleResourceScriptFileName), ResourceScriptContent);
            File.WriteAllText(Path.Combine(this.Folder, ExamplePromptScriptFileName), PromptScriptContent);
            File.WriteAllText(Path.Combine(this.Folder, ExampleCompletionScriptFileName), CompletionScriptContent);
            this.ToolDefinitionsFile = Path.Combine(this.Folder, "Tools.json");
            File.WriteAllText(this.ToolDefinitionsFile, @"{
    ""mcp-servers"": [
        {
            ""url-namespace"": """ + UrlNamespace + @""",
            ""authorized-roles"": [ """ + RoleName + @""" ],
            ""tools"": [
                {
                    ""tool-name"": """ + ExampleToolName + @""",
                    ""type"": ""python"",
                    ""calls"": ""ExampleTool.py"",
                    ""description"": ""Answers with the value it got."",
                    ""parameters"": [ """ + ExampleToolParameterName + @""" ]
                },
                {
                    ""tool-name"": """ + StructuredExampleToolName + @""",
                    ""type"": ""python"",
                    ""calls"": """ + StructuredExampleToolScriptFileName + @""",
                    ""description"": ""Answers with the value it got as a json-document."",
                    ""parameters"": [ """ + ExampleToolParameterName + @""" ],
                    ""output-schema"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""answer"": { ""type"": ""string"" },
                            ""length"": { ""type"": ""integer"" }
                        },
                        ""required"": [ ""answer"", ""length"" ]
                    }
                },
                {
                    ""tool-name"": """ + WaitingExampleToolName + @""",
                    ""type"": ""python"",
                    ""calls"": """ + WaitingScript.FileName + @""",
                    ""description"": ""Waits until it is released."",
                    ""parameters"": [ """ + ExampleToolParameterName + @""" ]
                }
            ],
            ""resources"": [
                {
                    ""uri"": """ + ExampleResourceUri + @""",
                    ""resource-name"": """ + ExampleResourceName + @""",
                    ""type"": ""python"",
                    ""calls"": """ + ExampleResourceScriptFileName + @""",
                    ""description"": ""The handbook of the team."",
                    ""mime-type"": """ + ExampleResourceMimeType + @"""
                }
            ],
            ""prompts"": [
                {
                    ""prompt-name"": """ + ExamplePromptName + @""",
                    ""type"": ""python"",
                    ""calls"": """ + ExamplePromptScriptFileName + @""",
                    ""description"": ""Asks for an explanation of a topic."",
                    ""parameters"": [ """ + ExamplePromptArgumentName + @""" ],
                    ""completions"": {
                        """ + ExamplePromptArgumentName + @""": {
                            ""type"": ""python"",
                            ""calls"": """ + ExampleCompletionScriptFileName + @"""
                        }
                    }
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
            if (this._OwnsRoot)
            {
                if (Directory.Exists(this._Root))
                {
                    Directory.Delete(this._Root, true);
                }
            }
            else
            {
                // A root this instance does not own may hold other content (for example the rest of a server's configuration-folder) which must survive
                // this instance being disposed, so only the files this instance actually wrote are removed, never the whole root.
                File.Delete(Path.Combine(this.Folder, "ExampleTool.py"));
                File.Delete(Path.Combine(this.Folder, StructuredExampleToolScriptFileName));
                File.Delete(Path.Combine(this.Folder, WaitingScript.FileName));
                File.Delete(Path.Combine(this.Folder, ExampleResourceScriptFileName));
                File.Delete(Path.Combine(this.Folder, ExamplePromptScriptFileName));
                File.Delete(Path.Combine(this.Folder, ExampleCompletionScriptFileName));
                File.Delete(this.ToolDefinitionsFile);
            }
        }
    }
}
