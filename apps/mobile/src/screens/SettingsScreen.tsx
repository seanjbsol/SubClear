import React, { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Linking, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useAuth } from '../AuthContext';
import { API_URL } from '../api';
import { colours } from '../theme';
import { InlineNotice } from '../ui';
import type { BillingSessionDto, ChaseSettingsDto, EntitlementsDto } from '../types';

const roleLabels: Record<string, string> = {
  owner: 'Owner',
  admin: 'Admin',
  contractsManager: 'Contracts manager',
  viewer: 'Viewer',
  reviewer: 'Reviewer'
};

function statusLabel(status: string): string {
  switch (status.toLowerCase()) {
    case 'active':
      return 'Active';
    case 'trialing':
    case 'trial':
      return 'Trialing';
    case 'past_due':
    case 'pastdue':
      return 'Past due';
    case 'canceled':
    case 'cancelled':
      return 'Cancelled';
    case 'none':
      return 'No subscription';
    default:
      return status;
  }
}

function formatPeriodEnd(iso?: string | null): string {
  if (!iso) {
    return '—';
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return date.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}

export function SettingsScreen() {
  const { user, logout, request, entitlements: cached } = useAuth();
  const [entitlements, setEntitlements] = useState<EntitlementsDto | null>(cached);
  const [chase, setChase] = useState<ChaseSettingsDto | null>(null);
  const [billingError, setBillingError] = useState<string | null>(null);
  const [billingBusy, setBillingBusy] = useState(false);
  const [chaseNotice, setChaseNotice] = useState<string | null>(null);
  const [chaseFailed, setChaseFailed] = useState(false);
  const canManageBilling = user?.role === 'owner' || user?.role === 'admin';

  const loadEntitlements = useCallback(async () => {
    setBillingError(null);
    try {
      const data = await request<EntitlementsDto>('/api/billing/entitlements');
      setEntitlements(data);
      try {
        setChase(await request<ChaseSettingsDto>('/api/chase-automation'));
      } catch {
        setChase(null);
      }
    } catch (error) {
      setBillingError(error instanceof Error ? error.message : 'Could not load billing status.');
    }
  }, [request]);

  useEffect(() => {
    void loadEntitlements();
  }, [loadEntitlements]);

  const openBillingUrl = useCallback(
    async (path: '/api/billing/checkout' | '/api/billing/portal') => {
      setBillingBusy(true);
      setBillingError(null);
      try {
        const session = await request<BillingSessionDto>(path, { method: 'POST', body: {} });
        const supported = await Linking.canOpenURL(session.url);
        if (!supported) {
          throw new Error('Cannot open the billing URL on this device.');
        }
        await Linking.openURL(session.url);
      } catch (error) {
        setBillingError(error instanceof Error ? error.message : 'Billing request failed.');
      } finally {
        setBillingBusy(false);
      }
    },
    [request]
  );

  return (
    <ScrollView style={styles.flex} contentContainerStyle={styles.container}>
      <Text style={styles.title}>Settings</Text>
      <View style={styles.card}>
        <Text style={styles.label}>Organisation</Text>
        <Text style={styles.value}>{user?.organisationName}</Text>
        <Text style={styles.label}>Signed in as</Text>
        <Text style={styles.value}>{user?.fullName}</Text>
        <Text style={styles.meta}>{user?.email}</Text>
        <Text style={styles.label}>Role</Text>
        <Text style={styles.value}>{roleLabels[user?.role ?? ''] ?? user?.role}</Text>
        <Text style={styles.label}>Tenant ID</Text>
        <Text style={styles.mono}>{user?.tenantId}</Text>
        <Text style={styles.label}>API</Text>
        <Text style={styles.mono}>{API_URL}</Text>
      </View>
      <View style={styles.card}>
        <Text style={styles.value}>Subscription</Text>
        {entitlements ? (
          <>
            <Text style={styles.label}>Plan</Text>
            <Text style={styles.value}>{entitlements.planName || entitlements.plan || '—'}</Text>
            <Text style={styles.label}>Status</Text>
            <Text style={[styles.value, entitlements.isEntitled ? styles.ok : styles.alert]}>
              {statusLabel(entitlements.status)}
            </Text>
            <Text style={styles.label}>Current period end</Text>
            <Text style={styles.meta}>{formatPeriodEnd(entitlements.currentPeriodEnd)}</Text>
            <Text style={styles.label}>Included</Text>
            <Text style={styles.meta}>
              {entitlements.isPro
                ? 'Pro: portal invites, automated chases, expert review.'
                : `Starter: manual chase log, up to ${entitlements.subcontractorLimit ?? 15} subcontractors. Upgrade for portal, automation and review.`}
            </Text>
          </>
        ) : billingError ? (
          <Text style={styles.alertText}>{billingError}</Text>
        ) : (
          <ActivityIndicator color={colours.navy} style={{ marginTop: 12 }} />
        )}
        {billingError && entitlements ? <Text style={styles.alertText}>{billingError}</Text> : null}
        {canManageBilling ? (
          <Pressable
            style={[styles.button, styles.secondary, billingBusy && styles.disabled]}
            disabled={billingBusy}
            onPress={() => void openBillingUrl(entitlements?.isEntitled ? '/api/billing/portal' : '/api/billing/checkout')}
          >
            <Text style={styles.secondaryText}>
              {billingBusy ? 'Opening…' : entitlements?.isEntitled ? 'Manage billing' : 'Upgrade'}
            </Text>
          </Pressable>
        ) : (
          <Text style={styles.meta}>Ask an Owner or Admin to manage billing for this organisation.</Text>
        )}
      </View>
      <View style={styles.card}>
        <Text style={styles.value}>Document chases</Text>
        <Text style={styles.meta}>
          {chase
            ? `Cadence every ${chase.cadenceDays} days. Automation ${chase.automationEnabled ? 'on' : 'off'}.`
            : 'Manual chase log is included on Starter. Automated emails are Pro.'}
        </Text>
        {canManageBilling && entitlements?.hasEmailAutomation ? (
          <Pressable
            style={[styles.button, styles.secondary]}
            onPress={() => {
              void (async () => {
                setChaseNotice(null);
                setChaseFailed(false);
                try {
                  await request('/api/chase-automation/run', { method: 'POST' });
                  setChaseNotice('Due reminders have been sent where documents are missing or expired. Each send is in the email log.');
                  await loadEntitlements();
                } catch (error) {
                  setChaseFailed(true);
                  setChaseNotice(error instanceof Error ? error.message : 'Could not run chases.');
                }
              })();
            }}
          >
            <Text style={styles.secondaryText}>Run chase emails now</Text>
          </Pressable>
        ) : null}
        {chaseNotice ? <InlineNotice tone={chaseFailed ? 'error' : 'info'}>{chaseNotice}</InlineNotice> : null}
      </View>
      <View style={styles.card}>
        <Text style={styles.value}>Tenant isolation</Text>
        <Text style={styles.meta}>
          Every JWT includes tenant_id and role. The API applies EF Core global query filters and explicit TenantId
          checks, so another main contractor cannot see this register.
        </Text>
      </View>
      <Pressable style={styles.button} onPress={() => void logout()}>
        <Text style={styles.buttonText}>Sign out</Text>
      </Pressable>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  container: { padding: 20 },
  title: { color: colours.navy, fontSize: 28, fontWeight: '800', marginBottom: 16 },
  card: { backgroundColor: colours.white, borderRadius: 12, padding: 16, marginBottom: 16, borderWidth: 1, borderColor: colours.line },
  label: { color: colours.muted, fontWeight: '600', marginTop: 12 },
  value: { color: colours.navy, fontSize: 18, fontWeight: '700', marginTop: 2 },
  meta: { color: colours.muted, marginTop: 6, lineHeight: 20 },
  mono: { color: colours.ink, marginTop: 4, fontSize: 12 },
  button: { backgroundColor: colours.navy, borderRadius: 10, padding: 14, alignItems: 'center' },
  buttonText: { color: colours.white, fontWeight: '700' },
  secondary: { backgroundColor: colours.white, borderWidth: 1, borderColor: colours.navy, marginTop: 16 },
  secondaryText: { color: colours.navy, fontWeight: '700' },
  disabled: { opacity: 0.6 },
  ok: { color: colours.green },
  alert: { color: colours.red },
  alertText: { color: colours.red, marginTop: 10, lineHeight: 20 }
});
