import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { IconComponent } from '../icons/icon.component';

@Component({
  selector: 'app-empty-state',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty fade-in">
      <div class="empty__icon"><app-icon [name]="icon()" [size]="32" /></div>
      <h2>{{ title() }}</h2>
      <p>{{ message() }}</p>
      <div class="empty__actions"><ng-content /></div>
    </div>
  `,
  styles: `
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 48px 24px;
      text-align: center;
      color: var(--text-muted);
      border: 2px dashed var(--border);
      border-radius: 16px;
      background: var(--surface);
    }

    .empty__icon {
      display: grid;
      place-items: center;
      width: 64px;
      height: 64px;
      border-radius: 50%;
      background: var(--primary-soft);
      color: var(--primary);
      margin-bottom: 6px;
    }

    h2 {
      color: var(--text);
    }

    p {
      max-width: 40ch;
    }

    .empty__actions {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
      justify-content: center;
      margin-top: 10px;
    }
  `,
})
export class EmptyStateComponent {
  readonly icon = input('inbox');
  readonly title = input.required<string>();
  readonly message = input('');
}
