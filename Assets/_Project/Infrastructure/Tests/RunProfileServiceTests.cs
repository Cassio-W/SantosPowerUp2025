using Mandato.Core;
using Mandato.Infrastructure;
using NUnit.Framework;

namespace Mandato.Infrastructure.Tests
{
    [TestFixture]
    public class RunProfileServiceTests
    {
        [Test]
        public void InitializeProfile_ReturnsValidInstance()
        {
            var service = new RunProfileService();
            var profile = service.InitializeProfile();

            Assert.IsNotNull(profile);
            Assert.IsNotNull(service.CurrentProfile);
        }

        [Test]
        public void RecordRunCompleted_IncrementsCompletedRuns()
        {
            var service = new RunProfileService();
            service.InitializeProfile();
            int initialRuns = service.CurrentProfile.totalRunsPlayed;

            service.RecordRunCompleted(victory: true, endingId: "ending_mandato_ouro");

            Assert.AreEqual(initialRuns + 1, service.CurrentProfile.totalRunsPlayed);
            Assert.IsTrue(service.CurrentProfile.discoveredEndingIds.Contains("ending_mandato_ouro"));
        }
    }
}
