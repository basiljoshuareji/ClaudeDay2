import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult, SubTask, Task, TaskPayload, TaskQuery } from '../../core/models/api.models';
import { todayIso } from '../../core/utils/dates';

/** Thin wrapper over the /api/tasks endpoints. State lives in the components that use it. */
@Injectable({ providedIn: 'root' })
export class TasksApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/tasks`;

  list(query: TaskQuery): Observable<PagedResult<Task>> {
    let params = new HttpParams().set('today', todayIso()); // the user's local date drives Today/Overdue
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<Task>>(this.url, { params });
  }

  create(payload: TaskPayload): Observable<Task> {
    return this.http.post<Task>(this.url, payload);
  }

  update(id: number, payload: TaskPayload): Observable<Task> {
    return this.http.put<Task>(`${this.url}/${id}`, payload);
  }

  setCompletion(id: number, isCompleted: boolean): Observable<Task> {
    return this.http.patch<Task>(`${this.url}/${id}/completion`, { isCompleted });
  }

  move(id: number, sectionId: number): Observable<Task> {
    return this.http.post<Task>(`${this.url}/${id}/move`, { sectionId });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  restore(ids: number[]): Observable<Task[]> {
    return this.http.post<Task[]>(`${this.url}/restore`, { ids });
  }

  reorder(orderedIds: number[]): Observable<void> {
    return this.http.put<void>(`${this.url}/reorder`, { orderedIds });
  }

  bulkComplete(ids: number[], isCompleted: boolean): Observable<{ affected: number }> {
    return this.http.post<{ affected: number }>(`${this.url}/bulk/complete`, { ids, isCompleted });
  }

  bulkDelete(ids: number[]): Observable<{ affected: number }> {
    return this.http.post<{ affected: number }>(`${this.url}/bulk/delete`, { ids });
  }

  addSubTask(taskId: number, title: string): Observable<SubTask> {
    return this.http.post<SubTask>(`${this.url}/${taskId}/subtasks`, { title });
  }

  updateSubTask(taskId: number, subTask: SubTask): Observable<SubTask> {
    return this.http.put<SubTask>(`${this.url}/${taskId}/subtasks/${subTask.id}`, {
      title: subTask.title,
      isCompleted: subTask.isCompleted,
    });
  }

  deleteSubTask(taskId: number, subTaskId: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${taskId}/subtasks/${subTaskId}`);
  }
}
