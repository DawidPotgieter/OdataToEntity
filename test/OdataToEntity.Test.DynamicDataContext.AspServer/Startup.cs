﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using OdataToEntity.AspNetCore;
using OdataToEntity.EfCore.DynamicDataContext;
using OdataToEntity.EfCore.DynamicDataContext.InformationSchema;
using System;
using System.IO;

namespace OdataToEntity.Test.DynamicDataContext.AspServer
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvcCore();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
                loggingBuilder.AddConfiguration(Configuration.GetSection("Logging"));
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            String basePath = Configuration.GetValue<String>("OdataToEntity:BasePath");
            String provider = Configuration.GetValue<String>("OdataToEntity:Provider");
            String connectionString = Configuration.GetValue<String>("OdataToEntity:ConnectionString");
            bool useRelationalNulls = Configuration.GetValue<bool>("OdataToEntity:UseRelationalNulls");
            String informationSchemaMappingFileName = Configuration.GetValue<String>("OdataToEntity:InformationSchemaMappingFileName");

            InformationSchemaMapping informationSchemaMapping = null;
            if (informationSchemaMappingFileName != null)
            {
                String json = File.ReadAllText(informationSchemaMappingFileName);
                informationSchemaMapping = Newtonsoft.Json.JsonConvert.DeserializeObject<InformationSchemaMapping>(json);
            }

            var schemaFactory = new DynamicSchemaFactory(provider, connectionString);
            ProviderSpecificSchema providerSchema = schemaFactory.CreateSchema(useRelationalNulls);
            IEdmModel dynamicEdmModel;
            using (var metadataProvider = providerSchema.CreateMetadataProvider(informationSchemaMapping))
            {
                DynamicTypeDefinitionManager typeDefinitionManager = DynamicTypeDefinitionManager.Create(metadataProvider);
                var dataAdapter = new DynamicDataAdapter(typeDefinitionManager);
                dynamicEdmModel = dataAdapter.BuildEdmModel(metadataProvider);
            }

            if (!String.IsNullOrEmpty(basePath) && basePath[0] != '/')
                basePath = "/" + basePath;

            app.UseOdataToEntityMiddleware<OePageMiddleware>(basePath, dynamicEdmModel);
            app.UseMvcWithDefaultRoute();
        }
    }
}