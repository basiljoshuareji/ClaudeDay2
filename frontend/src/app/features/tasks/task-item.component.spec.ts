import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Section, Task } from '../../core/models/api.models';
import { toIsoDate } from '../../core/utils/dates';
import { TaskItemComponent } from './task-item.component';

const day = (offset: number) => {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  return toIsoDate(d);
};

const baseTask: Task = {
  id: 7,
  sectionId: 1,
  title: 'Pay rent',
  description: 'Transfer before noon',
  dueDate: null,
  priority: 'High',
  status: 'Pending',
  sortOrder: 0,
  completedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  tags: [{ id: 1, name: 'money', color: '#22c55e' }],
  subTasks: [],
};

const sections: Section[] = [
  { id: 1, name: 'Home', color: '#3b82f6', icon: 'home', sortOrder: 0, isDefault: true, totalCount: 1, pendingCount: 1, inProgressCount: 0, completedCount: 0 },
  { id: 2, name: 'Work', color: '#8b5cf6', icon: 'briefcase', sortOrder: 1, isDefault: true, totalCount: 0, pendingCount: 0, inProgressCount: 0, completedCount: 0 },
];

describe('TaskItemComponent', () => {
  let fixture: ComponentFixture<TaskItemComponent>;
  let el: HTMLElement;

  async function render(task: Partial<Task> = {}, inputs: Record<string, unknown> = {}) {
    fixture = TestBed.createComponent(TaskItemComponent);
    fixture.componentRef.setInput('task', { ...baseTask, ...task });
    fixture.componentRef.setInput('sections', sections);
    for (const [key, value] of Object.entries(inputs)) fixture.componentRef.setInput(key, value);
    el = fixture.nativeElement;
    await fixture.whenStable();
  }

  const click = async (selector: string) => {
    el.querySelector<HTMLElement>(selector)!.click();
    await fixture.whenStable();
  };

  beforeEach(() => TestBed.configureTestingModule({ imports: [TaskItemComponent] }));

  it('renders title, notes, priority and tags', async () => {
    await render();

    expect(el.querySelector('.title')!.textContent).toContain('Pay rent');
    expect(el.querySelector('.desc')!.textContent).toContain('Transfer before noon');
    expect(el.textContent).toContain('High');
    expect(el.querySelector('.tag')!.textContent).toContain('money');
  });

  it('highlights overdue tasks (colour class plus a text label for screen readers)', async () => {
    await render({ dueDate: day(-2) });

    expect(el.classList).toContain('is-overdue');
    expect(el.querySelector('.chip--danger')!.textContent).toContain('Overdue');
  });

  it('does not highlight a completed task even if its date has passed', async () => {
    await render({ dueDate: day(-2), status: 'Completed' });

    expect(el.classList).not.toContain('is-overdue');
    expect(el.classList).toContain('is-completed');
    expect(el.querySelector('.check')!.getAttribute('aria-checked')).toBe('true');
  });

  it('shows friendly due labels', async () => {
    await render({ dueDate: day(0) });
    expect(el.textContent).toContain('Today');
  });

  it('emits when the completion circle, title, edit and delete are used', async () => {
    await render();
    const toggled = vi.fn();
    const edited = vi.fn();
    const removed = vi.fn();
    fixture.componentInstance.toggleComplete.subscribe(toggled);
    fixture.componentInstance.edit.subscribe(edited);
    fixture.componentInstance.remove.subscribe(removed);

    await click('.check');
    await click('.title');
    await click('button[aria-label^="Delete"]');

    expect(toggled).toHaveBeenCalledWith(expect.objectContaining({ id: 7 }));
    expect(edited).toHaveBeenCalledOnce();
    expect(removed).toHaveBeenCalledOnce();
  });

  it('gives the completion control an accessible name that includes the task', async () => {
    await render();

    expect(el.querySelector('.check')!.getAttribute('aria-label')).toBe('Mark as done: Pay rent');
    expect(el.querySelector('.check')!.getAttribute('role')).toBe('checkbox');
  });

  it('shows checklist progress and toggles an item', async () => {
    await render({
      subTasks: [
        { id: 1, taskId: 7, title: 'Log in', isCompleted: true, sortOrder: 0 },
        { id: 2, taskId: 7, title: 'Send money', isCompleted: false, sortOrder: 1 },
      ],
    });
    const toggled = vi.fn();
    fixture.componentInstance.toggleSubTask.subscribe(toggled);

    expect(el.querySelector('.chip-button')!.textContent).toContain('1/2');
    await click('.chip-button');
    el.querySelectorAll<HTMLInputElement>('.sub input')[1].click();
    await fixture.whenStable();

    expect(toggled).toHaveBeenCalledWith({
      task: expect.objectContaining({ id: 7 }),
      subTask: expect.objectContaining({ id: 2, isCompleted: true }),
    });
  });

  it('offers to move the task to the other sections only', async () => {
    await render();
    const moved = vi.fn();
    fixture.componentInstance.move.subscribe(moved);

    await click('.menu-btn');
    const items = [...el.querySelectorAll<HTMLElement>('[role="menuitem"]')];
    expect(items.map((i) => i.textContent!.trim())).toEqual(['Work']);

    items[0].click();
    expect(moved).toHaveBeenCalledWith({ task: expect.objectContaining({ id: 7 }), sectionId: 2 });
  });

  it('exposes keyboard reordering with Alt+Arrow on the drag handle', async () => {
    // The CDK drag handle needs a drag parent, so verify the handler directly.
    await render({}, { reorderable: false });
    const reorder = vi.fn();
    fixture.componentInstance.reorderBy.subscribe(reorder);

    (fixture.componentInstance as unknown as { onHandleKey(e: KeyboardEvent): void }).onHandleKey(
      new KeyboardEvent('keydown', { key: 'ArrowDown', altKey: true }),
    );
    (fixture.componentInstance as unknown as { onHandleKey(e: KeyboardEvent): void }).onHandleKey(
      new KeyboardEvent('keydown', { key: 'ArrowUp' }),
    );

    expect(reorder).toHaveBeenCalledTimes(1);
    expect(reorder).toHaveBeenCalledWith({ task: expect.objectContaining({ id: 7 }), direction: 1 });
  });
});
