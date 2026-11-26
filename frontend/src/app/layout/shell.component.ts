import { ChangeDetectionStrategy, Component, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../core/services/auth.service';
import { LayoutService } from '../core/services/layout.service';
import { SectionDialogService } from '../features/sections/section-dialog.service';
import { SectionFormDialogComponent } from '../features/sections/section-form-dialog.component';
import { SectionsStore } from '../features/sections/sections.store';
import { TagsStore } from '../features/tasks/tags.store';
import { ConfirmDialogComponent } from '../shared/components/confirm-dialog.component';
import { ThemeMenuComponent } from '../shared/components/theme-menu.component';
import { ToastContainerComponent } from '../shared/components/toast-container.component';
import { IconComponent } from '../shared/icons/icon.component';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    IconComponent,
    ThemeMenuComponent,
    ToastContainerComponent,
    ConfirmDialogComponent,
    SectionFormDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent implements OnDestroy {
  protected readonly auth = inject(AuthService);
  protected readonly layout = inject(LayoutService);
  protected readonly sections = inject(SectionsStore);
  protected readonly sectionDialog = inject(SectionDialogService);
  private readonly tags = inject(TagsStore);
  private readonly router = inject(Router);

  constructor() {
    this.sections.refresh();
    this.tags.refresh();

    // The mobile drawer closes after navigating.
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.layout.closeMobile());
  }

  protected get initial(): string {
    return (this.auth.user()?.displayName.trim()[0] ?? '?').toUpperCase();
  }

  /** Narrow helper so the template can read the section being edited. */
  protected editingSection() {
    const state = this.sectionDialog.state();
    return state?.mode === 'edit' ? state.section : null;
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.layout.mobileOpen()) this.layout.closeMobile();
  }

  ngOnDestroy(): void {
    // Don't leak one user's lists into the next session.
    this.sections.clear();
    this.tags.clear();
  }
}
