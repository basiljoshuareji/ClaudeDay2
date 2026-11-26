import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { errorMessage } from '../../core/errors/api-exception';
import { TagListItem } from '../../core/models/api.models';
import { AuthService } from '../../core/services/auth.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { ToastService } from '../../core/services/toast.service';
import { ThemePickerComponent } from '../../shared/components/theme-picker.component';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { IconComponent } from '../../shared/icons/icon.component';
import { TAG_COLORS, TagsStore } from '../tasks/tags.store';

@Component({
  selector: 'app-settings-page',
  imports: [ThemePickerComponent, IconComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings.page.html',
  styleUrl: './settings.page.scss',
})
export class SettingsPageComponent {
  protected readonly auth = inject(AuthService);
  protected readonly tagsStore = inject(TagsStore);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);

  protected readonly newTagColor = signal<string>(TAG_COLORS[6]);
  protected readonly newTagError = signal<string | null>(null);
  protected readonly colors = TAG_COLORS;

  constructor() {
    inject(Title).setTitle('Settings · Todo');
    this.tagsStore.refresh();
  }

  protected addTag(input: HTMLInputElement, event: Event): void {
    event.preventDefault();
    const name = input.value.trim();
    this.newTagError.set(null);
    if (!name) {
      this.newTagError.set('Enter a tag name.');
      return;
    }

    this.tagsStore.create(name, this.newTagColor()).subscribe({
      next: () => {
        input.value = '';
        this.toast.success(`Tag “${name}” created.`);
      },
      error: (error: unknown) => this.newTagError.set(errorMessage(error, 'Could not create the tag.')),
    });
  }

  protected rename(tag: TagListItem, input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!name || name === tag.name) {
      input.value = tag.name;
      return;
    }
    this.tagsStore.update(tag.id, name, tag.color).subscribe({
      error: (error: unknown) => {
        input.value = tag.name;
        this.toast.error(errorMessage(error, 'Could not rename the tag.'));
      },
    });
  }

  protected recolor(tag: TagListItem, color: string): void {
    this.tagsStore.update(tag.id, tag.name, color).subscribe({
      error: (error: unknown) => this.toast.error(errorMessage(error, 'Could not change the colour.')),
    });
  }

  protected async remove(tag: TagListItem): Promise<void> {
    const confirmed = await this.confirm.confirm({
      title: `Delete the “${tag.name}” tag?`,
      message:
        tag.taskCount > 0
          ? `It will be removed from ${tag.taskCount} task${tag.taskCount === 1 ? '' : 's'}. The tasks themselves are kept.`
          : 'No tasks use this tag.',
      confirmLabel: 'Delete tag',
      danger: true,
    });
    if (!confirmed) return;

    this.tagsStore.delete(tag.id).subscribe({
      next: () => this.toast.success(`Tag “${tag.name}” deleted.`),
      error: (error: unknown) => this.toast.error(errorMessage(error, 'Could not delete the tag.')),
    });
  }
}
