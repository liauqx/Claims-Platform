import { Component, OnInit, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './core/auth.service';
import { ClaimsApiService } from './core/claims-api.service';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent implements OnInit {
  readonly unreadCount = signal(0);

  constructor(
    readonly auth: AuthService,
    private readonly api: ClaimsApiService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      if (this.auth.user()) {
        this.refreshUnreadCount();
      }
    });

    if (this.auth.user()) {
      this.refreshUnreadCount();
    }
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/auth');
  }

  private refreshUnreadCount(): void {
    this.api.unreadNotificationCount().subscribe({
      next: (response) => this.unreadCount.set(response.count),
      error: (error: unknown) => console.error('Could not load notification count.', error),
    });
  }
}
