export type AuthStackParamList = {
  Login: undefined;
  Register: undefined;
};

export type HomeStackParamList = {
  Dashboard: undefined;
  SubcontractorDetail: { id: string; name: string };
  AddDocument: { subcontractorId: string; name: string };
};

export type SubsStackParamList = {
  SubcontractorList: undefined;
  SubcontractorDetail: { id: string; name: string };
  AddSubcontractor: undefined;
  AddDocument: { subcontractorId: string; name: string };
  Directory: undefined;
};

export type ChaseStackParamList = {
  ChaseQueue: undefined;
  SubcontractorDetail: { id: string; name: string };
  AddDocument: { subcontractorId: string; name: string };
};

export type ReviewStackParamList = {
  ReviewQueue: undefined;
  SubcontractorDetail: { id: string; name: string };
  AddDocument: { subcontractorId: string; name: string };
};
