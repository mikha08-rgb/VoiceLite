using Xunit;

namespace VoiceLite.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void TestFramework_IsWorking()
        {
            Assert.True(true, "Test framework is properly configured");
        }

        [Fact]
        public void AssemblyReference_CanAccessVoiceLiteTypes()
        {
            var settings = new VoiceLite.Models.Settings();
            Assert.NotNull(settings);
        }
    }
}
