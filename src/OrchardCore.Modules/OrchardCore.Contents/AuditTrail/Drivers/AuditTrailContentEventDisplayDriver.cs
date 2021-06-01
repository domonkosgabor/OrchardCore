using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OrchardCore.AuditTrail.Drivers;
using OrchardCore.AuditTrail.Extensions;
using OrchardCore.AuditTrail.Models;
using OrchardCore.Contents.AuditTrail.Models;
using OrchardCore.Contents.AuditTrail.ViewModels;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.ContentManagement.Records;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.AuditTrail.Indexes;
using OrchardCore.Entities;
using YesSql;

namespace OrchardCore.Contents.AuditTrail.Drivers
{
    public class AuditTrailContentEventDisplayDriver : AuditTrailEventSectionDisplayDriver<AuditTrailContentEvent>
    {
        private readonly Dictionary<string, string> _latestVersionId = new Dictionary<string, string>();
        private readonly IAuditTrailManager _auditTrailManager;
        private readonly ISession _session;

        public AuditTrailContentEventDisplayDriver(IAuditTrailManager auditTrailManager, ISession session)
        {
            _auditTrailManager = auditTrailManager;
            _session = session;
        }

        public override async Task<IDisplayResult> DisplayAsync(AuditTrailEvent auditTrailEvent, AuditTrailContentEvent contentEvent, BuildDisplayContext context)
        {
            var contentItemId = contentEvent.ContentItem.ContentItemId;

            if (!_latestVersionId.TryGetValue(contentItemId, out var latestVersionId))
            {
                latestVersionId = (await _session.QueryIndex<ContentItemIndex>(index => index.ContentItemId == contentItemId && index.Latest)
                    .FirstOrDefaultAsync())
                    ?.ContentItemVersionId;

                _latestVersionId[contentItemId] = latestVersionId;
            }


            var descriptor = _auditTrailManager.DescribeEvent(auditTrailEvent);

            return Combine(
                Initialize<AuditTrailContentEventViewModel>("AuditTrailContentEventEventData_SummaryAdmin", m => BuildSummaryViewModel(m, auditTrailEvent, contentEvent, descriptor, latestVersionId))
                        .Location("SummaryAdmin","EventData:10"),
                Initialize<AuditTrailContentEventViewModel>("AuditTrailContentEventActions_SummaryAdmin", m => BuildSummaryViewModel(m, auditTrailEvent, contentEvent, descriptor, latestVersionId))
                        .Location("SummaryAdmin","Actions:5"),
                Initialize<AuditTrailContentEventDetailViewModel>("AuditTrailContentEventDetail_DetailAdmin", async m =>
                {
                    BuildSummaryViewModel(m, auditTrailEvent, contentEvent, descriptor, latestVersionId);
                    m.DiffNodes = await BuildDiffNodesAsync(auditTrailEvent, contentEvent, m);
                }).Location("DetailAdmin","Content:5")
            );
        }

        private async Task<DiffNode[]> BuildDiffNodesAsync(AuditTrailEvent auditTrailEvent, AuditTrailContentEvent contentEvent, AuditTrailContentEventDetailViewModel model)
        {
            var contentItem = contentEvent.ContentItem;

            var previousAuditTrailEvent = await _session.Query<AuditTrailEvent, AuditTrailEventIndex>(collection: AuditTrailEvent.Collection)
                .Where(index =>
                    index.Category == "Content" &&
                    index.CreatedUtc <= auditTrailEvent.CreatedUtc &&
                    index.EventId != auditTrailEvent.EventId &&
                    index.CorrelationId == contentItem.ContentItemId)
                .OrderByDescending(index => index.Id)
                .FirstOrDefaultAsync();

            if (previousAuditTrailEvent == null)
            {
                return null;
            }

            var previousContentItem = previousAuditTrailEvent.As<AuditTrailContentEvent>().ContentItem;

            var current = JObject.FromObject(contentItem);
            var previous = JObject.FromObject(previousContentItem);
            previous.Remove(nameof(AuditTrailPart));
            current.Remove(nameof(AuditTrailPart));

            model.PreviousContentItem = previousContentItem;

            model.Previous = previous.ToString();
            model.Current = current.ToString();

            if (current.FindDiff(previous, out var diff))
            {
                return diff.GenerateDiffNodes(contentItem.ContentType);
            }

            return null;
        }


        private static void BuildSummaryViewModel(AuditTrailContentEventViewModel m, AuditTrailEvent model, AuditTrailContentEvent contentEvent, AuditTrailEventDescriptor descriptor, string latestVersionId)
        {
            m.AuditTrailEvent = model;
            m.Descriptor = descriptor;
            m.Name = contentEvent.Name;
            m.ContentItem = contentEvent.ContentItem;
            m.VersionNumber = contentEvent.VersionNumber;
            m.LatestVersionId = latestVersionId;
            m.ContentEvent = contentEvent;
        }
    }
}