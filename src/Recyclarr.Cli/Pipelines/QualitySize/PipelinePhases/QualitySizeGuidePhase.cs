using Recyclarr.Common.Extensions;
using Recyclarr.Config.Models;
using Recyclarr.TrashGuide.QualitySize;

namespace Recyclarr.Cli.Pipelines.QualitySize.PipelinePhases;

public class QualitySizeGuidePhase(ILogger log, IQualitySizeGuideService guide)
{
    public QualitySizeData? Execute(IServiceConfiguration config)
    {
        var qualityDef = config.QualityDefinition;
        if (qualityDef is null)
        {
            log.Debug("{Instance} has no quality definition", config.InstanceName);
            return null;
        }

        var qualityDefinitions = guide.GetQualitySizeData(config.ServiceType);
        var selectedQuality = qualityDefinitions
            .FirstOrDefault(x => x.Type.EqualsIgnoreCase(qualityDef.Type));

        if (selectedQuality == null)
        {
            log.Error("The specified quality definition type does not exist: {Type}", qualityDef.Type);
            return null;
        }

        log.Information("Processing Quality Definition: {QualityDefinition}", qualityDef.Type);
        AdjustPreferredRatio(qualityDef, selectedQuality);
        return selectedQuality;
    }

    private void AdjustPreferredRatio(QualityDefinitionConfig config, QualitySizeData selectedQuality)
    {
        if (config.PreferredRatio is null)
        {
            return;
        }

        log.Information("Using an explicit preferred ratio which will override values from the guide");

        // Fix an out of range ratio and warn the user
        if (config.PreferredRatio is < 0 or > 1)
        {
            var clampedRatio = Math.Clamp(config.PreferredRatio.Value, 0, 1);
            log.Warning("Your `preferred_ratio` of {CurrentRatio} is out of range. " +
                "It must be a decimal between 0.0 and 1.0. It has been clamped to {ClampedRatio}",
                config.PreferredRatio, clampedRatio);

            config.PreferredRatio = clampedRatio;
        }

        // Apply a calculated preferred size
        foreach (var quality in selectedQuality.Qualities)
        {
            quality.Preferred = quality.InterpolatedPreferred(config.PreferredRatio.Value);
        }
    }
}
