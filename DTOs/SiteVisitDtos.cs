// DTOs/SiteVisitDtos.cs
using System;

namespace geoback.DTOs
{
    public class ScheduleSiteVisitDto
    {
        public DateTime ScheduledDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class ConfirmSiteVisitDto
    {
        public string Findings { get; set; } = string.Empty;
    }

    public class SiteVisitStatsDto
    {
        public int Total { get; set; }
        public int Today { get; set; }
        public int Upcoming { get; set; }
    }
}