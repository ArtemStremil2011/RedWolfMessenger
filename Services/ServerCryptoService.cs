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
                _logger.LogWarning("Server crypto keys not configured");
                _isConfigured = false;
                _serverRsa = RSA.Create(2048);
                _serverPublicKeyBase64 = Convert.ToBase64String(_serverRsa.ExportSubjectPublicKeyInfo());
                return;
            }
            
            try
            {
                var cleanPublicKey = publicKey.Trim().Replace("\n", "").Replace("\r", "").Replace(" ", "");
                var cleanPrivateKey = privateKey.Trim().Replace("\n", "").Replace("\r", "").Replace(" ", "");
                
                _serverRsa = RSA.Create();
                
                var publicKeyBytes = Convert.FromBase64String(cleanPublicKey);
                _serverRsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
                
                var privateKeyBytes = Convert.FromBase64String(cleanPrivateKey);
                _serverRsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
                
                _serverPublicKeyBase64 = cleanPublicKey;
                _isConfigured = true;
                
                _logger.LogInformation("Server crypto service initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize server crypto");
                _isConfigured = false;
                _serverRsa = RSA.Create(2048);
                _serverPublicKeyBase64 = Convert.ToBase64String(_serverRsa.ExportSubjectPublicKeyInfo());
            }
        }

        public async Task<string> DecryptMessageAsync(string encryptedDataBase64, string ivBase64)
        {
            if (!_isConfigured)
                throw new InvalidOperationException("Server crypto not configured");
            
            return await Task.Run(() =>
            {
                try
                {
                    var cleanData = encryptedDataBase64.Trim().Replace("\n", "").Replace("\r", "").Replace(" ", "");
                    var combined = Convert.FromBase64String(cleanData);
                    var iv = Convert.FromBase64String(ivBase64);
                    
                    const int rsaEncryptedKeySize = 256;
                    
                    if (combined.Length < rsaEncryptedKeySize)
                        throw new ArgumentException($"Data too short: {combined.Length} bytes");
                    
                    var encryptedAesKey = new byte[rsaEncryptedKeySize];
                    var encryptedMessage = new byte[combined.Length - rsaEncryptedKeySize];
                    
                    Buffer.BlockCopy(combined, 0, encryptedAesKey, 0, rsaEncryptedKeySize);
                    Buffer.BlockCopy(combined, rsaEncryptedKeySize, encryptedMessage, 0, encryptedMessage.Length);
                    
                    var aesKey = _serverRsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);
                    
                    // ===== ИСПРАВЛЕННЫЙ AesGcm ДЛЯ .NET 9.0 =====
                    // Размер тега (Tag) - 16 байт (128 бит) для совместимости
                    var tagSize = 16;
                    using var aesGcm = new AesGcm(aesKey, tagSize);
                    
                    // В GCM расшифровка: (nonce, ciphertext, tag) -> plaintext
                    // Но у нас зашифрованное сообщение без отдельного тега
                    // Используем стандартный подход: тег - последние 16 байт
                    var tag = new byte[tagSize];
                    var ciphertext = new byte[encryptedMessage.Length - tagSize];
                    
                    // Последние 16 байт - это тег
                    Buffer.BlockCopy(encryptedMessage, encryptedMessage.Length - tagSize, tag, 0, tagSize);
                    // Остальное - зашифрованное сообщение
                    Buffer.BlockCopy(encryptedMessage, 0, ciphertext, 0, encryptedMessage.Length - tagSize);
                    
                    var decryptedBytes = new byte[ciphertext.Length];
                    aesGcm.Decrypt(iv, ciphertext, tag, decryptedBytes);
                    
                    var plainText = Encoding.UTF8.GetString(decryptedBytes);
                    
                    _logger.LogInformation($"Server decrypted: {plainText.Length} chars");
                    return plainText;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt message: {Message}", ex.Message);
                    throw;
                }
            });
        }

        public async Task<(string encryptedData, string iv)> EncryptForUserAsync(string plainText, string userPublicKeyBase64)
        {
            return await Task.FromResult(("", ""));
        }

        public string GetServerPublicKey() => _serverPublicKeyBase64;
        public bool IsConfigured() => _isConfigured;
    }
}