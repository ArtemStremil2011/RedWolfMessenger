using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Messenger.Hubs
{
    public class MessengerHub : Hub
    {
        // Присоединение пользователя к чату (группе)
        public async Task JoinChat(string chatId, string userId, string userName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
            await Clients.Group(chatId).SendAsync("UserJoined", userId, userName);
        }

        // Выход пользователя из чата
        public async Task LeaveChat(string chatId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
        }

        // Отправка сообщения в чат
        public async Task SendMessage(string chatId, string userId, string userName, string messageText)
        {
            await Clients.Group(chatId).SendAsync("ReceiveMessage", userId, userName, messageText);
        }

        // Уведомление о том, что пользователь печатает (опционально)
        public async Task UserIsTyping(string chatId, string userId, string userName)
        {
            await Clients.Group(chatId).SendAsync("UserTyping", userId, userName);
        }

        // Уведомление о том, что пользователь перестал печатать
        public async Task UserStoppedTyping(string chatId, string userId)
        {
            await Clients.Group(chatId).SendAsync("UserStoppedTyping", userId);
        }
    }
}