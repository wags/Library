﻿using System.Linq;
using Library.API.Entities;
using Library.API.Helpers;
using Library.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using NLog.Extensions.Logging;
using NLog.Web;

namespace Library.API
{
    public class Startup
    {
        public static IConfigurationRoot Configuration;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appSettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            env.ConfigureNLog("nlog.config");
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(setupAction =>
            {
                setupAction.ReturnHttpNotAcceptable = true;
                setupAction.OutputFormatters.Add(new XmlDataContractSerializerOutputFormatter());
                // setupAction.InputFormatters.Add(new XmlDataContractSerializerInputFormatter());

                var xmlDataContractSerializerInputFormatter =
                new XmlDataContractSerializerInputFormatter();
                xmlDataContractSerializerInputFormatter.SupportedMediaTypes
                    .Add("application/vnd.marvin.authorwithdateofdeath.full+xml");
                setupAction.InputFormatters.Add(xmlDataContractSerializerInputFormatter);                    

                var jsonInputFormatter = setupAction.InputFormatters
                .OfType<JsonInputFormatter>().FirstOrDefault();

                if (jsonInputFormatter != null)
                {
                    jsonInputFormatter.SupportedMediaTypes
                    .Add("application/vnd.marvin.author.full+json");
                    jsonInputFormatter.SupportedMediaTypes
                    .Add("application/vnd.marvin.authorwithdateofdeath.full+json");
                }

                var jsonOutputFormatter = setupAction.OutputFormatters
                    .OfType<JsonOutputFormatter>().FirstOrDefault();

                if (jsonOutputFormatter != null)
                {
                    jsonOutputFormatter.SupportedMediaTypes.Add("application/vnd.marvin.hateoas+json");
                }
            })
            .AddJsonOptions(options =>
            {
                options.SerializerSettings.ContractResolver =
                new CamelCasePropertyNamesContractResolver();
            });

            // register the DbContext on the container, getting the connection string from
            // appSettings (note: use this during development; in a production environment,
            // it's better to store the connection string in an environment variable)
            var connectionString = Configuration["connectionStrings:libraryDBConnectionString"];
            services.AddDbContext<LibraryContext>(o => o.UseSqlServer(connectionString));

            // register the repository
            services.AddScoped<ILibraryRepository, LibraryRepository>();

            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();

            services.AddScoped<IUrlHelper, UrlHelper>(implementationFactory =>
            {
                var actionContext =
                implementationFactory.GetService<IActionContextAccessor>().ActionContext;
                return new UrlHelper(actionContext);
            });

            services.AddTransient<IPropertyMappingService, PropertyMappingService>();

            services.AddTransient<ITypeHelperService, TypeHelperService>();

            // Needed for NLog.Web
            // Call this in case you need aspnet-user-authtype/aspnet-user-identity
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddHttpCacheHeaders(
                (expirationModelOptions)
                =>
                {
                    expirationModelOptions.MaxAge = 600;
                },
                (validationModelOptions)
                =>
                {
                    validationModelOptions.AddMustRevalidate = true;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
            ILoggerFactory loggerFactory, LibraryContext libraryContext)
        {
            loggerFactory.AddConsole();

            loggerFactory.AddDebug(LogLevel.Information);

            // loggerFactory.AddProvider(new NLog.Extensions.Logging.NLogLoggerProvider());
            loggerFactory.AddNLog();

            app.AddNLogWeb();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(appBuilder =>
                {
                    appBuilder.Run(async context =>
                    {
                        var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                        if (exceptionHandlerFeature != null)
                        {
                            var logger = loggerFactory.CreateLogger("Global exception logger");
                            logger.LogError(500,
                                exceptionHandlerFeature.Error,
                                exceptionHandlerFeature.Error.Message);
                        }

                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("An unexpected fault happened. Try again later.");
                    });
                });
            }

            AutoMapper.Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<Entities.Author, Models.AuthorDto>()
                    .ForMember(dest => dest.Name, opt => opt.MapFrom(src =>
                    $"{src.FirstName} {src.LastName}"))
                    .ForMember(dest => dest.Age, opt => opt.MapFrom(src =>
                    src.DateOfBirth.GetCurrentAge(src.DateOfDeath)));

                cfg.CreateMap<Entities.Book, Models.BookDto>();

                cfg.CreateMap<Models.AuthorForCreationDto, Entities.Author>();

                cfg.CreateMap<Models.AuthorForCreationWithDateOfDeathDto, Entities.Author>();

                cfg.CreateMap<Models.BookForCreationDto, Entities.Book>();

                cfg.CreateMap<Models.BookForUpdateDto, Entities.Book>();

                cfg.CreateMap<Entities.Book, Models.BookForUpdateDto>();
            });

            libraryContext.EnsureSeedDataForContext();

            app.UseHttpCacheHeaders();

            app.UseMvc();
        }
    }
}
