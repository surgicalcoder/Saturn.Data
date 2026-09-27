using System;

namespace GoLive.Saturn.Data.Entities;

public interface ISuppressTracking
{
    IDisposable SuppressTracking();
}
