import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../../core/services/toast.service';
import { IconComponent } from '../icons/icon.component';

@Component({
  selector: 'app-toast-container',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toasts" role="region" aria-label="Notifications" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast fade-in" [class]="'toast toast--' + toast.type" [attr.role]="toast.type === 'error' ? 'alert' : 'status'">
          <app-icon class="toast__icon" [name]="toast.type === 'error' ? 'alert' : toast.type === 'success' ? 'check-circle' : 'info'" [size]="20" />
          <span class="toast__message">{{ toast.message }}</span>
          @if (toast.actionLabel) {
            <button type="button" class="toast__action" (click)="toasts.runAction(toast)">{{ toast.actionLabel }}</button>
          }
          <button type="button" class="toast__close" aria-label="Dismiss notification" (click)="toasts.dismiss(toast.id)">
            <app-icon name="x" [size]="16" />
          </button>
        </div>
      }
    </div>
  `,
  styleUrl: './toast-container.component.scss',
})
export class ToastContainerComponent {
  protected readonly toasts = inject(ToastService);
}
