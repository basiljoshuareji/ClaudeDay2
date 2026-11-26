import { CdkDrag, CdkDragDrop, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { EMPTY, catchError, debounceTime, distinctUntilChanged, switchMap, tap } from 'rxjs';
import { errorMessage } from '../../core/errors/api-exception';
import { DueFilter, Priority, SortBy, SortDir, Task, TaskQuery, TaskStatus } from '../../core/models/api.models';
import { ConfirmService } from '../../core/services/confirm.service';
import { ToastService } from '../../core/services/toast.service';
import { todayIso } from '../../core/utils/dates';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { ProgressBarComponent } from '../../shared/components/progress-bar.component';
import { IconComponent } from '../../shared/icons/icon.component';
import { SectionDialogService } from '../sections/section-dialog.service';
import { SectionsStore } from '../sections/sections.store';
import { TagsStore } from './tags.store';
import { TaskFormDialogComponent } from './task-form-dialog.component';
import { MoveEvent, SubTaskEvent, TaskItemComponent } from './task-item.component';
import { TasksApiService } from './tasks-api.service';

const PAGE_SIZE = 50;

/** Sort fields whose natural direction is descending (newest / highest first). */
const DESC_BY_DEFAULT: SortBy[] = ['Priority', 'Created'];

@Component({
  selector: 'app-task-list-page',
  imports: [CdkDropList, CdkDrag, IconComponent, TaskItemComponent, ProgressBarComponent, EmptyStateComponent, TaskFormDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './task-list.page.html',
  styleUrl: './task-list.page.scss',
})
export class TaskListPageComponent {
  private readonly api = inject(TasksApiService);
  protected readonly sections = inject(SectionsStore);
  protected readonly tagsStore = inject(TagsStore);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly sectionDialog = inject(SectionDialogService);
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  private readonly doc = inject(DOCUMENT);
  private readonly injector = inject(Injector);

  /** Route param `sections/:id`; absent on the "All tasks" route. */
  readonly id = input<string>();

  protected readonly sectionId = computed(() => {
    const raw = this.id();
    const parsed = raw ? Number(raw) : NaN;
    return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
  });
  protected readonly isAll = computed(() => this.sectionId() === null);
  protected readonly section = computed(() => this.sections.byId(this.sectionId()));

  // ------------------------------------------------------------- filter state
  protected readonly searchText = signal('');
  private readonly search = signal('');
  protected readonly status = signal<TaskStatus | ''>('');
  protected readonly priority = signal<Priority | ''>('');
  protected readonly due = signal<DueFilter | ''>('');
  protected readonly tagId = signal<number | null>(null);
  protected readonly sortBy = signal<SortBy>('Manual');
  protected readonly sortDir = signal<SortDir | ''>('');
  private readonly refreshTick = signal(0);

  protected readonly effectiveDir = computed<SortDir>(
    () => this.sortDir() || (DESC_BY_DEFAULT.includes(this.sortBy()) ? 'Desc' : 'Asc'),
  );
  protected readonly activeFilterCount = computed(
    () => [this.search(), this.status(), this.priority(), this.due(), this.tagId()].filter((v) => v !== '' && v !== null).length,
  );

  // --------------------------------------------------------------- list state
  protected readonly items = signal<Task[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly initialised = signal(false);
  protected readonly loadingMore = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly announcement = signal('');
  private page = 1;

  protected readonly remaining = computed(() => Math.max(0, this.total() - this.items().length));
  protected readonly canReorder = computed(() => !this.isAll() && this.sortBy() === 'Manual' && !this.selectMode());

  // ---------------------------------------------------------------- selection
  protected readonly selectMode = signal(false);
  protected readonly selectedIds = signal<ReadonlySet<number>>(new Set());
  protected readonly selectedCount = computed(() => this.selectedIds().size);
  protected readonly allSelected = computed(() => this.items().length > 0 && this.items().every((t) => this.selectedIds().has(t.id)));

  // ------------------------------------------------------------------ dialogs
  protected readonly formState = signal<{ task: Task | null } | null>(null);

  // --------------------------------------------------------------- quick add
  protected readonly quickTitle = signal('');
  protected readonly quickSection = signal<number | null>(null);
  protected readonly quickSaving = signal(false);

  // ------------------------------------------------------------------ header
  protected readonly counts = computed(() => {
    const section = this.section();
    if (this.isAll()) {
      const t = this.sections.totals();
      return { total: t.total, pending: t.pending, completed: t.completed };
    }
    return section
      ? { total: section.totalCount, pending: section.pendingCount, completed: section.completedCount }
      : { total: 0, pending: 0, completed: 0 };
  });

  protected readonly heading = computed(() => (this.isAll() ? 'All tasks' : (this.section()?.name ?? '')));

  private lastUndoToast: number | null = null;

  constructor() {
    // The same component instance is reused when moving between sections, so reset per-section state.
    effect(() => {
      this.id();
      untracked(() => {
        this.resetFilters();
        this.clearSelection();
        this.selectMode.set(false);
        this.quickSection.set(null);
      });
    });

    // Unknown / deleted section id -> go back to "All tasks".
    effect(() => {
      const id = this.sectionId();
      if (id !== null && this.sections.loaded() && !this.sections.byId(id)) {
        void this.router.navigate(['/all'], { replaceUrl: true });
      }
    });

    effect(() => {
      const name = this.heading();
      this.title.setTitle(name ? `${name} · Todo` : 'Todo');
    });

    toObservable(this.searchText)
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((text) => this.search.set(text.trim()));

    const request = computed<{ query: TaskQuery; tick: number }>(() => ({
      query: {
        sectionId: this.sectionId(),
        search: this.search(),
        status: this.status(),
        priority: this.priority(),
        due: this.due(),
        tagId: this.tagId(),
        sortBy: this.sortBy(),
        sortDir: this.sortBy() === 'Manual' ? '' : this.sortDir(),
        page: 1,
        pageSize: PAGE_SIZE,
      },
      tick: this.refreshTick(),
    }));

    toObservable(request)
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.loadError.set(null);
        }),
        // switchMap cancels a stale request when filters change quickly.
        switchMap(({ query }) =>
          this.api.list(query).pipe(
            catchError((error: unknown) => {
              this.loadError.set(errorMessage(error, 'Could not load your tasks.'));
              this.loading.set(false);
              this.initialised.set(true);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.items.set(result.items);
        this.total.set(result.totalCount);
        this.page = 1;
        this.loading.set(false);
        this.initialised.set(true);
        this.pruneSelection();
      });
  }

  // ======================================================== filters and sorting
  protected onSearch(value: string): void {
    this.searchText.set(value);
  }

  protected setSort(value: string): void {
    this.sortBy.set(value as SortBy);
    this.sortDir.set('');
  }

  protected toggleSortDir(): void {
    this.sortDir.set(this.effectiveDir() === 'Asc' ? 'Desc' : 'Asc');
  }

  protected setTag(value: string): void {
    this.tagId.set(value ? Number(value) : null);
  }

  protected clearFilters(): void {
    this.searchText.set('');
    this.search.set('');
    this.status.set('');
    this.priority.set('');
    this.due.set('');
    this.tagId.set(null);
  }

  private resetFilters(): void {
    this.clearFilters();
    this.sortBy.set('Manual');
    this.sortDir.set('');
  }

  protected reload(): void {
    this.refreshTick.update((n) => n + 1);
  }

  protected loadMore(): void {
    if (this.loadingMore() || this.remaining() === 0) return;
    this.loadingMore.set(true);
    const next = this.page + 1;
    this.api
      .list({
        sectionId: this.sectionId(),
        search: this.search(),
        status: this.status(),
        priority: this.priority(),
        due: this.due(),
        tagId: this.tagId(),
        sortBy: this.sortBy(),
        sortDir: this.sortBy() === 'Manual' ? '' : this.sortDir(),
        page: next,
        pageSize: PAGE_SIZE,
      })
      .subscribe({
        next: (result) => {
          const known = new Set(this.items().map((t) => t.id));
          this.items.update((list) => [...list, ...result.items.filter((t) => !known.has(t.id))]);
          this.total.set(result.totalCount);
          this.page = next;
          this.loadingMore.set(false);
        },
        error: (error: unknown) => {
          this.loadingMore.set(false);
          this.toast.error(errorMessage(error, 'Could not load more tasks.'));
        },
      });
  }

  // ===================================================================== CRUD
  protected openCreate(): void {
    this.formState.set({ task: null });
  }

  protected openEdit(task: Task): void {
    this.formState.set({ task });
  }

  protected closeForm(): void {
    this.formState.set(null);
  }

  protected onSaved(): void {
    this.closeForm();
    this.reload();
    this.sections.refresh();
    this.tagsStore.refresh();
  }

  protected quickAdd(event: Event): void {
    event.preventDefault();
    const title = this.quickTitle().trim();
    const sectionId = this.sectionId() ?? this.quickSection() ?? this.sections.sections()[0]?.id;
    if (!title || !sectionId || this.quickSaving()) return;

    this.quickSaving.set(true);
    this.api
      .create({ sectionId, title, description: null, dueDate: null, priority: 'Medium', status: 'Pending', tagIds: [] })
      .subscribe({
        next: () => {
          this.quickSaving.set(false);
          this.quickTitle.set('');
          this.announce(`Task “${title}” added.`);
          this.reload();
          this.sections.refresh();
        },
        error: (error: unknown) => {
          this.quickSaving.set(false);
          this.toast.error(errorMessage(error, 'Could not add the task.'));
        },
      });
  }

  protected toggleComplete(task: Task): void {
    const completing = task.status !== 'Completed';
    this.replace({ ...task, status: completing ? 'Completed' : 'Pending', completedAt: completing ? new Date().toISOString() : null });

    this.api.setCompletion(task.id, completing).subscribe({
      next: (updated) => {
        this.replace(updated);
        this.sections.refresh();
        this.announce(`“${task.title}” marked ${completing ? 'done' : 'not done'}.`);
        // With a status/due filter active the task may no longer belong in this list.
        if (this.status() || this.due()) this.reload();
      },
      error: (error: unknown) => {
        this.replace(task);
        this.toast.error(errorMessage(error, 'Could not update the task.'));
      },
    });
  }

  protected async remove(task: Task): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: 'Delete this task?',
      message: `“${task.title}” will be removed. You can undo right after.`,
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!confirmed) return;

    this.api.delete(task.id).subscribe({
      next: () => {
        this.removeLocally([task.id]);
        this.offerUndo('Task deleted.', [task.id]);
      },
      error: (error: unknown) => this.toast.error(errorMessage(error, 'Could not delete the task.')),
    });
  }

  protected move(event: MoveEvent): void {
    const target = this.sections.byId(event.sectionId);
    this.api.move(event.task.id, event.sectionId).subscribe({
      next: (updated) => {
        if (this.isAll()) {
          this.replace(updated);
        } else {
          this.removeLocally([event.task.id]);
        }
        this.sections.refresh();
        this.toast.success(`Moved to ${target?.name ?? 'the other section'}.`);
      },
      error: (error: unknown) => this.toast.error(errorMessage(error, 'Could not move the task.')),
    });
  }

  // ============================================================== sub-tasks
  protected toggleSubTask(event: SubTaskEvent): void {
    const { task, subTask } = event;
    this.patchSubTask(task.id, subTask);
    this.api.updateSubTask(task.id, subTask).subscribe({
      error: (error: unknown) => {
        this.patchSubTask(task.id, { ...subTask, isCompleted: !subTask.isCompleted });
        this.toast.error(errorMessage(error, 'Could not update the checklist item.'));
      },
    });
  }

  protected addSubTask(event: { task: Task; title: string }): void {
    this.api.addSubTask(event.task.id, event.title).subscribe({
      next: (sub) => {
        const current = this.items().find((t) => t.id === event.task.id);
        if (current) this.replace({ ...current, subTasks: [...current.subTasks, sub] });
      },
      error: (error: unknown) => this.toast.error(errorMessage(error, 'Could not add the checklist item.')),
    });
  }

  private patchSubTask(taskId: number, sub: { id: number; isCompleted: boolean }): void {
    const task = this.items().find((t) => t.id === taskId);
    if (!task) return;
    this.replace({ ...task, subTasks: task.subTasks.map((s) => (s.id === sub.id ? { ...s, isCompleted: sub.isCompleted } : s)) });
  }

  // =========================================================== drag and drop
  protected drop(event: CdkDragDrop<Task[]>): void {
    if (event.previousIndex === event.currentIndex) return;
    this.reorderTo(event.previousIndex, event.currentIndex);
  }

  protected reorderBy(task: Task, direction: -1 | 1): void {
    const from = this.items().findIndex((t) => t.id === task.id);
    const to = from + direction;
    if (from < 0 || to < 0 || to >= this.items().length) return;
    this.reorderTo(from, to);

    // Keep keyboard focus on the moved task's handle once the DOM has been re-ordered.
    afterNextRender(
      () => this.doc.querySelector<HTMLElement>(`[data-task-id="${task.id}"] .handle`)?.focus(),
      { injector: this.injector },
    );
  }

  private reorderTo(from: number, to: number): void {
    const next = [...this.items()];
    moveItemInArray(next, from, to);
    this.items.set(next);
    this.announce(`Moved “${next[to].title}” to position ${to + 1} of ${next.length}.`);

    this.api.reorder(next.map((t) => t.id)).subscribe({
      error: (error: unknown) => {
        this.toast.error(errorMessage(error, 'Could not save the new order.'));
        this.reload();
      },
    });
  }

  // ================================================================= bulk
  protected toggleSelectMode(): void {
    this.selectMode.update((v) => !v);
    if (!this.selectMode()) this.clearSelection();
  }

  protected onSelect(task: Task, checked: boolean): void {
    this.selectedIds.update((set) => {
      const next = new Set(set);
      if (checked) next.add(task.id);
      else next.delete(task.id);
      return next;
    });
  }

  protected toggleSelectAll(checked: boolean): void {
    this.selectedIds.set(checked ? new Set(this.items().map((t) => t.id)) : new Set());
  }

  protected clearSelection(): void {
    this.selectedIds.set(new Set());
  }

  protected bulkComplete(isCompleted: boolean): void {
    const ids = [...this.selectedIds()];
    if (ids.length === 0) return;

    this.api.bulkComplete(ids, isCompleted).subscribe({
      next: ({ affected }) => {
        this.toast.success(`${affected} task${affected === 1 ? '' : 's'} marked ${isCompleted ? 'done' : 'not done'}.`);
        this.clearSelection();
        this.reload();
        this.sections.refresh();
      },
      error: (error: unknown) => this.toast.error(errorMessage(error, 'Could not update the selected tasks.')),
    });
  }

  protected async bulkDelete(): Promise<void> {
    const ids = [...this.selectedIds()];
    if (ids.length === 0) return;

    const confirmed = await this.confirm.confirm({
      title: `Delete ${ids.length} task${ids.length === 1 ? '' : 's'}?`,
      message: 'The selected tasks will be removed. You can undo right after.',
      confirmLabel: `Delete ${ids.length}`,
      danger: true,
    });
    if (!confirmed) return;

    this.api.bulkDelete(ids).subscribe({
      next: ({ affected }) => {
        this.removeLocally(ids);
        this.offerUndo(`${affected} task${affected === 1 ? '' : 's'} deleted.`, ids);
      },
      error: (error: unknown) => this.toast.error(errorMessage(error, 'Could not delete the selected tasks.')),
    });
  }

  // ============================================================== sections
  protected editSection(): void {
    const section = this.section();
    if (section) this.sectionDialog.openEdit(section);
  }

  protected async deleteSection(): Promise<void> {
    const section = this.section();
    if (!section) return;

    const message =
      section.totalCount > 0
        ? `This also permanently deletes its ${section.totalCount} task${section.totalCount === 1 ? '' : 's'}. This can't be undone.`
        : 'This section is empty. This can\'t be undone.';
    const confirmed = await this.confirm.confirm({
      title: `Delete “${section.name}”?`,
      message,
      confirmLabel: 'Delete section',
      danger: true,
    });
    if (!confirmed) return;

    this.sections.delete(section.id).subscribe({
      next: () => {
        this.toast.success(`Section “${section.name}” deleted.`);
        void this.router.navigate(['/all']);
      },
      error: (error: unknown) => this.toast.error(errorMessage(error, 'Could not delete the section.')),
    });
  }

  // ============================================================== helpers
  protected today(): string {
    return todayIso();
  }

  private replace(updated: Task): void {
    this.items.update((list) => list.map((t) => (t.id === updated.id ? updated : t)));
  }

  private removeLocally(ids: number[]): void {
    const gone = new Set(ids);
    this.items.update((list) => list.filter((t) => !gone.has(t.id)));
    this.total.update((n) => Math.max(0, n - ids.length));
    this.selectedIds.update((set) => new Set([...set].filter((id) => !gone.has(id))));
    this.sections.refresh();
  }

  private pruneSelection(): void {
    const present = new Set(this.items().map((t) => t.id));
    this.selectedIds.update((set) => new Set([...set].filter((id) => present.has(id))));
  }

  /** Shows an "Undo" toast. Only the most recent delete stays undoable. */
  private offerUndo(message: string, ids: number[]): void {
    if (this.lastUndoToast !== null) this.toast.dismiss(this.lastUndoToast);
    this.lastUndoToast = this.toast.show(message, {
      type: 'info',
      duration: 9000,
      actionLabel: 'Undo',
      action: () => this.undoDelete(ids),
    });
  }

  private undoDelete(ids: number[]): void {
    this.api.restore(ids).subscribe({
      next: (restored) => {
        this.toast.success(`Restored ${restored.length} task${restored.length === 1 ? '' : 's'}.`);
        this.reload();
        this.sections.refresh();
      },
      error: (error: unknown) => this.toast.error(errorMessage(error, 'Could not restore the task.')),
    });
  }

  private announce(message: string): void {
    // Toggle the text so identical consecutive messages are still announced.
    this.announcement.set('');
    queueMicrotask(() => this.announcement.set(message));
  }
}
