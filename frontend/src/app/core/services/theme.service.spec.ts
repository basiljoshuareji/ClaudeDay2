import { TestBed } from '@angular/core/testing';
import { PreferencesSyncService } from './preferences-sync.service';
import { THEME_OPTIONS, ThemeService } from './theme.service';

function mockOsPreference(prefersDark: boolean): void {
  vi.stubGlobal(
    'matchMedia',
    vi.fn().mockReturnValue({ matches: prefersDark, addEventListener: vi.fn(), removeEventListener: vi.fn() }),
  );
  // ThemeService reads matchMedia through document.defaultView (the jsdom window).
  window.matchMedia = globalThis.matchMedia;
}

describe('ThemeService', () => {
  const sync = { push: vi.fn(), setBaseline: vi.fn() };

  function create(): ThemeService {
    TestBed.configureTestingModule({ providers: [{ provide: PreferencesSyncService, useValue: sync }] });
    return TestBed.inject(ThemeService);
  }

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
    sync.push.mockReset();
    mockOsPreference(false);
  });

  afterEach(() => vi.unstubAllGlobals());

  it('offers at least five themes including the required ones', () => {
    const ids = THEME_OPTIONS.map((t) => t.id);
    expect(ids.length).toBeGreaterThanOrEqual(5);
    expect(ids).toEqual(expect.arrayContaining(['light', 'dark', 'ocean', 'forest', 'sunset']));
  });

  it('follows the OS preference on first visit', () => {
    mockOsPreference(true);
    const service = create();

    expect(service.theme()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem('todo.theme')).toBeNull(); // not an explicit choice yet
  });

  it('prefers a saved choice over the OS preference', () => {
    mockOsPreference(true);
    localStorage.setItem('todo.theme', 'forest');

    expect(create().theme()).toBe('forest');
    expect(document.documentElement.getAttribute('data-theme')).toBe('forest');
  });

  it('ignores an unknown value in storage', () => {
    localStorage.setItem('todo.theme', 'hot-pink');
    expect(create().theme()).toBe('light');
  });

  it('switches at runtime: updates the DOM, localStorage and the server sync', () => {
    const service = create();

    service.set('ocean');

    expect(service.theme()).toBe('ocean');
    expect(document.documentElement.getAttribute('data-theme')).toBe('ocean');
    expect(localStorage.getItem('todo.theme')).toBe('ocean');
    expect(sync.push).toHaveBeenCalledWith({ theme: 'ocean' });
  });

  it('applies the server preference without echoing it back', () => {
    const service = create();

    service.hydrateFromServer('sunset');

    expect(service.theme()).toBe('sunset');
    expect(localStorage.getItem('todo.theme')).toBe('sunset');
    expect(sync.push).not.toHaveBeenCalled();
  });

  it('rejects theme names it does not know', () => {
    const service = create();

    service.set('neon' as never);

    expect(service.theme()).toBe('light');
    expect(sync.push).not.toHaveBeenCalled();
  });
});
