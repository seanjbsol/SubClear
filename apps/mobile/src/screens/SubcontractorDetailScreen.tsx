import React, { useCallback, useState } from 'react';
import { Linking, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { chaseOutcomes, colours, formatUkDate } from '../theme';
import { InlineNotice, TrafficLight } from '../ui';
import type { PackDto, SubcontractorDetail } from '../types';

type DetailNav = {
  SubcontractorDetail: { id: string; name: string };
  AddDocument: { subcontractorId: string; name: string };
};

type Props = NativeStackScreenProps<DetailNav, 'SubcontractorDetail'>;

export function SubcontractorDetailScreen({ navigation, route }: Props) {
  const { request, canWrite, entitlements } = useAuth();
  const [detail, setDetail] = useState<SubcontractorDetail | null>(null);
  const [pack, setPack] = useState<PackDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [inviteBusy, setInviteBusy] = useState(false);
  const [inviteNotice, setInviteNotice] = useState<string | null>(null);
  const [inviteUrl, setInviteUrl] = useState<string | null>(null);
  const [inviteFailed, setInviteFailed] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [sub, packDto] = await Promise.all([
        request<SubcontractorDetail>(`/api/subcontractors/${route.params.id}`),
        request<PackDto>(`/api/subcontractors/${route.params.id}/pack`)
      ]);
      setDetail(sub);
      setPack(packDto);
      navigation.setOptions({ title: sub.name });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load subcontractor.');
    }
  }, [navigation, request, route.params.id]);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load])
  );

  const sendInvite = async () => {
    if (!detail) {
      return;
    }
    setInviteBusy(true);
    setInviteNotice(null);
    setInviteUrl(null);
    setInviteFailed(false);
    try {
      const invite = await request<{ portalUrl?: string }>(`/api/subcontractors/${detail.id}/portal-invites`, {
        method: 'POST',
        body: { email: detail.email }
      });
      setInviteUrl(invite.portalUrl ?? null);
      setInviteNotice(
        invite.portalUrl
          ? 'Portal invite emailed. The subcontractor can upload without installing the app — open the private link below.'
          : 'A private upload link has been emailed.'
      );
    } catch (err) {
      setInviteFailed(true);
      setInviteNotice(err instanceof Error ? err.message : 'Could not send invite.');
    } finally {
      setInviteBusy(false);
    }
  };

  const markExpired = async (documentId: string) => {
    try {
      await request(`/api/documents/${documentId}/mark-expired`, { method: 'POST' });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not mark expired.');
    }
  };

  if (!detail) {
    return (
      <View style={styles.flex}>
        <Text style={styles.error}>{error ?? 'Loading…'}</Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.flex} contentContainerStyle={styles.container}>
      <View style={styles.head}>
        <Text style={styles.title}>{detail.name}</Text>
        <TrafficLight light={detail.compliance} size="large" />
      </View>
      <Text style={styles.meta}>
        {detail.contactName ?? 'No contact'} · {detail.email ?? 'No email'} · {detail.phone ?? 'No phone'}
      </Text>

      <Text style={styles.section}>Required pack</Text>
      {(pack?.items ?? [])
        .filter((item) => item.required)
        .map((item) => (
          <View key={item.type} style={styles.row}>
            <View style={{ flex: 1 }}>
              <Text style={styles.rowTitle}>{item.label}</Text>
              <Text style={styles.meta}>
                {item.missing
                  ? 'Missing'
                  : item.expired
                    ? 'Expired'
                    : item.reviewStatus === 'pending'
                      ? 'Awaiting review'
                      : item.reviewStatus === 'rejected'
                        ? `Rejected${item.reviewComment ? ` — ${item.reviewComment}` : ''}`
                        : `Expires ${formatUkDate(item.expiryDate)}`}
              </Text>
            </View>
            <TrafficLight light={item.light} />
          </View>
        ))}

      <View style={styles.actions}>
        {canWrite ? (
          <>
            <Pressable
              style={styles.button}
              onPress={() => navigation.navigate('AddDocument', { subcontractorId: detail.id, name: detail.name })}
            >
              <Text style={styles.buttonText}>Add document</Text>
            </Pressable>
            <Pressable
              style={[styles.secondary, inviteBusy && styles.disabled]}
              disabled={inviteBusy}
              onPress={() => void sendInvite()}
            >
              <Text style={styles.secondaryText}>
                {inviteBusy ? 'Sending…' : entitlements?.hasPortal ? 'Email portal link' : 'Email portal link (Pro)'}
              </Text>
            </Pressable>
            {inviteNotice ? <InlineNotice tone={inviteFailed ? 'error' : 'info'}>{inviteNotice}</InlineNotice> : null}
            {inviteUrl ? (
              <Pressable onPress={() => void Linking.openURL(inviteUrl)}>
                <Text style={styles.portalUrl} selectable>
                  {inviteUrl}
                </Text>
              </Pressable>
            ) : null}
          </>
        ) : null}
      </View>

      <Text style={styles.section}>Documents</Text>
      {detail.documents.map((doc) => (
        <View key={doc.id} style={styles.card}>
          <View style={styles.head}>
            <Text style={styles.rowTitle}>{doc.title}</Text>
            <TrafficLight light={doc.light} />
          </View>
          <Text style={styles.meta}>
            {doc.typeLabel} · {doc.isManuallyExpired ? 'Marked expired' : `Expiry ${formatUkDate(doc.expiryDate)}`}
            {doc.fileName ? ` · ${doc.fileName}` : ''}
            {` · ${doc.reviewStatus === 'approved' ? 'Approved' : doc.reviewStatus === 'rejected' ? 'Rejected' : 'Awaiting review'}`}
          </Text>
          {doc.reviewComment ? <Text style={styles.meta}>{doc.reviewComment}</Text> : null}
          {canWrite && !doc.isManuallyExpired ? (
            <Pressable onPress={() => void markExpired(doc.id)}>
              <Text style={styles.link}>Mark expired</Text>
            </Pressable>
          ) : null}
        </View>
      ))}
      {detail.documents.length === 0 ? <Text style={styles.meta}>No documents stored yet.</Text> : null}

      <Text style={styles.section}>Chase log</Text>
      {detail.recentChases.map((chase) => (
        <View key={chase.id} style={styles.card}>
          <Text style={styles.rowTitle}>
            {formatUkDate(chase.chaseDate)} · {chaseOutcomes.find((o) => o.value === chase.outcome)?.label ?? chase.outcome}
          </Text>
          <Text style={styles.meta}>{chase.note}</Text>
          <Text style={styles.meta}>Logged by {chase.isAutomated ? 'SubClear (automated)' : chase.createdByName}</Text>
        </View>
      ))}
      {detail.recentChases.length === 0 ? <Text style={styles.meta}>No chase notes yet. Log a call, or use Pro automated chases.</Text> : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  container: { padding: 20, paddingBottom: 48 },
  head: { flexDirection: 'row', justifyContent: 'space-between', gap: 12, alignItems: 'center' },
  title: { color: colours.navy, fontSize: 24, fontWeight: '800', flex: 1 },
  meta: { color: colours.muted, marginTop: 4 },
  section: { marginTop: 22, marginBottom: 8, fontWeight: '800', color: colours.navy, fontSize: 18 },
  row: {
    backgroundColor: colours.white,
    borderRadius: 12,
    padding: 12,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: colours.line,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8
  },
  rowTitle: { fontWeight: '700', color: colours.navy },
  card: { backgroundColor: colours.white, borderRadius: 12, padding: 12, marginBottom: 8, borderWidth: 1, borderColor: colours.line },
  actions: { marginTop: 12 },
  button: { backgroundColor: colours.navy, borderRadius: 10, padding: 12, alignItems: 'center' },
  buttonText: { color: colours.white, fontWeight: '700' },
  secondary: { marginTop: 8, backgroundColor: colours.white, borderRadius: 10, padding: 12, alignItems: 'center', borderWidth: 1, borderColor: colours.navy },
  secondaryText: { color: colours.navy, fontWeight: '700' },
  disabled: { opacity: 0.6 },
  portalUrl: { color: colours.navyMid, marginTop: 8, fontSize: 12, fontWeight: '600' },
  link: { color: colours.red, fontWeight: '700', marginTop: 8 },
  error: { padding: 20, color: colours.muted }
});
