namespace AcxiomCRM.Models;

// Any CRM record that belongs to a user (7.1 "Own/Assigned").
// Lets us filter Customers, Leads, Opportunities, FollowUps and Activities
// with ONE shared rule instead of repeating it in every module.
public interface IOwnedEntity
{
    string? AssignedTo { get; set; }
}