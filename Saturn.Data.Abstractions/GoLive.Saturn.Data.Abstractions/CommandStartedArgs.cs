namespace GoLive.Saturn.Data.Abstractions
{
    public class CommandStartedArgs
    {
        public int RequestId { get; set; }
        public string CommandName { get; set; }
        public string Command { get; set; }
    }
}