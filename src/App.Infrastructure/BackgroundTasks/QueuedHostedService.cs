using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using App.Application.Common.Interfaces;
using App.Domain.Entities;

namespace App.Infrastructure.BackgroundTasks;

public class QueuedHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public QueuedHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await BackgroundProcessing(stoppingToken);
    }

    private async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Create a new scope for each task to ensure fresh DbContext and proper disposal
            using var scope = _serviceProvider.CreateScope();
            var taskQueue = scope.ServiceProvider.GetRequiredService<IBackgroundTaskQueue>();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            var backgroundTask = await taskQueue.DequeueAsync(stoppingToken);
            if (backgroundTask == null)
                continue;

            try
            {
                var taskType = Type.GetType(backgroundTask.Name);
                if (taskType == null)
                {
                    backgroundTask.Status = BackgroundTaskStatus.Error;
                    backgroundTask.ErrorMessage = $"Task type not found: {backgroundTask.Name}";
                }
                else
                {
                    var scopedProcessingService =
                        scope.ServiceProvider.GetRequiredService(taskType) as IExecuteBackgroundTask;

                    await scopedProcessingService.Execute(
                        backgroundTask.Id,
                        JsonSerializer.Deserialize<JsonElement>(backgroundTask.Args),
                        stoppingToken
                    );
                    backgroundTask.Status = BackgroundTaskStatus.Complete;
                }
            }
            catch (Exception ex)
            {
                backgroundTask.Status = BackgroundTaskStatus.Error;
                backgroundTask.ErrorMessage = ex.Message;
            }

            backgroundTask.PercentComplete = 100;
            backgroundTask.CompletionTime = DateTime.UtcNow;

            // Attach the entity (from Dapper) to EF Core for tracking and update
            db.BackgroundTasks.Update(backgroundTask);
            await db.SaveChangesAsync(stoppingToken);
        }
    }
}
