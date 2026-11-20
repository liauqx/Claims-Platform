import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Claim,
  ClaimDocument,
  ClaimHistory,
  ClaimStatus,
  InformationRequest,
  Notification,
  Officer,
  PagedResponse,
} from './models';

@Injectable({ providedIn: 'root' })
export class ClaimsApiService {
  private readonly api = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  listClaims(page = 1): Observable<PagedResponse<Claim>> {
    return this.http.get<PagedResponse<Claim>>(`${this.api}/claims`, {
      params: new HttpParams().set('page', page).set('pageSize', 50),
    });
  }

  getClaim(id: string): Observable<Claim> {
    return this.http.get<Claim>(`${this.api}/claims/${id}`);
  }

  createClaim(input: {
    claimType: 'MOTOR' | 'PROPERTY';
    incidentDate: string;
    description: string;
    estimatedAmount: number;
  }): Observable<Claim> {
    return this.http.post<Claim>(`${this.api}/claims`, input);
  }

  updateClaim(
    id: string,
    input: {
      description?: string;
      estimatedAmount?: number;
      status?: ClaimStatus;
      approvedAmount?: number;
      remarks?: string;
    },
  ): Observable<Claim> {
    return this.http.patch<Claim>(`${this.api}/claims/${id}`, input);
  }

  deleteClaim(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/claims/${id}`);
  }

  assignToMe(id: string): Observable<Claim> {
    return this.http.post<Claim>(`${this.api}/claims/${id}/assign-to-me`, {});
  }

  assignOfficer(id: string, officerId: string): Observable<Claim> {
    return this.http.put<Claim>(`${this.api}/claims/${id}/assignment`, { officerId });
  }

  listOfficers(): Observable<Officer[]> {
    return this.http.get<Officer[]>(`${this.api}/users/officers`);
  }

  listHistory(claimId: string): Observable<ClaimHistory[]> {
    return this.http.get<ClaimHistory[]>(`${this.api}/claims/${claimId}/history`);
  }

  listDocuments(claimId: string): Observable<ClaimDocument[]> {
    return this.http.get<ClaimDocument[]>(`${this.api}/claims/${claimId}/documents`);
  }

  uploadDocument(claimId: string, file: File): Observable<ClaimDocument> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ClaimDocument>(`${this.api}/claims/${claimId}/documents`, formData);
  }

  downloadDocument(claimId: string, documentId: string): Observable<Blob> {
    return this.http.get(`${this.api}/claims/${claimId}/documents/${documentId}/content`, {
      responseType: 'blob',
    });
  }

  deleteDocument(claimId: string, documentId: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/claims/${claimId}/documents/${documentId}`);
  }

  listInformationRequests(claimId: string): Observable<InformationRequest[]> {
    return this.http.get<InformationRequest[]>(`${this.api}/claims/${claimId}/information-requests`);
  }

  createInformationRequest(claimId: string, description: string): Observable<InformationRequest> {
    return this.http.post<InformationRequest>(
      `${this.api}/claims/${claimId}/information-requests`,
      { description },
    );
  }

  respondToInformationRequest(
    claimId: string,
    requestId: string,
    response: string,
  ): Observable<InformationRequest> {
    return this.http.post<InformationRequest>(
      `${this.api}/claims/${claimId}/information-requests/${requestId}/respond`,
      { response },
    );
  }

  listNotifications(page = 1): Observable<PagedResponse<Notification>> {
    return this.http.get<PagedResponse<Notification>>(`${this.api}/notifications`, {
      params: new HttpParams().set('page', page).set('pageSize', 50),
    });
  }

  unreadNotificationCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.api}/notifications/unread-count`);
  }

  markNotificationRead(id: string): Observable<Notification> {
    return this.http.patch<Notification>(`${this.api}/notifications/${id}/read`, {});
  }
}
