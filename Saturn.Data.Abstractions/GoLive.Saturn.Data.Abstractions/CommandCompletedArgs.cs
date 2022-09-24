using System;

namespace GoLive.Saturn.Data.Abstractions
{
    public class CommandCompletedArgs
    {
        public int RequestId { get; set; }
        public string CommandName { get; set; }
        public TimeSpan Time { get; set; }
    }
}