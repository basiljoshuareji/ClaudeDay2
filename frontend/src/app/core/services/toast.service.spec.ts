import { TestBed } from '@angular/core/testing';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    vi.useFakeTimers();
    service = TestBed.inject(ToastService);
  });

  afterEach(() => vi.useRealTimers());

  it('shows a toast and auto-dismisses it', () => {
    service.success('Saved');
    expect(service.toasts().map((t) => t.message)).toEqual(['Saved']);

    vi.advanceTimersByTime(4001);

    expect(service.toasts()).toHaveLength(0);
  });

  it('keeps errors on screen longer than successes', () => {
    service.error('Boom');
    vi.advanceTimersByTime(4001);
    expect(service.toasts()).toHaveLength(1);

    vi.advanceTimersByTime(3000);
    expect(service.toasts()).toHaveLength(0);
  });

  it('runs the action (e.g. Undo) and closes the toast', () => {
    const undo = vi.fn();
    service.show('Task deleted', { actionLabel: 'Undo', action: undo });

    service.runAction(service.toasts()[0]);

    expect(undo).toHaveBeenCalledOnce();
    expect(service.toasts()).toHaveLength(0);
  });

  it('never auto-dismisses when duration is 0, but can be dismissed manually', () => {
    const id = service.show('Sticky', { duration: 0 });
    vi.advanceTimersByTime(60_000);
    expect(service.toasts()).toHaveLength(1);

    service.dismiss(id);
    expect(service.toasts()).toHaveLength(0);
  });

  it('limits the stack to the four most recent toasts', () => {
    for (let i = 1; i <= 6; i++) service.info(`toast ${i}`, { duration: 0 });

    expect(service.toasts().map((t) => t.message)).toEqual(['toast 3', 'toast 4', 'toast 5', 'toast 6']);
  });
});
