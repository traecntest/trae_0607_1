using UTM.SharedKernel.Infrastructure;
using UTM.TelemetryService;
using UTM.TelemetryService.Gateway;
using UTM.TelemetryService.Pipeline;
using UTM.TelemetryService.Services;
using UTM.TelemetryService.Simulation;

var builder = Host.CreateApplicationBuilder(args);

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
builder.Services.AddSingleton<ITelemetryDataSink>(sp =>
    sp.GetRequiredService<ITelemetryPipeline>());

builder.Services.AddHostedService<TelemetryWorker>();

var host = builder.Build();
host.Run();
