import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TagListItem } from '../../core/models/api.models';

export const TAG_COLORS: readonly string[] = [
  '#64748b',
  '#ef4444',
  '#f97316',
  '#eab308',
  '#22c55e',
  '#14b8a6',
  '#3b82f6',
  '#8b5cf6',
  '#ec4899',
];

@Injectable({ providedIn: 'root' })
export class TagsStore {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/tags`;

  private readonly _tags = signal<TagListItem[]>([]);
  private readonly _loaded = signal(false);

  readonly tags = this._tags.asReadonly();
  readonly loaded = this._loaded.asReadonly();

  refresh(): void {
    this.http.get<TagListItem[]>(this.url).subscribe({
      next: (tags) => {
        this._tags.set(tags);
        this._loaded.set(true);
      },
      error: () => this._loaded.set(true),
    });
  }

  create(name: string, color: string = TAG_COLORS[0]): Observable<TagListItem> {
    return this.http.post<TagListItem>(this.url, { name, color }).pipe(tap(() => this.refresh()));
  }

  update(id: number, name: string, color: string): Observable<TagListItem> {
    return this.http.put<TagListItem>(`${this.url}/${id}`, { name, color }).pipe(tap(() => this.refresh()));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`).pipe(tap(() => this.refresh()));
  }

  clear(): void {
    this._tags.set([]);
    this._loaded.set(false);
  }
}
