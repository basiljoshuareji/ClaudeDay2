import { A11yModule } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiException, errorMessage } from '../../core/errors/api-exception';
import { Priority, Section, Task, TaskPayload, TaskStatus } from '../../core/models/api.models';
import { ToastService } from '../../core/services/toast.service';
import { IconComponent } from '../../shared/icons/icon.component';
import { TagsStore } from './tags.store';
import { TasksApiService } from './tasks-api.service';

type SubTaskGroup = FormGroup<{
  id: FormControl<number | null>;
  title: FormControl<string>;
  isCompleted: FormControl<boolean>;
}>;

export const PRIORITIES: readonly Priority[] = ['Low', 'Medium', 'High'];
export const STATUSES: readonly { value: TaskStatus; label: string }[] = [
  { value: 'Pending', label: 'Pending' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Completed', label: 'Completed' },
];

/** Create / edit dialog for a task, including tags and the sub-task checklist. */
@Component({
  selector: 'app-task-form-dialog',
  imports: [A11yModule, ReactiveFormsModule, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './task-form-dialog.component.html',
  styleUrl: './task-form-dialog.component.scss',
})
export class TaskFormDialogComponent implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(TasksApiService);
  private readonly toast = inject(ToastService);
  protected readonly tagsStore = inject(TagsStore);

  /** Task being edited; null to create. */
  readonly task = input<Task | null>(null);
  readonly sections = input.required<Section[]>();
  /** Section pre-selected when creating. */
  readonly defaultSectionId = input<number | null>(null);

  readonly saved = output<Task>();
  readonly closed = output<void>();

  protected readonly priorities = PRIORITIES;
  protected readonly statuses = STATUSES;

  protected readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(/\S/)]],
    description: ['', [Validators.maxLength(4000)]],
    sectionId: [0, [Validators.required, Validators.min(1)]],
    dueDate: [''],
    priority: ['Medium' as Priority],
    status: ['Pending' as TaskStatus],
    subTasks: this.fb.array<SubTaskGroup>([]),
  });

  protected readonly selectedTagIds = signal<number[]>([]);
  protected readonly saving = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly serverFieldErrors = signal<Record<string, string>>({});
  protected readonly tagError = signal<string | null>(null);
  protected readonly creatingTag = signal(false);

  protected get isEdit(): boolean {
    return this.task() !== null;
  }

  protected get title() {
    return this.form.controls.title;
  }

  protected get description() {
    return this.form.controls.description;
  }

  protected get subTasks(): FormArray<SubTaskGroup> {
    return this.form.controls.subTasks;
  }

  ngOnInit(): void {
    const task = this.task();
    const sections = this.sections();
    const fallbackSection = this.defaultSectionId() ?? sections[0]?.id ?? 0;

    if (task) {
      this.form.patchValue({
        title: task.title,
        description: task.description ?? '',
        sectionId: task.sectionId,
        dueDate: task.dueDate ?? '',
        priority: task.priority,
        status: task.status,
      });
      this.selectedTagIds.set(task.tags.map((t) => t.id));
      task.subTasks.forEach((s) => this.addSubTaskGroup(s.title, s.isCompleted, s.id));
    } else {
      this.form.patchValue({ sectionId: fallbackSection });
    }
  }

  // ------------------------------------------------------------ checklist
  protected addSubTaskGroup(title = '', isCompleted = false, id: number | null = null): void {
    this.subTasks.push(
      this.fb.group({
        id: this.fb.control<number | null>(id),
        title: this.fb.control(title, [Validators.required, Validators.maxLength(200)]),
        isCompleted: this.fb.control(isCompleted),
      }),
    );
  }

  protected addFromInput(input: HTMLInputElement, event?: Event): void {
    event?.preventDefault();
    const title = input.value.trim();
    if (!title) return;
    this.addSubTaskGroup(title);
    input.value = '';
    input.focus();
  }

  protected removeSubTask(index: number): void {
    this.subTasks.removeAt(index);
  }

  // ----------------------------------------------------------------- tags
  protected isTagSelected(id: number): boolean {
    return this.selectedTagIds().includes(id);
  }

  protected toggleTag(id: number): void {
    this.selectedTagIds.update((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]));
  }

  protected createTag(input: HTMLInputElement, event?: Event): void {
    event?.preventDefault();
    const name = input.value.trim();
    this.tagError.set(null);
    if (!name) return;

    const existing = this.tagsStore.tags().find((t) => t.name.toLowerCase() === name.toLowerCase());
    if (existing) {
      if (!this.isTagSelected(existing.id)) this.toggleTag(existing.id);
      input.value = '';
      return;
    }

    this.creatingTag.set(true);
    this.tagsStore.create(name).subscribe({
      next: (tag) => {
        this.creatingTag.set(false);
        this.selectedTagIds.update((ids) => [...ids, tag.id]);
        input.value = '';
      },
      error: (error: unknown) => {
        this.creatingTag.set(false);
        this.tagError.set(errorMessage(error, 'Could not create the tag.'));
      },
    });
  }

  // ----------------------------------------------------------------- save
  protected serverFieldError(field: string): string | null {
    return this.serverFieldErrors()[field] ?? null;
  }

  protected save(): void {
    this.serverError.set(null);
    this.serverFieldErrors.set({});
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const payload: TaskPayload = {
      sectionId: Number(value.sectionId),
      title: value.title.trim(),
      description: value.description.trim() || null,
      dueDate: value.dueDate || null,
      priority: value.priority,
      status: value.status,
      tagIds: this.selectedTagIds(),
      subTasks: value.subTasks
        .filter((s) => s.title.trim().length > 0)
        .map((s) => ({ ...(s.id ? { id: s.id } : {}), title: s.title.trim(), isCompleted: s.isCompleted })),
    };

    this.saving.set(true);
    const existing = this.task();
    const request = existing ? this.api.update(existing.id, payload) : this.api.create(payload);
    request.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.toast.success(existing ? 'Task updated.' : 'Task added.');
        this.saved.emit(saved);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        if (error instanceof ApiException && Object.keys(error.fieldErrors).length > 0) {
          const fields: Record<string, string> = {};
          for (const [field, messages] of Object.entries(error.fieldErrors)) fields[field] = messages[0];
          this.serverFieldErrors.set(fields);
        } else {
          this.serverError.set(errorMessage(error, 'Could not save the task.'));
        }
      },
    });
  }

  protected close(): void {
    this.closed.emit();
  }

  protected onBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.close();
  }
}
