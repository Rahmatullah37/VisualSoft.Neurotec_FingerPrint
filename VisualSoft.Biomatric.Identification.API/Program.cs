//using VisualSoft.Biomatric.Identification.Services;
//using Microsoft.Extensions.Configuration;

//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.

//builder.Services.AddControllers();
//// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();

//app.UseAuthorization();

//app.MapControllers();

//app.Run();



using VisualSoft.Biomatric.Identification.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ===== CONFIGURE SERILOG =====
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "BiometricIdentificationAPI")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/biometric-api-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("=== Starting Biometric Identification API ===");

try
{
    // ===== ADD SERVICES =====
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Configure Swagger
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Biometric Identification API",
            Version = "v1",
            Description = "API for fingerprint identification using Neurotec Biometric SDK",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "VisualSoft"
            }
        });

        // Enable XML comments if you have them
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });

    // ===== REGISTER BIOMETRIC SERVICE AS SINGLETON =====
    Log.Information("Registering BiometricService as Singleton...");
    builder.Services.AddSingleton<IBiometricService>(serviceProvider =>
    {
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var logger = serviceProvider.GetRequiredService<ILogger<BiometricClusterConnector>>();

        Log.Information("Initializing BiometricClusterConnector...");
        var connector = BiometricClusterConnector.ConnectAsync(config, logger).GetAwaiter().GetResult();
        Log.Information("BiometricClusterConnector initialized successfully");

        return connector;
    });

    // ===== CONFIGURE CORS =====
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // ===== BUILD APP =====
    var app = builder.Build();

    // ===== CONFIGURE MIDDLEWARE =====
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Biometric API v1");
            c.RoutePrefix = "swagger"; // Swagger is now at /swagger
        });
        Log.Information("Swagger UI enabled at root URL");
    }

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    app.UseHttpsRedirection();
    app.UseCors("AllowAll");
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("API is ready to accept requests");
    Log.Information("Swagger UI: https://localhost:{Port}", builder.Configuration["ASPNETCORE_HTTPS_PORT"] ?? "7000");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("=== Shutting down Biometric Identification API ===");
    Log.CloseAndFlush();
}