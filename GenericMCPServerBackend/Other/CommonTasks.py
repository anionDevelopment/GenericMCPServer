from ScriptCollection.TFCPS.DotNet.TFCPS_CodeUnitSpecific_DotNet import TFCPS_CodeUnitSpecific_DotNet_Functions,TFCPS_CodeUnitSpecific_DotNet_CLI
from ScriptCollection.TFCPS.DotNet.CertificateGeneratorInformationNoGenerate import CertificateGeneratorInformationNoGenerate


def common_tasks():
    tf:TFCPS_CodeUnitSpecific_DotNet_Functions=TFCPS_CodeUnitSpecific_DotNet_CLI.parse(__file__)
    # No certificate is generated: this codeunit serves plain HTTP (the MCP-endpoint is meant to be reached over a
    # reverse-proxy or on the local machine), so it needs neither a development-certificate nor a productive one.
    tf.do_common_tasks(tf.get_version_of_project(),CertificateGeneratorInformationNoGenerate())#codeunit-version should alsways be the same as project-version


if __name__ == "__main__":
    common_tasks()
