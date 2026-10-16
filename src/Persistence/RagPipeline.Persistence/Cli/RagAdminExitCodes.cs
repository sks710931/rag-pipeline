namespace RagPipeline.Persistence.Cli;

public static class RagAdminExitCodes
{
    public const int Success = 0;
    public const int InvalidArguments = 1;
    public const int StateConflict = 2;
    public const int DatabaseError = 3;
    public const int SecurityViolation = 4;
}
