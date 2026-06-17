using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Messenger.Data;
using Messenger.Hubs;
using Messenger.Services;
using Messenger.Services.Interfaces;
using Messenger.Services.Crypto;
using System.Security.Cryptography;




var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// PostgreSQL подключение
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddSignalR();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUserReadService, UserReadService>();
builder.Services.AddScoped<IUserWriteService, UserWriteService>();
builder.Services.AddScoped<IChatReadService, ChatReadService>();
builder.Services.AddScoped<IChatWriteService, ChatWriteService>();
builder.Services.AddScoped<IMessageReadService, MessageReadService>();
builder.Services.AddScoped<IMessageWriteService, MessageWriteService>();
builder.Services.AddScoped<IFileReadService, FileReadService>();
builder.Services.AddScoped<IFileWriteService, FileWriteService>();
builder.Services.AddScoped<IServerCryptoService, ServerCryptoService>();

// ============ АВТОМАТИЧЕСКАЯ ГЕНЕРАЦИЯ КЛЮЧЕЙ ПРИ ПЕРВОМ ЗАПУСКЕ ============
var serverPublicKey = builder.Configuration["ServerCrypto:PublicKey"];
var serverPrivateKey = builder.Configuration["ServerCrypto:PrivateKey"];

if (string.IsNullOrEmpty(serverPublicKey) || string.IsNullOrEmpty(serverPrivateKey) || 
    serverPublicKey.Length < 100 || serverPrivateKey.Length < 100)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n⚠️  SERVER CRYPTO KEYS NOT FOUND OR INVALID! ⚠️");
    Console.ResetColor();
    
    // Генерируем новые ключи
    using var rsa = RSA.Create(2048);
    var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
    var privateKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n🔑 Generated new server keys:\n");
    Console.WriteLine($"PublicKey: {publicKey}");
    Console.WriteLine($"\nPrivateKey: {privateKey}");
    Console.ResetColor();
    
    // Пытаемся сохранить ключи в appsettings.json
    var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    if (File.Exists(configPath))
    {
        try
        {
            var jsonContent = File.ReadAllText(configPath);
            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonContent)!;
            
            if (json.ServerCrypto == null)
                json.ServerCrypto = new Newtonsoft.Json.Linq.JObject();
            
            json.ServerCrypto.PublicKey = publicKey;
            json.ServerCrypto.PrivateKey = privateKey;
            
            var updatedJson = Newtonsoft.Json.JsonConvert.SerializeObject(json, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(configPath, updatedJson);
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✅ Keys saved to appsettings.json");
            Console.ResetColor();
            
            // Обновляем конфигурацию в памяти
            builder.Configuration["ServerCrypto:PublicKey"] = publicKey;
            builder.Configuration["ServerCrypto:PrivateKey"] = privateKey;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Failed to save keys to appsettings.json: {ex.Message}");
            Console.WriteLine("\n⚠️ Please manually add these keys to appsettings.json:");
            Console.WriteLine($"\"PublicKey\": \"{publicKey}\",");
            Console.WriteLine($"\"PrivateKey\": \"{privateKey}\"");
            Console.ResetColor();
            
            // Всё равно используем сгенерированные ключи для текущей сессии
            builder.Configuration["ServerCrypto:PublicKey"] = publicKey;
            builder.Configuration["ServerCrypto:PrivateKey"] = privateKey;
        }
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n❌ appsettings.json not found at: {configPath}");
        Console.WriteLine("\n⚠️ Please manually add these keys to appsettings.json:");
        Console.WriteLine($"\"PublicKey\": \"{publicKey}\",");
        Console.WriteLine($"\"PrivateKey\": \"{privateKey}\"");
        Console.ResetColor();
        
        // Используем сгенерированные ключи для текущей сессии
        builder.Configuration["ServerCrypto:PublicKey"] = publicKey;
        builder.Configuration["ServerCrypto:PrivateKey"] = privateKey;
    }
    
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("\n🚀 Server will continue with generated keys...\n");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n✅ Server crypto keys loaded from configuration\n");
    Console.ResetColor();
}

// ============ НАСТРОЙКА JWT ============
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret key is not configured.");
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/messengerHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDBContext>();
    dbContext.Database.EnsureCreated();
}

app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MessengerHub>("/messengerHub");
app.MapFallbackToFile("index.html");

app.Run();