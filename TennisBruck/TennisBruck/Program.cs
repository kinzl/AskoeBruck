using GrueneisR.RestClientGenerator;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using TennisBruck.Extensions;
using TennisBruck.Services;
using TennisDb;

string corsKey = "_myCorsKey";
string swaggerVersion = "v1";
string swaggerTitle = "MinApiDemo";
string restClientFolder = Environment.CurrentDirectory;
string restClientFilename = "_requests.http";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

#region -------------------------------------------- ConfigureServices

builder.Services.AddControllers();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
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

string? connectionStringMariaDb = builder.Configuration.GetConnectionString("TennisDbSqlite");

string location = System.Reflection.Assembly.GetEntryAssembly()!.Location;
string dataDirectory = Path.GetDirectoryName(location)!;
Console.WriteLine("Path: " + dataDirectory);
string? connectionString =
    connectionStringMariaDb?.Replace("|DataDirectory|", dataDirectory + Path.DirectorySeparatorChar);
Console.WriteLine($"******** ConnectionString: {connectionStringMariaDb}");
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"******** Don't forget to comment out NorthwindContext.OnConfiguring !");
Console.ResetColor();

builder.Services.AddDbContext<TennisContext>(options => options.UseSqlite(connectionString));
// builder.Services.AddDbContext<TennisContext>(options => options
//     .UseMySql(connectionStringMariaDb,
//         ServerVersion.Create(new Version(11, 3), ServerType.MariaDb)));
builder.Services.AddLogging();
builder.Services.AddHostedService<StartupBackgroundService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddSingleton<PasswordEncryption>();
builder.Services.AddSingleton<PlayerService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(10);
    options.Cookie.Name = "TennisBruck.Session";
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
});
builder.Services.AddHttpLogging();

#endregion

var app = builder.Build();

#region -------------------------------------------- Middleware pipeline

app.UseHttpLogging();
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
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

#endregion

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseSession();
app.MapRazorPages();
app.MapControllers();
//Load environment variables from .env file
DotNetEnv.Env.Load();

app.Run();