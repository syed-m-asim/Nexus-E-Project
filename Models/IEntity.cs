namespace NexusCommunication.Models;

/// <summary>Contract so one generic CRUD controller can serve the simple master tables.</summary>
public interface IEntity { int Id { get; set; } }
