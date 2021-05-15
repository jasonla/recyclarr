using System.Collections.Generic;
using Trash.Radarr.CustomFormat.Guide;
using Trash.Radarr.CustomFormat.Models;
using Trash.Radarr.CustomFormat.Models.Cache;

namespace Trash.Radarr.CustomFormat.Processors.GuideSteps
{
    public interface ICustomFormatStep
    {
        List<ProcessedCustomFormatData> ProcessedCustomFormats { get; }
        List<TrashIdMapping> DeletedCustomFormatsInCache { get; }
        List<(string, string)> CustomFormatsWithOutdatedNames { get; }

        void Process(IEnumerable<CustomFormatData> customFormatGuideData, IEnumerable<CustomFormatConfig> config,
            CustomFormatCache? cache);
    }
}