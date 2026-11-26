import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Section, SectionPayload } from '../../core/models/api.models';

/** Client-side cache of the user's sections (with live task counts) shared by the sidebar and pages. */
@Injectable({ providedIn: 'root' })
export class SectionsStore {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/sections`;

  private readonly _sections = signal<Section[]>([]);
  private readonly _loaded = signal(false);

  readonly sections = this._sections.asReadonly();
  readonly loaded = this._loaded.asReadonly();

  /** Sum of every section, for the "All tasks" view. */
  readonly totals = computed(() =>
    this._sections().reduce(
      (acc, s) => ({
        total: acc.total + s.totalCount,
        pending: acc.pending + s.pendingCount,
        completed: acc.completed + s.completedCount,
      }),
      { total: 0, pending: 0, completed: 0 },
    ),
  );

  byId(id: number | null | undefined): Section | undefined {
    return id == null ? undefined : this._sections().find((s) => s.id === id);
  }

  /** Loads (or silently refreshes) the list, e.g. after a task changes the counts. */
  refresh(): void {
    this.http.get<Section[]>(this.url).subscribe({
      next: (sections) => {
        this._sections.set(sections);
        this._loaded.set(true);
      },
      error: () => this._loaded.set(true),
    });
  }

  create(payload: SectionPayload): Observable<Section> {
    return this.http.post<Section>(this.url, payload).pipe(tap(() => this.refresh()));
  }

  update(id: number, payload: SectionPayload): Observable<Section> {
    return this.http.put<Section>(`${this.url}/${id}`, payload).pipe(tap(() => this.refresh()));
  }

  delete(id: number, moveTasksTo?: number): Observable<void> {
    const query = moveTasksTo ? `?moveTasksTo=${moveTasksTo}` : '';
    return this.http.delete<void>(`${this.url}/${id}${query}`).pipe(tap(() => this.refresh()));
  }

  clear(): void {
    this._sections.set([]);
    this._loaded.set(false);
  }
}
