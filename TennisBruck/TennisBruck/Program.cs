using GrueneisR.RestClientGenerator;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TennisBruck.Controller;
using TennisBruck.Extensions;
using TennisBruck.Services;
using TennisDb;

string corsKey = "_myCorsKey";
string swaggerVersion = "v1";
string swaggerTitle = "MinApiDemo";
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


string connectionString = builder.Configuration.GetConnectionString("PostgresSql")!;
connectionString = connectionString.Replace("myDatabase", Environment.GetEnvironmentVariable("POSTGRES_DATABASE"))
    .Replace("myUsername", Environment.GetEnvironmentVariable("POSTGRES_USER"))
    .Replace("myPassword", Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"));
Console.WriteLine($"Connection string: {connectionString}");
// connectionString = "Host=localhost;Port=5432;Database=mydatabase;Username=myuser;Password=mypassword";
builder.Services.AddDbContext<TennisContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddLogging();
builder.Services.AddHostedService<StartupBackgroundService>();
builder.Services.AddScoped<EmailService>();
// builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<CurrentPlayerService>();
builder.Services.AddScoped<PlanService>();
builder.Services.AddSingleton<PasswordEncryption>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.Name = "TennisBruck.Session";
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
});
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

#endregion

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();
app.MapRazorPages();
app.MapControllers();

app.Run();