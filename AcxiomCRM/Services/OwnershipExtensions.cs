namespace AcxiomCRM.Services;

// Server-side record ownership rules (7.1: Admin = Full, Manager = Team, SalesExecutive = Own/Assigned).
public static class OwnershipExtensions
{
    // Use on every list/search query:
    //   _db.Customers.VisibleTo(_currentUser)
    public static IQueryable<T> VisibleTo<T>(this IQueryable<T> query, ICurrentUserService user)
        where T : class, IOwnedEntity
    {
        if (user.CanSeeAllRecords)
            return query;

        var userId = user.UserId;

        // Safety: no user id -> show nothing (never fall back to "unassigned" rows)
        if (string.IsNullOrEmpty(userId))
            return query.Where(e => false);

        return query.Where(e => e.AssignedTo == userId);
    }

    // Use on Details / Edit / Delete after loading one record by id:
    //   if (!_currentUser.CanAccess(customer)) return Forbid();
    public static bool CanAccess(this ICurrentUserService user, IOwnedEntity entity)
    {
        if (user.CanSeeAllRecords)
            return true;

        return !string.IsNullOrEmpty(user.UserId) && entity.AssignedTo == user.UserId;
    }
}