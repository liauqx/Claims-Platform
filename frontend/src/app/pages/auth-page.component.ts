import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-auth-page',
  imports: [FormsModule],
  templateUrl: './auth-page.component.html',
  styleUrl: './auth-page.component.css',
})
export class AuthPageComponent {
  readonly registering = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  fullName = '';
  email = '';
  password = '';

  constructor(private readonly auth: AuthService, private readonly router: Router) {}

  toggleMode(): void {
    this.registering.update((value) => !value);
    this.error.set('');
  }

  submit(): void {
    this.error.set('');
    this.busy.set(true);
    const request = this.registering()
      ? this.auth.register(this.fullName.trim(), this.email.trim(), this.password)
      : this.auth.login(this.email.trim(), this.password);

    request.subscribe({
      next: () => void this.router.navigateByUrl('/claims'),
      error: (error: unknown) => {
        this.error.set(
          this.registering()
            ? this.getError(error, 'Could not create your account.')
            : this.getError(error, 'The email or password is incorrect.'),
        );
        this.busy.set(false);
      },
      complete: () => this.busy.set(false),
    });
  }

  private getError(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const body = error.error;
      if (typeof body === 'object' && body !== null && 'title' in body && typeof body.title === 'string') {
        return body.title;
      }
    }

    return fallback;
  }
}
