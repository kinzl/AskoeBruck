using GrueneisR.RestClientGenerator;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.OpenApi.Models;
using Resend;
using TennisContext = TennisDb.TennisContext;
using TennisBruck.Pages.Filter;
using Quartz;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;


string swaggerVersion = "v1";
string swaggerTitle = "TennisBruck";
string restClientFolder = Environment.CurrentDirectory;
string restClientFilename = "_requests.http";

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages()
    .AddMvcOptions(options => { options.Filters.Add<ZombieUserFilter>(); });
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
builder.Configuration.AddEnvironmentVariables();

//Resend service (Email)
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>(); // Registriert den ResendClient als typisierten HttpClient
builder.Services.Configure<ResendClientOptions>(options =>
{
    // API Key ausgeben und bei Fehlen direkt einen sauberen Fehler werfen
    var apiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY");
    options.ApiToken = apiKey ?? throw new InvalidOperationException("RESEND_API_KEY fehlt in der .env Datei!");
});

// 3. IResend Interface mappen (Best Practice für den offiziellen Client)
builder.Services.AddTransient<IResend, ResendClient>();

// 4. Deinen eigenen EmailSender registrieren
builder.Services.AddTransient<IEmailSender, EmailSender>();


// ConnectToPostgresDb();
// ConnectToSqliteDb();
if (builder.Environment.IsProduction())
{
    ConnectToNeonDb();
}
else
{
    ConnectToPostgresDb();
    // builder.Services.AddHostedService<StartupBackgroundService>();
}

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<TennisContext>()
    .SetApplicationName("TennisBruck");

builder.Services.AddDefaultIdentity<IdentityUser>(options => { options.SignIn.RequireConfirmedAccount = true; })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<TennisContext>();

builder.Services.Configure<SecurityStampValidatorOptions>(options => { options.ValidationInterval = TimeSpan.Zero; });

builder.Services.AddLogging();
builder.Services.AddHostedService<StartupBackgroundService>();
builder.Services.AddHttpClient<EmailService>();
// builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<CurrentPlayerService>();
builder.Services.AddScoped<ChampionshipInfoService>();
builder.Services.AddHttpClient<OetvScraperService>();

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("ItnSyncJob");
    q.AddJob<ItnSyncJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("ItnSyncJob-trigger")
        .WithCronSchedule("0 0 3 * * ?"));

    var cleanupKey = new JobKey("CleanupReservationsJob");
    q.AddJob<CleanupReservationsJob>(opts => opts.WithIdentity(cleanupKey));
    q.AddTrigger(opts => opts
        .ForJob(cleanupKey)
        .WithIdentity("CleanupReservationsJob-trigger")
        .WithCronSchedule("0 0 1 * * ?"));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 40,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

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

// Den Google-Login registrieren
builder.Services.AddAuthentication()
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = Environment.GetEnvironmentVariable("AUTHENTICATION_GOOGLE_CLIENTID")!;
        googleOptions.ClientSecret = Environment.GetEnvironmentVariable("AUTHENTICATION_GOOGLE_CLIENTSECRET")!;
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

#endregion

app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

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