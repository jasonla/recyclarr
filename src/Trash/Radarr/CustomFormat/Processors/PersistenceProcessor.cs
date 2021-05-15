﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trash.Config;
using Trash.Radarr.CustomFormat.Api;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;
using Trash.Radarr.CustomFormat.Processors.PersistenceSteps;

namespace Trash.Radarr.CustomFormat.Processors
{
    public interface IPersistenceProcessorSteps
    {
        public IJsonTransactionStep JsonTransactionStep { get; }
        public ICustomFormatApiPersistenceStep CustomFormatCustomFormatApiPersister { get; }
        public IQualityProfileApiPersistenceStep ProfileQualityProfileApiPersister { get; }
    }

    internal class PersistenceProcessor : IPersistenceProcessor
    {
        private readonly RadarrConfiguration _config;
        private readonly ICustomFormatService _customFormatService;
        private readonly IQualityProfileService _qualityProfileService;
        private readonly Func<IPersistenceProcessorSteps> _stepsFactory;
        private IPersistenceProcessorSteps _steps;

        public PersistenceProcessor(
            ICustomFormatService customFormatService,
            IQualityProfileService qualityProfileService,
            IServiceConfiguration config,
            Func<IPersistenceProcessorSteps> stepsFactory)
        {
            _customFormatService = customFormatService;
            _qualityProfileService = qualityProfileService;
            _stepsFactory = stepsFactory;
            _config = (RadarrConfiguration) config;
            _steps = _stepsFactory();
        }

        public CustomFormatTransactionData Transactions
            => _steps.JsonTransactionStep.Transactions;

        public IDictionary<string, List<QualityProfileCustomFormatScoreEntry>> UpdatedScores
            => _steps.ProfileQualityProfileApiPersister.UpdatedScores;

        public IReadOnlyCollection<string> InvalidProfileNames
            => _steps.ProfileQualityProfileApiPersister.InvalidProfileNames;

        public void Reset()
        {
            _steps = _stepsFactory();
        }

        public async Task PersistCustomFormats(IReadOnlyCollection<ProcessedCustomFormatData> guideCfs,
            IEnumerable<TrashIdMapping> deletedCfsInCache,
            IDictionary<string, List<QualityProfileCustomFormatScoreEntry>> profileScores)
        {
            var radarrCfs = await _customFormatService.GetCustomFormats();

            // Step 1: Match CFs between the guide & Radarr and merge the data. The goal is to retain as much of the
            // original data from Radarr as possible. There are many properties in the response JSON that we don't
            // directly care about. We keep those and just update the ones we do care about.
            _steps.JsonTransactionStep.Process(guideCfs, radarrCfs);

            // Step 1.1: Optionally record deletions of custom formats in cache but not in the guide
            if (_config.DeleteOldCustomFormats)
            {
                _steps.JsonTransactionStep.RecordDeletions(deletedCfsInCache, radarrCfs);
            }

            // Step 2: For each merged CF, persist it to Radarr via its API. This will involve a combination of updates
            // to existing CFs and creation of brand new ones, depending on what's already available in Radarr.
            await _steps.CustomFormatCustomFormatApiPersister.Process(_customFormatService,
                _steps.JsonTransactionStep.Transactions);

            // Step 3: Update all quality profiles with the scores from the guide for the uploaded custom formats
            await _steps.ProfileQualityProfileApiPersister.Process(_qualityProfileService, profileScores);
        }
    }
}