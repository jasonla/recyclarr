using System;
using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TestLibrary.FluentAssertions;
using Trash.Radarr;
using Trash.Radarr.CustomFormat.Guide;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;
using Trash.Radarr.CustomFormat.Processors.GuideSteps;

namespace Trash.Tests.Radarr.CustomFormat.Processors.GuideSteps
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class CustomFormatStepTest
    {
        private class Context
        {
            public List<CustomFormatData> TestGuideData { get; } = new()
            {
                new CustomFormatData
                {
                    Score = 100,
                    Json = JsonConvert.SerializeObject(new
                    {
                        trash_id = "id1",
                        name = "name1"
                    }, Formatting.Indented)
                },
                new CustomFormatData
                {
                    Score = 200,
                    Json = JsonConvert.SerializeObject(new
                    {
                        trash_id = "id2",
                        name = "name2"
                    }, Formatting.Indented)
                },
                new CustomFormatData
                {
                    Json = JsonConvert.SerializeObject(new
                    {
                        trash_id = "id3",
                        name = "name3"
                    }, Formatting.Indented)
                }
            };
        }

        [TestCase("name1", 0)]
        [TestCase("naME1", 0)]
        [TestCase("DifferentName", 1)]
        public void Match_cf_in_guide_with_different_name_with_cache_using_same_name_in_config(string variableCfName,
            int outdatedCount)
        {
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1"}}
            };

            var testCache = new CustomFormatCache
            {
                TrashIdMappings = new List<TrashIdMapping>
                {
                    new("id1", "name1")
                }
            };

            var testGuideData = new List<CustomFormatData>
            {
                new()
                {
                    Score = 100,
                    Json = JsonConvert.SerializeObject(new
                    {
                        trash_id = "id1",
                        name = variableCfName
                    }, Formatting.Indented)
                }
            };

            var processor = new CustomFormatStep();
            processor.Process(testGuideData, testConfig, testCache);

            processor.CustomFormatsWithOutdatedNames.Should().HaveCount(outdatedCount);
            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
                {
                    new(variableCfName, "id1", JObject.FromObject(new {name = variableCfName}))
                    {
                        Score = 100,
                        CacheEntry = testCache.TrashIdMappings[0]
                    }
                },
                op => op.Using(new JsonEquivalencyStep()));
        }

        [Test]
        public void Cache_entry_is_not_set_when_id_is_different()
        {
            var guideData = new List<CustomFormatData>
            {
                new()
                {
                    Json = @"{'name': 'name1', 'trash_id': 'id1'}"
                }
            };

            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1"}}
            };

            var testCache = new CustomFormatCache
            {
                TrashIdMappings = new List<TrashIdMapping>
                {
                    new("id1000", "name1")
                }
            };

            var processor = new CustomFormatStep();
            processor.Process(guideData, testConfig, testCache);

            processor.CustomFormatsWithOutdatedNames.Should().BeEmpty();
            processor.DeletedCustomFormatsInCache.Count.Should().Be(1);
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
                {
                    new("name1", "id1", JObject.FromObject(new {name = "name1"}))
                    {
                        Score = null,
                        CacheEntry = null
                    }
                },
                op => op.Using(new JsonEquivalencyStep()));
        }

        [Test]
        public void Cfs_not_in_config_are_skipped()
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1", "name3"}}
            };

            var processor = new CustomFormatStep();
            processor.Process(ctx.TestGuideData, testConfig, new CustomFormatCache());

            processor.CustomFormatsWithOutdatedNames.Should().BeEmpty();
            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
                {
                    new("name1", "id1", JObject.FromObject(new {name = "name1"}))
                    {
                        Score = 100
                    },
                    new("name3", "id3", JObject.FromObject(new {name = "name3"}))
                    {
                        Score = null
                    }
                },
                op => op.Using(new JsonEquivalencyStep()));
        }

        [Test]
        public void Config_cfs_in_different_sections_are_processed()
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1", "name3"}},
                new() {Names = new List<string> {"name2"}}
            };

            var processor = new CustomFormatStep();
            processor.Process(ctx.TestGuideData, testConfig, new CustomFormatCache());

            processor.CustomFormatsWithOutdatedNames.Should().BeEmpty();
            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
                {
                    new("name1", "id1", JObject.FromObject(new {name = "name1"})) {Score = 100},
                    new("name2", "id2", JObject.FromObject(new {name = "name2"})) {Score = 200},
                    new("name3", "id3", JObject.FromObject(new {name = "name3"})) {Score = null}
                },
                op => op.Using(new JsonEquivalencyStep()));
        }

        [Test]
        public void Custom_format_is_deleted_if_in_config_and_cache_but_not_in_guide()
        {
            var guideData = new List<CustomFormatData>
            {
                new()
                {
                    Json = @"{'name': 'name1', 'trash_id': 'id1'}"
                }
            };

            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1"}}
            };

            var testCache = new CustomFormatCache
            {
                TrashIdMappings = new List<TrashIdMapping> {new("id1000", "name1")}
            };

            var processor = new CustomFormatStep();
            processor.Process(guideData, testConfig, testCache);

            processor.CustomFormatsWithOutdatedNames.Should().BeEmpty();
            processor.DeletedCustomFormatsInCache.Should()
                .BeEquivalentTo(new TrashIdMapping("id1000", "name1"));
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
                {
                    new("name1", "id1", JObject.Parse(@"{'name': 'name1'}"))
                },
                op => op.Using(new JsonEquivalencyStep()));
        }

        [Test]
        public void Custom_format_is_deleted_if_not_in_config_but_in_cache_and_in_guide()
        {
            var cache = new CustomFormatCache
            {
                TrashIdMappings = new List<TrashIdMapping> {new("id1", "3D", 9)}
            };

            var guideCfs = new List<CustomFormatData>
            {
                new() {Json = "{'name': '3D', 'trash_id': 'id1'}"}
            };

            var processor = new CustomFormatStep();
            processor.Process(guideCfs, Array.Empty<CustomFormatConfig>(), cache);

            processor.CustomFormatsWithOutdatedNames.Should().BeEmpty();
            processor.DeletedCustomFormatsInCache.Should().BeEquivalentTo(cache.TrashIdMappings[0]);
            processor.ProcessedCustomFormats.Should().BeEmpty();
        }

        [Test]
        public void Custom_format_name_in_cache_is_updated_if_renamed_in_guide_and_config()
        {
            var guideData = new List<CustomFormatData>
            {
                new()
                {
                    Json = @"{'name': 'name2', 'trash_id': 'id1'}"
                }
            };

            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name2"}}
            };

            var testCache = new CustomFormatCache
            {
                TrashIdMappings = new List<TrashIdMapping> {new("id1", "name1")}
            };

            var processor = new CustomFormatStep();
            processor.Process(guideData, testConfig, testCache);

            processor.CustomFormatsWithOutdatedNames.Should().BeEmpty();
            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should()
                .ContainSingle().Which.CacheEntry.Should()
                .BeEquivalentTo(new TrashIdMapping("id1", "name2"));
        }

        [Test]
        public void Match_cf_names_regardless_of_case_in_config()
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"name1", "NAME1"}}
            };

            var processor = new CustomFormatStep();
            processor.Process(ctx.TestGuideData, testConfig, new CustomFormatCache());

            processor.CustomFormatsWithOutdatedNames.Should().BeEmpty();
            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEquivalentTo(new List<ProcessedCustomFormatData>
                {
                    new("name1", "id1", JObject.FromObject(new {name = "name1"})) {Score = 100}
                },
                op => op.Using(new JsonEquivalencyStep()));
        }

        [Test]
        public void Non_existent_cfs_in_config_are_skipped()
        {
            var ctx = new Context();
            var testConfig = new List<CustomFormatConfig>
            {
                new() {Names = new List<string> {"doesnt_exist"}}
            };

            var processor = new CustomFormatStep();
            processor.Process(ctx.TestGuideData, testConfig, new CustomFormatCache());

            processor.CustomFormatsWithOutdatedNames.Should().BeEmpty();
            processor.DeletedCustomFormatsInCache.Should().BeEmpty();
            processor.ProcessedCustomFormats.Should().BeEmpty();
        }
    }
}