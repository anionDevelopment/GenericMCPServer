using System.Runtime.CompilerServices;
// The test-project needs access to the internal types of this codeunit. The assembly is not signed, so the name of the
// test-assembly is sufficient here and no public key has to be given.
[assembly: InternalsVisibleTo("GenericMCPServerBackendTests")]
