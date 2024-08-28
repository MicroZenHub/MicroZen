namespace MicroZen.Api.Security.Policies;

/// <summary>
/// The potential Security Permissions to be Applied
/// </summary>
public enum Permission
{
	/// <summary>
	/// Can Manage data
	/// </summary>
	Manage,
	/// <summary>
	/// Can Read data
	/// </summary>
	Read,
	/// <summary>
	/// Can Update data
	/// </summary>
	Update,
	/// <summary>
	/// Can Create data
	/// </summary>
	Create,
	/// <summary>
	/// Can Delete data
	/// </summary>
	Delete
}
