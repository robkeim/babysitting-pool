namespace BabysittingPool.Infrastructure.Persistence;

/// <summary>One fixed window for one key. The key is a SHA-256 hash: the raw key embeds an email address or IP.</summary>
public sealed class RateLimitCounterRecord
{
    public string Key { get; set; } = "";

    /// <summary>Unix milliseconds at which the window reopens.</summary>
    public long WindowEnd { get; set; }

    public long Count { get; set; }
}
