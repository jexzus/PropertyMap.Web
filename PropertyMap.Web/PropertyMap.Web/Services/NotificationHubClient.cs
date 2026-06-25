using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using PropertyMap.Core.DTOs.Notifications;

namespace PropertyMap.Web.Services;

public class NotificationHubClient : IAsyncDisposable
{
    private readonly MemoryTokenStore _tokenStore;
    private readonly string _baseUrl;
    private HubConnection? _connection;

    public event Action<NotificationDto>? OnNotificationReceived;

    public NotificationHubClient(MemoryTokenStore tokenStore, IConfiguration config)
    {
        _tokenStore = tokenStore;
        _baseUrl = config["ApiSettings:BaseUrl"] ?? "https://localhost:7002/";
    }

    public async Task StartAsync()
    {
        if (_connection is not null || !_tokenStore.IsAuthenticated) return;

        _connection = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}hubs/notifications", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_tokenStore.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<NotificationDto>("ReceiveNotification", dto => OnNotificationReceived?.Invoke(dto));

        await _connection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
