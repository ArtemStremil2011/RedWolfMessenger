using Microsoft.AspNetCore.SignalR;
using Messenger.DTOs;
using System.Collections.Concurrent;

namespace Messenger.Hubs
{
    public class MessengerHub : Hub
    {
        private static readonly ConcurrentDictionary<Guid, string> _onlineUsers = new();
        private static readonly ConcurrentDictionary<Guid, DateTime> _lastSeen = new();

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                _onlineUsers[userId.Value] = Context.ConnectionId;
                _lastSeen[userId.Value] = DateTime.UtcNow;
                await Clients.All.SendAsync("UserOnline", userId.Value, true);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                _onlineUsers.TryRemove(userId.Value, out _);
                _lastSeen[userId.Value] = DateTime.UtcNow;
                await Clients.All.SendAsync("UserOnline", userId.Value, false);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinChat(string chatId, string userId, string userName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
            await Clients.Group(chatId).SendAsync("UserJoined", userId, userName);
        }

        public async Task SendMessage(string chatId, string userId, string userName, string messageText)
        {
            await Clients.Group(chatId).SendAsync("ReceiveMessage", userId, userName, messageText);
        }

        public async Task UserIsTyping(string chatId, string userId, string userName)
        {
            await Clients.Group(chatId).SendAsync("UserTyping", userId, userName);
            _ = Task.Delay(3000).ContinueWith(async _ =>
            {
                await Clients.Group(chatId).SendAsync("UserStoppedTyping", userId);
            });
        }

        public async Task UserStoppedTyping(string chatId, string userId)
        {
            await Clients.Group(chatId).SendAsync("UserStoppedTyping", userId);
        }

        // НОВЫЙ МЕТОД - УВЕДОМЛЕНИЕ О НОВОМ ЧАТЕ
        public async Task NotifyNewChat(string userId, object chatInfo)
        {
            await Clients.User(userId).SendAsync("NewChatCreated", chatInfo);
        }

        public async Task<UserStatusDTO?> GetUserStatus(Guid userId)
        {
            var isOnline = _onlineUsers.ContainsKey(userId);
            var lastSeen = _lastSeen.GetValueOrDefault(userId);
            return new UserStatusDTO
            {
                UserId = userId,
                IsOnline = isOnline,
                LastSeen = isOnline ? null : lastSeen
            };
        }

        private Guid? GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst("sub")?.Value
                ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
                return userId;
            return null;
        }
    }
}