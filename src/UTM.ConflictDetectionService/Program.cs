using UTM.ConflictDetectionService;
using UTM.ConflictDetectionService.Avoidance;
using UTM.ConflictDetectionService.Detection;
using UTM.SharedKernel.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<IConflictDetector, TimeWindowConflictDetector>();
builder.Services.AddSingleton<IAvoidancePlanner, PriorityAvoidancePlanner>();

builder.Services.AddHostedService<ConflictDetectionWorker>();

var host = builder.Build();
host.Run();
