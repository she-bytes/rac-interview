using System;

namespace Api.Configuration;

public sealed class CachingOptions
{
    public uint SlidingExpirationMinutes { get; set; }
    public uint AbsoluteExpirationMinutes { get; set; }
}