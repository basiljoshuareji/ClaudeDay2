import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConfirmService } from '../../core/services/confirm.service';
import { ThemeService } from '../../core/services/theme.service';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import { ProgressBarComponent } from './progress-bar.component';
import { ThemePickerComponent } from './theme-picker.component';

describe('ThemePickerComponent', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ imports: [ThemePickerComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  it('renders a radio for every theme with a live preview', async () => {
    const fixture = TestBed.createComponent(ThemePickerComponent);
    await fixture.whenStable();
    const el: HTMLElement = fixture.nativeElement;

    const radios = el.querySelectorAll('input[type="radio"]');
    const previews = [...el.querySelectorAll('.preview')].map((p) => p.getAttribute('data-theme'));
    expect(radios).toHaveLength(5);
    expect(previews).toEqual(['light', 'dark', 'ocean', 'forest', 'sunset']);
  });

  it('switches the whole app theme when a card is chosen', async () => {
    const fixture = TestBed.createComponent(ThemePickerComponent);
    await fixture.whenStable();
    const themes = TestBed.inject(ThemeService);

    const ocean = fixture.nativeElement.querySelectorAll('input[type="radio"]')[2] as HTMLInputElement;
    ocean.click();
    await fixture.whenStable();

    expect(themes.theme()).toBe('ocean');
    expect(document.documentElement.getAttribute('data-theme')).toBe('ocean');
    expect(ocean.checked).toBe(true);
  });
});

describe('ConfirmDialogComponent', () => {
  it('resolves true on confirm, false on cancel or Escape', async () => {
    TestBed.configureTestingModule({ imports: [ConfirmDialogComponent] });
    const fixture = TestBed.createComponent(ConfirmDialogComponent);
    const service = TestBed.inject(ConfirmService);
    const el: HTMLElement = fixture.nativeElement;
    await fixture.whenStable();
    expect(el.querySelector('[role="alertdialog"]')).toBeNull();

    // Confirm
    const yes = service.confirm({ title: 'Delete?', message: 'Sure?', confirmLabel: 'Delete', danger: true });
    await fixture.whenStable();
    expect(el.querySelector('h2')!.textContent).toContain('Delete?');
    expect(el.querySelector('.btn--danger')!.textContent).toContain('Delete');
    el.querySelector<HTMLButtonElement>('.btn--danger')!.click();
    await expect(yes).resolves.toBe(true);

    // Cancel
    const no = service.confirm({ title: 'Delete?', message: 'Sure?' });
    await fixture.whenStable();
    [...el.querySelectorAll<HTMLButtonElement>('button')].find((b) => b.textContent!.includes('Cancel'))!.click();
    await expect(no).resolves.toBe(false);

    // Escape
    const esc = service.confirm({ title: 'Delete?', message: 'Sure?' });
    await fixture.whenStable();
    el.querySelector('.dialog-backdrop')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await expect(esc).resolves.toBe(false);
  });
});

describe('ProgressBarComponent', () => {
  it('exposes progress to assistive tech and clamps division by zero', async () => {
    TestBed.configureTestingModule({ imports: [ProgressBarComponent] });
    const fixture = TestBed.createComponent(ProgressBarComponent);
    fixture.componentRef.setInput('done', 1);
    fixture.componentRef.setInput('total', 4);
    fixture.componentRef.setInput('label', 'Home progress');
    await fixture.whenStable();

    const bar: HTMLElement = fixture.nativeElement.querySelector('[role="progressbar"]');
    expect(bar.getAttribute('aria-valuenow')).toBe('25');
    expect(bar.getAttribute('aria-label')).toBe('Home progress');

    fixture.componentRef.setInput('total', 0);
    await fixture.whenStable();
    expect(bar.getAttribute('aria-valuenow')).toBe('0');
  });
});
