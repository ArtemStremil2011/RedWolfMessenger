using System.Security.Cryptography;

namespace Messenger.Services.Interfaces
{
    public interface IServerCryptoService
    {
        /// <summary>
        /// Расшифровать сообщение, зашифрованное публичным ключом сервера
        /// </summary>
        Task<string> DecryptMessageAsync(string encryptedDataBase64, string ivBase64);
        
        /// <summary>
        /// Зашифровать сообщение для конкретного пользователя его публичным ключом
        /// </summary>
        Task<(string encryptedData, string iv)> EncryptForUserAsync(string plainText, string userPublicKeyBase64);
        
        /// <summary>
        /// Получить публичный ключ сервера (base64)
        /// </summary>
        string GetServerPublicKey();
        
        /// <summary>
        /// Проверить, сконфигурированы ли ключи сервера
        /// </summary>
        bool IsConfigured();
    }
}