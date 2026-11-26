import { A11yModule } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiException, errorMessage } from '../../core/errors/api-exception';
import { Section } from '../../core/models/api.models';
import { ToastService } from '../../core/services/toast.service';
import { IconComponent, SECTION_ICON_NAMES } from '../../shared/icons/icon.component';
import { SectionDialogService } from './section-dialog.service';
import { SectionsStore } from './sections.store';

/** User-pickable section colours (data colours, not UI theme colours). */
export const SECTION_COLORS: readonly string[] = [
  '#3b82f6',
  '#8b5cf6',
  '#ec4899',
  '#ef4444',
  '#f97316',
  '#eab308',
  '#22c55e',
  '#14b8a6',
  '#06b6d4',
  '#64748b',
];

@Component({
  selector: 'app-section-form-dialog',
  imports: [A11yModule, ReactiveFormsModule, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './section-form-dialog.component.html',
  styleUrl: './section-form-dialog.component.scss',
})
export class SectionFormDialogComponent implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly store = inject(SectionsStore);
  private readonly dialog = inject(SectionDialogService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  /** The section being edited, or null when creating a new one. */
  readonly section = input<Section | null>(null);

  protected readonly colors = SECTION_COLORS;
  protected readonly icons = SECTION_ICON_NAMES;

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)]],
    color: [SECTION_COLORS[0], [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
    icon: [SECTION_ICON_NAMES[0], [Validators.required]],
  });

  protected readonly saving = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly nameServerError = signal<string | null>(null);

  // Mirrors form values as signals so the live preview updates under zoneless change detection.
  protected readonly previewName = signal('');
  protected readonly previewColor = signal<string>(SECTION_COLORS[0]);
  protected readonly previewIcon = signal<string>(SECTION_ICON_NAMES[0]);

  protected readonly isEdit = computed(() => this.section() !== null);

  ngOnInit(): void {
    const section = this.section();
    if (section) {
      this.form.setValue({ name: section.name, color: section.color, icon: section.icon });
    }
    this.syncPreview();
  }

  protected get name() {
    return this.form.controls.name;
  }

  protected pick(field: 'color' | 'icon', value: string): void {
    this.form.controls[field].setValue(value);
    this.form.controls[field].markAsDirty();
    this.syncPreview();
  }

  protected onNameInput(): void {
    this.nameServerError.set(null);
    this.syncPreview();
  }

  protected close(): void {
    this.dialog.close();
  }

  protected onBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.close();
  }

  protected save(): void {
    this.serverError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = { ...this.form.getRawValue(), name: this.form.getRawValue().name.trim() };
    const existing = this.section();
    this.saving.set(true);

    const request = existing ? this.store.update(existing.id, payload) : this.store.create(payload);
    request.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.toast.success(existing ? 'Section updated.' : `Section “${saved.name}” created.`);
        this.dialog.close();
        if (!existing) void this.router.navigate(['/sections', saved.id]);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        if (error instanceof ApiException && error.fieldError('name')) {
          this.nameServerError.set(error.fieldError('name'));
        } else {
          this.serverError.set(errorMessage(error, 'Could not save the section.'));
        }
      },
    });
  }

  private syncPreview(): void {
    const { name, color, icon } = this.form.getRawValue();
    this.previewName.set(name);
    this.previewColor.set(color);
    this.previewIcon.set(icon);
  }
}
