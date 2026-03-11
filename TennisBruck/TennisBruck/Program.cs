using GrueneisR.RestClientGenerator;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Resend;
using TennisBruck.Extensions;
using TennisBruck.Services;
using TennisDb;
using TennisBruck.Areas.Identity.Data;
using TennisBruck;
using TennisContext = TennisDb.TennisContext;

string corsKey = "_myCorsKey";
string swaggerVersion = "v1";
string swaggerTitle = "TennisBruck";
string restClientFolder = Environment.CurrentDirectory;
string restClientFilename = "_requests.http";

//Load environment variables from .env file
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

Console.WriteLine($"Current Environment: {builder.Environment.EnvironmentName}");

#region -------------------------------------------- ConfigureServices

builder.Services.AddControllers();
builder.Services
    .AddEndpointsApiExplorer()
    .AddAuthorization()
    .AddSwaggerGen(x => x.SwaggerDoc(
        swaggerVersion,
        new OpenApiInfo { Title = swaggerTitle, Version = swaggerVersion }
    ))
    .AddCors(options => options.AddPolicy(
        corsKey,
        x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
    ))
    .AddRestClientGenerator(options => options
            .SetFolder(restClientFolder)
            .SetFilename(restClientFilename)
            .SetAction($"swagger/{swaggerVersion}/swagger.json")
        //.EnableLogging()
    );

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true; // Email-Bestätigung
        options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider; // 2FA
    })
    .AddEntityFrameworkStores<TennisContext>();

// Resend Client via Options
builder.Services.Configure<ResendClientOptions>(
    builder.Configuration.GetSection("Resend")
);
builder.Services.AddScoped<ResendClient>();

// Email Sender
builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/tmp/dpkeys"));

// ConnectToPostgresDb();
// ConnectToSqliteDb();
ConnectToNeonDb();

builder.Services.AddLogging();
builder.Services.AddHostedService<StartupBackgroundService>();
builder.Services.AddHttpClient<EmailService>();
// builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<CurrentPlayerService>();
builder.Services.AddSingleton<PasswordEncryption>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.Name = "TennisBruck.Session";
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
});

builder.Services.AddDataProtection()
    .SetDefaultKeyLifetime(TimeSpan.FromDays(365));

// builder.Services.AddHttpLogging();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login"; // Path to the login page
        options.ExpireTimeSpan = TimeSpan.FromHours(2); // Expire authentication after 20 minutes
        options.SlidingExpiration = true; // Renew expiration if user is active
    });

#endregion

var app = builder.Build();

#region -------------------------------------------- Middleware pipeline

app.UseHttpsRedirection();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    Console.WriteLine("++++ Swagger enabled: http://localhost:5000 (to set as default route: see launchsettings.json)");
    app.UseSwagger();
    Console.WriteLine($@"++++ RestClient generating (after first request) to {restClientFolder}\{restClientFilename}");
    app.UseRestClientGenerator();
    app.UseSwaggerUI(x => x.SwaggerEndpoint($"/swagger/{swaggerVersion}/swagger.json", swaggerTitle));
}

app.UseCors(corsKey);

#endregion

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();
app.MapRazorPages();
app.MapControllers();

app.Run();
return;

void ConnectToPostgresDb()
{
    string connectionString = builder.Configuration.GetConnectionString("PostgresSql")!;
    connectionString = connectionString.Replace("myDatabase", Environment.GetEnvironmentVariable("POSTGRES_DATABASE"))
        .Replace("myUsername", Environment.GetEnvironmentVariable("POSTGRES_USER"))
        .Replace("myPassword", Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"));
    Console.WriteLine($"Connection string: {connectionString}");
// connectionString = "Host=localhost;Port=5432;Database=mydatabase;Username=myuser;Password=mypassword";
    builder.Services.AddDbContext<TennisContext>(options =>
        options.UseNpgsql(connectionString));
}

void ConnectToNeonDb()
{
    string connectionString = builder.Configuration.GetConnectionString("NeonDb")!;
    connectionString = connectionString.Replace("myDatabase", Environment.GetEnvironmentVariable("POSTGRES_DATABASE"))
        .Replace("neonDbUsername", Environment.GetEnvironmentVariable("NEONDB_USERNAME"))
        .Replace("neonDbPassword", Environment.GetEnvironmentVariable("NEONDB_PASSWORD"))
        .Replace("neonDbHost", Environment.GetEnvironmentVariable("NEONDB_HOST"));
    Console.WriteLine($"Connection string: {connectionString}");
// connectionString = "Host=localhost;Port=5432;Database=mydatabase;Username=myuser;Password=mypassword";
    builder.Services.AddDbContext<TennisContext>(options =>
        options.UseNpgsql(connectionString));
}

void ConnectToSqliteDb()
{
    var connectionString = builder.Configuration.GetConnectionString("TennisDbSqlite")!;
    var location = System.Reflection.Assembly.GetEntryAssembly()!.Location;
    var dataDirectory = Path.GetDirectoryName(location)!;
    connectionString = connectionString.Replace("|DataDirectory|", dataDirectory + Path.DirectorySeparatorChar);
    Console.WriteLine($"******** ConnectionString: {connectionString}");
    Console.ResetColor();
    builder.Services.AddDbContext<TennisContext>(options => options.UseSqlite(connectionString));
}