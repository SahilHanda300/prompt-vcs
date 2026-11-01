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

    /// Flips a prompt's favorite flag and returns the new value. Only the
    /// prompt's owner or an admin may toggle it, same as Edit/Delete.
    bool ToggleFavorite(Store store, string name, string callerUsername, bool callerIsAdmin);

    /// Called whenever a username changes (self-service rename or an admin
    /// renaming someone else) — without this, every prompt that user owns
    /// would silently become inaccessible to them.
    void RenameOwner(Store store, string oldUsername, string newUsername);

    /// Renames a prompt itself (display name, and the id it's keyed/slugged
    /// by) — distinct from RenameOwner, which changes who owns it. Only the
    /// prompt's owner or an admin may rename it, same as Edit/Delete.
    void Rename(Store store, string name, string newName, string callerUsername, bool callerIsAdmin);

    /// Deletes every prompt owned by the given username — called when an
    /// admin deletes a user account, so that account's data doesn't survive
    /// as orphaned prompts nobody (not even another admin acting on behalf
    /// of a "real" user) can ever legitimately own again. Returns the
    /// deleted prompts' ids so the caller can also clean up their generated
    /// site files on disk (this layer has no filesystem knowledge of its
    /// own — see PromptTools/AuthTools).
    List<string> DeleteAllOwnedBy(Store store, string username);

    /// Admins see every prompt; everyone else sees only their own plus any
    /// "legacy" prompt with no recorded owner.
    List<PromptRecord> List(Store store, string callerUsername, bool callerIsAdmin);

    (PromptRecord Record, int Version, string Content) Show(Store store, string name, string callerUsername, bool callerIsAdmin, int? version = null);
    DiffPaneModel Diff(Store store, string name, int v1, int v2, string callerUsername, bool callerIsAdmin);
}
