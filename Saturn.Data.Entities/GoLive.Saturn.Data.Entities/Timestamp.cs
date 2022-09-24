using System;

namespace GoLive.Saturn.Data.Entities
{
    public class Timestamp
    {
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }

        public bool BypassAutomaticDatePopulation { get; set; }
    }
}