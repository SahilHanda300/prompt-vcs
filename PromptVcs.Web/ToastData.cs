namespace PromptVcs.Web;

/// Carries a page's server-rendered error/success message into the shared
/// _Toast partial, which hands it off to _Layout's toast script instead of
/// printing a plain static div. Either or both may be null.
public record ToastData(string? Error, string? Success);
