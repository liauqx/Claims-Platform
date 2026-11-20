export type UserRole = 'CLAIMANT' | 'OFFICER' | 'MANAGER';
export type ClaimType = 'MOTOR' | 'PROPERTY';
export type ClaimStatus =
  | 'SUBMITTED'
  | 'UNDER_REVIEW'
  | 'MORE_INFO_REQUIRED'
  | 'APPROVED'
  | 'REJECTED'
  | 'SETTLED';
export type InformationRequestStatus = 'PENDING' | 'RESPONDED' | 'CANCELLED';

export interface User {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface Claim {
  id: string;
  claimNumber: string;
  claimantId: string;
  assignedOfficerId: string | null;
  claimType: ClaimType;
  incidentDate: string;
  description: string;
  estimatedAmount: number;
  approvedAmount: number | null;
  status: ClaimStatus;
  createdAt: string;
  updatedAt: string;
}

export interface ClaimHistory {
  id: string;
  claimId: string;
  changedBy: string;
  oldStatus: ClaimStatus | null;
  newStatus: ClaimStatus;
  remarks: string | null;
  createdAt: string;
}

export interface ClaimDocument {
  id: string;
  claimId: string;
  fileName: string;
  fileUrl: string;
  uploadedAt: string;
}

export interface InformationRequest {
  id: string;
  claimId: string;
  requestedBy: string;
  description: string;
  response: string | null;
  status: InformationRequestStatus;
  createdAt: string;
  respondedAt: string | null;
}

export interface Notification {
  id: string;
  claimId: string | null;
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface Officer {
  id: string;
  fullName: string;
  email: string;
  role: 'OFFICER';
}
