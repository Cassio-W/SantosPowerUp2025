using Mandato.Core;
using Mandato.Infrastructure;
using Mandato.Run;
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
        public void RecordRunCompleted_IncrementsCompletedRunsAndRecordsStats()
        {
            var service = new RunProfileService();
            var cleanProfile = new ProfileState();
            service.InitializeProfile(cleanProfile);

            service.RecordRunCompleted(
                victory: true,
                endingId: "ending_mandato_ouro",
                decisionsCount: 15,
                finalPopularity: 82
            );

            Assert.AreEqual(1, service.CurrentProfile.totalRunsPlayed);
            Assert.AreEqual(1, service.CurrentProfile.totalVictories);
            Assert.AreEqual(15, service.CurrentProfile.totalDecisionsMade);
            Assert.AreEqual(82, service.CurrentProfile.highestPopularityScore);
            Assert.IsTrue(service.CurrentProfile.discoveredEndingIds.Contains("ending_mandato_ouro"));
        }
    }
}
