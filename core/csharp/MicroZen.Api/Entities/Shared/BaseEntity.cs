namespace MicroZen.Api.Entities.Shared;

/// <summary>
/// The base class for reusable properties for entities.
/// </summary>
/// <typeparam name="TMessage">The Type of Message to map an entity to for gRPC</typeparam>
public abstract class BaseEntity<TMessage>
{
  /// <summary>
  /// The DateTime the entity was created. (Default: UtcNow)
  /// </summary>
  public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// The Guid of the user who created the entity.
  /// </summary>
  public Guid? CreatedBy { get; set; }

  /// <summary>
  /// The DateTime the entity was last modified.
  /// </summary>
  public DateTime? ModifiedOn { get; set; }

  /// <summary>
  /// The Guid of the user who last modified the entity.
  /// </summary>
  public Guid? ModifiedBy { get; set; }

  /// <summary>
  /// The DateTime the entity was deleted (soft delete).
  /// </summary>
  public DateTime? DeletedOn { get; set; }

  /// <summary>
  /// The Guid of the user who deleted the entity.
  /// </summary>
  public Guid? DeletedBy { get; set; }

  /// <summary>
  /// Method to convert an entity to a gRPC message.
  /// </summary>
  /// <returns>TMessage - Generic gRPC Message type</returns>
  public abstract TMessage ToMessage();
}
