using MediatR;
using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Features.Notifications;

public record ScheduledTaskCreatedNotification(ScheduledTask Task) : INotification;
public record ScheduledTaskUpdatedNotification(ScheduledTask Task) : INotification;
public record ScheduledTaskDeletedNotification(string TaskId) : INotification;
public record ScheduledExecutionUpdatedNotification(ScheduledTaskExecution Execution) : INotification;
