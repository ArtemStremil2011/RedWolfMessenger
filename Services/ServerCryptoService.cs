using System.Security.Cryptography;
using System.Text;
using Messenger.Services.Interfaces;

namespace Messenger.Services.Crypto
{
    public class ServerCryptoService : IServerCryptoService
    {
        private readonly RSA _serverRsa;
        private readonly string _serverPublicKeyBase64;
        private readonly ILogger<ServerCryptoService> _logger;
        private readonly bool _isConfigured;

        public ServerCryptoService(IConfiguration configuration, ILogger<ServerCryptoService> logger)
        {
            _logger = logger;
            
            var publicKey = configuration["ServerCrypto:PublicKey"];
            var privateKey = configuration["ServerCrypto:PrivateKey"];
            
            if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
            {
                _logger.LogWarning("Server crypto keys not configured. Server decryption will not work!");
                _isConfigured = false;
                _serverRsa = RSA.Create(2048);
                _serverPublicKeyBase64 = Convert.ToBase64String(_serverRsa.ExportSubjectPublicKeyInfo());
                return;
            }
            
            try
            {
                _serverRsa = RSA.Create();
                
                // Импортируем приватный ключ для расшифровки
                var privateKeyBytes = Convert.FromBase64String(privateKey);
                _serverRsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
                
                // Сохраняем публичный ключ для отправки клиентам
                _serverPublicKeyBase64 = publicKey;
                _isConfigured = true;
                
                _logger.LogInformation("Server crypto service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize server crypto service");
                _isConfigured = false;
                _serverRsa = RSA.Create(2048);
                _serverPublicKeyBase64 = Convert.ToBase64String(_serverRsa.ExportSubjectPublicKeyInfo());
            }
        }

        /// <summary>
        /// Расшифровывает сообщение, которое клиент зашифровал публичным ключом сервера
        /// Формат encryptedData: [RSA-зашифрованный AES-ключ] + [AES-зашифрованное сообщение]
        /// </summary>
        public async Task<string> DecryptMessageAsync(string encryptedDataBase64, string ivBase64)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException("Server crypto not configured");
            }
            
            return await Task.Run(() =>
            {
                try
                {
                    // 1. Декодируем входящие данные
                    var combinedData = Convert.FromBase64String(encryptedDataBase64);
                    var iv = Convert.FromBase64String(ivBase64);
                    
                    // 2. Извлекаем зашифрованный AES-ключ (первые 256 байт для RSA-2048)
                    const int rsaKeySizeInBytes = 256; // RSA 2048 бит = 256 байт
                    if (combinedData.Length < rsaKeySizeInBytes)
                    {
                        throw new ArgumentException($"Data too short: {combinedData.Length} bytes. Expected at least {rsaKeySizeInBytes} bytes.");
                    }
                    
                    var encryptedAesKey = new byte[rsaKeySizeInBytes];
                    var encryptedMessage = new byte[combinedData.Length - rsaKeySizeInBytes];
                    
                    Buffer.BlockCopy(combinedData, 0, encryptedAesKey, 0, rsaKeySizeInBytes);
                    Buffer.BlockCopy(combinedData, rsaKeySizeInBytes, encryptedMessage, 0, encryptedMessage.Length);
                    
                    // 3. Расшифровываем AES-ключ приватным ключом сервера
                    var aesKey = _serverRsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);
                    
                    // 4. Расшифровываем само сообщение (используем CBC режим вместо GCM)
                    using var aes = Aes.Create();
                    aes.Key = aesKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    using var decryptor = aes.CreateDecryptor();
                    var decryptedBytes = decryptor.TransformFinalBlock(encryptedMessage, 0, encryptedMessage.Length);
                    var plainText = Encoding.UTF8.GetString(decryptedBytes);
                    
                    _logger.LogInformation($"Message decrypted by server. Length: {plainText.Length} chars");
                    return plainText;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt message on server");
                    throw new CryptographicException("Failed to decrypt message", ex);
                }
            });
        }

        /// <summary>
        /// Зашифровывает сообщение для пользователя его публичным ключом
        /// Формат результата: [RSA-зашифрованный AES-ключ] + [AES-зашифрованное сообщение]
        /// </summary>
        public async Task<(string encryptedData, string iv)> EncryptForUserAsync(string plainText, string userPublicKeyBase64)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 1. Импортируем публичный ключ пользователя
                    var userPublicKeyBytes = Convert.FromBase64String(userPublicKeyBase64);
                    using var userRsa = RSA.Create();
                    userRsa.ImportSubjectPublicKeyInfo(userPublicKeyBytes, out _);
                    
                    // 2. Генерируем случайный AES-256 ключ и IV
                    using var aes = Aes.Create();
                    aes.KeySize = 256;
                    aes.GenerateKey();
                    aes.GenerateIV();
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    // 3. Шифруем AES-ключ публичным ключом пользователя
                    var encryptedAesKey = userRsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);
                    
                    // 4. Шифруем сообщение AES-ключом
                    var plainBytes = Encoding.UTF8.GetBytes(plainText);
                    using var encryptor = aes.CreateEncryptor();
                    var encryptedMessage = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    
                    // 5. Объединяем зашифрованный ключ и сообщение
                    var combined = new byte[encryptedAesKey.Length + encryptedMessage.Length];
                    Buffer.BlockCopy(encryptedAesKey, 0, combined, 0, encryptedAesKey.Length);
                    Buffer.BlockCopy(encryptedMessage, 0, combined, encryptedAesKey.Length, encryptedMessage.Length);
                    
                    return (
                        encryptedData: Convert.ToBase64String(combined),
                        iv: Convert.ToBase64String(aes.IV)
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to encrypt message for user");
                    throw;
                }
            });
        }

        public string GetServerPublicKey()
        {
            return _serverPublicKeyBase64;
        }

        public bool IsConfigured()
        {
            return _isConfigured;
        }
    }
}