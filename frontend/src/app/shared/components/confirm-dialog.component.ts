import { A11yModule } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ConfirmService } from '../../core/services/confirm.service';
import { IconComponent } from '../icons/icon.component';

@Component({
  selector: 'app-confirm-dialog',
  imports: [A11yModule, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (confirm.request(); as req) {
      <div class="dialog-backdrop" (click)="onBackdrop($event)" (keydown.escape)="confirm.answer(false)">
        <div
          class="dialog dialog--sm"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="confirm-title"
          aria-describedby="confirm-message"
          cdkTrapFocus
          [cdkTrapFocusAutoCapture]="true"
        >
          <div class="dialog__header">
            <h2 id="confirm-title" class="title">
              @if (req.danger) {
                <app-icon name="alert" [size]="22" class="title__icon" />
              }
              {{ req.title }}
            </h2>
          </div>
          <div class="dialog__body">
            <p id="confirm-message">{{ req.message }}</p>
          </div>
          <div class="dialog__footer">
            <button type="button" class="btn" (click)="confirm.answer(false)">{{ req.cancelLabel ?? 'Cancel' }}</button>
            <button
              type="button"
              class="btn"
              [class.btn--danger]="req.danger"
              [class.btn--primary]="!req.danger"
              (click)="confirm.answer(true)"
            >
              {{ req.confirmLabel ?? 'Confirm' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: `
    .title {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .title__icon {
      color: var(--danger);
    }
  `,
})
export class ConfirmDialogComponent {
  protected readonly confirm = inject(ConfirmService);

  protected onBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.confirm.answer(false);
  }
}
