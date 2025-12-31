namespace SYSTEM.HeadlessClient.Connection;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Subscribing,
    Subscribed,
    Authenticating,
    Authenticated,
    CreatingPlayer,
    Ready,
    Error
}
