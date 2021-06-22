using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.AuditTrail.Controllers;
using OrchardCore.AuditTrail.Drivers;
using OrchardCore.AuditTrail.Indexes;
using OrchardCore.AuditTrail.Models;
using OrchardCore.AuditTrail.Navigation;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.AuditTrail.Settings;
using OrchardCore.AuditTrail.ViewModels;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Settings.Deployment;
using YesSql.Filters.Query;
using YesSql.Indexes;

namespace OrchardCore.AuditTrail
{
    public class Startup : StartupBase
    {
        private readonly AdminOptions _adminOptions;

        public Startup(IOptions<AdminOptions> adminOptions)
        {
            _adminOptions = adminOptions.Value;
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            // Add ILookupNormalizer as Singleton because it is needed by Users
            services.TryAddSingleton<ILookupNormalizer, UpperInvariantLookupNormalizer>();

            services.AddScoped<IAuditTrailManager, AuditTrailManager>();

            services
                .AddScoped<IDisplayManager<AuditTrailEvent>, DisplayManager<AuditTrailEvent>>()
                .AddScoped<IDisplayDriver<AuditTrailEvent>, AuditTrailEventDisplayDriver>();

            services.AddSingleton<IAuditTrailIdGenerator, AuditTrailIdGenerator>();

            services.Configure<StoreCollectionOptions>(o => o.Collections.Add(AuditTrailEvent.Collection));

            services.AddScoped<IDataMigration, Migrations>();
            services.AddSingleton<IIndexProvider, AuditTrailEventIndexProvider>();
            services.AddSingleton<IBackgroundTask, AuditTrailBackgroundTask>();

            services.AddScoped<IPermissionProvider, Permissions>();
            services.AddScoped<INavigationProvider, AuditTrailAdminMenu>();
            services.AddScoped<INavigationProvider, AuditTrailSettingsAdminMenu>();

            services.AddScoped<IDisplayDriver<ISite>, AuditTrailSettingsDisplayDriver>();
            services.AddScoped<IDisplayDriver<ISite>, AuditTrailTrimmingSettingsDisplayDriver>();

            services.AddScoped<IDisplayManager<AuditTrailIndexOptions>, DisplayManager<AuditTrailIndexOptions>>()
                .AddScoped<IDisplayDriver<AuditTrailIndexOptions>, AuditTrailOptionsDisplayDriver>();

            services.AddScoped<IAuditTrailAdminListQueryService, DefaultAuditTrailAdminListQueryService>();

            services.AddSingleton<IAuditTrailAdminListFilterParser>(sp =>
            {
                var filterProviders = sp.GetServices<IAuditTrailAdminListFilterProvider>();
                var builder = new QueryEngineBuilder<AuditTrailEvent>();
                foreach (var provider in filterProviders)
                {
                    provider.Build(builder);
                }

                var parser = builder.Build();

                return new DefaultAuditTrailAdminListFilterParser(parser);
            });

            services.AddTransient<IAuditTrailAdminListFilterProvider, DefaultAuditTrailAdminListFilterProvider>();

            services.Configure<AuditTrailAdminListOptions>(options =>
            {
                options.ForSort("time-desc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["Newest"], opt.Value, model.Sort == String.Empty))
                    .AsDefault();

                options.ForSort("time-asc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["Oldest"], opt.Value, model.Sort == opt.Value));

                options.ForSort("category-asc-time-desc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.Category).ThenByDescending(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["Category"], opt.Value, model.Sort == opt.Value));

                options.ForSort("category-asc-time-asc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.Category).ThenBy(i => i.CreatedUtc));

                options.ForSort("category-desc-time-desc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.Category).ThenByDescending(i => i.CreatedUtc));

                options.ForSort("category-desc-time-asc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.Category).ThenBy(i => i.CreatedUtc));

                options.ForSort("event-asc-time-desc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.Name).ThenByDescending(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["Event"], opt.Value, model.Sort == opt.Value));

                options.ForSort("event-asc-time-asc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.Name).ThenBy(i => i.CreatedUtc));

                options.ForSort("event-desc-time-desc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.Name).ThenByDescending(i => i.CreatedUtc));

                options.ForSort("event-desc-time-asc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.Name).ThenBy(i => i.CreatedUtc));

                options.ForSort("user-asc-time-asc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderBy(i => i.NormalizedUserName).ThenBy(i => i.CreatedUtc))
                    .WithSelectListItem<Startup>((S, opt, model) => new SelectListItem(S["User"], opt.Value, model.Sort == opt.Value));

                options.ForSort("user-desc-time-desc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.NormalizedUserName).ThenByDescending(i => i.CreatedUtc));

                options.ForSort("user-desc-time-asc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.NormalizedUserName).ThenBy(i => i.CreatedUtc));

                options.ForSort("user-desc-time-desc")
                    .WithQuery((val, query) => query.With<AuditTrailEventIndex>().OrderByDescending(i => i.NormalizedUserName).ThenByDescending(i => i.CreatedUtc));
            });
        }

        public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        {
            var adminControllerName = typeof(AdminController).ControllerName();

            routes.MapAreaControllerRoute(
                name: "AuditTrailIndex",
                areaName: "OrchardCore.AuditTrail",
                pattern: _adminOptions.AdminUrlPrefix + "/AuditTrail/{correlationId?}",
                defaults: new { controller = adminControllerName, action = nameof(AdminController.Index) }
            );

            routes.MapAreaControllerRoute(
                name: "AuditTrailDisplay",
                areaName: "OrchardCore.AuditTrail",
                pattern: _adminOptions.AdminUrlPrefix + "/AuditTrail/Display/{auditTrailEventId}",
                defaults: new { controller = adminControllerName, action = nameof(AdminController.Display) }
            );
        }
    }

    [RequireFeatures("OrchardCore.Deployment")]
    public class DeploymentStartup : StartupBase
    {
        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<IDeploymentSource, SiteSettingsPropertyDeploymentSource<AuditTrailSettings>>();
            services.AddScoped<IDisplayDriver<DeploymentStep>>(sp =>
            {
                var S = sp.GetService<IStringLocalizer<Startup>>();
                return new SiteSettingsPropertyDeploymentStepDriver<AuditTrailSettings>(S["Audit Trail settings"], S["Exports the audit trail settings."]);
            });

            services.AddSingleton<IDeploymentStepFactory>(new SiteSettingsPropertyDeploymentStepFactory<AuditTrailSettings>());

            services.AddTransient<IDeploymentSource, SiteSettingsPropertyDeploymentSource<AuditTrailTrimmingSettings>>();
            services.AddScoped<IDisplayDriver<DeploymentStep>>(sp =>
            {
                var S = sp.GetService<IStringLocalizer<Startup>>();
                return new SiteSettingsPropertyDeploymentStepDriver<AuditTrailTrimmingSettings>(S["Audit Trail Trimming settings"], S["Exports the audit trail trimming settings."]);
            });

            services.AddSingleton<IDeploymentStepFactory>(new SiteSettingsPropertyDeploymentStepFactory<AuditTrailTrimmingSettings>());
        }
    }
}
