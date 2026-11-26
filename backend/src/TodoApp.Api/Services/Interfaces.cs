using TodoApp.Api.Dtos;

namespace TodoApp.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<SessionDto> GetSessionAsync(int userId, CancellationToken ct = default);
}

public interface ISectionService
{
    Task<IReadOnlyList<SectionDto>> GetAllAsync(int userId, CancellationToken ct = default);
    Task<SectionDto> GetAsync(int userId, int id, CancellationToken ct = default);
    Task<SectionDto> CreateAsync(int userId, SectionRequest request, CancellationToken ct = default);
    Task<SectionDto> UpdateAsync(int userId, int id, SectionRequest request, CancellationToken ct = default);

    /// <summary>Deletes a custom section. When <c>moveTasksTo</c> is set its tasks move there instead of being deleted.</summary>
    Task DeleteAsync(int userId, int id, int? moveTasksTo, CancellationToken ct = default);
    Task<IReadOnlyList<SectionDto>> ReorderAsync(int userId, IReadOnlyList<int> orderedIds, CancellationToken ct = default);
}

public interface ITaskService
{
    Task<PagedResult<TaskDto>> GetTasksAsync(int userId, TaskQuery query, CancellationToken ct = default);
    Task<TaskDto> GetAsync(int userId, int id, CancellationToken ct = default);
    Task<TaskDto> CreateAsync(int userId, CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto> UpdateAsync(int userId, int id, UpdateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto> SetCompletionAsync(int userId, int id, bool isCompleted, CancellationToken ct = default);
    Task<TaskDto> MoveAsync(int userId, int id, int sectionId, CancellationToken ct = default);
    Task ReorderAsync(int userId, IReadOnlyList<int> orderedIds, CancellationToken ct = default);

    /// <summary>Soft-deletes a task so it can be restored (Undo).</summary>
    Task DeleteAsync(int userId, int id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskDto>> RestoreAsync(int userId, IReadOnlyList<int> ids, CancellationToken ct = default);
    Task<int> BulkCompleteAsync(int userId, IReadOnlyList<int> ids, bool isCompleted, CancellationToken ct = default);
    Task<int> BulkDeleteAsync(int userId, IReadOnlyList<int> ids, CancellationToken ct = default);
}

public interface ISubTaskService
{
    Task<SubTaskDto> AddAsync(int userId, int taskId, CreateSubTaskRequest request, CancellationToken ct = default);
    Task<SubTaskDto> UpdateAsync(int userId, int taskId, int id, UpdateSubTaskRequest request, CancellationToken ct = default);
    Task DeleteAsync(int userId, int taskId, int id, CancellationToken ct = default);
}

public interface ITagService
{
    Task<IReadOnlyList<TagListItemDto>> GetAllAsync(int userId, CancellationToken ct = default);
    Task<TagListItemDto> CreateAsync(int userId, TagRequest request, CancellationToken ct = default);
    Task<TagListItemDto> UpdateAsync(int userId, int id, TagRequest request, CancellationToken ct = default);
    Task DeleteAsync(int userId, int id, CancellationToken ct = default);
}

public interface IPreferenceService
{
    Task<PreferenceDto> GetAsync(int userId, CancellationToken ct = default);
    Task<PreferenceDto> UpdateAsync(int userId, UpdatePreferencesRequest request, CancellationToken ct = default);
}
