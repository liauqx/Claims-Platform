import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ClaimsApiService } from '../core/claims-api.service';
import { Notification } from '../core/models';

@Component({
  selector: 'app-notifications-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './notifications-page.component.html',
  styleUrl: './notifications-page.component.css',
})
export class NotificationsPageComponent implements OnInit {
  readonly notifications = signal<Notification[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');

  constructor(private readonly api: ClaimsApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listNotifications().subscribe({
      next: (response) => { this.notifications.set(response.items); this.loading.set(false); },
      error: (error: unknown) => { this.error.set(this.getError(error, 'Could not load notifications.')); this.loading.set(false); },
    });
  }

  markRead(notification: Notification): void {
    if (notification.isRead) return;
    this.api.markNotificationRead(notification.id).subscribe({
      next: (updated) => this.notifications.update((items) => items.map((item) => item.id === updated.id ? updated : item)),
      error: (error: unknown) => this.error.set(this.getError(error, 'Could not update this notification.')),
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
