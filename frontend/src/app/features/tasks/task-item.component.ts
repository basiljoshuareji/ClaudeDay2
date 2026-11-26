import { CdkDragHandle } from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { Section, SubTask, Task } from '../../core/models/api.models';
import { dueLabel, isOverdue, todayIso } from '../../core/utils/dates';
import { IconComponent } from '../../shared/icons/icon.component';
import { ProgressBarComponent } from '../../shared/components/progress-bar.component';

export interface MoveEvent {
  task: Task;
  sectionId: number;
}

export interface SubTaskEvent {
  task: Task;
  subTask: SubTask;
}

@Component({
  selector: 'app-task-item',
  imports: [IconComponent, ProgressBarComponent, CdkDragHandle],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    role: 'listitem',
    '[class.is-completed]': 'done()',
    '[class.is-overdue]': 'overdue()',
    '[class.is-selected]': 'selected()',
  },
  templateUrl: './task-item.component.html',
  styleUrl: './task-item.component.scss',
})
export class TaskItemComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly task = input.required<Task>();
  /** All sections, for the "Move to" menu and the section chip. */
  readonly sections = input<Section[]>([]);
  readonly showSection = input(false);
  readonly selectMode = input(false);
  readonly selected = input(false);
  /** Shows the drag handle and enables keyboard reordering. */
  readonly reorderable = input(false);

  readonly toggleComplete = output<Task>();
  readonly edit = output<Task>();
  readonly remove = output<Task>();
  readonly selectionChange = output<{ task: Task; checked: boolean }>();
  readonly move = output<MoveEvent>();
  readonly reorderBy = output<{ task: Task; direction: -1 | 1 }>();
  readonly toggleSubTask = output<SubTaskEvent>();
  readonly addSubTask = output<{ task: Task; title: string }>();

  protected readonly expanded = signal(false);
  protected readonly menuOpen = signal(false);

  protected readonly done = computed(() => this.task().status === 'Completed');
  protected readonly overdue = computed(() => isOverdue(this.task().dueDate, this.task().status));
  protected readonly due = computed(() => {
    const date = this.task().dueDate;
    return date ? dueLabel(date, todayIso()) : null;
  });
  protected readonly sectionOf = computed(() => this.sections().find((s) => s.id === this.task().sectionId));
  protected readonly otherSections = computed(() => this.sections().filter((s) => s.id !== this.task().sectionId));
  protected readonly subDone = computed(() => this.task().subTasks.filter((s) => s.isCompleted).length);
  protected readonly priorityIcon = 'flag';

  protected toggleMenu(event: Event): void {
    event.stopPropagation();
    this.menuOpen.update((v) => !v);
  }

  protected moveTo(sectionId: number): void {
    this.menuOpen.set(false);
    this.move.emit({ task: this.task(), sectionId });
  }

  protected reorder(direction: -1 | 1): void {
    this.menuOpen.set(false);
    this.reorderBy.emit({ task: this.task(), direction });
  }

  protected onHandleKey(event: KeyboardEvent): void {
    if (event.altKey && (event.key === 'ArrowUp' || event.key === 'ArrowDown')) {
      event.preventDefault();
      this.reorderBy.emit({ task: this.task(), direction: event.key === 'ArrowUp' ? -1 : 1 });
    }
  }

  protected onSubTaskChange(subTask: SubTask, isCompleted: boolean): void {
    this.toggleSubTask.emit({ task: this.task(), subTask: { ...subTask, isCompleted } });
  }

  protected submitSubTask(input: HTMLInputElement): void {
    const title = input.value.trim();
    if (!title) return;
    this.addSubTask.emit({ task: this.task(), title });
    input.value = '';
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (this.menuOpen() && !this.host.nativeElement.contains(event.target as Node)) {
      this.menuOpen.set(false);
    }
  }

  @HostListener('keydown.escape')
  protected onEscape(): void {
    if (this.menuOpen()) {
      this.menuOpen.set(false);
      this.host.nativeElement.querySelector<HTMLElement>('.menu-btn')?.focus();
    }
  }
}
