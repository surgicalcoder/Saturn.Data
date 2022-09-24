using System;

namespace GoLive.Saturn.Data.Abstractions
{
    public class CommandFailedArgs
    {
        public int RequestId { get; set; }
        public string CommandName { get; set; }
        public Exception Exception { get; set; }
    }
}