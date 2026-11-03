namespace AcxiomCRM.Models.Enums;

public enum CustomerStatus { Active, Inactive }
public enum LeadStatus { New, Contacted, Qualified, Unqualified, Converted, Lost }
public enum LeadSource { Website, Referral, Email, Phone, SocialMedia, Event, Other }
public enum Priority { Low, Medium, High }
public enum OpportunityStage { Qualification, Proposal, Negotiation, Won, Lost }
public enum OpportunityStatus { Open, Closed }
public enum FollowUpType { Call, Meeting, Email, Visit }
public enum FollowUpStatus { Planned, Completed, Missed, Cancelled }
public enum ActivityType { Call, Meeting, Email, Task }
public enum ActivityStatus { Planned, Completed, Cancelled }