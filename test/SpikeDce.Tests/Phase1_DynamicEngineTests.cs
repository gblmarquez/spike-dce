using SpikeDce.Schema;
using Xunit;

namespace SpikeDce.Tests;

public class Phase1_DynamicEngineTests
{
    [Fact]
    public void SchemaModel_resolves_DCe_root_element()
    {
        var m = SchemaModel.Load(TestEnv.DceXsdDir);
        Assert.NotNull(m.GlobalElement("DCe", TestEnv.DceNs));
    }
}
