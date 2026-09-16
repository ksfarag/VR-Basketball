using NUnit.Framework;

namespace VRBasketball.Tests
{
    /// <summary>
    /// Confirms the EditMode test assembly compiles and runs. This exists so the
    /// harness is known-good before any gameplay logic depends on it; delete it
    /// once real tests cover the scoring rules.
    /// </summary>
    public class TestHarnessSmokeTests
    {
        [Test]
        public void TestAssembly_Runs()
        {
            Assert.Pass("EditMode test assembly is wired up correctly.");
        }
    }
}
