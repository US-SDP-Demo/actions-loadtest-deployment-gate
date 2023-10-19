internal class TestRun {
    public string testId { get; set; }
    public string testRunId { get; set; }
    public string testResult { get; set; }
}

internal static class PFTestResult {
    public static readonly string FAILED = "FAILED";
    public static readonly string NOT_APPLICABLE = "NOT_APPLICABLE";
    public static readonly string PASSED = "PASSED";
}