import { Component, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe, SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { ClaimsApiService } from '../core/claims-api.service';
import { Claim, ClaimDocument, ClaimHistory, ClaimStatus, InformationRequest, UserRole } from '../core/models';

@Component({
  selector: 'app-claim-detail-page',
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink, SlicePipe],
  templateUrl: './claim-detail-page.component.html',
  styleUrl: './claim-detail-page.component.css',
})
export class ClaimDetailPageComponent implements OnInit {
  readonly claim = signal<Claim | null>(null);
  readonly history = signal<ClaimHistory[]>([]);
  readonly documents = signal<ClaimDocument[]>([]);
  readonly requests = signal<InformationRequest[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly responseDrafts: Record<string, string> = {};

  constructor(
    readonly auth: AuthService,
    private readonly api: ClaimsApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {}

  get role(): UserRole | null {
    return this.auth.user()?.role ?? null;
  }

  get isClaimant(): boolean {
    return this.role === 'CLAIMANT';
  }

  get canStaffWork(): boolean {
    const claim = this.claim();
    const user = this.auth.user();
    return !!claim && !!user &&
      (user.role === 'MANAGER' ||
        user.role === 'OFFICER' && claim.assignedOfficerId === user.id);
  }

  get canUploadDocuments(): boolean {
    return this.isClaimant &&
      (this.claim()?.status === 'SUBMITTED' || this.claim()?.status === 'MORE_INFO_REQUIRED');
  }

  get canDeleteDocuments(): boolean {
    return this.role === 'MANAGER' ||
      this.canUploadDocuments && this.claim()?.claimantId === this.auth.user()?.id;
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const claimId = this.route.snapshot.paramMap.get('id');
    if (!claimId) {
      void this.router.navigateByUrl('/claims');
      return;
    }

    this.loading.set(true);
    this.error.set('');
    forkJoin({
      claim: this.api.getClaim(claimId),
      history: this.api.listHistory(claimId),
      documents: this.api.listDocuments(claimId),
      requests: this.api.listInformationRequests(claimId),
    }).subscribe({
      next: (result) => {
        this.claim.set(result.claim);
        this.history.set(result.history);
        this.documents.set(result.documents);
        this.requests.set(result.requests);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(this.getError(error, 'Could not load this claim.'));
        this.loading.set(false);
      },
    });
  }

  pickUpClaim(): void {
    const claim = this.claim();
    if (!claim) return;
    this.busy.set(true);
    this.api.assignToMe(claim.id).subscribe({
      next: () => { this.success.set('Claim assigned to you.'); this.load(); },
      error: (error: unknown) => { this.error.set(this.getError(error, 'Could not assign this claim.')); this.busy.set(false); },
      complete: () => this.busy.set(false),
    });
  }

  submitResponse(request: InformationRequest): void {
    const response = this.responseDrafts[request.id]?.trim();
    const claim = this.claim();
    if (!response || !claim) return;

    this.error.set('');
    this.api.respondToInformationRequest(claim.id, request.id, response).subscribe({
      next: () => {
        this.responseDrafts[request.id] = '';
        this.success.set('Your response was sent to the claims team.');
        this.load();
      },
      error: (error: unknown) => this.error.set(this.getError(error, 'Could not send your response.')),
    });
  }

  hasResponse(requestId: string): boolean {
    return !!this.responseDrafts[requestId]?.trim();
  }

  uploadDocument(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const claim = this.claim();
    if (!file || !claim) return;

    this.error.set('');
    this.success.set('');
    this.api.uploadDocument(claim.id, file).subscribe({
      next: () => { this.success.set('Document uploaded.'); this.load(); },
      error: (error: unknown) => this.error.set(this.getError(error, 'Could not upload this document.')),
    });
    input.value = '';
  }

  downloadDocument(claimDocument: ClaimDocument): void {
    this.api.downloadDocument(claimDocument.claimId, claimDocument.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = claimDocument.fileName;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: (error: unknown) => this.error.set(this.getError(error, 'Could not download this document.')),
    });
  }

  deleteDocument(document: ClaimDocument): void {
    if (!window.confirm(`Delete ${document.fileName}?`)) return;
    this.api.deleteDocument(document.claimId, document.id).subscribe({
      next: () => { this.success.set('Document deleted.'); this.load(); },
      error: (error: unknown) => this.error.set(this.getError(error, 'Could not delete this document.')),
    });
  }

  statusLabel(status: ClaimStatus): string {
    return status.replaceAll('_', ' ').toLowerCase().replace(/(^|\s)\S/g, (letter) => letter.toUpperCase());
  }

  requestStatusLabel(status: InformationRequest['status']): string {
    return status.toLowerCase().replace(/(^|\s)\S/g, (letter) => letter.toUpperCase());
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
