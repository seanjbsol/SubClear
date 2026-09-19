import React, { useCallback, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { colours, formatUkDate } from '../theme';
import { TrafficLight } from '../ui';
import type { DashboardDto } from '../types';
import type { HomeStackParamList } from '../navigationTypes';

type Props = NativeStackScreenProps<HomeStackParamList, 'Dashboard'>;

export function HomeScreen({ navigation }: Props) {
  const { request, user } = useAuth();
  const [data, setData] = useState<DashboardDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const dashboard = await request<DashboardDto>('/api/dashboard');
      setData(dashboard);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load dashboard.');
    }
  }, [request]);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load])
  );

  return (
    <ScrollView
      style={styles.flex}
      contentContainerStyle={styles.container}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={async () => {
            setRefreshing(true);
            await load();
            setRefreshing(false);
          }}
        />
      }
    >
      <Text style={styles.kicker}>{user?.organisationName}</Text>
      <Text style={styles.title}>Compliance register</Text>
      <Text style={styles.lede}>Red = non-compliant. Amber = expiring within 30 days. Green = pack in date.</Text>

      {error ? <Text style={styles.error}>{error}</Text> : null}

      <View style={styles.row}>
        <Stat label="Non-compliant" value={data?.nonCompliantCount} tone="red" />
        <Stat label="Expiring in 30 days" value={data?.expiringWithin30Days} tone="amber" />
      </View>
      <View style={styles.row}>
        <Stat label="Compliant" value={data?.compliantCount} tone="green" />
        <Stat label="Awaiting review" value={data?.pendingReviewCount} tone="navy" />
      </View>

      <Text style={styles.section}>Needs attention</Text>
      {(data?.attention ?? []).map((item) => (
        <Pressable
          key={item.id}
          style={styles.card}
          onPress={() => navigation.navigate('SubcontractorDetail', { id: item.id, name: item.name })}
        >
          <View style={styles.cardHead}>
            <Text style={styles.cardTitle}>{item.name}</Text>
            <TrafficLight light={item.compliance} />
          </View>
          <Text style={styles.meta}>
            {item.contactName ?? 'No contact'} · Next expiry {formatUkDate(item.nextExpiry)}
          </Text>
        </Pressable>
      ))}
      {data && data.attention.length === 0 ? <Text style={styles.empty}>All subcontractors are green.</Text> : null}
    </ScrollView>
  );
}

function Stat({
  label,
  value,
  tone
}: {
  label: string;
  value?: number;
  tone: 'red' | 'amber' | 'green' | 'navy';
}) {
  const map = {
    red: { bg: colours.redSoft, fg: colours.red },
    amber: { bg: colours.amberSoft, fg: colours.amber },
    green: { bg: colours.greenSoft, fg: colours.green },
    navy: { bg: '#E8EEF6', fg: colours.navy }
  } as const;
  return (
    <View style={[styles.stat, { backgroundColor: map[tone].bg }]}>
      <Text style={[styles.statValue, { color: map[tone].fg }]}>{value ?? '—'}</Text>
      <Text style={styles.statLabel}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  container: { padding: 20, paddingBottom: 40 },
  kicker: { color: colours.amber, fontWeight: '700' },
  title: { color: colours.navy, fontSize: 28, fontWeight: '800', marginTop: 4 },
  lede: { color: colours.muted, marginTop: 6, marginBottom: 18 },
  row: { flexDirection: 'row', gap: 12, marginBottom: 12 },
  stat: { flex: 1, borderRadius: 14, padding: 14 },
  statValue: { fontSize: 28, fontWeight: '800' },
  statLabel: { color: colours.ink, marginTop: 4, fontWeight: '600' },
  section: { marginTop: 16, marginBottom: 8, fontWeight: '800', color: colours.navy, fontSize: 18 },
  card: { backgroundColor: colours.white, borderRadius: 12, padding: 14, marginBottom: 10, borderWidth: 1, borderColor: colours.line },
  cardHead: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: 8 },
  cardTitle: { fontWeight: '700', color: colours.navy, flex: 1, fontSize: 16 },
  meta: { color: colours.muted, marginTop: 6 },
  empty: { color: colours.muted, marginTop: 8 },
  error: { color: colours.red, marginBottom: 12 }
});
