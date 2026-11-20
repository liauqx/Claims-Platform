import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ClaimsApiService } from '../core/claims-api.service';
import { Claim } from '../core/models';

@Component({
  selector: 'app-information-request-page',
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './information-request-page.component.html',
  styleUrl: './information-request-page.component.css',
})
export class InformationRequestPageComponent implements OnInit {
  readonly claim = signal<Claim | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  description = '';

  constructor(
    private readonly api: ClaimsApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      void this.router.navigateByUrl('/claims');
      return;
    }

    this.api.getClaim(id).subscribe({
      next: (claim) => { this.claim.set(claim); this.loading.set(false); },
      error: (error: unknown) => { this.error.set(this.getError(error, 'Could not load this claim.')); this.loading.set(false); },
    });
  }

  submit(): void {
    const claim = this.claim();
    const description = this.description.trim();
    if (!claim || !description) return;

    this.error.set('');
    this.saving.set(true);
    this.api.createInformationRequest(claim.id, description).subscribe({
      next: () => void this.router.navigate(['/claims', claim.id]),
      error: (error: unknown) => { this.error.set(this.getError(error, 'Could not send this request.')); this.saving.set(false); },
      complete: () => this.saving.set(false),
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
