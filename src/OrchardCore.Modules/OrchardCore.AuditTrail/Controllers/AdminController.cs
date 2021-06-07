using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using OrchardCore.AuditTrail.Models;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.AuditTrail.ViewModels;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using OrchardCore.Routing;
using YesSql;
using YesSql.Services;
using YesSql.Filters.Query;

namespace OrchardCore.AuditTrail.Controllers
{
    public class AdminController : Controller
    {
        private readonly ISiteService _siteService;
        private readonly IShapeFactory _shapeFactory;
        private readonly IAuditTrailManager _auditTrailManager;
        private readonly IUpdateModelAccessor _updateModelAccessor;
        private readonly IAuthorizationService _authorizationService;
        private readonly IAuditTrailAdminListQueryService _auditTrailAdminListQueryService;
        private readonly IDisplayManager<AuditTrailEvent> _displayManager;
        private readonly IDisplayManager<AuditTrailIndexOptions> _auditTrailOptionsDisplayManager;
        private readonly IClock _clock;
        private readonly ILocalClock _localClock;
        private readonly IStringLocalizer S;
        private readonly dynamic New;

        public AdminController(
            ISiteService siteService,
            IShapeFactory shapeFactory,
            IAuditTrailManager auditTrailManager,
            IUpdateModelAccessor updateModelAccessor,
            IAuthorizationService authorizationService,
            IAuditTrailAdminListQueryService auditTrailAdminListQueryService,
            IDisplayManager<AuditTrailEvent> displayManager,
            IDisplayManager<AuditTrailIndexOptions> auditTrailOptionsDisplayManager,
            IClock clock,
            ILocalClock localClock,
            IStringLocalizer<AdminController> stringLocalizer)
        {
            _siteService = siteService;
            _shapeFactory = shapeFactory;
            New = shapeFactory;
            _auditTrailManager = auditTrailManager;
            _updateModelAccessor = updateModelAccessor;
            _authorizationService = authorizationService;
            _auditTrailAdminListQueryService = auditTrailAdminListQueryService;
            _displayManager = displayManager;
            _auditTrailOptionsDisplayManager = auditTrailOptionsDisplayManager;
            _clock = clock;
            _localClock = localClock;
            S = stringLocalizer;
        }

        public async Task<ActionResult> Index([ModelBinder(BinderType = typeof(AuditTrailFilterEngineModelBinder), Name = "q")] QueryFilterResult<AuditTrailEvent> queryFilterResult, PagerParameters pagerParameters, string correlationId = "")
        {
            if (!await _authorizationService.AuthorizeAsync(User, AuditTrailPermissions.ViewAuditTrail))
            {
                return Forbid();
            }

            var options = new AuditTrailIndexOptions
            {
                FilterResult = queryFilterResult
            };


            // This is used by Contents feature for routing so needs to be passed into the options.
            if (!String.IsNullOrEmpty(correlationId))
            {
                options.CorrelationId = correlationId;
                options.CorrelationIdFromRoute = true;
            }

            if (options.CorrelationIdFromRoute)
            {
                // When the correlation id is provided via the route or options a placeholder node is used to apply a filter.
                options.FilterResult.TryAddOrReplace(new CorrelationIdFilterNode(options.CorrelationId));
            }

            var siteSettings = await _siteService.GetSiteSettingsAsync();
            var pager = new Pager(pagerParameters, siteSettings.PageSize);

            // With the options populated we filter the query, allowing the filters to alter the options.
            var result = await _auditTrailAdminListQueryService.QueryAsync(pager.Page, pager.PageSize, options);

            // The search text is provided back to the UI.
            options.SearchText = options.FilterResult.ToString();
            options.OriginalSearchText = options.SearchText;

            // Populate route values to maintain previous route data when generating page links.
            options.RouteValues.TryAdd("q", options.FilterResult.ToString());

            // TODO route data.
            // var routeData = new RouteData(options.RouteValues);

            // var count = await query.CountAsync();

            // var auditTrailEvents = await query
            //     .Skip(pager.GetStartIndex())
            //     .Take(pager.PageSize)
            //     .ListAsync();

// var pagerShape = (await New.Pager(pager)).TotalItemCount(count).RouteData(routeData);
            var pagerShape = await _shapeFactory.CreateAsync("Pager", Arguments.From(new
            {
                pager.Page,
                pager.PageSize,
                TotalItemCount = result.TotalCount
            }));

/*

            var categories = _auditTrailManager.DescribeCategories().ToArray();

            options.Categories = categories
                .GroupBy(category => category.Name)
                .Select(categories => categories.First())
                .Select(category => new SelectListItem(category.LocalizedName.Value, category.Name, category.Name == options.Category))
                .ToList();

            options.Categories.Insert(0, new SelectListItem(S["All categories"], String.Empty, String.IsNullOrEmpty(options.Category)));


            if (options.CorrelationIdFromRoute)
            {
                var firstEvent = auditTrailEvents.FirstOrDefault();
                if (firstEvent != null)
                {
                    var currentCategory = categories.FirstOrDefault(x => x.Name == firstEvent.Category);
                    if (currentCategory != null)
                    {
                        options.Events = currentCategory.Events.Select(category =>
                            new SelectListItem(category.LocalizedName.Value, category.Name, category.Name == options.Category)).ToList();
                    }
                }
            }

            options.AuditTrailDates = new List<SelectListItem>()
            {
                new SelectListItem(S["Any date"], String.Empty, options.Date == String.Empty),
            };

            var localNow = await _localClock.LocalNowAsync;
            var dateTimeValue = ">@now-1";
            options.AuditTrailDates.Add(new SelectListItem(S["Last 24 hours"], dateTimeValue, options.Date == dateTimeValue));

            dateTimeValue = "@now-2..@now-1";
            options.AuditTrailDates.Add(new SelectListItem(S["Previous 48 hours"], dateTimeValue, options.Date == dateTimeValue));

            dateTimeValue = ">@now-7";
            options.AuditTrailDates.Add(new SelectListItem(S["Last 7 days"], dateTimeValue, options.Date == dateTimeValue));

            dateTimeValue = $">{localNow.AddDays(-30).LocalDateTime.Date.ToString("o")}";
            options.AuditTrailDates.Add(new SelectListItem(S["Last 30 days"], dateTimeValue, options.Date == dateTimeValue));

            dateTimeValue = $">{localNow.AddDays(-90).LocalDateTime.Date.ToString("o")}";
            options.AuditTrailDates.Add(new SelectListItem(S["Last 90 days"], dateTimeValue, options.Date == dateTimeValue));

            dateTimeValue = $">{localNow.AddHours(-1).ToString("o")}";
            options.AuditTrailDates.Add(new SelectListItem(S["Last hour"], dateTimeValue, options.Date == dateTimeValue));

            dateTimeValue = $"{localNow.AddHours(-2).ToString("o")}..{localNow.AddHours(-1).ToString("o")}";
            options.AuditTrailDates.Add(new SelectListItem(S["Previous hour"], dateTimeValue, options.Date == dateTimeValue));
*/
            var items = new List<IShape>();

            foreach (var auditTrailEvent in result.Events)
            {
                items.Add(
                    await _displayManager.BuildDisplayAsync(auditTrailEvent, updater: _updateModelAccessor.ModelUpdater, displayType: "SummaryAdmin")
                );
            }

            var startIndex = (pager.Page - 1) * (pager.PageSize) + 1;
            options.StartIndex = startIndex;
            options.EndIndex = startIndex + items.Count - 1;
            options.EventsCount = items.Count;
            options.TotalItemCount = result.TotalCount;

            var header = await _auditTrailOptionsDisplayManager.BuildEditorAsync(options, _updateModelAccessor.ModelUpdater, false);

            var shapeViewModel = await _shapeFactory.CreateAsync<AuditTrailListViewModel>("AuditTrailAdminList", viewModel =>
            {
                viewModel.Events = items;
                viewModel.Pager = pagerShape;
                viewModel.Options = options;
                viewModel.Header = header;
            });

            return View(shapeViewModel);
        }

        [HttpPost, ActionName("Index")]
        [FormValueRequired("submit.Filter")]
         public async Task<ActionResult> IndexFilterPOST(AuditTrailIndexOptions options)
        {
            await _auditTrailOptionsDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, false);
            // When the user has typed something into the search input no further evaluation of the form post is required.
            if (!String.Equals(options.SearchText, options.OriginalSearchText, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index), new RouteValueDictionary { { "q", options.SearchText } });
            }

            // Evaluate the values provided in the form post and map them to the filter result and route values.
            await _auditTrailOptionsDisplayManager.UpdateEditorAsync(options, _updateModelAccessor.ModelUpdater, false);

            // The route value must always be added after the editors have updated the models.
            options.RouteValues.TryAdd("q", options.FilterResult.ToString());

            return RedirectToAction(nameof(Index), options.RouteValues);
        }

        public async Task<ActionResult> Display(string auditTrailEventId)
        {
            if (!await _authorizationService.AuthorizeAsync(User, AuditTrailPermissions.ViewAuditTrail))
            {
                return Forbid();
            }

            var auditTrailEvent = await _auditTrailManager.GetEventAsync(auditTrailEventId);
            if (auditTrailEvent == null)
            {
                return NotFound();
            }


            var shape = await _displayManager.BuildDisplayAsync(auditTrailEvent, updater: _updateModelAccessor.ModelUpdater, displayType: "DetailAdmin");

            return View(new AuditTrailItemViewModel { Shape = shape});
        }
    }
}
