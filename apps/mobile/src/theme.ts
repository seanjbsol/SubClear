export const colours = {
  navy: '#0B1F3A',
  navyMid: '#16345C',
  ink: '#122033',
  muted: '#5C6B7A',
  line: '#D9E1EA',
  paper: '#F4F6F8',
  white: '#FFFFFF',
  amber: '#C47B00',
  amberSoft: '#FFF4DE',
  red: '#B42318',
  redSoft: '#FDECEC',
  green: '#1F7A4D',
  greenSoft: '#E7F6EE',
  gold: '#D4A017'
};

export function lightColour(light: string): string {
  switch (light) {
    case 'green':
      return colours.green;
    case 'amber':
      return colours.amber;
    default:
      return colours.red;
  }
}

export function lightBackground(light: string): string {
  switch (light) {
    case 'green':
      return colours.greenSoft;
    case 'amber':
      return colours.amberSoft;
    default:
      return colours.redSoft;
  }
}

export function lightLabel(light: string): string {
  switch (light) {
    case 'green':
      return 'Green';
    case 'amber':
      return 'Amber';
    default:
      return 'Red';
  }
}

export function formatUkDate(iso?: string | null): string {
  if (!iso) {
    return '—';
  }
  const [year, month, day] = iso.split('-');
  if (!year || !month || !day) {
    return iso;
  }
  return `${day}/${month}/${year}`;
}

export const documentTypes = [
  { value: 'employersLiability', label: "Employers' liability (EL)" },
  { value: 'publicLiability', label: 'Public liability (PL)' },
  { value: 'professionalIndemnity', label: 'Professional indemnity (PI)' },
  { value: 'ssip', label: 'SSIP' },
  { value: 'rams', label: 'RAMS' },
  { value: 'cscs', label: 'CSCS' },
  { value: 'other', label: 'Other' }
] as const;

export const chaseOutcomes = [
  { value: 'leftVoicemail', label: 'Left voicemail' },
  { value: 'emailSent', label: 'Email sent' },
  { value: 'documentsReceived', label: 'Documents received' },
  { value: 'noAnswer', label: 'No answer' },
  { value: 'callbackArranged', label: 'Callback arranged' },
  { value: 'escalated', label: 'Escalated' },
  { value: 'other', label: 'Other' }
] as const;

export const subcontractorStatuses = [
  { value: 'active', label: 'Active' },
  { value: 'onHold', label: 'On hold' },
  { value: 'inactive', label: 'Inactive' }
] as const;
