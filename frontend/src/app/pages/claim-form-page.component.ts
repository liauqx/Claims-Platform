import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { ClaimsApiService } from '../core/claims-api.service';
import { Claim, ClaimStatus, ClaimType, UserRole } from '../core/models';

@Component({
  selector: 'app-claim-form-page',
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './claim-form-page.component.html',
  styleUrl: './claim-form-page.component.css',
})
export class ClaimFormPageComponent implements OnInit {
  readonly claim = signal<Claim | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly isNew: boolean;
  readonly today = new Date().toISOString().slice(0, 10);
  readonly claimTypes: ClaimType[] = ['MOTOR', 'PROPERTY'];
  claimType: ClaimType = 'MOTOR';
  incidentDate = '';
  description = '';
  estimatedAmount: number | null = null;
  nextStatus: ClaimStatus | '' = '';
  approvedAmount: number | null = null;
  remarks = '';

  constructor(
    readonly auth: AuthService,
    private readonly api: ClaimsApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {
    this.isNew = !this.route.snapshot.paramMap.has('id');
  }

  get role(): UserRole | null {
    return this.auth.user()?.role ?? null;
  }

  get canEditClaimantDetails(): boolean {
    return this.role === 'CLAIMANT' && this.claim()?.status === 'SUBMITTED';
  }

  get canAssess(): boolean {
    const claim = this.claim();
    const user = this.auth.user();
    return !!claim && !!user &&
      (user.role === 'MANAGER' ||
        user.role === 'OFFICER' && claim.assignedOfficerId === user.id);
  }

  get availableStatuses(): ClaimStatus[] {
    switch (this.claim()?.status) {
      case 'SUBMITTED':
        return ['UNDER_REVIEW'];
      case 'UNDER_REVIEW':
        return ['APPROVED', 'REJECTED'];
      case 'MORE_INFO_REQUIRED':
        return ['UNDER_REVIEW', 'APPROVED', 'REJECTED'];
      case 'APPROVED':
        return ['SETTLED'];
      default:
        return [];
    }
  }

  ngOnInit(): void {
    if (this.isNew) {
      return;
    }

    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      void this.router.navigateByUrl('/claims');
      return;
    }

    this.loading.set(true);
    this.api.getClaim(id).subscribe({
      next: (claim) => {
        this.claim.set(claim);
        this.description = claim.description;
        this.estimatedAmount = claim.estimatedAmount;
        this.approvedAmount = claim.approvedAmount;
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(this.getError(error, 'Could not load this claim.'));
        this.loading.set(false);
      },
    });
  }

  submit(): void {
    this.error.set('');
    this.saving.set(true);

    if (this.isNew) {
      if (!this.incidentDate || this.incidentDate > this.today || !this.estimatedAmount) {
        this.error.set('Enter a valid incident date and estimated amount.');
        this.saving.set(false);
        return;
      }

      this.api.createClaim({
        claimType: this.claimType,
        incidentDate: this.incidentDate,
        description: this.description.trim(),
        estimatedAmount: this.estimatedAmount,
      }).subscribe({
        next: (claim) => void this.router.navigate(['/claims', claim.id]),
        error: (error: unknown) => {
          this.error.set(this.getError(error, 'Could not submit this claim.'));
          this.saving.set(false);
        },
        complete: () => this.saving.set(false),
      });
      return;
    }

    const claim = this.claim();
    if (!claim) {
      this.saving.set(false);
      return;
    }

    const input: {
      description?: string;
      estimatedAmount?: number;
      status?: ClaimStatus;
      approvedAmount?: number;
      remarks?: string;
    } = {};

    if (this.canEditClaimantDetails) {
      if (!this.description.trim() || !this.estimatedAmount) {
        this.error.set('Description and estimated amount are required.');
        this.saving.set(false);
        return;
      }

      input.description = this.description.trim();
      input.estimatedAmount = this.estimatedAmount;
      input.remarks = 'Claim details updated by claimant.';
    } else if (this.canAssess) {
      if (!this.nextStatus) {
        this.error.set('Choose a status to update this claim.');
        this.saving.set(false);
        return;
      }

      input.status = this.nextStatus;
      input.remarks = this.remarks.trim();
      if (this.nextStatus === 'APPROVED' || this.nextStatus === 'SETTLED') {
        if (!this.approvedAmount) {
          this.error.set('Enter the approved amount.');
          this.saving.set(false);
          return;
        }
        input.approvedAmount = this.approvedAmount;
      }
    } else {
      this.error.set('You do not have permission to edit this claim.');
      this.saving.set(false);
      return;
    }

    this.api.updateClaim(claim.id, input).subscribe({
      next: () => void this.router.navigate(['/claims', claim.id]),
      error: (error: unknown) => {
        this.error.set(this.getError(error, 'Could not update this claim.'));
        this.saving.set(false);
      },
      complete: () => this.saving.set(false),
    });
  }

  statusLabel(status: ClaimStatus): string {
    return status.replaceAll('_', ' ').toLowerCase().replace(/(^|\s)\S/g, (letter) => letter.toUpperCase());
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
