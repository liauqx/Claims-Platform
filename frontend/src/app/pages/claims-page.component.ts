import { Component, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe, SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { ClaimsApiService } from '../core/claims-api.service';
import { Claim, ClaimStatus, Officer, UserRole } from '../core/models';

@Component({
  selector: 'app-claims-page',
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink, SlicePipe],
  templateUrl: './claims-page.component.html',
  styleUrl: './claims-page.component.css',
})
export class ClaimsPageComponent implements OnInit {
  readonly claims = signal<Claim[]>([]);
  readonly officers = signal<Officer[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly success = signal('');
  search = '';
  statusFilter: ClaimStatus | '' = '';
  page = 1;
  totalCount = 0;
  readonly pageSize = 50;
  readonly assignments: Record<string, string> = {};

  constructor(readonly auth: AuthService, private readonly api: ClaimsApiService) {}

  get role(): UserRole | null {
    return this.auth.user()?.role ?? null;
  }

  get filteredClaims(): Claim[] {
    const term = this.search.trim().toLowerCase();
    return this.claims().filter((claim) => {
      const matchesSearch =
        !term ||
        claim.claimNumber.toLowerCase().includes(term) ||
        claim.description.toLowerCase().includes(term);
      return matchesSearch && (!this.statusFilter || claim.status === this.statusFilter);
    });
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  ngOnInit(): void {
    this.loadClaims();
    if (this.role === 'MANAGER') {
      this.api.listOfficers().subscribe({
        next: (officers) => this.officers.set(officers),
        error: (error: unknown) => this.error.set(this.getError(error, 'Could not load claims officers.')),
      });
    }
  }

  loadClaims(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.listClaims(this.page).subscribe({
      next: (response) => {
        this.claims.set(response.items);
        this.totalCount = response.totalCount;
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(this.getError(error, 'Could not load claims.'));
        this.loading.set(false);
      },
    });
  }

  setPage(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.page = page;
      this.loadClaims();
    }
  }

  deleteClaim(claim: Claim): void {
    if (!window.confirm(`Delete claim ${claim.claimNumber}? This cannot be undone.`)) {
      return;
    }

    this.error.set('');
    this.api.deleteClaim(claim.id).subscribe({
      next: () => {
        this.success.set(`Claim ${claim.claimNumber} was deleted.`);
        this.loadClaims();
      },
      error: (error: unknown) => this.error.set(this.getError(error, 'Could not delete this claim.')),
    });
  }

  pickUpClaim(claim: Claim): void {
    this.error.set('');
    this.api.assignToMe(claim.id).subscribe({
      next: () => {
        this.success.set(`Claim ${claim.claimNumber} is now assigned to you.`);
        this.loadClaims();
      },
      error: (error: unknown) => this.error.set(this.getError(error, 'Could not assign this claim.')),
    });
  }

  assignClaim(claim: Claim): void {
    const officerId = this.assignments[claim.id];
    if (!officerId) {
      this.error.set('Select an officer before assigning the claim.');
      return;
    }

    this.error.set('');
    this.api.assignOfficer(claim.id, officerId).subscribe({
      next: () => {
        this.success.set(`Claim ${claim.claimNumber} was assigned.`);
        this.loadClaims();
      },
      error: (error: unknown) => this.error.set(this.getError(error, 'Could not assign this claim.')),
    });
  }

  statusLabel(status: ClaimStatus): string {
    return status.replaceAll('_', ' ').toLowerCase().replace(/(^|\s)\S/g, (letter) => letter.toUpperCase());
  }

  isClosed(status: ClaimStatus): boolean {
    return status === 'APPROVED' || status === 'REJECTED' || status === 'SETTLED';
  }

  isOpen(claim: Claim): boolean {
    return claim.status === 'SUBMITTED' ||
      claim.status === 'UNDER_REVIEW' ||
      claim.status === 'MORE_INFO_REQUIRED';
  }

  isResolved(claim: Claim): boolean {
    return this.isClosed(claim.status);
  }

  clearMessages(): void {
    this.success.set('');
    this.error.set('');
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
