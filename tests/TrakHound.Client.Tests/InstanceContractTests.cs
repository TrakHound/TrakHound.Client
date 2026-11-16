// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using TrakHound.Clients;
using TrakHound.Logging;
using Xunit;
using Xunit.Abstractions;

namespace TrakHound.Client.Tests
{
    /// <summary>
    /// Calls a real Instance, to check that the synced models still match what the Instance sends. Runs when TRAKHOUND_CLIENT_TEST_URL is
    /// set (ex. http://localhost:8472/), with TRAKHOUND_CLIENT_TEST_API_KEY when the Instance needs an API Key. Without them each test returns
    /// without checking anything. A local Instance needs no API Key: resources that no Security Profile covers can be read from its own machine
    /// </summary>
    public class InstanceContractTests
    {
        private const string UrlVariable = "TRAKHOUND_CLIENT_TEST_URL";
        private const string ApiKeyVariable = "TRAKHOUND_CLIENT_TEST_API_KEY";

        private readonly ITestOutputHelper _output;


        public InstanceContractTests(ITestOutputHelper output)
        {
            _output = output;
        }


        [Fact]
        public async Task ReadsTheInstance()
        {
            var client = CreateClient();
            if (client == null) return;

            var information = await client.Instances.GetHostInformation();
            Assert.NotNull(information);
            Assert.False(string.IsNullOrEmpty(information.Id));
            Assert.False(string.IsNullOrEmpty(information.Version));

            Assert.NotNull(await client.Instances.GetHealth());
            Assert.NotNull(await client.Instances.GetBoots(5));
        }

        [Fact]
        public async Task ListsTheModules()
        {
            var client = CreateClient();
            if (client == null) return;

            // System API calls that answer on any Instance. Client.Functions and Client.Workers call the Functions and Workers Apis, which
            // are packages an Instance may not have, and the Services endpoints return Not Found (null) when no Service is running
            Assert.NotNull(await client.Api.GetRouteInformation());
            Assert.NotNull(await client.Apps.GetInformation());
        }

        [Fact]
        public async Task QueriesTheLogs()
        {
            var client = CreateClient();
            if (client == null) return;

            var result = await client.Logs.Query(new TrakHoundLogQuery { From = DateTime.UtcNow.AddDays(-1), To = DateTime.UtcNow, Take = 10 });
            Assert.NotNull(result);
            Assert.NotNull(result.Items);
        }

        /// <summary>
        /// The Instance's OpenAPI document describes each response with a schema made from the Instance's own models. Every property in a
        /// schema must be in the synced model of the same name, or the client would drop it
        /// </summary>
        [Fact]
        public async Task OpenApiSchemasMatchTheSyncedModels()
        {
            var url = GetUrl();
            if (url == null) return;

            var document = await GetOpenApiDocument(url);
            var schemas = document["components"]?["schemas"] as JsonObject;
            Assert.NotNull(schemas);

            var models = GetModels();
            var problems = new List<string>();
            var matched = 0;

            foreach (var schema in schemas)
            {
                if (!(schema.Value?["properties"] is JsonObject properties)) continue;
                if (!models.TryGetValue(schema.Key, out var model)) continue;

                matched++;
                var modelProperties = new HashSet<string>(GetJsonPropertyNames(model));
                foreach (var property in properties)
                {
                    if (!modelProperties.Contains(property.Key)) problems.Add($"{model.FullName} has no '{property.Key}'");
                }
            }

            _output.WriteLine($"Compared {matched} of {schemas.Count} schemas with synced models");

            // The comparison only means something if the document's schemas are found in the client
            Assert.True(matched >= 10, $"Only {matched} schemas matched a synced model");
            Assert.True(problems.Count == 0, "The Instance sends properties that the synced models don't have (sync from the Framework):\n" + string.Join("\n", problems));
        }

        /// <summary>
        /// Gets each System API endpoint that needs no parameters and returns a synced model, and reads the response into that model with
        /// unmapped properties disallowed, so a property the Instance sends and the model doesn't have fails
        /// </summary>
        [Fact]
        public async Task ResponsesHaveNoUnmappedProperties()
        {
            var url = GetUrl();
            if (url == null) return;

            var document = await GetOpenApiDocument(url);
            var models = GetModels();

            var options = Json.DefaultOptions;
            if (options.TypeInfoResolver == null) options.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
            options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;

            var problems = new List<string>();
            var checkedCount = 0;

            using (var http = CreateHttpClient(url))
            {
                foreach (var path in (document["paths"] as JsonObject) ?? new JsonObject())
                {
                    var operation = path.Value?["get"];
                    if (operation == null || path.Key.Contains("{")) continue;
                    if (operation["parameters"] is JsonArray parameters && parameters.Any(o => o?["required"]?.GetValue<bool>() == true)) continue;

                    var type = GetResponseType(operation, models);
                    if (type == null) continue;

                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        HttpResponseMessage response;
                        try
                        {
                            response = await http.GetAsync(path.Key.TrimStart('/'), timeout.Token);
                        }
                        catch (HttpRequestException ex)
                        {
                            // Not a model problem (ex. an Instance that answers with an invalid status), so it's reported but doesn't fail
                            _output.WriteLine($"Couldn't read GET {path.Key}: {ex.InnerException?.Message ?? ex.Message}");
                            continue;
                        }
                        if (!response.IsSuccessStatusCode) continue;

                        var json = await response.Content.ReadAsStringAsync();
                        checkedCount++;
                        try
                        {
                            JsonSerializer.Deserialize(json, type, options);
                        }
                        catch (JsonException ex)
                        {
                            problems.Add($"GET {path.Key} ({type.Name}): {ex.Message}");
                        }
                    }
                }
            }

            _output.WriteLine($"Read {checkedCount} responses into synced models");

            Assert.True(checkedCount > 0, "No endpoint returned a synced model");
            Assert.True(problems.Count == 0, "The Instance sends properties that the synced models don't have (sync from the Framework):\n" + string.Join("\n", problems));
        }


        private static Uri GetUrl()
        {
            var url = Environment.GetEnvironmentVariable(UrlVariable);
            if (string.IsNullOrEmpty(url)) return null;

            return new Uri(url.EndsWith("/") ? url : url + "/");
        }

        private static TrakHoundHttpClient CreateClient()
        {
            var url = GetUrl();
            if (url == null) return null;

            var configuration = new TrakHoundHttpClientConfiguration(url.Host, url.Port, url.AbsolutePath, url.Scheme == Uri.UriSchemeHttps);
            configuration.ApiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
            return new TrakHoundHttpClient(configuration);
        }

        private static HttpClient CreateHttpClient(Uri url)
        {
            var http = new HttpClient { BaseAddress = url };
            var apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
            if (!string.IsNullOrEmpty(apiKey)) http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            return http;
        }

        private static async Task<JsonNode> GetOpenApiDocument(Uri url)
        {
            using (var http = CreateHttpClient(url))
            {
                var json = await http.GetStringAsync("_instances/openapi");
                return JsonNode.Parse(json);
            }
        }

        // The client's public model classes by name. A name used by more than one class is left out, since a schema can't tell them apart
        private static Dictionary<string, Type> GetModels()
        {
            return typeof(TrakHoundHttpClient).Assembly.GetTypes()
                .Where(o => o.IsPublic && o.IsClass && !o.IsAbstract && !o.IsGenericTypeDefinition)
                .GroupBy(o => o.Name)
                .Where(o => o.Count() == 1)
                .ToDictionary(o => o.Key, o => o.First());
        }

        private static IEnumerable<string> GetJsonPropertyNames(Type type)
        {
            var options = Json.DefaultOptions;
            if (options.TypeInfoResolver == null) options.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
            return options.GetTypeInfo(type).Properties.Select(o => o.Name);
        }

        // The model a GET returns: a schema reference, or an array of them (the Instance describes each model once, as a named component)
        private static Type GetResponseType(JsonNode operation, Dictionary<string, Type> models)
        {
            var schema = operation["responses"]?["200"]?["content"]?["application/json"]?["schema"];
            if (schema == null) return null;

            var isArray = IsArray(schema);
            var reference = ((isArray ? schema["items"]?["$ref"] : schema["$ref"]) as JsonValue)?.GetValue<string>();
            if (reference == null) return null;

            var name = reference.Substring(reference.LastIndexOf('/') + 1);
            if (!models.TryGetValue(name, out var model)) return null;

            return isArray ? model.MakeArrayType() : model;
        }

        // "type" is "array", or a list that includes it (ex. ["array", "null"])
        private static bool IsArray(JsonNode schema)
        {
            var type = schema["type"];
            if (type is JsonValue value) return value.GetValue<string>() == "array";
            if (type is JsonArray values) return values.Any(o => (o as JsonValue)?.GetValue<string>() == "array");
            return false;
        }
    }
}
