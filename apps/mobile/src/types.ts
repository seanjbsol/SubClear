export type MembershipRole = 'owner' | 'admin' | 'contractsManager' | 'viewer';
export type ComplianceLight = 'green' | 'amber' | 'red';
export type SubcontractorStatus = 'active' | 'onHold' | 'inactive';
export type DocumentType =
  | 'employersLiability'
  | 'publicLiability'
  | 'professionalIndemnity'
  | 'ssip'
  | 'rams'
  | 'cscs'
  | 'other';
export type ChaseOutcome =
  | 'leftVoicemail'
  | 'emailSent'
  | 'documentsReceived'
  | 'noAnswer'
  | 'callbackArranged'
  | 'escalated'
  | 'other';

export type MeResponse = {
  userId: string;
  tenantId: string;
  organisationName: string;
  fullName: string;
  email: string;
  role: MembershipRole;
};

export type AuthResponse = {
  token: string;
  expiresAt: string;
  user: MeResponse;
};

export type SubcontractorSummary = {
  id: string;
  name: string;
  tradingName?: string | null;
  contactName?: string | null;
  email?: string | null;
  phone?: string | null;
  companyNumber?: string | null;
  status: SubcontractorStatus;
  notes?: string | null;
  compliance: ComplianceLight;
  nextExpiry?: string | null;
  documentCount: number;
  lastChasedOn?: string | null;
};

export type DocumentDto = {
  id: string;
  subcontractorId: string;
  type: DocumentType;
  typeLabel: string;
  title: string;
  expiryDate?: string | null;
  fileName?: string | null;
  contentType?: string | null;
  fileSizeBytes?: number | null;
  notes?: string | null;
  isManuallyExpired: boolean;
  light: ComplianceLight;
};

export type ChaseLogDto = {
  id: string;
  subcontractorId: string;
  subcontractorName: string;
  chaseDate: string;
  note: string;
  outcome: ChaseOutcome;
  createdByName: string;
  createdAt: string;
};

export type SubcontractorDetail = SubcontractorSummary & {
  documents: DocumentDto[];
  recentChases: ChaseLogDto[];
};

export type DashboardDto = {
  totalSubcontractors: number;
  nonCompliantCount: number;
  expiringWithin30Days: number;
  compliantCount: number;
  chaseQueueCount: number;
  attention: SubcontractorSummary[];
};

export type ChaseQueueItem = {
  subcontractorId: string;
  name: string;
  contactName?: string | null;
  email?: string | null;
  phone?: string | null;
  compliance: ComplianceLight;
  nextExpiry?: string | null;
  lastChasedOn?: string | null;
  lastChaseNote?: string | null;
  issues: string[];
};

export type PackItem = {
  type: DocumentType;
  label: string;
  required: boolean;
  light: ComplianceLight;
  expiryDate?: string | null;
  missing: boolean;
  expired: boolean;
  documentId?: string | null;
};

export type PackDto = {
  subcontractorId: string;
  subcontractorName: string;
  overall: ComplianceLight;
  items: PackItem[];
};

export type EntitlementsDto = {
  productCode: string;
  tenantId: string;
  status: string;
  plan?: string | null;
  planName?: string | null;
  currentPeriodEnd?: string | null;
  isEntitled: boolean;
};

export type BillingSessionDto = {
  url: string;
  sessionId?: string | null;
};
