namespace HackerNewsApi.Configurations;
public static class HttpResiliencePipelineKeys
{
    public const string SafeRead = "http-safe-read";
    public const string SideEffectWrite = "http-side-effect-write";
    public const string NotificationDispatch = "http-notification-dispatch";
}