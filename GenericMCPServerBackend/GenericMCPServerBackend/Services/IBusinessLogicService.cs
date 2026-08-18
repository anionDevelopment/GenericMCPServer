namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// The business-logic of this codeunit. Every operation declared here is offered as a tool of the MCP-server.
    /// </summary>
    public interface IBusinessLogicService
    {


        /// <summary>
        /// Returns the details of the given CVE.
        /// </summary>
        /// <param name="cveIdentifier">The identifier of the CVE, for example "CVE-2021-44228".</param>
        /// <returns>The details of the CVE as a json-document.</returns>
        string GetCVEDetails(string cveIdentifier);

        /// <summary>
        /// Returns the versions of the given maven-package which are available on maven-central.
        /// </summary>
        /// <param name="groupId">The group-id of the package, for example "org.apache.logging.log4j".</param>
        /// <param name="artifactId">The artifact-id of the package, for example "log4j-core".</param>
        /// <returns>The available versions as a json-document.</returns>
        string GetAvailableMavenVersions(string groupId, string artifactId);

        /// <summary>
        /// Returns the versions of the given nuget-package which are available on nuget.org.
        /// </summary>
        /// <param name="packageId">The id of the package, for example "Newtonsoft.Json".</param>
        /// <returns>The available versions as a json-document.</returns>
        string GetAvailableNuGetVersions(string packageId);

        /// <summary>
        /// Returns the versions of the given python-package which are available on pypi.
        /// </summary>
        /// <param name="packageName">The name of the package, for example "requests".</param>
        /// <returns>The available versions as a json-document.</returns>
        string GetAvailablePyPIVersions(string packageName);

        /// <summary>
        /// Returns the versions of the given npm-package which are available on the npm-registry.
        /// </summary>
        /// <param name="packageName">The name of the package, for example "typescript" or "@angular/cli".</param>
        /// <returns>The available versions as a json-document.</returns>
        string GetAvailableNPMVersions(string packageName);
    }
}
