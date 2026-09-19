import React, { useCallback, useState } from 'react';
import { Alert, FlatList, Pressable, RefreshControl, StyleSheet, Text, TextInput, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import { Picker } from '@react-native-picker/picker';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { chaseOutcomes, colours, formatUkDate } from '../theme';
import { TrafficLight } from '../ui';
import type { ChaseQueueItem } from '../types';
import type { ChaseStackParamList } from '../navigationTypes';

type Props = NativeStackScreenProps<ChaseStackParamList, 'ChaseQueue'>;

export function ChaseQueueScreen({ navigation }: Props) {
  const { request, canWrite } = useAuth();
  const [rows, setRows] = useState<ChaseQueueItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [note, setNote] = useState('Requested current certificates before site start.');
  const [outcome, setOutcome] = useState('emailSent');

  const load = useCallback(async () => {
    setError(null);
    try {
      setRows(await request<ChaseQueueItem[]>('/api/chase-queue'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load chase queue.');
    }
  }, [request]);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load])
  );

  const logChase = async (subcontractorId: string, name: string) => {
    if (!canWrite) {
      return;
    }
    try {
      await request(`/api/subcontractors/${subcontractorId}/chases`, {
        method: 'POST',
        body: { note, outcome }
      });
      Alert.alert('Chase logged', `Noted against ${name}. Email send is not automated in MVP.`);
      await load();
    } catch (err) {
      Alert.alert('Could not log chase', err instanceof Error ? err.message : 'Try again.');
    }
  };

  return (
    <View style={styles.flex}>
      {canWrite ? (
        <View style={styles.composer}>
          <Text style={styles.label}>Default chase note</Text>
          <TextInput style={styles.input} value={note} onChangeText={setNote} multiline />
          <Picker selectedValue={outcome} onValueChange={setOutcome}>
            {chaseOutcomes.map((item) => (
              <Picker.Item key={item.value} label={item.label} value={item.value} />
            ))}
          </Picker>
        </View>
      ) : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList
        data={rows}
        keyExtractor={(item) => item.subcontractorId}
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
            onPress={() => navigation.navigate('SubcontractorDetail', { id: item.subcontractorId, name: item.name })}
          >
            <View style={styles.head}>
              <Text style={styles.name}>{item.name}</Text>
              <TrafficLight light={item.compliance} />
            </View>
            {item.issues.map((issue) => (
              <Text key={issue} style={styles.issue}>
                • {issue}
              </Text>
            ))}
            <Text style={styles.meta}>Last chased {formatUkDate(item.lastChasedOn)}</Text>
            {canWrite ? (
              <Pressable style={styles.log} onPress={() => void logChase(item.subcontractorId, item.name)}>
                <Text style={styles.logText}>Log chase</Text>
              </Pressable>
            ) : null}
          </Pressable>
        )}
        ListEmptyComponent={<Text style={styles.empty}>Chase queue is clear.</Text>}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  composer: { margin: 16, marginBottom: 0, backgroundColor: colours.white, borderRadius: 12, padding: 12, borderWidth: 1, borderColor: colours.line },
  label: { color: colours.muted, fontWeight: '600', marginBottom: 6 },
  input: { minHeight: 60, color: colours.ink },
  list: { padding: 16, paddingBottom: 40 },
  card: { backgroundColor: colours.white, borderRadius: 12, padding: 14, marginBottom: 10, borderWidth: 1, borderColor: colours.line },
  head: { flexDirection: 'row', justifyContent: 'space-between', gap: 8, alignItems: 'center' },
  name: { fontWeight: '700', color: colours.navy, flex: 1, fontSize: 16 },
  issue: { color: colours.ink, marginTop: 6 },
  meta: { color: colours.muted, marginTop: 8 },
  log: { marginTop: 10, alignSelf: 'flex-start', backgroundColor: colours.navy, borderRadius: 8, paddingHorizontal: 10, paddingVertical: 6 },
  logText: { color: colours.white, fontWeight: '700' },
  empty: { color: colours.muted, textAlign: 'center', marginTop: 40 },
  error: { color: colours.red, marginHorizontal: 16, marginTop: 8 }
});
