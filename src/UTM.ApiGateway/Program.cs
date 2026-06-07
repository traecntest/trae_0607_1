using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using UTM.ApiGateway.Services;
using UTM.ConflictDetectionService.Avoidance;
using UTM.ConflictDetectionService.Detection;
using UTM.SharedKernel.Infrastructure;
using UTM.TelemetryService.Gateway;
using UTM.TelemetryService.Pipeline;
using UTM.TelemetryService.Services;
using UTM.TelemetryService.Simulation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UTM System API",
        Version = "v1",
        Description = "低空经济与无人机交通管理系统 - API网关"
    });
});

builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<DroneSimulator>();
builder.Services.AddSingleton<ITelemetryPipeline, ChannelTelemetryPipeline>(sp =>
{
    var eventBus = sp.GetRequiredService<IEventBus>();
    int capacity = builder.Configuration.GetValue("Pipeline:Capacity", 10000);
    return new ChannelTelemetryPipeline(eventBus, capacity);
});
builder.Services.AddSingleton<IDroneStateStore, InMemoryDroneStateStore>();
builder.Services.AddSingleton<ITrajectoryService, TrajectoryService>();
builder.Services.AddSingleton<TelemetryGateway>();
builder.Services.AddSingleton<ITelemetryDataSink>(sp => sp.GetRequiredService<ITelemetryPipeline>());

builder.Services.AddSingleton<IConflictDetector, TimeWindowConflictDetector>();
builder.Services.AddSingleton<IAvoidancePlanner, PriorityAvoidancePlanner>();

builder.Services.AddHostedService<SimulationHostedService>();
builder.Services.AddHostedService<TelemetryProcessingHostedService>();
builder.Services.AddHostedService<ConflictDetectionHostedService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UTM System API v1");
        c.DisplayRequestDuration();
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
