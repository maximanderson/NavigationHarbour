using PortalCore.Api.Configuration.Server;

namespace PortalCore.Api.Tests;

/// <summary>Writes a server.ini into the scratch home and loads the configuration the way the host does.</summary>
public abstract class TlsTestBase : ApplyTestBase
{
    protected ServerConfigReport LoadIni(string ini)
    {
        WriteIni(ini);
        return Load(WithShippedDefaults());
    }
}
