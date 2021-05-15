using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Trash.Radarr;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Processors.GuideSteps;

namespace Trash.Tests.Radarr.CustomFormat.Processors.GuideSteps
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class QualityProfileStepTest
    {
        [Test]
        public void No_score_used_if_no_score_in_config_or_guide()
        {
            var testConfigData = new List<ProcessedConfigData>
            {
                new()
                {
                    CustomFormats = new List<ProcessedCustomFormatData>
                    {
                        new("name1", "id1", new JObject()) {Score = null}
                    },
                    QualityProfiles = new List<QualityProfileConfig>
                    {
                        new() {Name = "profile1"}
                    }
                }
            };

            var processor = new QualityProfileStep();
            processor.Process(testConfigData);

            processor.ProfileScores.Should().BeEmpty();
            processor.CustomFormatsWithoutScore.Should().Equal(new List<object> {("name1", "id1", "profile1")});
        }

        [Test]
        public void Overwrite_score_from_guide_if_config_defines_score()
        {
            var testConfigData = new List<ProcessedConfigData>
            {
                new()
                {
                    CustomFormats = new List<ProcessedCustomFormatData>
                    {
                        new("", "id1", new JObject()) {Score = 100}
                    },
                    QualityProfiles = new List<QualityProfileConfig>
                    {
                        new() {Name = "profile1", Score = 50}
                    }
                }
            };

            var processor = new QualityProfileStep();
            processor.Process(testConfigData);

            processor.ProfileScores.Should().ContainKey("profile1")
                .WhichValue.Should().BeEquivalentTo(new List<QualityProfileCustomFormatScoreEntry>
                {
                    new(testConfigData[0].CustomFormats[0], 50)
                });

            processor.CustomFormatsWithoutScore.Should().BeEmpty();
        }

        [Test]
        public void Use_guide_score_if_no_score_in_config()
        {
            var testConfigData = new List<ProcessedConfigData>
            {
                new()
                {
                    CustomFormats = new List<ProcessedCustomFormatData>
                    {
                        new("", "id1", new JObject()) {Score = 100}
                    },
                    QualityProfiles = new List<QualityProfileConfig>
                    {
                        new() {Name = "profile1"},
                        new() {Name = "profile2", Score = null}
                    }
                }
            };

            var processor = new QualityProfileStep();
            processor.Process(testConfigData);

            var expectedScoreEntries = new List<QualityProfileCustomFormatScoreEntry>
            {
                new(testConfigData[0].CustomFormats[0], 100)
            };

            processor.ProfileScores.Should().BeEquivalentTo(
                new Dictionary<string, List<QualityProfileCustomFormatScoreEntry>>
                {
                    {"profile1", expectedScoreEntries},
                    {"profile2", expectedScoreEntries}
                });

            processor.CustomFormatsWithoutScore.Should().BeEmpty();
        }

        [Test]
        public void Zero_score_is_not_ignored()
        {
            var testConfigData = new List<ProcessedConfigData>
            {
                new()
                {
                    CustomFormats = new List<ProcessedCustomFormatData>
                    {
                        new("name1", "id1", new JObject()) {Score = 0}
                    },
                    QualityProfiles = new List<QualityProfileConfig>
                    {
                        new() {Name = "profile1"}
                    }
                }
            };

            var processor = new QualityProfileStep();
            processor.Process(testConfigData);

            processor.ProfileScores.Should().ContainKey("profile1")
                .WhichValue.Should().BeEquivalentTo(new List<QualityProfileCustomFormatScoreEntry>
                {
                    new(testConfigData[0].CustomFormats[0], 0)
                });

            processor.CustomFormatsWithoutScore.Should().BeEmpty();
        }
    }
}