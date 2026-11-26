import { ChangeDetectionStrategy, Component, ElementRef, HostListener, inject, signal } from '@angular/core';
import { IconComponent } from '../icons/icon.component';
import { ThemePickerComponent } from './theme-picker.component';

/** Button that opens a small popover with the theme picker (used in the top bar and on the auth pages). */
@Component({
  selector: 'app-theme-menu',
  imports: [IconComponent, ThemePickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="btn btn--icon"
      aria-haspopup="true"
      aria-controls="theme-menu-panel"
      [attr.aria-expanded]="open()"
      aria-label="Change theme"
      title="Change theme"
      (click)="toggle()"
    >
      <app-icon name="palette" [size]="20" />
    </button>

    @if (open()) {
      <div id="theme-menu-panel" class="panel card fade-in" role="dialog" aria-label="Choose a theme">
        <h3>Theme</h3>
        <app-theme-picker [compact]="true" />
      </div>
    }
  `,
  styles: `
    :host {
      position: relative;
      display: inline-block;
    }

    .panel {
      position: absolute;
      right: 0;
      top: calc(100% + 8px);
      z-index: 150;
      width: min(300px, calc(100vw - 32px));
      padding: 14px;
      box-shadow: var(--shadow-lg);

      h3 {
        margin-bottom: 10px;
      }
    }
  `,
})
export class ThemeMenuComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((v) => !v);
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  @HostListener('keydown.escape')
  protected onEscape(): void {
    if (this.open()) {
      this.open.set(false);
      this.host.nativeElement.querySelector('button')?.focus();
    }
  }
}
