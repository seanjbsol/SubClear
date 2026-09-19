import React, { useCallback, useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { colours, formatUkDate } from '../theme';
import { TrafficLight } from '../ui';
import type { SubcontractorSummary } from '../types';
import type { SubsStackParamList } from '../navigationTypes';

type Props = NativeStackScreenProps<SubsStackParamList, 'SubcontractorList'>;

export function SubcontractorListScreen({ navigation }: Props) {
  const { request, canWrite } = useAuth();
  const [rows, setRows] = useState<SubcontractorSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      setRows(await request<SubcontractorSummary[]>('/api/subcontractors'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load subcontractors.');
    }
  }, [request]);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load])
  );

  return (
    <View style={styles.flex}>
      {canWrite ? (
        <Pressable style={styles.add} onPress={() => navigation.navigate('AddSubcontractor')}>
          <Text style={styles.addText}>Add subcontractor</Text>
        </Pressable>
      ) : null}
      <Pressable style={styles.network} onPress={() => navigation.navigate('Directory')}>
        <Text style={styles.networkText}>SubClear network</Text>
      </Pressable>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList
        data={rows ?? []}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
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
        renderItem={({ item }) => (
          <Pressable
            style={styles.card}
            onPress={() => navigation.navigate('SubcontractorDetail', { id: item.id, name: item.name })}
          >
            <View style={styles.head}>
              <Text style={styles.name}>{item.name}</Text>
              <TrafficLight light={item.compliance} />
            </View>
            <Text style={styles.meta}>
              {item.contactName ?? 'No contact'} · {item.documentCount} docs · Next {formatUkDate(item.nextExpiry)}
            </Text>
          </Pressable>
        )}
        ListEmptyComponent={<Text style={styles.empty}>{rows === null ? 'Loading subcontractors…' : 'No subcontractors on this register yet.'}</Text>}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  add: { margin: 16, marginBottom: 0, backgroundColor: colours.navy, borderRadius: 10, padding: 12, alignItems: 'center' },
  addText: { color: colours.white, fontWeight: '700' },
  network: { margin: 16, marginBottom: 0, backgroundColor: colours.white, borderRadius: 10, padding: 12, alignItems: 'center', borderWidth: 1, borderColor: colours.navy },
  networkText: { color: colours.navy, fontWeight: '700' },
  list: { padding: 16, paddingBottom: 40 },
  card: { backgroundColor: colours.white, borderRadius: 12, padding: 14, marginBottom: 10, borderWidth: 1, borderColor: colours.line },
  head: { flexDirection: 'row', justifyContent: 'space-between', gap: 8, alignItems: 'center' },
  name: { fontWeight: '700', color: colours.navy, flex: 1, fontSize: 16 },
  meta: { color: colours.muted, marginTop: 6 },
  empty: { color: colours.muted, textAlign: 'center', marginTop: 40 },
  error: { color: colours.red, marginHorizontal: 16, marginTop: 8 }
});
