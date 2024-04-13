namespace MicroZen.Data.Entities;

public abstract class BaseEntity<TMessage>
{
  public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
  public Guid? CreatedBy { get; set; }
  public DateTime? ModifiedOn { get; set; }
  public Guid? ModifiedBy { get; set; }
  public DateTime? DeletedOn { get; set; }
  public Guid? DeletedBy { get; set; }

  public abstract TMessage ToMessage();
}
