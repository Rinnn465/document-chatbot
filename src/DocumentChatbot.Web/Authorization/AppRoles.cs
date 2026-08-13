namespace DocumentChatbot.Web.Authorization;

public static class AppRoles
{
    public const string SubjectLeader = "SubjectLeader";
    public const string Student = "Student";
}

public static class AppPolicies
{
    public const string SubjectLeaderOnly = "SubjectLeaderOnly";
    public const string StudentOnly = "StudentOnly";
}
