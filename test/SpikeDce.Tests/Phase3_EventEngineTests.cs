using SpikeDce.Schema;
using Xunit;
using Xunit.Abstractions;

namespace SpikeDce.Tests;

public class Phase3_EventEngineTests
{
    private readonly ITestOutputHelper _out;
    public Phase3_EventEngineTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void SchemaModel_loads_evento_root()
    {
        var dir = Path.Combine(TestEnv.AssetsDir, "xsd", "evento");
        var m = SchemaModel.Load(dir, "eventoDCe_v1.00.xsd");
        Assert.NotNull(m.GlobalElement("eventoDCe", TestEnv.DceNs));
    }
}
