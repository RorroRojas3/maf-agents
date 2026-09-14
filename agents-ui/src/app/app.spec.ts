import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { patchState } from '@ngrx/signals';
import { unprotected } from '@ngrx/signals/testing';
import { AuthStore } from '@core/auth/auth-store';
import { createFakeMsal, FakeMsal, provideAuthTesting } from '@testing/msal';
import { App } from './app';

describe('App', () => {
  let msal: FakeMsal;

  beforeEach(async () => {
    localStorage.clear();
    sessionStorage.clear();
    msal = createFakeMsal();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideAuthTesting(msal)],
    }).compileComponents();
  });

  it('renders the brand in the navbar', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const brand = (fixture.nativeElement as HTMLElement).querySelector('.navbar-brand');
    expect(brand?.textContent).toContain('Andes Agents');
  });

  it('shows a failed sign-in in place of the page, with a retry that redirects again', async () => {
    patchState(unprotected(TestBed.inject(AuthStore)), {
      status: 'failed',
      errorCode: 'access_denied',
    });
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const alert = host.querySelector('[role="alert"]');

    expect(alert?.textContent).toContain('access_denied');
    expect(host.querySelector('router-outlet')).toBeNull();

    alert?.querySelector('button')?.click();

    expect(msal.loginRedirect).toHaveBeenCalledTimes(1);
  });
});
