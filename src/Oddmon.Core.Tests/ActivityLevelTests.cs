using Oddmon.Core;
using Xunit;

namespace Oddmon.Core.Tests;

public class ActivityLevelTests
{
    // M0 smoke test: the solution wires up and Core is referenceable.
    [Fact]
    public void ActivityLevel_HasTheFourDocumentedStates()
    {
        Assert.Equal(
            new[] { "Idle", "Read", "Write", "Mixed" },
            Enum.GetNames<ActivityLevel>());
    }
}
