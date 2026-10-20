using DiffPlex.DiffBuilder.Model;

namespace PromptVcs.Core;

/// <summary>
/// Prompt CRUD plus ownership enforcement. All methods take the in-memory
/// Store directly (the caller — ServerStore's ReadAsync/MutateAsync — owns
/// loading/persisting it), so this stays a pure business-logic layer with
/// no storage/transport knowledge of its own.
/// </summary>
public interface IPromptService
{
    Task<PipelineResult> CreateAsync(Store store, string name, string content, string owner, List<Attachment>? attachments = null);
    Task<PipelineResult> EditAsync(Store store, string name, string content, string callerUsername, bool callerIsAdmin, List<Attachment>? attachments = null);
    void Delete(Store store, string name, string callerUsername, bool callerIsAdmin);

    /// Called whenever a username changes (self-service rename or an admin
    /// renaming someone else) — without this, every prompt that user owns
    /// would silently become inaccessible to them.
    void RenameOwner(Store store, string oldUsername, string newUsername);

    /// Admins see every prompt; everyone else sees only their own plus any
    /// "legacy" prompt with no recorded owner.
    List<PromptRecord> List(Store store, string callerUsername, bool callerIsAdmin);

    (PromptRecord Record, int Version, string Content) Show(Store store, string name, string callerUsername, bool callerIsAdmin, int? version = null);
    DiffPaneModel Diff(Store store, string name, int v1, int v2, string callerUsername, bool callerIsAdmin);
}
