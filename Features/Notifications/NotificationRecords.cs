using MediatR;
using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Features.Notifications;

public record MachineCreatedNotification(Machine Machine) : INotification;
public record MachineUpdatedNotification(Machine Machine) : INotification;
public record MachineDeletedNotification(string MachineId) : INotification;
public record CommandCreatedNotification(Command Command) : INotification;
public record CommandUpdatedNotification(Command Command) : INotification;
