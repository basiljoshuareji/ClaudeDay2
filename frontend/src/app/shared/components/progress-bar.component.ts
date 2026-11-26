import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'app-progress-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="progress"
      role="progressbar"
      aria-valuemin="0"
      aria-valuemax="100"
      [attr.aria-valuenow]="percent()"
      [attr.aria-label]="label()"
    >
      <div class="progress__bar" [style.width.%]="percent()" [style.background]="color() || null"></div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class ProgressBarComponent {
  /** Completed items. */
  readonly done = input(0);
  readonly total = input(0);
  readonly label = input('Progress');
  /** Optional accent colour (e.g. the section colour). Defaults to the theme primary. */
  readonly color = input<string>('');

  protected readonly percent = computed(() => {
    const total = this.total();
    return total > 0 ? Math.round((this.done() / total) * 100) : 0;
  });
}
