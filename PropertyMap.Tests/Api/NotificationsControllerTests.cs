using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;
using Xunit;

namespace PropertyMap.Tests.Api;

public class NotificationsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public NotificationsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task SeedNotificationAsync(string userId, bool leida = false, string titulo = "Notif")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Tipo = TipoNotificacion.AlertaCoincidencia,
            Titulo = titulo,
            Mensaje = "Mensaje de prueba",
            Leida = leida,
            FechaCreacion = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnNotifications()
    {
        var (userAClient, userAId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var (_, userBId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        await SeedNotificationAsync(userAId, titulo: "Para A");
        await SeedNotificationAsync(userBId, titulo: "Para B");

        var mine = await userAClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");

        Assert.Contains(mine!, n => n.Titulo == "Para A");
        Assert.DoesNotContain(mine!, n => n.Titulo == "Para B");
    }

    [Fact]
    public async Task GetMine_RespectsTakeParameter()
    {
        var (userClient, userId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        for (var i = 0; i < 5; i++)
            await SeedNotificationAsync(userId, titulo: $"Notif {i}");

        var limited = await userClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications?take=3");

        Assert.Equal(3, limited!.Count);
    }

    [Fact]
    public async Task GetUnreadCount_CountsOnlyUnread()
    {
        var (userClient, userId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await SeedNotificationAsync(userId, leida: false);
        await SeedNotificationAsync(userId, leida: false);
        await SeedNotificationAsync(userId, leida: true);

        var countResp = await userClient.GetAsync("/api/notifications/unread-count");
        var count = await countResp.Content.ReadFromJsonAsync<int>();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task MarkAsRead_SetsLeidaTrue_DoesNotAffectOthers()
    {
        var (userClient, userId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await SeedNotificationAsync(userId, titulo: "A marcar");
        await SeedNotificationAsync(userId, titulo: "Sin marcar");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var toMark = db.Notifications.First(n => n.UserId == userId && n.Titulo == "A marcar");
        var other = db.Notifications.First(n => n.UserId == userId && n.Titulo == "Sin marcar");

        var resp = await userClient.PatchAsync($"/api/notifications/{toMark.Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(verifyDb.Notifications.First(n => n.Id == toMark.Id).Leida);
        Assert.False(verifyDb.Notifications.First(n => n.Id == other.Id).Leida);
    }

    [Fact]
    public async Task MarkAsRead_OtherUsersNotification_DoesNotAffectIt()
    {
        var (userAClient, userAId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var (_, userBId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await SeedNotificationAsync(userBId, titulo: "De B");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifB = db.Notifications.First(n => n.UserId == userBId && n.Titulo == "De B");

        await userAClient.PatchAsync($"/api/notifications/{notifB.Id}/read", null);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(verifyDb.Notifications.First(n => n.Id == notifB.Id).Leida);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksOnlyCurrentUsersNotifications()
    {
        var (userAClient, userAId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var (_, userBId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await SeedNotificationAsync(userAId, titulo: "A1");
        await SeedNotificationAsync(userAId, titulo: "A2");
        await SeedNotificationAsync(userBId, titulo: "B1");

        var resp = await userAClient.PatchAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.Notifications.Where(n => n.UserId == userAId).All(n => n.Leida));
        Assert.False(db.Notifications.First(n => n.UserId == userBId).Leida);
    }
}
