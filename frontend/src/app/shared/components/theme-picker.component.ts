import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { ThemeName } from '../../core/models/api.models';
import { ThemeService } from '../../core/services/theme.service';
import { IconComponent } from '../icons/icon.component';

/**
 * Radio-group of theme cards with a live preview swatch.
 * Each swatch carries its own `data-theme`, so it renders with that theme's real CSS variables
 * (no duplicated colour values here). Native radios give keyboard arrow navigation and screen reader support.
 */
@Component({
  selector: 'app-theme-picker',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset class="picker" [class.picker--compact]="compact()">
      <legend class="sr-only">Colour theme</legend>
      @for (option of themes.options; track option.id) {
        <label class="card-option" [class.is-selected]="themes.theme() === option.id">
          <input
            type="radio"
            class="sr-only"
            name="theme"
            [value]="option.id"
            [checked]="themes.theme() === option.id"
            (change)="choose(option.id)"
          />
          <span class="preview" [attr.data-theme]="option.id" aria-hidden="true">
            <span class="preview__sidebar"></span>
            <span class="preview__main">
              <span class="preview__bar preview__bar--title"></span>
              <span class="preview__bar"></span>
              <span class="preview__bar preview__bar--short"></span>
              <span class="preview__pill"></span>
            </span>
          </span>
          <span class="label">
            <span class="label__name">{{ option.label }}</span>
            @if (!compact()) {
              <span class="label__desc">{{ option.description }}</span>
            }
          </span>
          @if (themes.theme() === option.id) {
            <span class="check" aria-hidden="true"><app-icon name="check" [size]="14" /></span>
          }
        </label>
      }
    </fieldset>
  `,
  styleUrl: './theme-picker.component.scss',
})
export class ThemePickerComponent {
  protected readonly themes = inject(ThemeService);

  /** Smaller cards without descriptions, for use inside the dropdown menu. */
  readonly compact = input(false);

  protected choose(theme: ThemeName): void {
    this.themes.set(theme);
  }
}
