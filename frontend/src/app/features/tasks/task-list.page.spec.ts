import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PagedResult, Section, Task } from '../../core/models/api.models';
import { ConfirmService } from '../../core/services/confirm.service';
import { ToastService } from '../../core/services/toast.service';
import { SectionsStore } from '../sections/sections.store';
import { TagsStore } from './tags.store';
import { TaskListPageComponent } from './task-list.page';
import { TasksApiService } from './tasks-api.service';

const section = (id: number, name: string): Section => ({
  id,
  name,
  color: '#3b82f6',
  icon: 'home',
  sortOrder: id,
  isDefault: true,
  totalCount: 2,
  pendingCount: 1,
  inProgressCount: 0,
  completedCount: 1,
});

const task = (id: number, title: string, extra: Partial<Task> = {}): Task => ({
  id,
  sectionId: 3,
  title,
  description: null,
  dueDate: null,
  priority: 'Medium',
  status: 'Pending',
  sortOrder: id,
  completedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  tags: [],
  subTasks: [],
  ...extra,
});

const page = (items: Task[], totalCount = items.length): PagedResult<Task> => ({ items, page: 1, pageSize: 50, totalCount, totalPages: 1 });

describe('TaskListPageComponent', () => {
  let fixture: ComponentFixture<TaskListPageComponent>;
  let el: HTMLElement;

  const api = {
    list: vi.fn(),
    create: vi.fn(),
    setCompletion: vi.fn(),
    delete: vi.fn(),
    restore: vi.fn(),
    reorder: vi.fn(),
    bulkComplete: vi.fn(),
    bulkDelete: vi.fn(),
    move: vi.fn(),
    updateSubTask: vi.fn(),
    addSubTask: vi.fn(),
  };
  const sectionsStore = {
    sections: signal([section(3, 'Home'), section(4, 'Work')]),
    loaded: signal(true),
    totals: signal({ total: 4, pending: 2, completed: 2 }),
    byId: (id: number | null | undefined) => [section(3, 'Home'), section(4, 'Work')].find((s) => s.id === id),
    refresh: vi.fn(),
    delete: vi.fn(),
  };
  const tagsStore = { tags: signal([{ id: 9, name: 'urgent', color: '#ef4444', taskCount: 1 }]), loaded: signal(true), refresh: vi.fn() };

  async function render(id?: string) {
    fixture = TestBed.createComponent(TaskListPageComponent);
    if (id) fixture.componentRef.setInput('id', id);
    el = fixture.nativeElement;
    await fixture.whenStable();
    await fixture.whenStable();
  }

  const titles = () => [...el.querySelectorAll('app-task-item .title')].map((t) => t.textContent!.trim());
  const lastQuery = () => api.list.mock.calls.at(-1)![0];

  beforeEach(() => {
    vi.clearAllMocks();
    api.list.mockReturnValue(of(page([task(1, 'Buy milk'), task(2, 'Call plumber')])));
    TestBed.configureTestingModule({
      imports: [TaskListPageComponent],
      providers: [
        provideRouter([]),
        { provide: TasksApiService, useValue: api },
        { provide: SectionsStore, useValue: sectionsStore },
        { provide: TagsStore, useValue: tagsStore },
      ],
    });
  });

  it('loads and lists the tasks of the section from the route', async () => {
    await render('3');

    expect(lastQuery()).toMatchObject({ sectionId: 3, sortBy: 'Manual', page: 1 });
    expect(titles()).toEqual(['Buy milk', 'Call plumber']);
    expect(el.querySelector('h1')!.textContent).toBe('Home');
    expect(el.querySelector('.head__sub')!.textContent).toContain('2 tasks');
  });

  it('loads every section on the "All tasks" route', async () => {
    await render();

    expect(lastQuery().sectionId).toBeNull();
    expect(el.querySelector('h1')!.textContent).toBe('All tasks');
    // The section chip is shown on each task in the combined view.
    expect(el.querySelector('app-task-item .chip .dot')).not.toBeNull();
  });

  it('shows an empty state when there are no tasks', async () => {
    api.list.mockReturnValue(of(page([])));
    await render('3');

    expect(el.querySelector('app-empty-state h2')!.textContent).toContain('No tasks in Home yet');
  });

  it('applies the due-date filter and shows a way to clear it', async () => {
    await render('3');

    [...el.querySelectorAll<HTMLButtonElement>('.due-tab')].find((b) => b.textContent!.includes('Overdue'))!.click();
    await fixture.whenStable();
    await fixture.whenStable();

    expect(lastQuery().due).toBe('Overdue');
    expect(el.textContent).toContain('Clear 1 filter');
  });

  it('explains when filters match nothing', async () => {
    await render('3');
    api.list.mockReturnValue(of(page([])));

    [...el.querySelectorAll<HTMLButtonElement>('.due-tab')].find((b) => b.textContent!.includes('Today'))!.click();
    await fixture.whenStable();
    await fixture.whenStable();

    expect(el.querySelector('app-empty-state h2')!.textContent).toContain('No matching tasks');
  });

  it('completes a task optimistically and persists it', async () => {
    api.setCompletion.mockReturnValue(of(task(1, 'Buy milk', { status: 'Completed' })));
    await render('3');

    el.querySelector<HTMLButtonElement>('app-task-item .check')!.click();
    await fixture.whenStable();

    expect(api.setCompletion).toHaveBeenCalledWith(1, true);
    expect(el.querySelector('app-task-item')!.classList).toContain('is-completed');
    expect(sectionsStore.refresh).toHaveBeenCalled();
  });

  it('asks before deleting, then offers Undo which restores the task', async () => {
    api.delete.mockReturnValue(of(undefined));
    api.restore.mockReturnValue(of([task(1, 'Buy milk')]));
    const confirm = TestBed.inject(ConfirmService);
    const toast = TestBed.inject(ToastService);
    await render('3');

    el.querySelector<HTMLButtonElement>('app-task-item button[aria-label^="Delete"]')!.click();
    await fixture.whenStable();
    expect(confirm.request()?.title).toMatch(/delete/i);
    expect(api.delete).not.toHaveBeenCalled(); // nothing happens until confirmed

    confirm.answer(true);
    await fixture.whenStable();
    await fixture.whenStable();

    expect(api.delete).toHaveBeenCalledWith(1);
    expect(titles()).toEqual(['Call plumber']);
    const undo = toast.toasts().find((t) => t.actionLabel === 'Undo')!;
    expect(undo).toBeDefined();

    toast.runAction(undo);
    await fixture.whenStable();

    expect(api.restore).toHaveBeenCalledWith([1]);
  });

  it('does nothing when the delete is cancelled', async () => {
    const confirm = TestBed.inject(ConfirmService);
    await render('3');

    el.querySelector<HTMLButtonElement>('app-task-item button[aria-label^="Delete"]')!.click();
    await fixture.whenStable();
    confirm.answer(false);
    await fixture.whenStable();

    expect(api.delete).not.toHaveBeenCalled();
    expect(titles()).toHaveLength(2);
  });

  it('quick-add creates a task in the current section', async () => {
    api.create.mockReturnValue(of(task(3, 'Water plants')));
    await render('3');

    const input = el.querySelector<HTMLInputElement>('#quick-title')!;
    input.value = 'Water plants';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    el.querySelector<HTMLFormElement>('form.quick')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(api.create).toHaveBeenCalledWith(expect.objectContaining({ sectionId: 3, title: 'Water plants', priority: 'Medium', status: 'Pending' }));
  });

  it('bulk-completes the selected tasks', async () => {
    api.bulkComplete.mockReturnValue(of({ affected: 2 }));
    await render('3');

    [...el.querySelectorAll<HTMLButtonElement>('.toolbar__row button')].find((b) => b.textContent!.includes('Select'))!.click();
    await fixture.whenStable();
    el.querySelectorAll<HTMLInputElement>('app-task-item .select-box').forEach((box) => box.click());
    await fixture.whenStable();

    expect(el.querySelector('.bulk__count')!.textContent).toContain('2 selected');
    [...el.querySelectorAll<HTMLButtonElement>('.bulk button')].find((b) => b.textContent!.includes('Complete'))!.click();
    await fixture.whenStable();

    expect(api.bulkComplete).toHaveBeenCalledWith([1, 2], true);
  });

  it('only allows drag-and-drop reordering within one section in manual order', async () => {
    await render('3');
    expect(el.querySelectorAll('app-task-item .handle')).toHaveLength(2);

    await render(); // "All tasks"
    expect(el.querySelectorAll('app-task-item .handle')).toHaveLength(0);
  });
});
