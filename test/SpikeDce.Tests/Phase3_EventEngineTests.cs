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

    [Fact]
    public void Transforms_padLeft_and_now()
    {
        Assert.Equal("001", SpikeDce.Mapping.Transforms.Invoke("padLeft", new object?[] { "1", 3, "0" }));
        var now = (string)SpikeDce.Mapping.Transforms.Invoke("now", System.Array.Empty<object?>())!;
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[-+]\d{2}:\d{2}$", now);
    }
}
