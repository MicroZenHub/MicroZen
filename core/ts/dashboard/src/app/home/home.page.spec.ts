import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HomePage } from './home.page';

/**
 * REFERENCE EXAMPLE — Angular/Jest unit test.
 *
 * Copy this shape when adding unit tests for `core-dashboard`. It uses the
 * existing `jest-preset-angular` setup (see `src/test-setup.ts`) and runs via
 * `npx nx test core-dashboard`.
 *
 * Key conventions demonstrated here:
 * - `TestBed` configures a throwaway Angular module around the unit under test.
 * - `CUSTOM_ELEMENTS_SCHEMA` lets Ionic's `ion-*` web components render without
 *   importing the whole Ionic module (the template uses `ion-header`, etc.).
 * - Each test follows an Arrange / Act / Assert structure for readability.
 */
describe('HomePage', () => {
  let fixture: ComponentFixture<HomePage>;
  let component: HomePage;

  // Arrange (shared): compile the component once before each test so every
  // case starts from a clean, fully initialised fixture.
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HomePage],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(HomePage);
    component = fixture.componentInstance;
  });

  it('should create the component', () => {
    // Act: trigger change detection to render the template.
    fixture.detectChanges();

    // Assert: the component instance exists and rendered successfully.
    expect(component).toBeTruthy();
  });

  it('should render the welcome heading', () => {
    // Act: render the template so its DOM is available for querying.
    fixture.detectChanges();

    // Assert: the static welcome copy is present in the rendered output.
    const heading: HTMLElement | null =
      fixture.nativeElement.querySelector('#container h1');
    expect(heading?.textContent).toContain('Welcome core-dashboard!');
  });
});
