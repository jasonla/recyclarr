using System.Diagnostics.CodeAnalysis;
using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;
using Recyclarr.Config.Models;
using Recyclarr.Json;
using Recyclarr.Settings;
using Serilog;

namespace Recyclarr.ServarrApi.Http;

public class ServarrRequestBuilder(
    ILogger log,
    IFlurlClientCache clientCache,
    ISettingsProvider settingsProvider)
    : IServarrRequestBuilder
{
    public IFlurlRequest Request(IServiceConfiguration config, params object[] path)
    {
        var client = clientCache.GetOrAdd(
            config.InstanceName,
            config.BaseUrl.AppendPathSegments("api", "v3"),
            Configure);

        return client.Request(path)
            .WithHeader("X-Api-Key", config.ApiKey);
    }

    [SuppressMessage("SonarCloud", "S4830:Server certificates should be verified during SSL/TLS connections")]
    [SuppressMessage("Security", "CA5359:Do Not Disable Certificate Validation")]
    private void Configure(IFlurlClientBuilder builder)
    {
        builder.WithSettings(settings =>
        {
            settings.JsonSerializer = new DefaultJsonSerializer(GlobalJsonSerializerSettings.Services);
            FlurlLogging.SetupLogging(settings, log);
        });

        builder.ConfigureInnerHandler(handler =>
        {
            if (!settingsProvider.Settings.EnableSslCertificateValidation)
            {
                log.Warning(
                    "Security Risk: Certificate validation is being DISABLED because setting " +
                    "`enable_ssl_certificate_validation` is set to `false`");

                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }
        });
    }
}
