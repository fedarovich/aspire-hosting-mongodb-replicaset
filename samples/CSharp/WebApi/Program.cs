using System.Net.Mime;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMongoDBClient("TestMongoRS", 
    configureClientSettings: settings =>
    {
        // When running this project locally, it's not possible to override the certificate trust on the aspire side, however we can add our custom validation callback.
        // In this example, we look for a configuration value containing the expected thumbprint of the MongoDB server certificate,
        // and if it's set, we add a custom validation callback that checks the server certificate thumbprint against the expected value.
        // The configuration value is set on the Aspire side using the environment variable TESTMONGORS_CERTIFICATE_THUMBPRINT.
        var mongoCertificateThumbprint = builder.Configuration["TESTMONGORS_CERTIFICATE_THUMBPRINT"];
        if (!string.IsNullOrEmpty(mongoCertificateThumbprint))
        {
            settings.SslSettings.ServerCertificateValidationCallback = (_, certificate, _, _) =>
                certificate is X509Certificate2 cert && cert.Thumbprint.Equals(mongoCertificateThumbprint, StringComparison.OrdinalIgnoreCase);
        }

        // As an alternative to the above approach, you can also choose to allow insecure TLS connections in Development environment:
        // if (builder.Environment.IsDevelopment())
        // {
        //     settings.AllowInsecureTls = true;
        // }
    });

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/");
}

app.UseHttpsRedirection();

app.MapGet("/mongo/hello", async ([FromServices] IMongoClient mongoClient) =>
    {
        var database = mongoClient.GetDatabase("admin");
        var command = new BsonDocument { { "hello", 1 } };
        var result = database.RunCommand<BsonDocument>(command);
        return Results.Content(result.ToJson(), MediaTypeNames.Application.Json);
    });

app.Run();
